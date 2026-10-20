using Microsoft.EntityFrameworkCore;
using PrintavoShipper.Core.Data;
using PrintavoShipper.Core.Interfaces;
using PrintavoShipper.Core.Models;

namespace PrintavoShipper.Core.Services;

public class FedExAccountRepository : IFedExAccountRepository
{
    private readonly IDbContextFactory<PrintavoShipperDbContext> _dbContextFactory;

    public FedExAccountRepository(IDbContextFactory<PrintavoShipperDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<List<OwnFedExAccount>> GetAllAsync(CancellationToken ct = default)
    {
        using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        return await db.OwnFedExAccounts.AsNoTracking().OrderBy(a => a.Label).ToListAsync(ct);
    }

    public async Task<OwnFedExAccount> SaveAsync(OwnFedExAccount account, CancellationToken ct = default)
    {
        using var db = await _dbContextFactory.CreateDbContextAsync(ct);

        if (account.IsDefault)
        {
            var others = await db.OwnFedExAccounts.Where(a => a.Id != account.Id && a.IsDefault).ToListAsync(ct);
            foreach (var other in others)
            {
                other.IsDefault = false;
            }
        }

        if (account.Id == 0)
        {
            db.OwnFedExAccounts.Add(account);
        }
        else
        {
            db.OwnFedExAccounts.Update(account);
        }

        await db.SaveChangesAsync(ct);
        return account;
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = await db.OwnFedExAccounts.FindAsync([id], ct);
        if (entity is not null)
        {
            db.OwnFedExAccounts.Remove(entity);
            await db.SaveChangesAsync(ct);
        }
    }
}
