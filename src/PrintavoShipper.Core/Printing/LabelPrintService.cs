using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.Runtime.InteropServices;
using PrintavoShipper.Core.Data;
using PrintavoShipper.Core.Interfaces;
using PrintavoShipper.Core.Models;

namespace PrintavoShipper.Core.Printing;

/// <summary>
/// Prints to the Omezizy D520BT (and similar consumer thermal printers) via
/// the standard Windows print spooler — these install as a normal raster
/// printer driver, not a raw ZPL/socket device, so a plain PrintDocument at
/// 4x6in is the right approach rather than sending ZPL commands. Also
/// supports printing to a standard office printer loaded with 8.5x11in
/// letter paper, with the 4x6 label drawn at true size and centered on the
/// sheet (LabelPaperMode.Letter8x11Centered) — for shops without a
/// dedicated label printer.
///
/// Every print job (decode + spool) runs on its own background STA thread,
/// one job at a time, so the UI stays responsive while a label prints —
/// STA rather than a thread-pool thread because some printer drivers show
/// their own UI (e.g. "Microsoft Print to PDF"'s Save dialog).
/// </summary>
public class LabelPrintService : ILabelPrintService
{
    private const float LabelWidthInches = 4f;
    private const float LabelHeightInches = 6f;
    private const float LetterWidthInches = 8.5f;
    private const float LetterHeightInches = 11f;

    /// <summary>
    /// One print job at a time, app-wide: keeps a multi-box shipment's
    /// labels in order even if two shipment cards print at once, and keeps
    /// PDFium (not thread-safe) single-threaded.
    /// </summary>
    private static readonly SemaphoreSlim PrintJobGate = new(1, 1);

    public List<string> GetInstalledPrinterNames()
    {
        var names = new List<string>();
        foreach (string printer in PrinterSettings.InstalledPrinters)
        {
            names.Add(printer);
        }

        return names;
    }

    public Task PrintLabelsAsync(IReadOnlyList<(byte[] Bytes, LabelSourceFormat Format)> labels, string printerName, LabelPaperMode paperMode, string? documentName = null, CancellationToken ct = default) =>
        labels.Count == 0
            ? Task.CompletedTask
            : RunPrintJobAsync(() => PrintLabels(labels, printerName, paperMode, documentName), ct);

    private static async Task RunPrintJobAsync(Action job, CancellationToken ct)
    {
        await PrintJobGate.WaitAsync(ct);
        try
        {
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() =>
            {
                try
                {
                    job();
                    done.SetResult();
                }
                catch (Exception ex)
                {
                    done.SetException(ex);
                }
            })
            {
                IsBackground = true,
                Name = "Label print job"
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            await done.Task;
        }
        finally
        {
            PrintJobGate.Release();
        }
    }

    /// <summary>
    /// All labels go out as one multi-page print job. Each page's bitmap is
    /// decoded only when that page prints and disposed right after, rather
    /// than all up front — a rasterized label is ~7-9 MB, so a big
    /// shipment would otherwise hold them all in memory at once.
    /// </summary>
    private static void PrintLabels(IReadOnlyList<(byte[] Bytes, LabelSourceFormat Format)> labels, string printerName, LabelPaperMode paperMode, string? documentName)
    {
        using var doc = new PrintDocument();
        doc.PrinterSettings.PrinterName = printerName;
        if (!string.IsNullOrWhiteSpace(documentName))
        {
            doc.DocumentName = documentName;
        }

        var destRect = ConfigurePaperMode(doc, paperMode);

        var index = 0;
        doc.PrintPage += (_, e) =>
        {
            if (e.Graphics is null)
            {
                return;
            }

            var (bytes, format) = labels[index];
            using var image = DecodeToBitmap(bytes, format);

            // High-quality interpolation matters here specifically because
            // the source bitmap's resolution (fixed by the carrier for a
            // raster format, or our own chosen rasterization DPI for a PDF)
            // rarely matches the printer's native DPI — GDI+'s default
            // interpolation looks visibly soft/blocky when scaling either
            // up or down across that mismatch.
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            e.Graphics.DrawImage(image, destRect);

            index++;
            e.HasMorePages = index < labels.Count;
        };

        doc.Print();
    }

    /// <summary>
    /// Decodes raw label bytes into a printable bitmap. Gif (UPS) and Png
    /// (legacy FedEx) go through GDI+'s native decoder; Pdf (current FedEx)
    /// is rasterized via PDFium first since GDI+ cannot read PDF at all.
    /// </summary>
    private static Bitmap DecodeToBitmap(byte[] imageBytes, LabelSourceFormat sourceFormat)
    {
        if (sourceFormat == LabelSourceFormat.Pdf)
        {
            return RasterizePdfToBitmap(imageBytes);
        }

        using var ms = new MemoryStream(imageBytes);
        using var rawImage = Image.FromStream(ms);
        return sourceFormat == LabelSourceFormat.Gif ? BuildUpsFixedUpImage(rawImage) : new Bitmap(rawImage);
    }

