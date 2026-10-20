namespace PrintavoShipper.Core.Interfaces;

public record ShipmentArchiveResult(int ArchivedCount, string? FilePath);

/// <summary>
/// Periodically exports completed (Purchased/Voided) shipments older than
/// AppSettings.ShipmentArchiveIntervalWeeks to a local JSON file and
/// permanently removes them from the live database, so Shipment History
/// only ever holds recent, active data.
/// </summary>
public interface IShipmentArchiveService
{
    /// <summary>Runs the sweep only if it's actually due (elapsed time since the last run is at least the configured interval). Safe to call on every History-screen load.</summary>
    Task<ShipmentArchiveResult> RunScheduledSweepAsync(CancellationToken ct = default);

    /// <summary>Runs the sweep immediately regardless of cadence — for the Settings "Run Archive Now" button.</summary>
    Task<ShipmentArchiveResult> RunNowAsync(CancellationToken ct = default);
}
