using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using PrintavoShipper.Core.Interfaces;
using PrintavoShipper.FedEx.Dto;

namespace PrintavoShipper.FedEx;

/// <summary>
/// FedEx Ship + Rate + Address Validation REST API client. See the header
/// comment on FedExShipWireDto.cs — wire shapes here are a best-effort
/// starting point built from FedEx's publicly documented conventions, NOT
/// yet confirmed against a live sandbox call (no FedEx developer
/// credentials were available while this was written). Verify empirically
/// before relying on this for a real shipment.
/// </summary>
public class FedExClient : IFedExClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly IFedExAuthService _authService;
    private readonly IFedExCredentialProvider _credentialProvider;

    public FedExClient(HttpClient httpClient, IFedExAuthService authService, IFedExCredentialProvider credentialProvider)
    {
        _httpClient = httpClient;
        _authService = authService;
        _credentialProvider = credentialProvider;
    }

    public async Task<IReadOnlyList<FedExRateQuote>> ShopRatesAsync(FedExShipmentRequest request, CancellationToken ct = default)
    {
        var credentials = await CredentialsAsync(ct);

        var wire = new FedExRateRequestWrapper
        {
            AccountNumber = new FedExAccountNumber { Value = credentials.AccountNumber ?? string.Empty },
            RequestedShipment = new FedExRateRequestedShipment
            {
                Shipper = BuildParty(request.ShipFrom),
                Recipient = BuildParty(request.ShipTo),
                ShippingChargesPayment = BuildShippingChargesPayment(request),
                RequestedPackageLineItems = request.Packages.Select(pkg => BuildPackageLineItem(pkg)).ToList()
            }
        };

        var baseUrl = BaseUrl(credentials);
        using var httpRequest = await BuildRequestAsync(HttpMethod.Post, $"{baseUrl}/rate/v1/rates/quotes", ct);
        httpRequest.Content = JsonContent(wire);

        using var response = await _httpClient.SendAsync(httpRequest, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        EnsureSuccess(response, body);

        var parsed = JsonSerializer.Deserialize<FedExRateResponseWrapper>(body, JsonOptions);
        var details = parsed?.Output?.RateReplyDetails ?? new List<FedExRateReplyDetail>();

        return details.Select(d =>
        {
            var rated = d.RatedShipmentDetails.FirstOrDefault(r => r.RateType == "ACCOUNT") ?? d.RatedShipmentDetails.FirstOrDefault();
            return new FedExRateQuote(
                d.ServiceType ?? string.Empty,
                d.ServiceName ?? FedExServiceLevels.NameForCode(d.ServiceType ?? string.Empty),
                rated?.TotalNetCharge,
                rated?.Currency,
                null);
        }).ToList();
    }

    public async Task<FedExShipmentResult> CreateShipmentAsync(FedExShipmentRequest request, CancellationToken ct = default)
    {
        var credentials = await CredentialsAsync(ct);

        var wire = new FedExShipRequestWrapper
        {
            AccountNumber = new FedExAccountNumber { Value = credentials.AccountNumber ?? string.Empty },
            RequestedShipment = new FedExRequestedShipment
            {
                ShipDatestamp = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ServiceType = request.ServiceType,
                Shipper = BuildParty(request.ShipFrom),
                Recipients = new List<FedExShipParty> { BuildParty(request.ShipTo) },
                ShippingChargesPayment = BuildShippingChargesPayment(request),
                RequestedPackageLineItems = request.Packages.Select((pkg, index) =>
                    BuildPackageLineItem(pkg, index == 0 ? BuildCustomerReferences(request) : null)).ToList()
            }
        };

        var baseUrl = BaseUrl(credentials);
        using var httpRequest = await BuildRequestAsync(HttpMethod.Post, $"{baseUrl}/ship/v1/shipments", ct);
        httpRequest.Content = JsonContent(wire);

        using var response = await _httpClient.SendAsync(httpRequest, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        EnsureSuccess(response, body);

        var parsed = JsonSerializer.Deserialize<FedExShipResponseWrapper>(body, JsonOptions);
        var shipment = parsed?.Output?.TransactionShipments?.FirstOrDefault()
            ?? throw new FedExShippingException("FedEx returned a successful status but no transactionShipments.");

        var rateDetail = shipment.CompletedShipmentDetail?.ShipmentRating?.ShipmentRateDetails?.FirstOrDefault();

        var packages = shipment.PieceResponses.Select(p => new FedExPackageResult(
            p.TrackingNumber,
            Convert.FromBase64String(p.PackageDocuments.FirstOrDefault()?.EncodedLabel ?? string.Empty),
            "PDF")).ToList();

        return new FedExShipmentResult(
            shipment.MasterTrackingNumber ?? packages.FirstOrDefault()?.TrackingNumber ?? string.Empty,
            rateDetail?.TotalNetCharge,
            rateDetail?.Currency,
            packages);
    }

    public async Task<FedExAddressValidationResult> ValidateAddressAsync(FedExPartyInput address, CancellationToken ct = default)
    {
        var credentials = await CredentialsAsync(ct);

        var wire = new FedExAddressValidationRequestWrapper
        {
            AddressesToValidate = new List<FedExAddressToValidate>
            {
                new() { Address = BuildAddress(address) }
            }
        };

        var baseUrl = BaseUrl(credentials);
        using var httpRequest = await BuildRequestAsync(HttpMethod.Post, $"{baseUrl}/address/v1/addresses/resolve", ct);
        httpRequest.Content = JsonContent(wire);

        using var response = await _httpClient.SendAsync(httpRequest, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        EnsureSuccess(response, body);

        var parsed = JsonSerializer.Deserialize<FedExAddressValidationResponseWrapper>(body, JsonOptions);
        var resolved = parsed?.Output?.ResolvedAddresses?.FirstOrDefault();

        if (resolved is null)
        {
            return new FedExAddressValidationResult(false, false, true, null, new List<FedExAddressCandidate>());
        }

        var classification = ParseClassification(resolved.Classification);
        var candidate = new FedExAddressCandidate(
            resolved.StreetLinesToken ?? new List<string>(),
            resolved.City ?? address.City,
            resolved.StateOrProvinceCode ?? address.State,
            resolved.PostalCode ?? address.PostalCode,
            resolved.CountryCode ?? address.CountryCode,
            classification);

        return new FedExAddressValidationResult(
            IsExactMatch: true,
            IsAmbiguous: false,
            HasNoCandidates: false,
            Classification: classification,
            Candidates: new List<FedExAddressCandidate> { candidate });
    }

    public async Task VoidShipmentAsync(string trackingNumber, CancellationToken ct = default)
    {
        var credentials = await CredentialsAsync(ct);

        var wire = new FedExCancelShipmentRequest
        {
            AccountNumber = new FedExAccountNumber { Value = credentials.AccountNumber ?? string.Empty },
            TrackingNumber = trackingNumber
        };

        var baseUrl = BaseUrl(credentials);
        using var httpRequest = await BuildRequestAsync(HttpMethod.Put, $"{baseUrl}/ship/v1/shipments/cancel", ct);
        httpRequest.Content = JsonContent(wire);

        using var response = await _httpClient.SendAsync(httpRequest, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        EnsureSuccess(response, body);
    }

    private async Task<Core.Models.FedExCredentials> CredentialsAsync(CancellationToken ct) =>
        await _credentialProvider.GetCredentialsAsync(ct)
            ?? throw new FedExShippingException("FedEx credentials are not configured yet — set them in App Settings.");

    private static string BaseUrl(Core.Models.FedExCredentials credentials) =>
        credentials.Environment == Core.Models.FedExEnvironment.Production
            ? "https://apis.fedex.com"
            : "https://apis-sandbox.fedex.com";

    private async Task<HttpRequestMessage> BuildRequestAsync(HttpMethod method, string url, CancellationToken ct)
    {
        var token = await _authService.GetAccessTokenAsync(ct);
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("x-locale", "en_US");
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
            // FedEx nests its error list one level shallower than UPS
            // (no top-level "response" wrapper) — see FedExErrorResponseWrapper.
            var error = JsonSerializer.Deserialize<FedExErrorResponseWrapper>(body, JsonOptions);
            var messages = error?.Errors?.SelectMany(FlattenFedExError) ?? Enumerable.Empty<string>();
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

        throw new FedExShippingException($"FedEx API error ({(int)response.StatusCode}): {message}");
    }

    /// <summary>
    /// FedEx's real validation detail lives at whatever depth of the nested
    /// "errors" array actually holds it, with the generic outer wrapper
    /// (e.g. "Validation failed for object='shipShipmentInputVO'. Error
    /// count: 1") telling you nothing actionable on its own — so recurse
    /// into every leaf that has no further nested errors and report those,
    /// including any parameterList key/value pairs attached to it.
    /// </summary>
    private static IEnumerable<string> FlattenFedExError(FedExErrorMessage error)
    {
        if (error.Errors is { Count: > 0 })
        {
            foreach (var nested in error.Errors.SelectMany(FlattenFedExError))
            {
                yield return nested;
            }

            yield break;
        }

        var text = $"{error.Code}: {error.Message}";
        if (error.ParameterList is { Count: > 0 })
        {
            var parameters = string.Join(", ", error.ParameterList.Select(p => $"{p.Key}={p.Value}"));
            text += $" ({parameters})";
        }

        yield return text;
    }

    private static FedExShipParty BuildParty(FedExPartyInput party) => new()
    {
        Contact = new FedExContact
        {
            PersonName = party.AttentionName,
            CompanyName = party.Name,
            PhoneNumber = party.PhoneNumber
        },
        Address = BuildAddress(party)
    };

    private static FedExWireAddress BuildAddress(FedExPartyInput party)
    {
        var lines = new List<string> { party.Address1 };
        if (!string.IsNullOrWhiteSpace(party.Address2))
        {
            lines.Add(party.Address2);
        }

        return new FedExWireAddress
        {
            StreetLines = lines,
            City = party.City,
            StateOrProvinceCode = party.State,
            PostalCode = party.PostalCode,
            CountryCode = party.CountryCode,
            Residential = party.IsResidential
        };
    }

    private static FedExShippingChargesPayment BuildShippingChargesPayment(FedExShipmentRequest request)
    {
        if (request.BillingType == FedExBillingType.ThirdParty)
        {
            return new FedExShippingChargesPayment
            {
                PaymentType = "THIRD_PARTY",
                Payor = new FedExPayor
                {
                    ResponsibleParty = new FedExResponsibleParty
                    {
                        AccountNumber = new FedExAccountNumber { Value = request.ThirdPartyAccountNumber ?? string.Empty },
                        Address = new FedExPayorAddress
                        {
                            PostalCode = request.ThirdPartyPostalCode ?? string.Empty,
                            CountryCode = request.ThirdPartyCountryCode ?? "US"
                        }
                    }
                }
            };
        }

        return new FedExShippingChargesPayment
        {
            PaymentType = "SENDER",
            Payor = new FedExPayor
            {
                ResponsibleParty = new FedExResponsibleParty
                {
                    AccountNumber = new FedExAccountNumber { Value = request.ShipperAccountNumber ?? string.Empty }
                }
            }
        };
    }

    private static List<FedExCustomerReference>? BuildCustomerReferences(FedExShipmentRequest request)
    {
        var refs = new List<FedExCustomerReference>();
        if (!string.IsNullOrWhiteSpace(request.ReferenceNumber1))
        {
            refs.Add(new FedExCustomerReference { Value = request.ReferenceNumber1 });
        }

        if (!string.IsNullOrWhiteSpace(request.ReferenceNumber2))
        {
            refs.Add(new FedExCustomerReference { Value = request.ReferenceNumber2 });
        }

        return refs.Count > 0 ? refs : null;
    }

    private static FedExAddressClassification? ParseClassification(string? classification) => classification switch
    {
        "BUSINESS" => FedExAddressClassification.Commercial,
        "RESIDENTIAL" => FedExAddressClassification.Residential,
        "UNKNOWN" => FedExAddressClassification.Unclassified,
        _ => null
    };

    private static FedExRequestedPackageLineItem BuildPackageLineItem(FedExPackageInput pkg, List<FedExCustomerReference>? references = null) => new()
    {
        Weight = new FedExWeight { Value = pkg.WeightLbs },
        Dimensions = BuildDimensions(pkg),
        CustomerReferences = references
    };

    private static FedExDimensions? BuildDimensions(FedExPackageInput pkg)
    {
        if (pkg.LengthIn is null || pkg.WidthIn is null || pkg.HeightIn is null)
        {
            return null;
        }

        return new FedExDimensions
        {
            Length = (int)Math.Ceiling(pkg.LengthIn.Value),
            Width = (int)Math.Ceiling(pkg.WidthIn.Value),
            Height = (int)Math.Ceiling(pkg.HeightIn.Value)
        };
    }
}
