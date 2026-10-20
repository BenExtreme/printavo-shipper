using PrintavoShipper.App.ViewModels;
using PrintavoShipper.Core.Models;

namespace PrintavoShipper.App.Services;

/// <summary>
/// Pushes the Settings-configured values that shipment cards and package rows
/// read synchronously (constructors, field initializers, x:Static XAML) into
/// their static holders. Applied once at startup and again on every Settings
/// save, so a card opened afterwards picks up the change.
/// </summary>
public static class ShopSettingsCache
{
    public static void Apply(AppSettings settings)
    {
        StaffContacts.Load(settings.StaffMembers);
        BoxSizePresets.Load(settings.BoxSizePresets);
        ShippingFeeCalculator.MarkupPercent = settings.ShippingFeeMarkupPercent;
    }
}
