using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace PrintavoShipper.Core.Models;

public enum ShipmentBillingType
{
    OwnAccount = 0,
    ThirdParty = 1
}

public enum ShipmentStatus
{
    Draft = 0,
    RateQuoted = 1,
    Purchased = 2,
    Voided = 3,
    Error = 4
}

public enum ShipmentCarrier
{
    Ups = 0,
    FedEx = 1
}

/// <summary>
/// Local audit trail of a single shipment (UPS or FedEx) created for a
/// Printavo order. Address/billing fields are snapshotted (copied), not
/// just foreign keys, so later edits to contractor config don't rewrite
/// history.
/// </summary>
public class ShipmentRecord
{
    public int Id { get; set; }

    public ShipmentCarrier Carrier { get; set; } = ShipmentCarrier.Ups;

    public string PrintavoOrderId { get; set; } = string.Empty;
    public string PrintavoVisualId { get; set; } = string.Empty;
    public string? ContractorId { get; set; }
    public Contractor? Contractor { get; set; }

    // Ship-from snapshot
    public string ShipFromLabel { get; set; } = string.Empty;
    public string? ShipFromAttentionName { get; set; }
    public string? ShipFromCompanyName { get; set; }
    public string ShipFromAddress1 { get; set; } = string.Empty;
    public string? ShipFromAddress2 { get; set; }
    public string ShipFromCity { get; set; } = string.Empty;
    public string ShipFromState { get; set; } = string.Empty;
    public string ShipFromPostalCode { get; set; } = string.Empty;
    public string ShipFromCountryCode { get; set; } = "US";

    // Ship-to snapshot (from Printavo's shippingAddress or a customAddress)
    public string? ShipToLabel { get; set; }
    public string? ShipToAttentionName { get; set; }
    public string? ShipToCompanyName { get; set; }
    public string ShipToAddress1 { get; set; } = string.Empty;
    public string? ShipToAddress2 { get; set; }
    public string ShipToCity { get; set; } = string.Empty;
    public string ShipToState { get; set; } = string.Empty;
    public string ShipToPostalCode { get; set; } = string.Empty;
    public string ShipToCountryCode { get; set; } = "US";

    public ShipmentBillingType BillingType { get; set; }
    public string? BillingAccountLabel { get; set; }
    public string? BillingAccountNumber { get; set; }
    public string? BillingAccountPostalCode { get; set; }

    public string ServiceLevelCode { get; set; } = string.Empty;
    public string ServiceLevelName { get; set; } = string.Empty;
    public int PackageCount { get; set; }
    public decimal? TotalCost { get; set; }

    public string? UpsShipmentIdentificationNumber { get; set; }

    /// <summary>FedEx's shipment transaction id, used to void a FedEx shipment. Only set when Carrier == FedEx — kept as a separate additive field rather than renaming UpsShipmentIdentificationNumber, since that field is load-bearing for every existing UPS row.</summary>
    public string? FedExShipmentTransactionId { get; set; }

    public string? FirstTrackingNumber { get; set; }

    public string? PrintavoTaskId { get; set; }
    public bool CustomerNoteAppended { get; set; }

    /// <summary>The "Shipping" fee staff chose to add to the Printavo invoice (Add Shipping Cost To Invoice), or null if none was requested. Kept after a void (when the fee itself is deleted) as a record of what was charged.</summary>
    public decimal? InvoiceFeeAmount { get; set; }

    /// <summary>Printavo Fee id once feeCreate succeeded — cleared again when Void deletes the fee.</summary>
    public string? PrintavoFeeId { get; set; }

    /// <summary>Whether a requested invoice fee still has to be created. Never for a voided shipment — there's nothing left to charge for.</summary>
    [NotMapped]
    public bool NeedsInvoiceFee =>
        InvoiceFeeAmount is not null && string.IsNullOrWhiteSpace(PrintavoFeeId) && Status != ShipmentStatus.Voided;

    /// <summary>
    /// True for shipments created via the New Shipment screen and linked to
    /// a Printavo order — these always get a single combined Task (tracking
    /// + cost) regardless of BillingType, and never get a customerNote
    /// append. False (default) preserves the Orders-screen behavior
    /// exactly. Persisted (not just an in-memory ship-time flag) because
    /// RetryPrintavoWriteBackAsync can re-invoke write-back after an app
    /// restart and must pick the same write-back style.
    /// </summary>
    public bool UseCombinedTaskWriteBack { get; set; }

    // Shipment reference numbers (printed on the label as REF1/REF2) —
    // ReferenceNumber1 is the order's Printavo PO#, ReferenceNumber2 its
    // Printavo visualId/invoice number.
    public string? ReferenceNumber1 { get; set; }
    public string? ReferenceNumber2 { get; set; }

    // Whether the order had more than one ship-to address available at the
    // time of shipping — controls the customerNote tracking line's wording
    // (address label prefix vs. just the method/tracking).
    public bool HasMultipleShipToAddresses { get; set; }