    /// <summary>
    /// Rasterizes FedEx's PDF label at a fixed high DPI (independent of
    /// whatever resolution a pre-rasterized PNG would have been fixed to)
    /// for print quality, straight into a GDI+ bitmap the rest of the
    /// pipeline (PrintDocument + DrawImage) uses as-is.
    ///
    /// FedEx's PDF page is NOT cropped to 4x6in — confirmed empirically
    /// against two real labels (Express and Ground, different content):
    /// it's a full US Letter (8.5x11in) page with the 4x6 label graphic
    /// positioned in the top-left corner, the rest left blank. Left
    /// uncropped, that blank page gets stretched into the print pipeline's
    /// 4x6 box right along with the real label, squeezing the actual
    /// content down to ~4/8.5 x 6/11 of the box. CropToLabelContent trims
    /// that away — this is the PDF equivalent of the UPS GIF fixup below,
    /// just auto-detected instead of a fixed known fraction since there's
    /// no confirmed documented offset to hardcode.
    /// </summary>
    private static Bitmap RasterizePdfToBitmap(byte[] pdfBytes)
    {
        var page = PdfiumRasterizer.RenderFirstPage(pdfBytes, dpi: 300);
        var contentBounds = FindLabelContentBounds(page);
        if (contentBounds.Size == page.Size)
        {
            return page;
        }

        using (page)
        {
            return page.Clone(contentBounds, page.PixelFormat);
        }
    }

