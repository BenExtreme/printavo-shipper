using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using PrintavoShipper.Core.Models;

namespace PrintavoShipper.Core.Printing;

/// <summary>
/// Draws one Pick-up box label into a 4x6in area, laid out after Printavo's
/// own box label (logo + phone, big order number, job name, a PO/due
/// date/delivery block, "Box N of M") without its QR code, plus a large
/// customer-name banner on shop-branded labels. All coordinates
/// are in the PrintDocument's default unit, hundredths of an inch, relative
/// to the given bounds. Kept separate from LabelPrintService so the layout
/// can be tweaked without touching the print plumbing.
/// </summary>
public static class BoxLabelRenderer
{
    private const string FontFamilyName = "Arial";

    private const float Margin = 18f;
    private const float LogoMaxWidth = 260f;
    private const float LogoMaxHeight = 100f;
    private const float FieldValueColumn = 185f;
    private const float FieldRowHeight = 42f;
    private const float CompactFieldRowHeight = 36f;
    private const float BannerPadding = 6f;
    private const float BannerMaxFontSize = 30f;
    private const float BannerMinFontSize = 18f;
    private const float RuleThickness = 2f;

    /// <summary>Converts a color logo to grayscale as it's drawn, so thermal printers threshold it cleanly instead of dithering colors into noise.</summary>
    private static readonly ColorMatrix GrayscaleMatrix = new(new[]
    {
        new[] { 0.299f, 0.299f, 0.299f, 0f, 0f },
        new[] { 0.587f, 0.587f, 0.587f, 0f, 0f },
        new[] { 0.114f, 0.114f, 0.114f, 0f, 0f },
        new[] { 0f, 0f, 0f, 1f, 0f },
        new[] { 0f, 0f, 0f, 0f, 1f },
    });

