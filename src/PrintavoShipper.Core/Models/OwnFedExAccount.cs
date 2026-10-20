namespace PrintavoShipper.Core.Models;

/// <summary>
/// One of the shop's own FedEx accounts (as opposed to a contractor's
/// third-party billing account — see ContractorFedExBillingAccount). Mirrors
/// OwnUpsAccount's shape. The shop does ship on its own FedEx account, just
/// less often than third-party billing — unlike OwnUpsAccount there's no
/// evidence of more than one FedEx account in use, so FedExShipmentCardViewModel
/// doesn't reproduce UPS's multi-account rate-shopping-and-merge logic.
/// </summary>
public class OwnFedExAccount
{
    public int Id { get; set; }

    public string Label { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string CountryCode { get; set; } = "US";
    public bool IsDefault { get; set; }
}
