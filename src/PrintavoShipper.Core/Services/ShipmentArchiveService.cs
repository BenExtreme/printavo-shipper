using System.Text.Json;
using PrintavoShipper.Core.Data;
using PrintavoShipper.Core.Interfaces;

namespace PrintavoShipper.Core.Services;

public class ShipmentArchiveService : IShipmentArchiveService
{
    private readonly IShipmentRepository _shipmentRepository;
    private readonly ISettingsService _settingsService;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public ShipmentArchiveService(IShipmentRepository shipmentRepository, ISettingsService settingsService)
    {
        _shipmentRepository = shipmentRepository;
        _settingsService = settingsService;
    }

    public async Task<ShipmentArchiveResult> RunScheduledSweepAsync(CancellationToken ct = default)
    {
        var settings = await _settingsService.GetSettingsAsync(ct);
        var interval = TimeSpan.FromDays(settings.ShipmentArchiveIntervalWeeks * 7);
        var due = settings.ShipmentArchiveLastRunAt is null
            || DateTimeOffset.UtcNow - settings.ShipmentArchiveLastRunAt.Value >= interval;

        if (!due)
        {
            return new ShipmentArchiveResult(0, null);
        }

        return await ExecuteSweepAsync(settings, ct);
    }

    public async Task<ShipmentArchiveResult> RunNowAsync(CancellationToken ct = default)
    {
        var settings = await _settingsService.GetSettingsAsync(ct);
        return await ExecuteSweepAsync(settings, ct);
    }

    private async Task<ShipmentArchiveResult> ExecuteSweepAsync(Models.AppSettings settings, CancellationToken ct)
    {
        if (settings.ShipmentArchiveIntervalWeeks <= 0)
        {
            return new ShipmentArchiveResult(0, null);
        }

        var cutoff = DateTimeOffset.UtcNow.AddDays(-settings.ShipmentArchiveIntervalWeeks * 7);
        var eligible = await _shipmentRepository.GetEligibleForArchiveAsync(cutoff, ct);

        string? filePath = null;
        if (eligible.Count > 0)
        {
            var folder = DbPathProvider.GetArchiveFolder();
            filePath = Path.Combine(folder, $"Archive_{DateTimeOffset.UtcNow:yyyy-MM-dd_HHmmss}.json");
            var json = JsonSerializer.Serialize(eligible, JsonOptions);
            await File.WriteAllTextAsync(filePath, json, ct);

            await _shipmentRepository.DeleteShipmentsAsync(eligible.Select(s => s.Id), ct);
        }

        // Reset the cadence even when nothing matched, so a quiet period
        // doesn't make every subsequent screen visit re-evaluate as "due".
        settings.ShipmentArchiveLastRunAt = DateTimeOffset.UtcNow;
        await _settingsService.SaveAsync(settings, null, null, null, ct: ct);

        return new ShipmentArchiveResult(eligible.Count, filePath);
    }
}
