namespace PrintavoShipper.Core.Models;

// Settings-configured, per-shop values stored as JSON lists on AppSettings
// (see PrintavoShipperDbContext). Printavo status and user ids are specific
// to one Printavo account, so these are picked in Settings rather than
// hard-coded. Names are snapshots for display only — matching is always by id.

/// <summary>A Printavo order status picked in Settings.</summary>
public record ConfiguredPrintavoStatus(string Id, string Name);

/// <summary>
/// A Printavo user picked as shop staff in Settings: offered in the order
/// screen's Add Task "Assign to" picker, and — when NotifyEmail is set — in a
/// UPS card's "also notify on delivery" picker, preselected on orders they
/// own. NotifyEmail starts as their Printavo login email but is editable,
/// since that isn't necessarily the address staff actually read.
/// </summary>
public record StaffMember(string PrintavoUserId, string Name, string? NotifyEmail);

/// <summary>One of the shop's standard box sizes, offered as a quick-fill preset on a package row.</summary>
public record BoxSizePreset(string Name, decimal LengthIn, decimal WidthIn, decimal HeightIn, decimal WeightLbs)
{
    public override string ToString() => Name;
}
