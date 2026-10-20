using PrintavoShipper.Core.Models;

namespace PrintavoShipper.App.ViewModels;

/// <summary>A shop staff member's notification email, or the "(none)" sentinel meaning no secondary notification.</summary>
public record StaffContact(string Name, string? Email, string? PrintavoUserId = null)
{
    public override string ToString() => Name;
}

/// <summary>
/// The shop's staff list for the Quantum View "also notify on delivery"
/// picker — the staff picked in Settings > Printavo Workflow that have a
/// notification email (loaded via ShopSettingsCache). Kept as a fixed list
/// rather than free-text entry on the card since this is always one of the
/// shop's own people, not a customer-supplied address.
/// </summary>
public static class StaffContacts
{
    public static readonly StaffContact None = new("(none)", null);

    public static IReadOnlyList<StaffContact> All { get; private set; } = new List<StaffContact> { None };

    public static void Load(IEnumerable<StaffMember> staff) =>
        All = new List<StaffContact> { None }
            .Concat(staff
                .Where(s => !string.IsNullOrWhiteSpace(s.NotifyEmail))
                .Select(s => new StaffContact(s.Name, s.NotifyEmail, s.PrintavoUserId)))
            .ToList();

    public static StaffContact FromEmail(string? email) =>
        All.FirstOrDefault(c => c.Email is not null && string.Equals(c.Email, email, StringComparison.OrdinalIgnoreCase)) ?? None;

    /// <summary>The contact for an order's owner (any Printavo user), by Printavo user id — None if the owner isn't staff with a notification email.</summary>
    public static StaffContact ForOrderOwner(string? ownerUserId) =>
        string.IsNullOrEmpty(ownerUserId)
            ? None
            : All.FirstOrDefault(c => c.PrintavoUserId == ownerUserId) ?? None;
}
