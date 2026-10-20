using PrintavoShipper.Core.Models;

namespace PrintavoShipper.Core.Interfaces;

public interface IShipmentRepository
{
    Task<ShipmentRecord> SaveAsync(ShipmentRecord record, CancellationToken ct = default);

    Task<ShipmentRecord?> GetByIdAsync(int id, CancellationToken ct = default);

    Task<List<ShipmentRecord>> GetByOrderIdAsync(string printavoOrderId, CancellationToken ct = default);

    Task<List<ShipmentRecord>> SearchAsync(string? query, int take = 100, CancellationToken ct = default);

    Task<List<ShipmentRecord>> GetEligibleForArchiveAsync(DateTimeOffset cutoff, CancellationToken ct = default);

    Task DeleteShipmentsAsync(IEnumerable<int> ids, CancellationToken ct = default);
}
