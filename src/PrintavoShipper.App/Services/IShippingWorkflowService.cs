using PrintavoShipper.Core.Models;
using PrintavoShipper.FedEx;
using PrintavoShipper.Ups;

namespace PrintavoShipper.App.Services;

/// <summary>
/// RatedCost is the Get Rates quote for the chosen service/account, rated
/// from the shop's Default Ship-From address rather than the label's
/// ship-from — it's what gets recorded as the shipment's cost. Null when no
/// current quote exists (only allowed for third-party billing). Same on
/// FedExShipAndPrintInput. InvoiceFeeAmount is the "Shipping" fee to add to
/// the Printavo invoice (already calculated via ShippingFeeCalculator), or
/// null for none.
/// </summary>
public record ShipAndPrintInput(
    string PrintavoOrderId,
    string PrintavoVisualId,
    string? ContractorId,
    string ShipFromLabel,
    UpsPartyInput ShipFrom,
    string? ShipToLabel,
    UpsPartyInput ShipTo,
    UpsBillingType BillingType,
    string? BillingAccountLabel,
    string? BillingAccountNumber,
    string? BillingAccountPostalCode,
    string? BillingAccountCountryCode,
    string ShipperAccountNumber,
    string ServiceCode,
    string ServiceDescription,
    IReadOnlyList<UpsPackageInput> Packages,
    string? ReferenceNumber1 = null,
    string? ReferenceNumber2 = null,
    bool HasMultipleShipToAddresses = false,
    bool EnableQuantumView = false,
    string? QuantumViewPrimaryEmail = null,
    string? QuantumViewSecondaryEmail = null,
    string? Notes = null,
    bool UseCombinedTaskWriteBack = false,
    decimal? RatedCost = null,
    decimal? InvoiceFeeAmount = null);

/// <summary>
/// FedEx equivalent of ShipAndPrintInput. Kept as its own record rather than
/// genericizing the UPS one — the two carriers' billing/service-level shapes
/// genuinely differ (FedEx has no Quantum-View-style notification feature),
/// and UPS's shipment-building path must not be touched to add this.
/// </summary>
public record FedExShipAndPrintInput(
    string PrintavoOrderId,
    string PrintavoVisualId,
    string? ContractorId,
    string ShipFromLabel,
    FedExPartyInput ShipFrom,
    string? ShipToLabel,
    FedExPartyInput ShipTo,
    FedExBillingType BillingType,
    string? BillingAccountLabel,
    string? BillingAccountNumber,
    string? BillingAccountPostalCode,
    string? BillingAccountCountryCode,
    string? ShipperAccountNumber,
    string ServiceType,
    string ServiceDescription,
    IReadOnlyList<FedExPackageInput> Packages,
    string? ReferenceNumber1 = null,
    string? ReferenceNumber2 = null,
    bool HasMultipleShipToAddresses = false,
    string? Notes = null,
    bool UseCombinedTaskWriteBack = false,
    decimal? RatedCost = null);

/// <summary>Result of a "Ship &amp; Print Label" attempt. The carrier purchase, the local print, and the Printavo write-back are reported separately, since a failure in any later step must never look like the (already billed) shipment itself failed.</summary>
public record ShipAndPrintOutcome(ShipmentRecord ShipmentRecord, string? PrintWarning, string? PrintavoWriteBackWarning);

/// <summary>
/// Orchestrates "Ship &amp; Print Label" for both carriers: call the UPS or
/// FedEx Shipping API, save + print the returned label(s), persist a
/// ShipmentRecord, then write back to Printavo — a Task as an expense
/// substitute (own-account shipments only) and a customerNote append with
/// tracking info (always). Each write-back step is attempted independently
/// and skipped on retry once it has already succeeded
/// (ShipmentRecord.PrintavoTaskId / CustomerNoteAppended track that), so a
/// failure in one never blocks the other and both are safely re-triable via
/// RetryPrintavoWriteBackAsync.
/// </summary>
public interface IShippingWorkflowService
{
    Task<ShipAndPrintOutcome> ShipAndPrintAsync(ShipAndPrintInput input, CancellationToken ct = default);

    Task<ShipAndPrintOutcome> ShipAndPrintAsync(FedExShipAndPrintInput input, CancellationToken ct = default);

    /// <summary>Voids a shipment via whichever carrier's client matches record.Carrier, then removes its Shipping fee from the Printavo invoice if it added one. A failed fee removal comes back as Warning rather than throwing — the carrier void has already happened by then.</summary>
    Task<(ShipmentRecord Record, string? Warning)> VoidShipmentAsync(ShipmentRecord record, CancellationToken ct = default);

    Task ReprintAsync(ShipmentRecord record, CancellationToken ct = default);

    Task<(ShipmentRecord Record, string? Warning)> RetryPrintavoWriteBackAsync(ShipmentRecord record, CancellationToken ct = default);
}
