using Microsoft.EntityFrameworkCore;
using PrintavoShipper.Core.Data;
using PrintavoShipper.Core.Interfaces;
using PrintavoShipper.Core.Models;

namespace PrintavoShipper.Core.Services;

public class UpsAccountRepository : IUpsAccountRepository
{
    private readonly IDbContextFactory<PrintavoShipperDbContext> _dbContextFactory;

    public UpsAccountRepository(IDbContextFactory<PrintavoShipperDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<List<OwnUpsAccount>> GetAllAsync(CancellationToken ct = default)
    {
        using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var accounts = await db.OwnUpsAccounts.AsNoTracking().OrderBy(a => a.Label).ToListAsync(ct);
        if (accounts.Count > 0)
        {
            return accounts;
        }

        // First run after upgrading from the single-account AppSettings
        // fields: seed one row so existing single-account setups keep
        // working without a manual step.
        var settings = await db.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == 1, ct);
        if (settings is null || string.IsNullOrWhiteSpace(settings.UpsShipperAccountNumber))
        {
            return accounts;
        }

        var seeded = new OwnUpsAccount
        {
            Label = "Primary",
            AccountNumber = settings.UpsShipperAccountNumber,
            PostalCode = settings.UpsShipperAccountPostalCode ?? string.Empty,
            CountryCode = settings.UpsShipperAccountCountryCode ?? "US",
            IsDefault = true
        };
        db.OwnUpsAccounts.Add(seeded);
        await db.SaveChangesAsync(ct);

        return new List<OwnUpsAccount> { seeded };
    }

    public async Task<OwnUpsAccount> SaveAsync(OwnUpsAccount account, CancellationToken ct = default)
    {
        using var db = await _dbContextFactory.CreateDbContextAsync(ct);

        if (account.IsDefault)
        {
            var others = await db.OwnUpsAccounts.Where(a => a.Id != account.Id && a.IsDefault).ToListAsync(ct);
            foreach (var other in others)
            {
                other.IsDefault = false;
            }
        }

        if (account.Id == 0)
        {
            db.OwnUpsAccounts.Add(account);
        }
        else
        {
            db.OwnUpsAccounts.Update(account);
        }

        await db.SaveChangesAsync(ct);
        return account;
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = await db.OwnUpsAccounts.FindAsync([id], ct);
        if (entity is not null)
        {
            db.OwnUpsAccounts.Remove(entity);
            await db.SaveChangesAsync(ct);
        }
    }
}
