using Microsoft.EntityFrameworkCore;
using PrintavoShipper.Core.Data;
using PrintavoShipper.Core.Interfaces;
using PrintavoShipper.Core.Models;

namespace PrintavoShipper.Core.Services;

public class ContractorRepository : IContractorRepository
{
    private readonly IDbContextFactory<PrintavoShipperDbContext> _dbContextFactory;

    public ContractorRepository(IDbContextFactory<PrintavoShipperDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<List<Contractor>> GetAllAsync(bool includeArchived = false, CancellationToken ct = default)
    {
        using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var query = db.Contractors
            .Include(c => c.ShipFromAddresses)
            .Include(c => c.BillingAccounts)
            .Include(c => c.FedExBillingAccounts)
            .AsNoTracking()
            .AsQueryable();

        if (!includeArchived)
        {
            query = query.Where(c => !c.IsArchived);
        }

        return await query.OrderBy(c => c.CompanyName).ToListAsync(ct);
    }

    public async Task<Contractor?> GetByIdAsync(string contractorId, CancellationToken ct = default)
    {
        using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        return await db.Contractors
            .Include(c => c.ShipFromAddresses)
            .Include(c => c.BillingAccounts)
            .Include(c => c.FedExBillingAccounts)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == contractorId, ct);
    }

    public async Task<HashSet<string>> GetAllIdsAsync(CancellationToken ct = default)
    {
        using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var ids = await db.Contractors.Select(c => c.Id).ToListAsync(ct);
        return ids.ToHashSet();
    }

    public async Task<Contractor> UpsertFromPrintavoAsync(string printavoCustomerId, string companyName, CancellationToken ct = default)
    {
        using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var existing = await db.Contractors.FirstOrDefaultAsync(c => c.Id == printavoCustomerId, ct);
        if (existing is not null)
        {
            existing.LastSeenAt = DateTimeOffset.UtcNow;
            // Keep the cached display name current if it changed in Printavo.
            if (!string.IsNullOrWhiteSpace(companyName))
            {
                existing.CompanyName = companyName;
            }

            await db.SaveChangesAsync(ct);
            return existing;
        }

        var contractor = new Contractor
        {
            Id = printavoCustomerId,
            CompanyName = companyName,
            FirstSeenAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow
        };

        db.Contractors.Add(contractor);
        await db.SaveChangesAsync(ct);
        return contractor;
    }

    public async Task<ContractorShipFromAddress> SaveShipFromAddressAsync(ContractorShipFromAddress address, CancellationToken ct = default)
    {
        using var db = await _dbContextFactory.CreateDbContextAsync(ct);

        if (address.IsDefault)
        {
            await ClearDefaultShipFromAsync(db, address.ContractorId, address.Id, ct);
        }

        if (address.Id == 0)
        {
            db.ContractorShipFromAddresses.Add(address);
        }
        else
        {
            // Entry(...).State = Modified rather than Update(...) — Update()
            // walks the full navigation graph (address.Contractor.ShipFromAddresses,
            // which can include sibling addresses ClearDefaultShipFromAsync's
            // own query just loaded into this same tracked context), which
            // throws a duplicate-tracking InvalidOperationException whenever
            // the caller passes an entity whose Contractor back-reference is
            // still populated (e.g. one read via GetAllAsync's .Include(...)).
            db.Entry(address).State = EntityState.Modified;
        }

        await db.SaveChangesAsync(ct);
        return address;
    }

    public async Task DeleteShipFromAddressAsync(int id, CancellationToken ct = default)
    {
        using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = await db.ContractorShipFromAddresses.FindAsync([id], ct);
        if (entity is null)
        {
            return;
        }

        var wasDefault = entity.IsDefault;
        var contractorId = entity.ContractorId;
        db.ContractorShipFromAddresses.Remove(entity);
        await db.SaveChangesAsync(ct);

        // Deleting the default must never leave a shipment card silently
        // falling back to "whichever's first" with no default set at all —
        // promote the most recently added survivor instead.
        if (wasDefault)
        {
            var replacement = await db.ContractorShipFromAddresses
                .Where(a => a.ContractorId == contractorId)
                .OrderByDescending(a => a.Id)
                .FirstOrDefaultAsync(ct);
            if (replacement is not null)
            {
                replacement.IsDefault = true;
                await db.SaveChangesAsync(ct);
            }
        }
    }

