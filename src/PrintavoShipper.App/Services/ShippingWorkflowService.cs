using System.IO;
using PrintavoShipper.Core.Interfaces;
using PrintavoShipper.Core.Models;
using PrintavoShipper.FedEx;
using PrintavoShipper.Printavo;
using PrintavoShipper.Ups;

namespace PrintavoShipper.App.Services;

public class ShippingWorkflowService : IShippingWorkflowService
{
    private readonly IUpsClient _upsClient;
    private readonly IFedExClient _fedExClient;
    private readonly ILabelPrintService _labelPrintService;
    private readonly IShipmentRepository _shipmentRepository;
    private readonly ISettingsService _settingsService;
    private readonly IPrintavoClient _printavoClient;

    public ShippingWorkflowService(
        IUpsClient upsClient,
        IFedExClient fedExClient,
        ILabelPrintService labelPrintService,
        IShipmentRepository shipmentRepository,
        ISettingsService settingsService,
        IPrintavoClient printavoClient)
    {
        _upsClient = upsClient;
        _fedExClient = fedExClient;
        _labelPrintService = labelPrintService;
        _shipmentRepository = shipmentRepository;
        _settingsService = settingsService;
        _printavoClient = printavoClient;
    }

    public async Task<ShipAndPrintOutcome> ShipAndPrintAsync(ShipAndPrintInput input, CancellationToken ct = default)
    {
        var upsRequest = new UpsShipmentRequest(
            input.ShipFrom,
            input.ShipTo,
            input.ShipperAccountNumber,
            input.BillingType,
            input.BillingAccountNumber,
            input.BillingAccountPostalCode,
            input.BillingAccountCountryCode,
            input.ServiceCode,
            input.ServiceDescription,
            input.Packages,
            $"Order #{input.PrintavoVisualId}",
            input.ReferenceNumber1,
            input.ReferenceNumber2,
            input.EnableQuantumView,
            input.QuantumViewPrimaryEmail,
            input.QuantumViewSecondaryEmail);

        var result = await _upsClient.CreateShipmentAsync(upsRequest, ct);
        var record = BuildShipmentRecord(input, result);
        var labelBytesByPackage = result.Packages.Select(p => p.LabelImageBytes).ToList();

        return await FinishShipmentAsync(record, labelBytesByPackage, LabelSourceFormat.Gif, ct);
    }

    public async Task<ShipAndPrintOutcome> ShipAndPrintAsync(FedExShipAndPrintInput input, CancellationToken ct = default)
    {
        var fedExRequest = new FedExShipmentRequest(
            input.ShipFrom,
            input.ShipTo,
            input.ShipperAccountNumber,
            input.BillingType,
            input.BillingAccountNumber,
            input.BillingAccountPostalCode,
            input.BillingAccountCountryCode,
            input.ServiceType,
            input.ServiceDescription,
            input.Packages,
            $"Order #{input.PrintavoVisualId}",
            input.ReferenceNumber1,
            input.ReferenceNumber2);

        var result = await _fedExClient.CreateShipmentAsync(fedExRequest, ct);
        var record = BuildShipmentRecord(input, result);
        var labelBytesByPackage = result.Packages.Select(p => p.LabelImageBytes).ToList();

        return await FinishShipmentAsync(record, labelBytesByPackage, LabelSourceFormat.Pdf, ct);
    }

    /// <summary>
    /// The part of "Ship &amp; Print" that's genuinely carrier-agnostic once a
    /// ShipmentRecord + raw label bytes exist: print each label, run the
    /// Printavo write-back, and persist the record. Shared by both
    /// ShipAndPrintAsync overloads so this orchestration logic — which must
    /// stay correct for UPS — is never duplicated per carrier.
    ///
    /// Printing and the write-back run concurrently: they're independent
    /// (they set different fields on the record, and both continuations run
    /// on the caller's UI context, so there's no data race), and the save
    /// still waits for both so it captures PrintedAt + the Printavo results.
    /// </summary>
    private async Task<ShipAndPrintOutcome> FinishShipmentAsync(
        ShipmentRecord record,
        IReadOnlyList<byte[]> labelBytesByPackage,
        LabelSourceFormat sourceFormat,
        CancellationToken ct)
    {
        var printTask = PrintLabelsAsync(record, labelBytesByPackage, sourceFormat, ct);
        var writeBackTask = PerformPrintavoWriteBackAsync(record, ct);
        await Task.WhenAll(printTask, writeBackTask);

        var saved = await _shipmentRepository.SaveAsync(record, ct);
        return new ShipAndPrintOutcome(saved, await printTask, await writeBackTask);
    }

