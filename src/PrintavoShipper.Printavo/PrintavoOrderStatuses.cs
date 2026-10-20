namespace PrintavoShipper.Printavo;

/// <summary>
/// One of this account's Printavo order statuses. IDs are specific to each
/// Printavo account's own custom status list — Printavo has no lookup-by-name,
/// only by id — so which ones the app offers is picked in Settings.
/// </summary>
public record PrintavoOrderStatusOption(string Id, string Name)
{
    public override string ToString() => Name;
}
