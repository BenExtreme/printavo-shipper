namespace PrintavoShipper.FedEx;

public class FedExShippingException : Exception
{
    public FedExShippingException(string message) : base(message)
    {
    }
}

public enum FedExBillingType
{
    OwnAccount = 0,
    ThirdParty = 1
}

/// <summary>
/// IsResidential is only meaningful (and only ever set) on a ShipTo — it
/// drives FedEx's residential surcharge/rating and is populated from
/// IFedExClient.ValidateAddressAsync's classification, mirroring
/// UpsPartyInput's IsResidential exactly.
/// </summary>
public record FedExPartyInput(
    string Name,
    string? AttentionName,
    string Address1,
    string? Address2,
    string City,
    string State,
    string PostalCode,
    string CountryCode,
    string? PhoneNumber,
    bool? IsResidential = null);

public record FedExPackageInput(decimal WeightLbs, decimal? LengthIn, decimal? WidthIn, decimal? HeightIn);

/// <summary>
/// Mirrors UpsShipmentRequest's shape. No Quantum-View-style notification
/// fields — FedEx has no equivalent feature wired into this app yet.
/// </summary>
public record FedExShipmentRequest(
    FedExPartyInput ShipFrom,
    FedExPartyInput ShipTo,
    string? ShipperAccountNumber,
    FedExBillingType BillingType,
    string? ThirdPartyAccountNumber,
    string? ThirdPartyPostalCode,
    string? ThirdPartyCountryCode,
    string ServiceType,
    string ServiceDescription,
    IReadOnlyList<FedExPackageInput> Packages,
    string? Description = null,
    string? ReferenceNumber1 = null,
    string? ReferenceNumber2 = null);

public record FedExPackageResult(string TrackingNumber, byte[] LabelImageBytes, string LabelImageFormat);

public record FedExShipmentResult(
    string TransactionId,
    decimal? TotalCost,
    string? CurrencyCode,
    IReadOnlyList<FedExPackageResult> Packages);

/// <summary>
/// AccountLabel/AccountNumber are populated by the caller (not by
/// IFedExClient itself), same convention as UpsRateQuote.
/// </summary>
public record FedExRateQuote(
    string ServiceType,
    string ServiceDescription,
    decimal? TotalCost,
    string? CurrencyCode,
    int? BusinessDaysInTransit,
    string? AccountLabel = null,
    string? AccountNumber = null);

public enum FedExAddressClassification
{
    Unclassified = 0,
    Commercial = 1,
    Residential = 2
}

public record FedExAddressCandidate(
    IReadOnlyList<string> AddressLines,
    string City,
    string State,
    string PostalCode,
    string CountryCode,
    FedExAddressClassification? Classification);

/// <summary>Result of the FedEx Address Validation API's classification request. Mirrors UpsAddressValidationResult's shape.</summary>
public record FedExAddressValidationResult(
    bool IsExactMatch,
    bool IsAmbiguous,
    bool HasNoCandidates,
    FedExAddressClassification? Classification,
    IReadOnlyList<FedExAddressCandidate> Candidates);

/// <summary>
/// Curated domestic FedEx service types (Ship/Rate API "serviceType") — a
/// starting list mirroring UpsServiceLevels' curated approach. NOT yet
/// verified against a real FedEx sandbox call; confirm exact enum values and
/// which ones this shop actually uses (Ground vs. Home Delivery in
/// particular depends on residential vs. commercial ship-to) before relying
/// on this for real shipments.
/// </summary>
public static class FedExServiceLevels
{
    public static readonly IReadOnlyList<(string Code, string Name)> All = new List<(string, string)>
    {
        ("FEDEX_GROUND", "FedEx Ground"),
        ("GROUND_HOME_DELIVERY", "FedEx Home Delivery"),
        ("FEDEX_EXPRESS_SAVER", "FedEx Express Saver"),
        ("FEDEX_2_DAY", "FedEx 2Day"),
        ("FEDEX_2_DAY_AM", "FedEx 2Day A.M."),
        ("STANDARD_OVERNIGHT", "FedEx Standard Overnight"),
        ("PRIORITY_OVERNIGHT", "FedEx Priority Overnight"),
        ("FIRST_OVERNIGHT", "FedEx First Overnight"),
    };

    public static string NameForCode(string code) =>
        All.FirstOrDefault(s => s.Code == code).Name is { Length: > 0 } name ? name : code;
}