    /// <summary>
    /// Prints every package's label in order as one print job, stamping
    /// PrintedAt on each package; returns a warning instead of throwing.
    /// </summary>
    private async Task<string?> PrintLabelsAsync(
        ShipmentRecord record,
        IReadOnlyList<byte[]> labelBytesByPackage,
        LabelSourceFormat sourceFormat,
        CancellationToken ct)
    {
        var settings = await _settingsService.GetSettingsAsync(ct);
        if (string.IsNullOrWhiteSpace(settings.SelectedPrinterName))
        {
            return "No printer selected in Settings — labels were saved but not printed.";
        }

        var packages = record.Packages.Take(labelBytesByPackage.Count).ToList();
        var labels = labelBytesByPackage.Take(packages.Count).Select(bytes => (bytes, sourceFormat)).ToList();
        try
        {
            await _labelPrintService.PrintLabelsAsync(labels, settings.SelectedPrinterName!, settings.LabelPaperMode, BuildPrintDocumentName(record), ct);
            var printedAt = DateTimeOffset.UtcNow;
            foreach (var pkgRecord in packages)
            {
                pkgRecord.PrintedAt = printedAt;
            }

            return null;
        }
        catch (Exception ex)
        {
            return $"Shipment purchased and label(s) saved, but printing failed: {ex.Message}";
        }
    }

    /// <summary>The name the print job shows in the Windows print queue (and "Print to PDF"'s default file name).</summary>
    private static string BuildPrintDocumentName(ShipmentRecord record) =>
        !string.IsNullOrWhiteSpace(record.PrintavoVisualId)
            ? $"Shipping labels #{record.PrintavoVisualId}"
            : $"Shipping labels {record.FirstTrackingNumber}".TrimEnd();

    public async Task<(ShipmentRecord Record, string? Warning)> RetryPrintavoWriteBackAsync(ShipmentRecord record, CancellationToken ct = default)
    {
        var warning = await PerformPrintavoWriteBackAsync(record, ct);
        var saved = await _shipmentRepository.SaveAsync(record, ct);
        return (saved, warning);
    }

    /// <summary>
    /// Attempts every Printavo write-back step for a shipment (task, note,
    /// invoice fee), mutating PrintavoTaskId/CustomerNoteAppended/
    /// PrintavoFeeId on success. Each step is skipped if it already succeeded
    /// or doesn't apply (supports safe re-invocation from
    /// RetryPrintavoWriteBackAsync) and failures never throw — they're
    /// collected into the returned warning text instead, since no step
    /// should ever be able to make an already-purchased, already-printed
    /// shipment look like it failed. The steps are independent of each
    /// other, so they run concurrently; warnings still come out in a fixed
    /// order (task, note, fee).
    /// </summary>
    private async Task<string?> PerformPrintavoWriteBackAsync(ShipmentRecord record, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(record.PrintavoOrderId))
        {
            // New Shipment, not linked to any order — nothing to write back to.
            return null;
        }

        // New-Shipment-screen shipments linked to an order: always a single
        // combined tracking+cost Task, never a customerNote append.
        var taskWarning = CreateShippingCostTaskAsync(record, ct);
        var noteWarning = record.UseCombinedTaskWriteBack
            ? Task.FromResult<string?>(null)
            : PrependTrackingNoteAsync(record, ct);
        var feeWarning = AddInvoiceFeeAsync(record, ct);
        await Task.WhenAll(taskWarning, noteWarning, feeWarning);

