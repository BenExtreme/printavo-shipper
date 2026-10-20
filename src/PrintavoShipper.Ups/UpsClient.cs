using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using PrintavoShipper.Core.Interfaces;
using PrintavoShipper.Ups.Dto;

namespace PrintavoShipper.Ups;

public class UpsClient : IUpsClient
{
    // v2409 is the version pinned in UPS's current Shipping/Rating OpenAPI
    // specs (both APIs default to it) — confirmed against the live schema,
    // not guessed.
    private const string ApiVersion = "v2409";

    private const string ProductionBaseUrl = "https://onlinetools.ups.com/api";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly IUpsAuthService _authService;
    private readonly IUpsCredentialProvider _credentialProvider;

    public UpsClient(HttpClient httpClient, IUpsAuthService authService, IUpsCredentialProvider credentialProvider)
    {
        _httpClient = httpClient;
        _authService = authService;
        _credentialProvider = credentialProvider;
    }

    public async Task<IReadOnlyList<UpsRateQuote>> ShopRatesAsync(UpsShipmentRequest request, CancellationToken ct = default)
    {
        var wire = new RateRequestWrapper
        {
            RateRequest = new RateRequestWire
            {
                Request = new RateRequest(),
                Shipment = new RateShipment
                {
                    ShipmentRatingOptions = new WireShipmentRatingOptions { NegotiatedRatesIndicator = "X" },
                    Shipper = BuildShipper(request.ShipFrom, request.ShipperAccountNumber),
                    ShipTo = BuildParty(request.ShipTo),
                    ShipFrom = BuildParty(request.ShipFrom),
                    PaymentDetails = new RatePaymentDetails
                    {
                        ShipmentCharge = { BuildShipmentCharge(request) }
                    },
                    Package = request.Packages.Select(BuildRatePackage).ToList(),
                    DeliveryTimeInformation = new RateDeliveryTimeInformation()
                }
            }
        };

        var baseUrl = await BaseUrlAsync(ct);
        using var httpRequest = await BuildRequestAsync(HttpMethod.Post, $"{baseUrl}/rating/{ApiVersion}/Shoptimeintransit", ct);
        httpRequest.Content = JsonContent(wire);

        using var response = await _httpClient.SendAsync(httpRequest, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        EnsureSuccess(response, body);

        var parsed = JsonSerializer.Deserialize<RateResponseWrapper>(body, JsonOptions);
        var ratedShipments = parsed?.RateResponse?.RatedShipment ?? new List<RatedShipment>();

        return ratedShipments.Select(rs =>
        {
            var negotiated = rs.NegotiatedRateCharges?.TotalCharge;
            var amount = negotiated ?? rs.TotalCharges;
            // TimeInTransit covers every service (incl. Ground); GuaranteedDelivery
            // is only filled for guaranteed services, so it's just the fallback.
            var transitDays = rs.TimeInTransit?.ServiceSummary?.EstimatedArrival?.BusinessDaysInTransit
                ?? rs.GuaranteedDelivery?.BusinessDaysInTransit;
            return new UpsRateQuote(
                rs.Service?.Code ?? string.Empty,
                UpsServiceLevels.NameForCode(rs.Service?.Code ?? string.Empty),
                ParseDecimal(amount?.MonetaryValue),
                amount?.CurrencyCode,
                int.TryParse(transitDays, out var days) ? days : null,
                PublishedCost: ParseDecimal(rs.TotalCharges?.MonetaryValue));
        }).ToList();
    }

    public async Task<UpsShipmentResult> CreateShipmentAsync(UpsShipmentRequest request, CancellationToken ct = default)
    {
        var wire = new ShipRequestWrapper
        {
            ShipmentRequest = new ShipmentRequestWire
            {
                Request = new ShipRequest(),
                Shipment = new WireShipment
                {
                    Description = string.IsNullOrWhiteSpace(request.Description) ? "Merchandise order" : request.Description,
                    ShipmentRatingOptions = new WireShipmentRatingOptions { NegotiatedRatesIndicator = "X" },
                    Shipper = BuildShipper(request.ShipFrom, request.ShipperAccountNumber),
                    ShipTo = BuildParty(request.ShipTo),
                    ShipFrom = BuildParty(request.ShipFrom),
                    PaymentInformation = new WirePaymentInformation { ShipmentCharge = BuildShipmentCharge(request) },
                    Service = new WireService { Code = request.ServiceCode, Description = request.ServiceDescription },
                    ShipmentServiceOptions = BuildServiceOptions(request),
                    // Reference numbers ride on the first package, not the
                    // shipment (see WireShipment's comment) — only the first
                    // 2 are shown on the label regardless, so REF1/REF2
                    // belong there whether there's 1 package or 10. The
                    // delivery notification rides on the first package too,
                    // so a multi-box shipment emails once, not once per box.
                    Package = request.Packages.Select((pkg, index) => index == 0
                        ? BuildShipPackage(pkg, BuildReferenceNumbers(request), BuildDeliveryNotificationOptions(request))
                        : BuildShipPackage(pkg)).ToList()
                },
                LabelSpecification = new WireLabelSpecification()
            }
        };

        var baseUrl = await BaseUrlAsync(ct);
        using var httpRequest = await BuildRequestAsync(HttpMethod.Post, $"{baseUrl}/shipments/{ApiVersion}/ship", ct);
        httpRequest.Content = JsonContent(wire);

        using var response = await _httpClient.SendAsync(httpRequest, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        EnsureSuccess(response, body);

        var parsed = JsonSerializer.Deserialize<ShipmentResponseWrapper>(body, JsonOptions);
        var results = parsed?.ShipmentResponse?.ShipmentResults
            ?? throw new UpsShippingException("UPS returned a successful status but no ShipmentResults.");

        var negotiated = results.NegotiatedRateCharges?.TotalCharge;
        var amount = negotiated ?? results.ShipmentCharges?.TotalCharges;

        var packages = results.PackageResults.Select(p => new UpsPackageResult(
            p.TrackingNumber,
            Convert.FromBase64String(p.ShippingLabel?.GraphicImage ?? string.Empty),
            "GIF")).ToList();

        return new UpsShipmentResult(
            results.ShipmentIdentificationNumber,
            ParseDecimal(amount?.MonetaryValue),
            amount?.CurrencyCode,
            packages);
    }

    public async Task<UpsAddressValidationResult> ValidateAddressAsync(UpsPartyInput address, CancellationToken ct = default)
    {
        var lines = new List<string> { address.Address1 };
        if (!string.IsNullOrWhiteSpace(address.Address2))
        {
            lines.Add(address.Address2);
        }

        // A ship-to that already carries ZIP+4 (9 bare digits, from an earlier
        // validation) has to be split back into UPS's two postcode fields.
        var (zip, zipExtension) = UsPostalCode.Split(address.PostalCode);

        var wire = new XavRequestWrapper
        {
            XAVRequest = new XavRequest
            {
                AddressKeyFormat = new XavAddressKeyFormat
                {
                    ConsigneeName = address.Name,
                    AddressLine = lines,
                    PoliticalDivision2 = address.City,
                    PoliticalDivision1 = address.State,
                    PostcodePrimaryLow = zip,
                    PostcodeExtendedLow = zipExtension,
                    CountryCode = address.CountryCode
                }
            }
        };

        var baseUrl = await BaseUrlAsync(ct);
        // requestoption 3 = validation + classification in one call.
        using var httpRequest = await BuildRequestAsync(HttpMethod.Post, $"{baseUrl}/addressvalidation/v2/3", ct);
        httpRequest.Content = JsonContent(wire);

        using var response = await _httpClient.SendAsync(httpRequest, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        EnsureSuccess(response, body);

        var parsed = JsonSerializer.Deserialize<XavResponseWrapper>(body, JsonOptions);
        var result = parsed?.XAVResponse
            ?? throw new UpsShippingException("UPS returned a successful status but no XAVResponse.");

        var candidates = result.Candidate.Select(c => new UpsAddressCandidate(
            c.AddressKeyFormat.AddressLine,
            c.AddressKeyFormat.PoliticalDivision2 ?? string.Empty,
            c.AddressKeyFormat.PoliticalDivision1 ?? string.Empty,
            c.AddressKeyFormat.PostcodePrimaryLow ?? string.Empty,
            c.AddressKeyFormat.CountryCode ?? address.CountryCode,
            ParseClassification(c.AddressClassification?.Code),
            c.AddressKeyFormat.PostcodeExtendedLow)).ToList();

        return new UpsAddressValidationResult(
            IsExactMatch: result.ValidAddressIndicator is not null,
            IsAmbiguous: result.AmbiguousAddressIndicator is not null,
            HasNoCandidates: result.NoCandidatesIndicator is not null,
            Classification: ParseClassification(result.AddressClassification?.Code),
            Candidates: candidates,
            IsTestEnvironment: baseUrl != ProductionBaseUrl);
    }

    public async Task<UpsThirdPartyAccountValidationResult> ValidateThirdPartyAccountAsync(
        UpsPartyInput shipFrom,
        UpsPartyInput shipTo,
        string ownAccountNumber,
        string accountNumber,
        string postalCode,
        string countryCode,
        CancellationToken ct = default)
    {
        var request = new UpsShipmentRequest(
            shipFrom,
            shipTo,
            ownAccountNumber,
            UpsBillingType.ThirdParty,
            accountNumber,
            postalCode,
            countryCode,
            "03",
            "UPS Ground",
            new List<UpsPackageInput> { new(1m, null, null, null) });

        try
        {
            await ShopRatesAsync(request, ct);
            return new UpsThirdPartyAccountValidationResult(true, "UPS confirmed this account number.");
        }
        catch (UpsShippingException ex)
        {
            return new UpsThirdPartyAccountValidationResult(false, ex.Message);
        }
    }

    private static UpsAddressClassification? ParseClassification(string? code) => code switch
    {
        "1" => UpsAddressClassification.Commercial,
        "2" => UpsAddressClassification.Residential,
        "0" => UpsAddressClassification.Unclassified,
        _ => null
    };

    public async Task VoidShipmentAsync(string shipmentIdentificationNumber, CancellationToken ct = default)
    {
        var baseUrl = await BaseUrlAsync(ct);
        using var httpRequest = await BuildRequestAsync(
            HttpMethod.Delete,
            $"{baseUrl}/shipments/{ApiVersion}/void/cancel/{shipmentIdentificationNumber}",
            ct);

        using var response = await _httpClient.SendAsync(httpRequest, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        EnsureSuccess(response, body);
    }

    private async Task<string> BaseUrlAsync(CancellationToken ct)
    {
        var credentials = await _credentialProvider.GetCredentialsAsync(ct)
            ?? throw new UpsShippingException("UPS credentials are not configured yet — set them in App Settings.");

        return credentials.Environment == Core.Models.UpsEnvironment.Production
            ? ProductionBaseUrl
            : "https://wwwcie.ups.com/api";
    }

    private async Task<HttpRequestMessage> BuildRequestAsync(HttpMethod method, string url, CancellationToken ct)
    {
        var token = await _authService.GetAccessTokenAsync(ct);
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("transactionSrc", "PrintavoShipper");
        return request;
    }

    private static HttpContent JsonContent<T>(T value) =>
        new StringContent(JsonSerializer.Serialize(value, JsonOptions), System.Text.Encoding.UTF8, "application/json");

    private static void EnsureSuccess(HttpResponseMessage response, string body)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string message;
        try
        {
            var error = JsonSerializer.Deserialize<UpsErrorResponseWrapper>(body, JsonOptions);
            var messages = error?.Response?.Errors?.Select(e => $"{e.Code}: {e.Message}") ?? Enumerable.Empty<string>();
            message = string.Join("; ", messages);
        }
        catch (JsonException)
        {
            message = string.Empty;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            message = body;
        }

        throw new UpsShippingException($"UPS API error ({(int)response.StatusCode}): {message}");
    }

    private static WireShipper BuildShipper(UpsPartyInput shipFrom, string shipperAccountNumber) => new()
    {
        Name = shipFrom.Name,
        AttentionName = shipFrom.AttentionName,
        ShipperNumber = shipperAccountNumber,
        Phone = string.IsNullOrWhiteSpace(shipFrom.PhoneNumber) ? null : new WirePhone { Number = shipFrom.PhoneNumber },
        Address = BuildAddress(shipFrom)
    };

    private static WireParty BuildParty(UpsPartyInput party) => new()
    {
        Name = party.Name,
        AttentionName = party.AttentionName,
        Phone = string.IsNullOrWhiteSpace(party.PhoneNumber) ? null : new WirePhone { Number = party.PhoneNumber },
        Address = BuildAddress(party),
        Residential = party.IsResidential == true ? "" : null
    };

    private static WireAddress BuildAddress(UpsPartyInput party)
    {
        var lines = new List<string> { party.Address1 };
        if (!string.IsNullOrWhiteSpace(party.Address2))
        {
            lines.Add(party.Address2);
        }

        return new WireAddress
        {
            AddressLine = lines,
            City = party.City,
            StateProvinceCode = party.State,
            PostalCode = party.PostalCode,
            CountryCode = party.CountryCode
        };
    }

    private static WireShipmentCharge BuildShipmentCharge(UpsShipmentRequest request)
    {
        var charge = new WireShipmentCharge();

        if (request.BillingType == UpsBillingType.ThirdParty)
        {
            charge.BillThirdParty = new WireBillThirdParty
            {
                AccountNumber = request.ThirdPartyAccountNumber ?? string.Empty,
                Address = new WireBillThirdPartyAddress
                {
                    PostalCode = request.ThirdPartyPostalCode ?? string.Empty,
                    CountryCode = request.ThirdPartyCountryCode ?? "US"
                }
            };
        }
        else
        {
            charge.BillShipper = new WireBillShipper { AccountNumber = request.ShipperAccountNumber };
        }

        return charge;
    }

    private static List<WireReferenceNumber>? BuildReferenceNumbers(UpsShipmentRequest request)
    {
        var refs = new List<WireReferenceNumber>();
        if (!string.IsNullOrWhiteSpace(request.ReferenceNumber1))
        {
            refs.Add(new WireReferenceNumber { Value = request.ReferenceNumber1 });
        }

        if (!string.IsNullOrWhiteSpace(request.ReferenceNumber2))
        {
            refs.Add(new WireReferenceNumber { Value = request.ReferenceNumber2 });
        }

        return refs.Count > 0 ? refs : null;
    }

    private static bool QuantumViewRequested(UpsShipmentRequest request) =>
        request.EnableQuantumView && !string.IsNullOrWhiteSpace(request.QuantumViewPrimaryEmail);

    /// <summary>Quantum View Notify, Ship half: code 6 goes only to the primary email, once per shipment. Delivery (code 8) is per-package — see BuildDeliveryNotificationOptions.</summary>
    private static WireShipmentServiceOptions? BuildServiceOptions(UpsShipmentRequest request)
    {
        if (!QuantumViewRequested(request))
        {
            return null;
        }

        return new WireShipmentServiceOptions
        {
            Notification = new List<WireNotification>
            {
                new()
                {
                    NotificationCode = "6",
                    EMail = new WireNotificationEmail { EMailAddress = { request.QuantumViewPrimaryEmail! } }
                }
            }
        };
    }

    /// <summary>
    /// Quantum View Notify, Delivery half: code 8 to both primary and
    /// secondary (the secondary contact only wants to know once it's
    /// arrived). Attached to the first package only — a shipment-level
    /// delivery notification fires once per delivered package, so a
    /// 10-box shipment sent 10 delivery emails to every recipient.
    /// </summary>
    private static WirePackageServiceOptions? BuildDeliveryNotificationOptions(UpsShipmentRequest request)
    {
        if (!QuantumViewRequested(request))
        {
            return null;
        }

        var deliveryRecipients = new List<string> { request.QuantumViewPrimaryEmail! };
        if (!string.IsNullOrWhiteSpace(request.QuantumViewSecondaryEmail))
        {
            deliveryRecipients.Add(request.QuantumViewSecondaryEmail);
        }

        return new WirePackageServiceOptions
        {
            Notification = new WireNotification
            {
                NotificationCode = "8",
                EMail = new WireNotificationEmail { EMailAddress = deliveryRecipients }
            }
        };
    }

    private static WirePackage BuildShipPackage(
        UpsPackageInput pkg,
        List<WireReferenceNumber>? referenceNumbers = null,
        WirePackageServiceOptions? serviceOptions = null) => new()
    {
        Dimensions = BuildDimensions(pkg),
        PackageWeight = new WirePackageWeight { Weight = FormatDecimal(pkg.WeightLbs) },
        ReferenceNumber = referenceNumbers,
        PackageServiceOptions = serviceOptions
    };

    private static RatePackage BuildRatePackage(UpsPackageInput pkg) => new()
    {
        Dimensions = BuildDimensions(pkg),
        PackageWeight = new WirePackageWeight { Weight = FormatDecimal(pkg.WeightLbs) }
    };

    private static WireDimensions? BuildDimensions(UpsPackageInput pkg)
    {
        if (pkg.LengthIn is null || pkg.WidthIn is null || pkg.HeightIn is null)
        {
            return null;
        }

        return new WireDimensions
        {
            Length = FormatDecimal(pkg.LengthIn.Value),
            Width = FormatDecimal(pkg.WidthIn.Value),
            Height = FormatDecimal(pkg.HeightIn.Value)
        };
    }

    private static string FormatDecimal(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static decimal? ParseDecimal(string? value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result) ? result : null;
}
