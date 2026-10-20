namespace PrintavoShipper.Core.Models;

/// <summary>
/// Field length limits from UPS's REST Shipping API schema (Shipment_ShipTo
/// and Shipment_ShipFrom in the official UPS-API/api-documentation
/// Shipping.yaml) — confirmed identical for ShipTo and ShipFrom, so one set
/// of constants covers both. Lives in Core (rather than the Ups project)
/// so both the data layer and the Ups API client can reference it without a
/// circular project dependency, alongside the App layer's UI.
///
/// Used today for the ship-from address form (Contractor Settings). Should
/// be reused wherever a ship-to address becomes editable (Phase 3's
/// shipment builder) and when validating Printavo-sourced ship-to data
/// before it's sent to UPS — Printavo doesn't enforce any of these, so a
/// long Printavo address could otherwise fail at the UPS API call.
/// </summary>
public static class UpsAddressLimits
{
    public const int NameMaxLength = 35;
    public const int AttentionNameMaxLength = 35;
    public const int AddressLineMaxLength = 35;
    public const int MaxAddressLines = 3;
    public const int CityMaxLength = 30;
    public const int StateProvinceCodeMaxLength = 5;
    public const int PostalCodeMaxLength = 9;
    public const int CountryCodeLength = 2;
    public const int PhoneNumberMaxLength = 15;
    public const int PhoneExtensionMaxLength = 4;

    /// <summary>UPS shipper/payer account number — always 6 alphanumeric characters.</summary>
    public const int AccountNumberMaxLength = 6;

    /// <summary>Quantum View Notify's Notification_EMail.EMailAddress limit.</summary>
    public const int EmailMaxLength = 50;

    /// <summary>Shipment.ReferenceNumber.Value limit.</summary>
    public const int ReferenceNumberMaxLength = 35;

    /// <summary>
    /// Safely clips a value to a UPS field limit. Needed anywhere address
    /// data is written without going through a length-limited UI control —
    /// e.g. pre-filling a ship-from address from a Printavo customer's own
    /// record, which Printavo doesn't length-constrain at all.
    /// </summary>
    public static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }
}
