namespace PrintavoShipper.Ups;

/// <summary>
/// UPS Rating + Shipping REST API client (Shipping.yaml / Rating.yaml from
/// the official UPS-API/api-documentation repo). Build and test against
/// UPS's CIE environment (AppSettings.UpsEnvironment) before switching to
/// Production — Production shipments are real, billable postage.
/// </summary>
public interface IUpsClient
{
    /// <summary>"Shop" rates: returns a cost/transit estimate for every UPS service level between the given addresses, for the given package set. Doesn't purchase anything.</summary>
    Task<IReadOnlyList<UpsRateQuote>> ShopRatesAsync(UpsShipmentRequest request, CancellationToken ct = default);

    /// <summary>Validates a street address against USPS data and classifies it residential vs. commercial (Address Validation "XAV" API).</summary>
    Task<UpsAddressValidationResult> ValidateAddressAsync(UpsPartyInput address, CancellationToken ct = default);

    /// <summary>
    /// Confirms a UPS account number is valid and active for third-party billing.
    /// UPS has no dedicated "verify this account" endpoint, so this is a
    /// minimal rate-shop dry run (a single 1lb package, never actually
    /// shipped/billed) billed third-party to the candidate account —
    /// UPS validates AccountNumber+PostalCode/CountryCode against its own
    /// file as a side effect of any BillThirdParty request. Rate quotes are
    /// always free; only CreateShipmentAsync incurs real cost.
    /// </summary>
    Task<UpsThirdPartyAccountValidationResult> ValidateThirdPartyAccountAsync(
        UpsPartyInput shipFrom,
        UpsPartyInput shipTo,
        string ownAccountNumber,
        string accountNumber,
        string postalCode,
        string countryCode,
        CancellationToken ct = default);

    /// <summary>Purchases a shipment and returns tracking number(s) + label image(s) per package.</summary>
    Task<UpsShipmentResult> CreateShipmentAsync(UpsShipmentRequest request, CancellationToken ct = default);

    Task VoidShipmentAsync(string shipmentIdentificationNumber, CancellationToken ct = default);
}