    public bool QuantumViewEnabled { get; set; }
    public string? QuantumViewPrimaryEmail { get; set; }
    public string? QuantumViewSecondaryEmail { get; set; }

    // Free-text staff note shown in parentheses after the tracking number in
    // the customerNote tracking line, e.g. "(DESIGN 1 & DESIGN 2)".
    public string? Notes { get; set; }

    public ShipmentStatus Status { get; set; } = ShipmentStatus.Draft;
    public string? ErrorMessage { get; set; }

    public string? CreatedByWindowsUser { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? VoidedAt { get; set; }

    /// <summary>Whether the Printavo write-back still has an outstanding step — drives the "Retry Printavo Sync" affordance on a shipment card and in Shipment History. A shipment not linked to any Printavo order (New Shipment, unlinked) never needs one. A UseCombinedTaskWriteBack shipment only ever needs its combined Task; everything else follows the original rule (Task for own-account shipments, customerNote append). Either kind also needs its invoice fee, if one was requested.</summary>
    [NotMapped]
    public bool NeedsPrintavoWriteBack
    {
        get
        {
            if (string.IsNullOrWhiteSpace(PrintavoOrderId))
            {
                return false;
            }

            if (NeedsInvoiceFee)
            {
                return true;
            }

            return UseCombinedTaskWriteBack
                ? string.IsNullOrWhiteSpace(PrintavoTaskId)
                : (BillingType == ShipmentBillingType.OwnAccount && string.IsNullOrWhiteSpace(PrintavoTaskId))
                  || !CustomerNoteAppended;
        }
    }

    /// <summary>CreatedAt converted to the workstation's local time zone, for display/grouping. CreatedAt itself stays UTC (correct for the stored audit trail) — this is a display-boundary conversion only.</summary>
    [NotMapped]
    public DateTimeOffset LocalCreatedAt => CreatedAt.ToLocalTime();

    /// <summary>Calendar-date-only projection of LocalCreatedAt, used as Shipment History's client-side date-group key (PropertyGroupDescription needs a plain property, not an inline expression). Local, not UTC, so an evening shipment groups under the date staff actually shipped it on.</summary>
    [NotMapped]
    public DateTime ShippedDateGroupKey => LocalCreatedAt.Date;

    /// <summary>Packages that actually got a tracking number, in label order — ordered explicitly by PackageIndex since EF's Include(s => s.Packages) doesn't guarantee child order. Drives Shipment History's expandable per-package tracking list. JsonIgnore'd (as are the other tracking helpers below) so ShipmentArchiveService's export doesn't write every package out twice.</summary>
    [NotMapped]
    [JsonIgnore]
    public IReadOnlyList<ShipmentPackage> TrackedPackages =>
        Packages.Where(p => !string.IsNullOrWhiteSpace(p.TrackingNumber)).OrderBy(p => p.PackageIndex).ToList();

    /// <summary>Every tracking number on this shipment, in label order. Falls back to FirstTrackingNumber alone when no package row carries one, so a copy from Shipment History still yields something.</summary>
    [NotMapped]
    [JsonIgnore]
    public IReadOnlyList<string> TrackingNumbers
    {
        get
        {
            var numbers = TrackedPackages.Select(p => p.TrackingNumber!).ToList();
            if (numbers.Count == 0 && !string.IsNullOrWhiteSpace(FirstTrackingNumber))
            {
                numbers.Add(FirstTrackingNumber);
            }

            return numbers;
        }
    }

    /// <summary>The first label's tracking number — the one UPS/FedEx treat as the shipment's master/lead tracking number.</summary>
    [NotMapped]
    [JsonIgnore]
    public string? MasterTrackingNumber =>
        string.IsNullOrWhiteSpace(FirstTrackingNumber) ? TrackingNumbers.FirstOrDefault() : FirstTrackingNumber;

    [NotMapped]
    [JsonIgnore]
    public bool HasTrackingNumber => TrackingNumbers.Count > 0;

    [NotMapped]
    [JsonIgnore]
    public bool HasMultipleTrackingNumbers => TrackingNumbers.Count > 1;

    public List<ShipmentPackage> Packages { get; set; } = new();
}

public class ShipmentPackage
{
    public int Id { get; set; }
    public int ShipmentRecordId { get; set; }

    // JsonIgnore: EF fixes this back-reference up automatically on
    // Include(s => s.Packages), which would otherwise make
    // System.Text.Json throw on a reference cycle when ShipmentArchiveService
    // serializes an archived shipment (with its packages) to the export file.
    [JsonIgnore]
    public ShipmentRecord? ShipmentRecord { get; set; }

    public int PackageIndex { get; set; }
    public decimal WeightLbs { get; set; }
    public decimal? LengthIn { get; set; }
    public decimal? WidthIn { get; set; }
    public decimal? HeightIn { get; set; }

    public string? TrackingNumber { get; set; }
    public string? LabelImagePath { get; set; }
    public DateTimeOffset? PrintedAt { get; set; }
}
