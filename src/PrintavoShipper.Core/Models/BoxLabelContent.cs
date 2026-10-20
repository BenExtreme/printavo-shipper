namespace PrintavoShipper.Core.Models;

/// <summary>
/// Everything printed on a Pick-up order's box label (modeled on Printavo's
/// own box label, minus its QR code). Values are pre-formatted display
/// strings — blank/null ones are simply left empty on the label. The
/// "Box N of M" line isn't here; it's supplied per page at print time.
/// </summary>
/// <param name="LogoImage">Raw image file bytes (PNG/JPG/BMP), or null.</param>
/// <param name="LogoFallbackText">Printed in bold in the logo's place when LogoImage is null or can't be decoded.</param>
/// <param name="CustomerName">Printed large in a black banner so the box can be matched to its customer from across the room. Only set for direct (shop-branded) orders — a contractor's label carries its own branding instead.</param>
public record BoxLabelContent(
    byte[]? LogoImage,
    string? LogoFallbackText,
    string? Phone,
    string OrderNumber,
    string? Title,
    string? PoNumber,
    string? ProductionDue,
    string? CustomerDue,
    string? DeliveryMethod,
    string? CustomerName = null);
