using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace PrintavoShipper.Core.Printing;

/// <summary>
/// Minimal direct binding to the native PDFium library (pdfium.dll, shipped
/// by the bblanchon.PDFium.Win32 package) — just enough to rasterize a
/// FedEx label PDF's first page straight into a GDI+ bitmap. Replaces the
/// PDFtoImage package, which pulled in all of SkiaSharp (~12.7 MB of native
/// code) only to hand back a bitmap this app then had to PNG-encode and
/// re-decode for System.Drawing's print pipeline anyway.
///
/// PDFium is not thread-safe, so every call goes through one static lock.
/// </summary>
internal static class PdfiumRasterizer
{
    private const string PdfiumLib = "pdfium";
    private const int FPDFBitmap_BGRA = 4;
    private const uint OpaqueWhite = 0xFFFFFFFF;

    private static readonly object PdfiumLock = new();
    private static bool _initialized;

    /// <summary>
    /// Renders page 1 of a PDF at the given DPI onto an opaque white
    /// background. Flags = 0 matches what PDFtoImage used by default
    /// (anti-aliased, annotations not rendered), which is what FedEx labels
    /// were confirmed to print correctly with.
    /// </summary>
    public static Bitmap RenderFirstPage(byte[] pdfBytes, int dpi)
    {
        lock (PdfiumLock)
        {
            if (!_initialized)
            {
                FPDF_InitLibrary();
                _initialized = true;
            }

            // PDFium reads from this buffer lazily for the document's whole
            // lifetime (not just during the load call), so it must stay
            // pinned until FPDF_CloseDocument — marshaller pinning of a
            // byte[] parameter would only last for the one call.
            var pin = GCHandle.Alloc(pdfBytes, GCHandleType.Pinned);
            try
            {
                var doc = FPDF_LoadMemDocument(pin.AddrOfPinnedObject(), pdfBytes.Length, IntPtr.Zero);
                if (doc == IntPtr.Zero)
                {
                    throw new InvalidOperationException($"Couldn't open the label PDF (PDFium error {FPDF_GetLastError()}).");
                }

                try
                {
                    var page = FPDF_LoadPage(doc, 0);
                    if (page == IntPtr.Zero)
                    {
                        throw new InvalidOperationException($"Couldn't read the label PDF's first page (PDFium error {FPDF_GetLastError()}).");
                    }

                    try
                    {
                        return RenderPage(page, dpi);
                    }
                    finally
                    {
                        FPDF_ClosePage(page);
                    }
                }
                finally
                {
                    FPDF_CloseDocument(doc);
                }
            }
            finally
            {
                pin.Free();
            }
        }
    }

    private static Bitmap RenderPage(IntPtr page, int dpi)
    {
        // PDF page size is in points (1/72 in).
        var width = (int)Math.Round(FPDF_GetPageWidth(page) / 72.0 * dpi);
        var height = (int)Math.Round(FPDF_GetPageHeight(page) / 72.0 * dpi);

        // GDI+'s Format32bppArgb is laid out in memory as B,G,R,A per pixel —
        // the same as PDFium's BGRA format — so PDFium renders directly into
        // the bitmap's own pixel buffer, no intermediate copy.
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        try
        {
            var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                var pdfBitmap = FPDFBitmap_CreateEx(width, height, FPDFBitmap_BGRA, data.Scan0, data.Stride);
                if (pdfBitmap == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Couldn't allocate a bitmap to render the label PDF into.");
                }

                try
                {
                    FPDFBitmap_FillRect(pdfBitmap, 0, 0, width, height, OpaqueWhite);
                    FPDF_RenderPageBitmap(pdfBitmap, page, 0, 0, width, height, 0, 0);
                }
                finally
                {
                    FPDFBitmap_Destroy(pdfBitmap);
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    [DllImport(PdfiumLib)]
    private static extern void FPDF_InitLibrary();

    [DllImport(PdfiumLib)]
    private static extern IntPtr FPDF_LoadMemDocument(IntPtr dataBuf, int size, IntPtr password);

    [DllImport(PdfiumLib)]
    private static extern uint FPDF_GetLastError();

    [DllImport(PdfiumLib)]
    private static extern void FPDF_CloseDocument(IntPtr document);

    [DllImport(PdfiumLib)]
    private static extern IntPtr FPDF_LoadPage(IntPtr document, int pageIndex);

    [DllImport(PdfiumLib)]
    private static extern void FPDF_ClosePage(IntPtr page);

    [DllImport(PdfiumLib)]
    private static extern double FPDF_GetPageWidth(IntPtr page);

    [DllImport(PdfiumLib)]
    private static extern double FPDF_GetPageHeight(IntPtr page);

    [DllImport(PdfiumLib)]
    private static extern IntPtr FPDFBitmap_CreateEx(int width, int height, int format, IntPtr firstScan, int stride);

    [DllImport(PdfiumLib)]
    private static extern int FPDFBitmap_FillRect(IntPtr bitmap, int left, int top, int width, int height, uint color);

    [DllImport(PdfiumLib)]
    private static extern void FPDF_RenderPageBitmap(IntPtr bitmap, IntPtr page, int startX, int startY, int sizeX, int sizeY, int rotate, int flags);

    [DllImport(PdfiumLib)]
    private static extern void FPDFBitmap_Destroy(IntPtr bitmap);
}