    public async Task<ContractorBillingAccount> SaveBillingAccountAsync(ContractorBillingAccount account, CancellationToken ct = default)
    {
        using var db = await _dbContextFactory.CreateDbContextAsync(ct);

        if (account.IsDefault)
        {
            await ClearDefaultBillingAsync(db, account.ContractorId, account.Id, ct);
        }

        if (account.Id == 0)
        {
            db.ContractorBillingAccounts.Add(account);
        }
        else
        {
            // Entry(...).State = Modified rather than Update(...) — see the
            // matching comment in SaveShipFromAddressAsync above; same bug
            // shape, same fix.
            db.Entry(account).State = EntityState.Modified;
        }

        await db.SaveChangesAsync(ct);
        return account;
    }

    public async Task DeleteBillingAccountAsync(int id, CancellationToken ct = default)
    {
        using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = await db.ContractorBillingAccounts.FindAsync([id], ct);
        if (entity is null)
        {
            return;
        }

        var wasDefault = entity.IsDefault;
        var contractorId = entity.ContractorId;
        db.ContractorBillingAccounts.Remove(entity);
        await db.SaveChangesAsync(ct);

        // Same reasoning as DeleteShipFromAddressAsync: don't leave the
        // contractor with no default billing account after a delete.
        if (wasDefault)
        {
            var replacement = await db.ContractorBillingAccounts
                .Where(a => a.ContractorId == contractorId)
                .OrderByDescending(a => a.Id)
                .FirstOrDefaultAsync(ct);
            if (replacement is not null)
            {
                replacement.IsDefault = true;
                await db.SaveChangesAsync(ct);
            }
        }
    }

    public async Task<ContractorFedExBillingAccount> SaveFedExBillingAccountAsync(ContractorFedExBillingAccount account, CancellationToken ct = default)
    {
        using var db = await _dbContextFactory.CreateDbContextAsync(ct);

        if (account.IsDefault)
        {
            await ClearDefaultFedExBillingAsync(db, account.ContractorId, account.Id, ct);
        }

        if (account.Id == 0)
        {
            db.ContractorFedExBillingAccounts.Add(account);
        }
        else
        {
            // Entry(...).State = Modified rather than Update(...) — see the
            // matching comment in SaveShipFromAddressAsync above; same bug
            // shape, same fix.
            db.Entry(account).State = EntityState.Modified;
        }

        await db.SaveChangesAsync(ct);
        return account;
    }

    public async Task DeleteFedExBillingAccountAsync(int id, CancellationToken ct = default)
    {
        using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = await db.ContractorFedExBillingAccounts.FindAsync([id], ct);
        if (entity is null)
        {
            return;
        }

        var wasDefault = entity.IsDefault;
        var contractorId = entity.ContractorId;
        db.ContractorFedExBillingAccounts.Remove(entity);
        await db.SaveChangesAsync(ct);

        // Same reasoning as DeleteBillingAccountAsync: don't leave the
        // contractor with no default FedEx billing account after a delete.
        if (wasDefault)
        {
            var replacement = await db.ContractorFedExBillingAccounts
                .Where(a => a.ContractorId == contractorId)
                .OrderByDescending(a => a.Id)
                .FirstOrDefaultAsync(ct);
            if (replacement is not null)
            {
                replacement.IsDefault = true;
                await db.SaveChangesAsync(ct);
            }
        }
    }

    public async Task SetArchivedAsync(string contractorId, bool isArchived, CancellationToken ct = default)
    {
        using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var contractor = await db.Contractors.FindAsync([contractorId], ct);
        if (contractor is not null)
        {
            contractor.IsArchived = isArchived;
            contractor.ArchivedAt = isArchived ? DateTimeOffset.UtcNow : null;
            await db.SaveChangesAsync(ct);
        }
    }

    private static async Task ClearDefaultShipFromAsync(PrintavoShipperDbContext db, string contractorId, int exceptId, CancellationToken ct)
    {
        var others = await db.ContractorShipFromAddresses
            .Where(a => a.ContractorId == contractorId && a.Id != exceptId && a.IsDefault)
            .ToListAsync(ct);
        foreach (var a in others)
        {
            a.IsDefault = false;
        }
    }

    private static async Task ClearDefaultBillingAsync(PrintavoShipperDbContext db, string contractorId, int exceptId, CancellationToken ct)
    {
        var others = await db.ContractorBillingAccounts
            .Where(a => a.ContractorId == contractorId && a.Id != exceptId && a.IsDefault)
            .ToListAsync(ct);
        foreach (var a in others)
        {
            a.IsDefault = false;
        }
    }

    private static async Task ClearDefaultFedExBillingAsync(PrintavoShipperDbContext db, string contractorId, int exceptId, CancellationToken ct)
    {
        var others = await db.ContractorFedExBillingAccounts
            .Where(a => a.ContractorId == contractorId && a.Id != exceptId && a.IsDefault)
            .ToListAsync(ct);
        foreach (var a in others)
        {
            a.IsDefault = false;
        }
    }
}
