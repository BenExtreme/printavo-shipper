using PrintavoShipper.Core.Models;

namespace PrintavoShipper.Core.Interfaces;

/// <summary>
/// What format labelBytes is actually encoded as, so LabelPrintService
/// knows how to decode it before printing. Gif is UPS's GraphicImage
/// (always needs the rotate/crop fixup — see LabelPrintService). Pdf is
/// FedEx's current label format (rasterized via PDFium at print time
/// for print quality independent of whatever fixed resolution a PNG would
/// have baked in). Png is FedEx's label format from before the PDF switch
/// — kept only so Reprint can still decode label files saved by an older
/// build (see ShippingWorkflowService.ReprintAsync, which derives this from
/// the saved file's extension rather than assuming from carrier).
/// </summary>
public enum LabelSourceFormat
{
    Gif,
    Png,
    Pdf
}

public interface ILabelPrintService
{
    List<string> GetInstalledPrinterNames();

    /// <summary>
    /// Prints a shipment's label images to the given Windows printer as a
    /// single multi-page print job, one label per page in list order — one
    /// spooler job per box was hanging printers on large shipments.
    /// paperMode selects whether each page is sized to exactly 4x6in
    /// (dedicated label printer) or a full 8.5x11in letter sheet with the
    /// 4x6 label centered on it (standard office printer).
    /// </summary>
    Task PrintLabelsAsync(IReadOnlyList<(byte[] Bytes, LabelSourceFormat Format)> labels, string printerName, LabelPaperMode paperMode, string? documentName = null, CancellationToken ct = default);

    /// <summary>Prints a simple text-based test page so a printer selection can be verified without a real label.</summary>
    Task PrintTestLabelAsync(string printerName, LabelPaperMode paperMode, CancellationToken ct = default);

    /// <summary>Prints boxCount Pick-up box labels (one page each, numbered "Box 1 of N"...) as a single print job.</summary>
    Task PrintBoxLabelsAsync(BoxLabelContent content, int boxCount, string printerName, LabelPaperMode paperMode, CancellationToken ct = default);

    /// <summary>Saves a label image to disk under the labels folder. fileExtension has no leading dot (e.g. "gif", "png").</summary>
    string SaveLabelToFile(byte[] imageBytes, string fileNameWithoutExtension, string fileExtension);
}