        var warnings = new[] { await taskWarning, await noteWarning, await feeWarning }.OfType<string>().ToList();
        return warnings.Count > 0 ? string.Join(" ", warnings) : null;
    }

    /// <summary>"Add Shipping Cost To Invoice": a single Shipping fee line on the order's invoice, priced at ship time (see ShippingFeeCalculator).</summary>
    private async Task<string?> AddInvoiceFeeAsync(ShipmentRecord record, CancellationToken ct)
    {
        if (!record.NeedsInvoiceFee)
        {
            return null;
        }

        try
        {
            record.PrintavoFeeId = await _printavoClient.CreateInvoiceFeeAsync(
                record.PrintavoOrderId, "Shipping", record.InvoiceFeeAmount!.Value, ct);
            return null;
        }
        catch (Exception ex)
        {
            return $"Shipping fee not added to invoice: {ex.Message}";
        }
    }

    private async Task<string?> CreateShippingCostTaskAsync(ShipmentRecord record, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(record.PrintavoTaskId))
        {
            return null;
        }

        // Expense substitute — Printavo's API has no expenseCreate mutation,
        // so own-account shipping cost is tracked as a Task instead. Not
        // needed for third-party-billed shipments since the shop isn't
        // paying for those. UseCombinedTaskWriteBack shipments (New Shipment
        // screen, linked to an order) always get the task regardless of who
        // pays, since it's the sole record of the tracking number too.
        if (!record.UseCombinedTaskWriteBack && record.BillingType != ShipmentBillingType.OwnAccount)
        {
            return null;
        }

        try
        {
            // Goes to the user (or the order's owner) picked in Settings >
            // Printavo Workflow — not to whichever Printavo login this PC's
            // Settings last ran Test Connection with, which varies by
            // workstation.
            var settings = await _settingsService.GetSettingsAsync(ct);
            var assigneeId = settings.ShippingCostTaskToOrderOwner
                ? await TryGetOrderOwnerIdAsync(record.PrintavoOrderId, ct)
                : settings.ShippingCostTaskAssigneeUserId;
            var taskName = BuildTaskName(record);
            record.PrintavoTaskId = await _printavoClient.CreateTaskAsync(
                taskName, DateTimeOffset.Now, assigneeId, record.PrintavoOrderId, ct);
            return null;
        }
        catch (Exception ex)
        {
            return $"Printavo task not created: {ex.Message}";
        }
    }

    /// <summary>Null if the order has no owner or the lookup fails — the task is still created, just unassigned, rather than lost.</summary>
    private async Task<string?> TryGetOrderOwnerIdAsync(string orderId, CancellationToken ct)
    {
        try
        {
            return await _printavoClient.GetOrderOwnerIdAsync(orderId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    private async Task<string?> PrependTrackingNoteAsync(ShipmentRecord record, CancellationToken ct)
    {
        if (record.CustomerNoteAppended)
        {
            return null;
        }

        try
        {
            // Printavo has no server-side append — read the current note
            // fresh right before writing so a concurrent edit elsewhere
            // isn't clobbered, then send the combined HTML back. Never reuse
            // the note the order screen loaded earlier: it could be stale,
            // and would drop tracking lines added since it was fetched.
            var currentNote = await _printavoClient.GetCustomerNoteAsync(record.PrintavoOrderId, ct);
            var item = BuildTrackingNoteItem(record);
            // A note already opening with a bulleted list (typically an
            // earlier shipment's tracking bullet) gets this one inserted as
            // its first item, so repeat shipments stack into one list instead
            // of a run of separate single-item lists.
            const string listOpen = "<ul>";
            var newNote = currentNote.StartsWith(listOpen, StringComparison.OrdinalIgnoreCase)
                ? listOpen + item + currentNote[listOpen.Length..]
                : $"{listOpen}{item}</ul>{currentNote}";
            await _printavoClient.UpdateCustomerNoteAsync(record.PrintavoOrderId, newNote, ct);
            record.CustomerNoteAppended = true;
            return null;
        }
        catch (Exception ex)
        {
            return $"Customer note not updated: {ex.Message}";
        }
    }

    private static string BuildTaskName(ShipmentRecord record)
    {
        var boxes = record.PackageCount == 1 ? "1 box" : $"{record.PackageCount} boxes";
        var cost = record.TotalCost is { } c ? c.ToString("C") : "cost n/a";
        var baseText = $"Shipped {record.ServiceLevelName}, {boxes}, {cost}";

        if (!record.UseCombinedTaskWriteBack)
        {
            return baseText;
        }

        // Printavo's Task type has no description/body field — only name —
        // so the New Shipment screen's combined write-back folds the
        // tracking number into this same line rather than a customerNote.
        var trackingText = record.FirstTrackingNumber ?? "n/a";
        return $"{baseText} — Tracking {trackingText}";
    }

    /// <summary>
    /// One bullet (an li, wrapped into a list by the caller) per shipment,
    /// matching this shop's existing customerNote
    /// shorthand convention (short, all-caps flags like "SPLIT SHIPS - SEE
    /// BELOW"). An order with only one possible ship-to address just gets
    /// the method and tracking number; an order with multiple ship-to
    /// addresses available (customAddresses on the order) is prefixed with
    /// which address this particular shipment went to, since staff need to
    /// tell the accumulated lines apart. Only the first package's tracking
    /// number is shown (multi-box shipments all travel together, so
    /// customers only need one number to track the shipment) and it's
    /// bolded for visibility. Prepended (not appended) so the most recent
    /// shipment is always the first thing the customer sees. An optional
    /// staff note (e.g. "DESIGN 1 & DESIGN 2") is appended in parentheses
    /// after the tracking number — HTML-encoded since it's free text
    /// landing straight in an HTML field.
    /// </summary>
    private static string BuildTrackingNoteItem(ShipmentRecord record)
    {
        var trackingText = record.FirstTrackingNumber ?? "n/a";
        var service = record.ServiceLevelName.ToUpperInvariant();
        var notesSuffix = string.IsNullOrWhiteSpace(record.Notes)
            ? string.Empty
            : $" ({System.Net.WebUtility.HtmlEncode(record.Notes.Trim())})";

        var line = record.HasMultipleShipToAddresses && !string.IsNullOrWhiteSpace(record.ShipToLabel)
            ? $"{record.ShipToLabel.ToUpperInvariant()} - {service}: <strong>{trackingText}</strong>{notesSuffix}"
            : $"{service}: <strong>{trackingText}</strong>{notesSuffix}";

        return $"<li>{line}</li>";
    }

    public async Task<(ShipmentRecord Record, string? Warning)> VoidShipmentAsync(ShipmentRecord record, CancellationToken ct = default)
    {
        if (record.Carrier == ShipmentCarrier.FedEx)
        {
            if (string.IsNullOrWhiteSpace(record.FedExShipmentTransactionId))
            {
                throw new InvalidOperationException("This shipment record has no FedEx transaction id to void.");
            }

            await _fedExClient.VoidShipmentAsync(record.FedExShipmentTransactionId, ct);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(record.UpsShipmentIdentificationNumber))
            {
                throw new InvalidOperationException("This shipment record has no UPS shipment identification number to void.");
            }

            await _upsClient.VoidShipmentAsync(record.UpsShipmentIdentificationNumber, ct);
        }

        record.Status = ShipmentStatus.Voided;
        record.VoidedAt = DateTimeOffset.UtcNow;

        // The label is already voided at the carrier, so a failure here must
        // never look like the void itself failed — it's reported alongside.
        string? warning = null;
        if (!string.IsNullOrWhiteSpace(record.PrintavoFeeId))
        {
            try
            {
                await _printavoClient.DeleteFeeAsync(record.PrintavoFeeId, ct);
                record.PrintavoFeeId = null;
            }
            catch (Exception ex)
            {
                warning = $"Shipment voided, but its Shipping fee couldn't be removed from the Printavo invoice — remove it there by hand: {ex.Message}";
            }
        }

        return (await _shipmentRepository.SaveAsync(record, ct), warning);
    }

    public async Task ReprintAsync(ShipmentRecord record, CancellationToken ct = default)
    {
        var settings = await _settingsService.GetSettingsAsync(ct);
        if (string.IsNullOrWhiteSpace(settings.SelectedPrinterName))
        {
            throw new InvalidOperationException("No printer selected in Settings.");
        }

        var labels = new List<(byte[] Bytes, LabelSourceFormat Format)>();
        foreach (var package in record.Packages)
        {
            if (string.IsNullOrWhiteSpace(package.LabelImagePath) || !File.Exists(package.LabelImagePath))
            {
                continue;
            }

            var bytes = await File.ReadAllBytesAsync(package.LabelImagePath, ct);
            labels.Add((bytes, SourceFormatForReprint(record.Carrier, package.LabelImagePath)));
        }

        await _labelPrintService.PrintLabelsAsync(labels, settings.SelectedPrinterName!, settings.LabelPaperMode, BuildPrintDocumentName(record), ct);
    }

    /// <summary>
    /// Derived from the saved label file's own extension rather than just
    /// the shipment's carrier — a FedEx shipment purchased before the
    /// PDF-label switch has a .png file on disk and must still be decoded
    /// as one, while a newer FedEx shipment has a .pdf file. UPS is always
    /// .gif regardless of when it was purchased.
    /// </summary>
    private static LabelSourceFormat SourceFormatForReprint(ShipmentCarrier carrier, string labelImagePath)
    {
        if (carrier == ShipmentCarrier.Ups)
        {
            return LabelSourceFormat.Gif;
        }

        return Path.GetExtension(labelImagePath).Equals(".png", StringComparison.OrdinalIgnoreCase)
            ? LabelSourceFormat.Png
            : LabelSourceFormat.Pdf;
    }

    private ShipmentRecord BuildShipmentRecord(ShipAndPrintInput input, UpsShipmentResult result)
    {
        var record = new ShipmentRecord
        {
            Carrier = ShipmentCarrier.Ups,
            PrintavoOrderId = input.PrintavoOrderId,
            PrintavoVisualId = input.PrintavoVisualId,
            ContractorId = input.ContractorId,

            ShipFromLabel = input.ShipFromLabel,
            ShipFromAttentionName = input.ShipFrom.AttentionName,
            ShipFromCompanyName = input.ShipFrom.Name,
            ShipFromAddress1 = input.ShipFrom.Address1,
            ShipFromAddress2 = input.ShipFrom.Address2,
            ShipFromCity = input.ShipFrom.City,
            ShipFromState = input.ShipFrom.State,
            ShipFromPostalCode = input.ShipFrom.PostalCode,
            ShipFromCountryCode = input.ShipFrom.CountryCode,

            ShipToLabel = input.ShipToLabel,
            ShipToAttentionName = input.ShipTo.AttentionName,
            ShipToCompanyName = input.ShipTo.Name,
            ShipToAddress1 = input.ShipTo.Address1,
            ShipToAddress2 = input.ShipTo.Address2,
            ShipToCity = input.ShipTo.City,
            ShipToState = input.ShipTo.State,
            ShipToPostalCode = input.ShipTo.PostalCode,
            ShipToCountryCode = input.ShipTo.CountryCode,

            BillingType = input.BillingType == UpsBillingType.ThirdParty ? ShipmentBillingType.ThirdParty : ShipmentBillingType.OwnAccount,
            BillingAccountLabel = input.BillingAccountLabel,
            BillingAccountNumber = input.BillingType == UpsBillingType.ThirdParty ? input.BillingAccountNumber : input.ShipperAccountNumber,
            BillingAccountPostalCode = input.BillingAccountPostalCode,

            ServiceLevelCode = input.ServiceCode,
            ServiceLevelName = input.ServiceDescription,
            PackageCount = input.Packages.Count,
            // The shop-origin Get Rates quote, never result.TotalCost: the
            // carrier rates the purchase from the label's ship-from (often a
            // contractor's address), but boxes always physically leave the
            // shop, so cost is always as if shipped from Default Ship-From.
            TotalCost = input.RatedCost,
            InvoiceFeeAmount = input.InvoiceFeeAmount,

            UpsShipmentIdentificationNumber = result.ShipmentIdentificationNumber,
            FirstTrackingNumber = result.Packages.Count > 0 ? result.Packages[0].TrackingNumber : null,

            ReferenceNumber1 = input.ReferenceNumber1,
            ReferenceNumber2 = input.ReferenceNumber2,
            HasMultipleShipToAddresses = input.HasMultipleShipToAddresses,
            QuantumViewEnabled = input.EnableQuantumView,
            QuantumViewPrimaryEmail = input.QuantumViewPrimaryEmail,
            QuantumViewSecondaryEmail = input.QuantumViewSecondaryEmail,
            Notes = input.Notes,
            UseCombinedTaskWriteBack = input.UseCombinedTaskWriteBack,

            Status = ShipmentStatus.Purchased,
            CreatedByWindowsUser = Environment.UserName
        };

        var index = 1;
        foreach (var (pkgResult, pkgInput) in result.Packages.Zip(input.Packages))
        {
            var labelPath = _labelPrintService.SaveLabelToFile(
                pkgResult.LabelImageBytes,
                $"{result.ShipmentIdentificationNumber}_{index}_{pkgResult.TrackingNumber}",
                "gif");

            record.Packages.Add(new ShipmentPackage
            {
                PackageIndex = index,
                WeightLbs = pkgInput.WeightLbs,
                LengthIn = pkgInput.LengthIn,
                WidthIn = pkgInput.WidthIn,
                HeightIn = pkgInput.HeightIn,
                TrackingNumber = pkgResult.TrackingNumber,
                LabelImagePath = labelPath
            });
            index++;
        }

        return record;
    }

    private ShipmentRecord BuildShipmentRecord(FedExShipAndPrintInput input, FedExShipmentResult result)
    {
        var record = new ShipmentRecord
        {
            Carrier = ShipmentCarrier.FedEx,
            PrintavoOrderId = input.PrintavoOrderId,
            PrintavoVisualId = input.PrintavoVisualId,
            ContractorId = input.ContractorId,

            ShipFromLabel = input.ShipFromLabel,
            ShipFromAttentionName = input.ShipFrom.AttentionName,
            ShipFromCompanyName = input.ShipFrom.Name,
            ShipFromAddress1 = input.ShipFrom.Address1,
            ShipFromAddress2 = input.ShipFrom.Address2,
            ShipFromCity = input.ShipFrom.City,
            ShipFromState = input.ShipFrom.State,
            ShipFromPostalCode = input.ShipFrom.PostalCode,
            ShipFromCountryCode = input.ShipFrom.CountryCode,

            ShipToLabel = input.ShipToLabel,
            ShipToAttentionName = input.ShipTo.AttentionName,
            ShipToCompanyName = input.ShipTo.Name,
            ShipToAddress1 = input.ShipTo.Address1,
            ShipToAddress2 = input.ShipTo.Address2,
            ShipToCity = input.ShipTo.City,
            ShipToState = input.ShipTo.State,
            ShipToPostalCode = input.ShipTo.PostalCode,
            ShipToCountryCode = input.ShipTo.CountryCode,

            BillingType = input.BillingType == FedExBillingType.ThirdParty ? ShipmentBillingType.ThirdParty : ShipmentBillingType.OwnAccount,
            BillingAccountLabel = input.BillingAccountLabel,
            BillingAccountNumber = input.BillingType == FedExBillingType.ThirdParty ? input.BillingAccountNumber : input.ShipperAccountNumber,
            BillingAccountPostalCode = input.BillingAccountPostalCode,

            ServiceLevelCode = input.ServiceType,
            ServiceLevelName = input.ServiceDescription,
            PackageCount = input.Packages.Count,
            // Shop-origin Get Rates quote, never result.TotalCost — see the UPS overload.
            TotalCost = input.RatedCost,

            FedExShipmentTransactionId = result.TransactionId,
            FirstTrackingNumber = result.Packages.Count > 0 ? result.Packages[0].TrackingNumber : null,

            ReferenceNumber1 = input.ReferenceNumber1,
            ReferenceNumber2 = input.ReferenceNumber2,
            HasMultipleShipToAddresses = input.HasMultipleShipToAddresses,
            Notes = input.Notes,
            UseCombinedTaskWriteBack = input.UseCombinedTaskWriteBack,

            Status = ShipmentStatus.Purchased,
            CreatedByWindowsUser = Environment.UserName
        };

        var index = 1;
        foreach (var (pkgResult, pkgInput) in result.Packages.Zip(input.Packages))
        {
            var labelPath = _labelPrintService.SaveLabelToFile(
                pkgResult.LabelImageBytes,
                $"{result.TransactionId}_{index}_{pkgResult.TrackingNumber}",
                "pdf");

            record.Packages.Add(new ShipmentPackage
            {
                PackageIndex = index,
                WeightLbs = pkgInput.WeightLbs,
                LengthIn = pkgInput.LengthIn,
                WidthIn = pkgInput.WidthIn,
                HeightIn = pkgInput.HeightIn,
                TrackingNumber = pkgResult.TrackingNumber,
                LabelImagePath = labelPath
            });
            index++;
        }

        return record;
    }
}
