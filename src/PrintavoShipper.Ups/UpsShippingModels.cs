namespace PrintavoShipper.Ups;

public class UpsShippingException : Exception
{
    public UpsShippingException(string message) : base(message)
    {
    }
}

public enum UpsBillingType
{
    OwnAccount = 0,
    ThirdParty = 1
}

/// <summary>
/// IsResidential is only meaningful (and only ever set) on a ShipTo — it
/// drives UPS's residential surcharge/rating and is populated from
/// IUpsClient.ValidateAddressAsync's classification, not entered by hand
/// (there's no ship-to edit UI yet). Left null when unknown/unclassified,
/// in which case UPS applies its own default (commercial) treatment.
/// </summary>
public record UpsPartyInput(
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

public record UpsPackageInput(decimal WeightLbs, decimal? LengthIn, decimal? WidthIn, decimal? HeightIn);

/// <summary>
/// ReferenceNumber1/2 and the QuantumView fields are only meaningful to
/// CreateShipmentAsync (they print on the label / trigger UPS's own
/// notification emails) — ShopRatesAsync ignores them, but they live on
/// the shared request type rather than a second one since every other
/// field is identical between rating and shipping.
/// </summary>
public record UpsShipmentRequest(
    UpsPartyInput ShipFrom,
    UpsPartyInput ShipTo,
    string ShipperAccountNumber,
    UpsBillingType BillingType,
    string? ThirdPartyAccountNumber,
    string? ThirdPartyPostalCode,
    string? ThirdPartyCountryCode,
    string ServiceCode,
    string ServiceDescription,
    IReadOnlyList<UpsPackageInput> Packages,
    string? Description = null,
    string? ReferenceNumber1 = null,
    string? ReferenceNumber2 = null,
    bool EnableQuantumView = false,
    string? QuantumViewPrimaryEmail = null,
    string? QuantumViewSecondaryEmail = null);

public record UpsPackageResult(string TrackingNumber, byte[] LabelImageBytes, string LabelImageFormat);

public record UpsShipmentResult(
    string ShipmentIdentificationNumber,
    decimal? TotalCost,
    string? CurrencyCode,
    IReadOnlyList<UpsPackageResult> Packages);

/// <summary>
/// AccountLabel/AccountNumber are populated by the caller (not by
/// IUpsClient itself, which doesn't know about the shop's saved account
/// list) when comparing rates across more than one of the shop's own UPS
/// accounts — see ShipmentCardViewModel.GetRatesAsync.
/// TotalCost is the negotiated rate when UPS returned one (published
/// otherwise); PublishedCost is always UPS's published/list rate.
/// </summary>
public record UpsRateQuote(
    string ServiceCode,
    string ServiceDescription,
    decimal? TotalCost,
    string? CurrencyCode,
    int? BusinessDaysInTransit,
    string? AccountLabel = null,
    string? AccountNumber = null,
    decimal? PublishedCost = null)
{
    public bool HasDiscount => PublishedCost is not null && TotalCost is not null && PublishedCost > TotalCost;
}

public enum UpsAddressClassification
{
    Unclassified = 0,
    Commercial = 1,
    Residential = 2
}

public record UpsAddressCandidate(
    IReadOnlyList<string> AddressLines,
    string City,
    string State,
    string PostalCode,
    string CountryCode,
    UpsAddressClassification? Classification,
    string? PostcodeExtension = null)
{
    /// <summary>ZIP+4 as 9 bare digits when UPS returned an extension (fits UpsAddressLimits.PostalCodeMaxLength; UPS accepts it as-is), else the 5-digit ZIP.</summary>
    public string FullPostalCode => string.IsNullOrWhiteSpace(PostcodeExtension) ? PostalCode : PostalCode + PostcodeExtension;

    public string DisplayText => $"{string.Join(" ", AddressLines)}, {City}, {State} {UsPostalCode.Format(FullPostalCode)}";
}

/// <summary>
/// ZIP+4 is kept as 9 bare digits internally (the app-wide postal-code limit
/// is 9 chars, so "12345-6789" wouldn't fit) — these helpers split it for
/// UPS's Address Validation API and dash it for display.
/// </summary>
public static class UsPostalCode
{
    public static string Format(string? postalCode)
    {
        var digits = postalCode?.Replace("-", string.Empty) ?? string.Empty;
        return digits.Length == 9 && digits.All(char.IsDigit) ? $"{digits[..5]}-{digits[5..]}" : postalCode ?? string.Empty;
    }

    public static (string Primary, string? Extension) Split(string? postalCode)
    {
        var digits = postalCode?.Replace("-", string.Empty) ?? string.Empty;
        return digits.Length == 9 && digits.All(char.IsDigit) ? (digits[..5], digits[5..]) : (postalCode ?? string.Empty, null);
    }
}

/// <summary>
/// Result of the Address Validation ("XAV") API's combined validation +
/// classification request (requestoption=3). ExactMatch means UPS matched
/// the address as given with no ambiguity; otherwise Candidates holds
/// UPS's suggested corrections (empty if NoCandidates). NOTE (verified
/// against UPS's own docs): in the CIE test environment, street-level
/// validation only returns real results for addresses in NY and CA — every
/// other CIE address comes back with no candidates regardless of whether
/// it's actually a real, deliverable address. This is a UPS sandbox
/// limitation, not a bug in this app.
/// </summary>
public record UpsAddressValidationResult(
    bool IsExactMatch,
    bool IsAmbiguous,
    bool HasNoCandidates,
    UpsAddressClassification? Classification,
    IReadOnlyList<UpsAddressCandidate> Candidates,
    bool IsTestEnvironment = false);

/// <summary>Result of IUpsClient.ValidateThirdPartyAccountAsync's rate-shop dry run.</summary>
public record UpsThirdPartyAccountValidationResult(bool IsValid, string Message);

/// <summary>
/// Common domestic + Canada/Mexico UPS service codes (Shipping API "Service.Code").
/// Not the full international list — this shop ships out of the US and this
/// list can grow if/when an order needs a code that's missing.
/// </summary>
public static class UpsServiceLevels
{
    public static readonly IReadOnlyList<(string Code, string Name)> All = new List<(string, string)>
    {
        ("03", "UPS Ground"),
        ("12", "UPS 3 Day Select"),
        ("02", "UPS 2nd Day Air"),
        ("59", "UPS 2nd Day Air A.M."),
        ("13", "UPS Next Day Air Saver"),
        ("01", "UPS Next Day Air"),
        ("14", "UPS Next Day Air Early"),
        ("11", "UPS Standard"),
    };

    public static string NameForCode(string code) =>
        All.FirstOrDefault(s => s.Code == code).Name is { Length: > 0 } name ? name : code;
}