    /// <summary>Decodes the logo once per print job (not per page); null if there is none or it isn't a readable image.</summary>
    public static Image? TryDecodeLogo(byte[]? logoBytes)
    {
        if (logoBytes is not { Length: > 0 })
        {
            return null;
        }

        try
        {
            using var ms = new MemoryStream(logoBytes);
            using var decoded = Image.FromStream(ms);
            // Copy out so the image doesn't depend on the (disposed) stream.
            return new Bitmap(decoded);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    public static void Draw(Graphics g, RectangleF bounds, BoxLabelContent content, Image? logo, int boxNumber, int boxCount)
    {
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var left = bounds.Left + Margin;
        var width = bounds.Width - 2 * Margin;
        var y = bounds.Top + Margin + 6f;

        // ---- Logo (or the company name in its place) ----
        var logoBottom = logo is not null
            ? DrawLogo(g, logo, left, y)
            : DrawWrapped(g, content.LogoFallbackText, FontStyle.Bold, 20f, left, y, LogoMaxWidth, maxLines: 2);

        y = logoBottom + 4f;
        y = DrawWrapped(g, content.Phone, FontStyle.Regular, 9f, left, y, width, maxLines: 1);

        // ---- Order number + job name ----
        y += 10f;
        y = DrawWrapped(g, "#" + content.OrderNumber, FontStyle.Bold, 34f, left - 3f, y, width, maxLines: 1);
        y += 6f;
        y = DrawWrapped(g, content.Title, FontStyle.Bold, 15f, left, y, width, maxLines: 2);

        // ---- Customer name banner (shop-branded labels only) ----
        var hasBanner = !string.IsNullOrWhiteSpace(content.CustomerName);
        if (hasBanner)
        {
            y += 10f;
            y = DrawCustomerBanner(g, content.CustomerName!, left, y, width);
        }

        // ---- Order details ----
        // Rows tighten up a little to make room for the banner.
        var rowHeight = hasBanner ? CompactFieldRowHeight : FieldRowHeight;
        y += hasBanner ? 10f : 14f;
        DrawRule(g, left, y, width);
        y += hasBanner ? 12f : 16f;

        DrawField(g, "PO #", content.PoNumber, left, y, width);
        y += rowHeight;
        DrawField(g, "Production Due Date", content.ProductionDue, left, y, width);
        y += rowHeight;
        DrawField(g, "Customer Due Date", content.CustomerDue, left, y, width);
        y += rowHeight;
        DrawField(g, "Delivery Method", content.DeliveryMethod, left, y, width);
        y += rowHeight;

        DrawRule(g, left, y, width);

        // ---- Box N of M, centered in whatever space is left below ----
        var boxArea = RectangleF.FromLTRB(left, y + RuleThickness, left + width, bounds.Bottom - Margin);
        using var boxFont = new Font(FontFamilyName, 26f, FontStyle.Bold);
        using var centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString($"Box {boxNumber} of {boxCount}", boxFont, Brushes.Black, boxArea, centered);
    }

    /// <summary>Draws the logo top-left, scaled down (never up past its box) with its aspect ratio kept. Returns its bottom edge.</summary>
    private static float DrawLogo(Graphics g, Image logo, float left, float top)
    {
        var scale = Math.Min(LogoMaxWidth / logo.Width, LogoMaxHeight / logo.Height);
        var dest = new RectangleF(left, top, logo.Width * scale, logo.Height * scale);

        using var attributes = new ImageAttributes();
        attributes.SetColorMatrix(GrayscaleMatrix);
        g.DrawImage(logo, Rectangle.Round(dest), 0, 0, logo.Width, logo.Height, GraphicsUnit.Pixel, attributes);

        return dest.Bottom;
    }

    /// <summary>
    /// Draws the customer's name as white-on-black across the full width, so
    /// it reads from a distance. Uses the largest size (30pt down to 18pt) that
    /// fits on one line; a name too long even at 18pt wraps to two lines.
    /// Returns the banner's bottom edge.
    /// </summary>
    private static float DrawCustomerBanner(Graphics g, string name, float left, float top, float width)
    {
        var textWidth = width - 2 * BannerPadding;
        using var oneLine = new StringFormat(StringFormatFlags.NoWrap) { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using var wrapped = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisWord };

        Font? font = null;
        var format = oneLine;
        for (var size = BannerMaxFontSize; size >= BannerMinFontSize; size -= 1f)
        {
            font?.Dispose();
            font = new Font(FontFamilyName, size, FontStyle.Bold);
            if (g.MeasureString(name, font, PointF.Empty, oneLine).Width <= textWidth)
            {
                break;
            }

            if (size - 1f < BannerMinFontSize)
            {
                format = wrapped;
            }
        }

        using (font)
        {
            var lines = format == wrapped ? 2 : 1;
            var height = font!.GetHeight(g) * lines + 2 * BannerPadding;
            g.FillRectangle(Brushes.Black, left, top, width, height);
            g.DrawString(name, font, Brushes.White, new RectangleF(left + BannerPadding, top + BannerPadding, textWidth, height - 2 * BannerPadding), format);
            return top + height;
        }
    }

    /// <summary>Draws text wrapped to width, cut off with an ellipsis past maxLines. Returns the bottom edge (unchanged top if text is blank).</summary>
    private static float DrawWrapped(Graphics g, string? text, FontStyle style, float sizePt, float left, float top, float width, int maxLines)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return top;
        }

        using var font = new Font(FontFamilyName, sizePt, style);
        using var format = new StringFormat { Trimming = StringTrimming.EllipsisWord };
        var maxHeight = font.GetHeight(g) * maxLines;
        var size = g.MeasureString(text, font, new SizeF(width, maxHeight), format);
        g.DrawString(text, font, Brushes.Black, new RectangleF(left, top, width, size.Height), format);
        return top + size.Height;
    }

    private static void DrawField(Graphics g, string label, string? value, float left, float top, float width)
    {
        using var labelFont = new Font(FontFamilyName, 10f, FontStyle.Bold);
        using var valueFont = new Font(FontFamilyName, 18f, FontStyle.Bold);
        using var oneLine = new StringFormat(StringFormatFlags.NoWrap) { Trimming = StringTrimming.EllipsisCharacter };

        // Bottom-align the small label with the large value's baseline-ish
        // region, the way Printavo's label lines them up.
        var labelOffset = valueFont.GetHeight(g) - labelFont.GetHeight(g) - 3f;
        g.DrawString(label, labelFont, Brushes.Black, new RectangleF(left, top + labelOffset, FieldValueColumn - 8f, labelFont.GetHeight(g)), oneLine);

        if (!string.IsNullOrWhiteSpace(value))
        {
            g.DrawString(value, valueFont, Brushes.Black, new RectangleF(left + FieldValueColumn, top, width - FieldValueColumn, valueFont.GetHeight(g)), oneLine);
        }
    }

    private static void DrawRule(Graphics g, float left, float y, float width)
    {
        g.FillRectangle(Brushes.Black, left, y, width, RuleThickness);
    }
}
