namespace PrintavoShipper.Core.Models;

/// <summary>
/// One of the shop's own UPS shipper accounts (as opposed to a contractor's
/// third-party billing account — see ContractorBillingAccount). Shops that
/// negotiate rates on more than one account want to compare rates across
/// all of them before picking who ships/pays; a single AppSettings field
/// can't represent that, so this became its own table. Whichever account
/// manifests a shipment becomes both the UPS "Shipper" (ShipperNumber) and,
/// when billing to our own account rather than a contractor's, the payer
/// too.
/// </summary>
public class OwnUpsAccount
{
    public int Id { get; set; }

    public string Label { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string CountryCode { get; set; } = "US";
    public bool IsDefault { get; set; }
}
