using Microsoft.EntityFrameworkCore;
using PrintavoShipper.Core.Data;
using PrintavoShipper.Core.Interfaces;
using PrintavoShipper.Core.Models;

namespace PrintavoShipper.Core.Services;

public class ShipmentRepository : IShipmentRepository
{
    private readonly IDbContextFactory<PrintavoShipperDbContext> _dbContextFactory;

    public ShipmentRepository(IDbContextFactory<PrintavoShipperDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<ShipmentRecord> SaveAsync(ShipmentRecord record, CancellationToken ct = default)
    {
        using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        if (record.Id == 0)
        {
            db.ShipmentRecords.Add(record);
        }
        else
        {
            db.ShipmentRecords.Update(record);
        }

        await db.SaveChangesAsync(ct);
        return record;
    }

    public async Task<ShipmentRecord?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        return await db.ShipmentRecords
            .Include(s => s.Packages)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<List<ShipmentRecord>> GetByOrderIdAsync(string printavoOrderId, CancellationToken ct = default)
    {
        using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        return await db.ShipmentRecords
            .Include(s => s.Packages)
            .AsNoTracking()
            .Where(s => s.PrintavoOrderId == printavoOrderId)
            // Sqlite's EF Core provider can't translate ORDER BY on
            // DateTimeOffset server-side ("does not support expressions of
            // type 'DateTimeOffset' in ORDER BY clauses") — Id is an
            // auto-increment surrogate key that already matches insertion
            // (i.e. chronological) order in this single-workstation app, so
            // it's an equivalent, SQL-translatable substitute.
            .OrderByDescending(s => s.Id)
            .ToListAsync(ct);
    }

    public async Task<List<ShipmentRecord>> SearchAsync(string? query, int take = 100, CancellationToken ct = default)
    {
        using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var q = db.ShipmentRecords.Include(s => s.Packages).AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            q = q.Where(s =>
                s.PrintavoVisualId.Contains(query) ||
                (s.FirstTrackingNumber != null && s.FirstTrackingNumber.Contains(query)) ||
                (s.ShipToCompanyName != null && s.ShipToCompanyName.Contains(query)) ||
                (s.ShipToLabel != null && s.ShipToLabel.Contains(query)));
        }

        // See the comment in GetByOrderIdAsync — order by Id, not CreatedAt,
        // since Sqlite can't translate ORDER BY on DateTimeOffset.
        return await q.OrderByDescending(s => s.Id).Take(take).ToListAsync(ct);
    }

    public async Task<List<ShipmentRecord>> GetEligibleForArchiveAsync(DateTimeOffset cutoff, CancellationToken ct = default)
    {
        using var db = await _dbContextFactory.CreateDbContextAsync(ct);

        // Only completed shipments are eligible; Draft/RateQuoted/Error stay
        // in History regardless of age. CreatedAt is a DateTimeOffset — per
        // the ORDER BY limitation noted above, the age comparison is done
        // client-side against a lightweight (Id, CreatedAt) projection
        // rather than risk translating a DateTimeOffset comparison into
        // SQL. The real fetch below filters by Id (int) only, which is
        // fully SQL-translatable.
        var candidates = await db.ShipmentRecords
            .Where(s => s.Status == ShipmentStatus.Purchased || s.Status == ShipmentStatus.Voided)
            .Select(s => new { s.Id, s.CreatedAt })
            .ToListAsync(ct);

        var idsToArchive = candidates.Where(c => c.CreatedAt <= cutoff).Select(c => c.Id).ToList();
        if (idsToArchive.Count == 0)
        {
            return new List<ShipmentRecord>();
        }

        return await db.ShipmentRecords
            .Include(s => s.Packages)
            .AsNoTracking()
            .Where(s => idsToArchive.Contains(s.Id))
            .ToListAsync(ct);
    }

    public async Task DeleteShipmentsAsync(IEnumerable<int> ids, CancellationToken ct = default)
    {
        var idList = ids.ToList();
        if (idList.Count == 0)
        {
            return;
        }

        using var db = await _dbContextFactory.CreateDbContextAsync(ct);

        // Packages first, explicitly — ExecuteDelete is a bulk SQL DELETE,
        // not an EF-tracked cascade, so it doesn't rely on SQLite's own
        // ON DELETE CASCADE enforcement being in effect.
        await db.ShipmentPackages.Where(p => idList.Contains(p.ShipmentRecordId)).ExecuteDeleteAsync(ct);
        await db.ShipmentRecords.Where(s => idList.Contains(s.Id)).ExecuteDeleteAsync(ct);
    }
}