    /// <summary>
    /// Finds the bounding box of a rasterized PDF page's actual (non-white)
    /// printed content, with a small padding margin so anti-aliased edges
    /// and any intentional quiet-zone whitespace right at the label's own
    /// designed border aren't clipped. Falls back to the full page if it
    /// somehow finds no content (e.g. a blank test PDF) rather than
    /// producing a degenerate crop.
    /// </summary>
    private static Rectangle FindLabelContentBounds(Bitmap page)
    {
        const byte whiteThreshold = 248;
        var width = page.Width;
        var height = page.Height;

        int minX = width, maxX = -1, minY = height, maxY = -1;
        var data = page.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            // Format32bppArgb rows are B,G,R,A bytes per pixel; scanned one
            // row at a time through a reused buffer rather than copying the
            // whole ~34 MB page (Letter at 300 DPI) into managed memory.
            var row = new byte[width * 4];
            for (var y = 0; y < height; y++)
            {
                Marshal.Copy(data.Scan0 + y * data.Stride, row, 0, row.Length);
                for (var x = 0; x < width; x++)
                {
                    var i = x * 4;
                    var isBlank = row[i + 3] == 0 || (row[i + 2] >= whiteThreshold && row[i + 1] >= whiteThreshold && row[i] >= whiteThreshold);
                    if (isBlank)
                    {
                        continue;
                    }

                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }
        finally
        {
            page.UnlockBits(data);
        }

        if (maxX < 0)
        {
            return new Rectangle(0, 0, width, height);
        }

        var paddingX = Math.Max(4, (maxX - minX) / 100);
        var paddingY = Math.Max(4, (maxY - minY) / 100);
        var left = Math.Max(0, minX - paddingX);
        var top = Math.Max(0, minY - paddingY);
        var right = Math.Min(width, maxX + paddingX + 1);
        var bottom = Math.Min(height, maxY + paddingY + 1);

        return Rectangle.FromLTRB(left, top, right, bottom);
    }

    /// <summary>
    /// Sizes/margins the page for the given mode and returns the rectangle
    /// (in the same hundredths-of-an-inch units PrintDocument uses for
    /// page/margin bounds) the label content should be drawn into: the full
    /// page for a dedicated 4x6 label printer, or a true-size 4x6 box
    /// centered on a full letter sheet for a standard office printer.
    /// </summary>
    private static Rectangle ConfigurePaperMode(PrintDocument doc, LabelPaperMode paperMode)
    {
        var labelWidth = (int)(LabelWidthInches * 100);
        var labelHeight = (int)(LabelHeightInches * 100);

        if (paperMode == LabelPaperMode.Letter8x11Centered)
        {
            var pageWidth = (int)(LetterWidthInches * 100);
            var pageHeight = (int)(LetterHeightInches * 100);
            doc.DefaultPageSettings.PaperSize = new PaperSize("Letter", pageWidth, pageHeight);
            doc.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
            doc.OriginAtMargins = false;

            return new Rectangle((pageWidth - labelWidth) / 2, (pageHeight - labelHeight) / 2, labelWidth, labelHeight);
        }

        doc.DefaultPageSettings.PaperSize = new PaperSize("Label4x6", labelWidth, labelHeight);
        doc.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
        doc.OriginAtMargins = false;

        return new Rectangle(0, 0, labelWidth, labelHeight);
    }

    /// <summary>
    /// UPS's GraphicImage for a 4x6 thermal label comes back landscape
    /// (confirmed against a real label: 1400x800px, content rotated 90°
    /// relative to how it reads) — rotate it back to portrait before
    /// printing. Confirmed live: this was coming out sideways on both a
    /// physical printer and "Microsoft Print to PDF", which ruled out a
    /// printer-driver quirk and pointed at the source image itself. UPS also
    /// bakes in a fixed blank margin below the real label content —
    /// confirmed empirically across two different real labels (different
    /// shipments, different content): the non-blank content always occupies
    /// exactly the top 6/7 of the rotated image, and that content region's
    /// own aspect ratio lands almost exactly on 4:6 once isolated. Left
    /// uncropped, that margin gets stretched into the printed 4x6 area along
    /// with everything else, which both wastes label stock as a blank bar
    /// at the bottom and slightly distorts the real content's proportions.
    /// Both quirks are only ever confirmed for UPS's GIF — never apply this
    /// to a FedEx label.
    /// </summary>
    private static Bitmap BuildUpsFixedUpImage(Image rawImage)
    {
        using var rotated = new Bitmap(rawImage);
        rotated.RotateFlip(RotateFlipType.Rotate90FlipNone);

        var contentHeight = rotated.Height * 6 / 7;
        return rotated.Clone(new Rectangle(0, 0, rotated.Width, contentHeight), rotated.PixelFormat);
    }

    public Task PrintTestLabelAsync(string printerName, LabelPaperMode paperMode, CancellationToken ct = default) =>
        RunPrintJobAsync(() => PrintTestLabel(printerName, paperMode), ct);

    private static void PrintTestLabel(string printerName, LabelPaperMode paperMode)
    {
        using var doc = new PrintDocument();
        doc.PrinterSettings.PrinterName = printerName;
        var destRect = ConfigurePaperMode(doc, paperMode);
        var textBounds = Rectangle.Inflate(destRect, -10, -10);

        doc.PrintPage += (_, e) =>
        {
            if (e.Graphics is null)
            {
                return;
            }

            // Outline the 4x6 label area on a full letter sheet so staff can
            // visually confirm it's centered — not needed on a dedicated
            // 4x6 printer, where the label area already is the whole page.
            if (paperMode == LabelPaperMode.Letter8x11Centered)
            {
                e.Graphics.DrawRectangle(Pens.Black, destRect);
            }

            using var font = new Font("Consolas", 12);
            var text = "Printavo Shipper\nTest label\n4x6in\n" + paperMode + "\n" + DateTimeOffset.Now.ToString("g");
            e.Graphics.DrawString(text, font, Brushes.Black, textBounds);
        };

        doc.Print();
    }

    public Task PrintBoxLabelsAsync(BoxLabelContent content, int boxCount, string printerName, LabelPaperMode paperMode, CancellationToken ct = default) =>
        RunPrintJobAsync(() => PrintBoxLabels(content, boxCount, printerName, paperMode), ct);

    /// <summary>All boxes go out as one multi-page print job, "Box 1 of N" through "Box N of N".</summary>
    private static void PrintBoxLabels(BoxLabelContent content, int boxCount, string printerName, LabelPaperMode paperMode)
    {
        using var logo = BoxLabelRenderer.TryDecodeLogo(content.LogoImage);

        using var doc = new PrintDocument();
        doc.PrinterSettings.PrinterName = printerName;
        doc.DocumentName = $"Box labels #{content.OrderNumber}";
        var destRect = ConfigurePaperMode(doc, paperMode);

        var boxNumber = 1;
        doc.PrintPage += (_, e) =>
        {
            if (e.Graphics is null)
            {
                return;
            }

            BoxLabelRenderer.Draw(e.Graphics, destRect, content, logo, boxNumber, boxCount);
            boxNumber++;
            e.HasMorePages = boxNumber <= boxCount;
        };

        doc.Print();
    }

    public string SaveLabelToFile(byte[] imageBytes, string fileNameWithoutExtension, string fileExtension)
    {
        var folder = DbPathProvider.GetLabelsFolder();
        var path = Path.Combine(folder, fileNameWithoutExtension + "." + fileExtension);
        File.WriteAllBytes(path, imageBytes);
        return path;
    }
}
