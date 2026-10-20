namespace PrintavoShipper.FedEx;

/// <summary>
/// FedEx Ship + Rate + Address Validation REST API client. Build and test
/// against FedEx's sandbox environment (AppSettings.FedExEnvironment) before
/// switching to Production — Production shipments are real, billable
/// postage, and FedEx additionally requires its own portal approval before a
/// third-party integration project can move from sandbox to production (no
/// UPS analog).
///
/// Unlike IUpsClient, there's no ValidateThirdPartyAccountAsync here: FedEx's
/// Rate API only accepts PaymentType SENDER (confirmed live — a THIRD_PARTY
/// rate-shop dry run fails with RATE.PAYMENTTYPE.NOTALLOWED regardless of
/// whether the account number is valid), and only the Ship API accepts
/// THIRD_PARTY. There's no way to validate a third-party account without
/// actually creating (and voiding) a real shipment, so third-party FedEx
/// billing accounts go unvalidated until first use.
/// </summary>
public interface IFedExClient
{
    /// <summary>Returns a cost/transit estimate for the given service type between the given addresses, for the given package set. Doesn't purchase anything.</summary>
    Task<IReadOnlyList<FedExRateQuote>> ShopRatesAsync(FedExShipmentRequest request, CancellationToken ct = default);

    /// <summary>Validates a street address and classifies it residential vs. commercial (FedEx Address Validation API).</summary>
    Task<FedExAddressValidationResult> ValidateAddressAsync(FedExPartyInput address, CancellationToken ct = default);

    /// <summary>Purchases a shipment and returns tracking number(s) + label image(s) per package.</summary>
    Task<FedExShipmentResult> CreateShipmentAsync(FedExShipmentRequest request, CancellationToken ct = default);

    Task VoidShipmentAsync(string trackingNumber, CancellationToken ct = default);
}
