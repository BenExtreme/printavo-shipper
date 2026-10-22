using PrintavoShipper.Core.Models;

namespace PrintavoShipper.App.Services;

/// <summary>
/// Maps a Printavo order's deliveryMethod.name to how it leaves the shop,
/// using the delivery method names configured in Settings > Printavo
/// Workflow (matched case-insensitively). Anything unlisted (null,
/// unrecognized future value) falls back to the shop's default carrier
/// (Settings > Printavo Workflow) rather than blocking shipping outright, but
/// callers should surface WasUnrecognized to staff (see
/// OrderDetailViewModel.ShowUnrecognizedDeliveryMethodWarning) so a real data
/// problem in Printavo doesn't silently ship on the wrong carrier.
/// </summary>
public static class CarrierResolver
{
    /// <param name="IsPickup">Pick-up orders don't ship at all (OrderDetailViewModel shows box labels instead of shipment cards), so there's nothing to warn about.</param>
    public record Resolution(ShipmentCarrier Carrier, bool IsPickup, bool WasUnrecognized);

    public static Resolution Resolve(string? deliveryMethodName, AppSettings settings)
    {
        var name = deliveryMethodName?.Trim();
        if (Matches(name, settings.PickupDeliveryMethodNames))
        {
            return new Resolution(settings.DefaultCarrier, IsPickup: true, WasUnrecognized: false);
        }

        if (Matches(name, settings.FedExDeliveryMethodNames))
        {
            return new Resolution(ShipmentCarrier.FedEx, IsPickup: false, WasUnrecognized: false);
        }

        if (Matches(name, settings.UpsDeliveryMethodNames))
        {
            return new Resolution(ShipmentCarrier.Ups, IsPickup: false, WasUnrecognized: false);
        }

        return new Resolution(settings.DefaultCarrier, IsPickup: false, WasUnrecognized: true);
    }

    private static bool Matches(string? name, IEnumerable<string> configured) =>
        !string.IsNullOrEmpty(name) && configured.Any(c => string.Equals(c.Trim(), name, StringComparison.OrdinalIgnoreCase));
}
