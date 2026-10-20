using CommunityToolkit.Mvvm.ComponentModel;

namespace PrintavoShipper.App.ViewModels;

// Editable rows for Settings > Printavo Workflow / Delivery Notifications /
// Shipping Defaults. Rebuilt wholesale (never patched in place) whenever the
// Printavo status/user lists are (re)loaded — see SettingsViewModel.

/// <summary>One of the Printavo account's order statuses and where the app uses it.</summary>
public partial class StatusSettingRow : ObservableObject
{
    public required string Id { get; init; }
    public required string Name { get; init; }

    /// <summary>Offered in the order screen's status picker on shipping orders.</summary>
    [ObservableProperty]
    private bool showForShipping;

    /// <summary>Offered in the order screen's status picker on Pick-up orders.</summary>
    [ObservableProperty]
    private bool showForPickup;

    /// <summary>Left out of the Orders list.</summary>
    [ObservableProperty]
    private bool hideFromOrders;
}

/// <summary>One of the Printavo account's users, and whether they're shop staff (Assign-to list + notification picker).</summary>
public partial class UserSettingRow : ObservableObject
{
    public required string Id { get; init; }
    public required string Name { get; init; }

    [ObservableProperty]
    private bool isStaff;

    /// <summary>Where "also notify on delivery" emails go — prefilled with the Printavo login email; blank leaves them out of that picker.</summary>
    [ObservableProperty]
    private string notifyEmail = string.Empty;
}

/// <summary>A choice in a Printavo user picker; Id null = "(none)", or OrderOwner.</summary>
public record PrintavoUserChoice(string? Id, string Name)
{
    public static readonly PrintavoUserChoice None = new(null, "(none)");

    /// <summary>Whoever owns the order being shipped (Invoice.owner), looked up at ship time.</summary>
    public static readonly PrintavoUserChoice OrderOwner = new(null, "Order owner");

    public override string ToString() => Name;
}

/// <summary>One of the shop's standard box sizes.</summary>
public partial class BoxPresetRow : ObservableObject
{
    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private decimal lengthIn;

    [ObservableProperty]
    private decimal widthIn;

    [ObservableProperty]
    private decimal heightIn;

    [ObservableProperty]
    private decimal weightLbs;
}
