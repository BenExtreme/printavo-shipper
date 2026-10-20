using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PrintavoShipper.Core.Data;
using PrintavoShipper.Core.Interfaces;
using PrintavoShipper.Core.Models;

namespace PrintavoShipper.Core.Services;

public class SettingsService : ISettingsService
{
    private readonly IDbContextFactory<PrintavoShipperDbContext> _dbContextFactory;

    public SettingsService(IDbContextFactory<PrintavoShipperDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<AppSettings> GetSettingsAsync(CancellationToken ct = default)
    {
        using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var settings = await db.AppSettings.FirstOrDefaultAsync(x => x.Id == 1, ct);
        if (settings is null)
        {
            settings = new AppSettings { Id = 1 };
            db.AppSettings.Add(settings);
            await db.SaveChangesAsync(ct);
        }

        return settings;
    }

    public async Task SaveAsync(
        AppSettings settings,
        string? printavoApiToken,
        string? upsClientId,
        string? upsClientSecret,
        string? fedExClientId = null,
        string? fedExClientSecret = null,
        CancellationToken ct = default)
    {
        settings.Id = 1;

        if (printavoApiToken is not null)
        {
            settings.PrintavoApiTokenProtected = Protect(printavoApiToken);
        }

        if (upsClientId is not null)
        {
            settings.UpsClientIdProtected = Protect(upsClientId);
        }

        if (upsClientSecret is not null)
        {
            settings.UpsClientSecretProtected = Protect(upsClientSecret);
        }

        if (fedExClientId is not null)
        {
            settings.FedExClientIdProtected = Protect(fedExClientId);
        }

        if (fedExClientSecret is not null)
        {
            settings.FedExClientSecretProtected = Protect(fedExClientSecret);
        }

        settings.UpdatedAt = DateTimeOffset.UtcNow;

        using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var existing = await db.AppSettings.FirstOrDefaultAsync(x => x.Id == 1, ct);
        if (existing is null)
        {
            db.AppSettings.Add(settings);
        }
        else if (!ReferenceEquals(existing, settings))
        {
            db.Entry(existing).CurrentValues.SetValues(settings);
        }

        await db.SaveChangesAsync(ct);
    }

    public string? DecryptPrintavoApiToken(AppSettings settings) => Unprotect(settings.PrintavoApiTokenProtected);

    public string? DecryptUpsClientId(AppSettings settings) => Unprotect(settings.UpsClientIdProtected);

    public string? DecryptUpsClientSecret(AppSettings settings) => Unprotect(settings.UpsClientSecretProtected);

    public string? DecryptFedExClientId(AppSettings settings) => Unprotect(settings.FedExClientIdProtected);

    public string? DecryptFedExClientSecret(AppSettings settings) => Unprotect(settings.FedExClientSecretProtected);

    // DataProtectionScope.CurrentUser: this shop runs one shared Windows login per
    // workstation (confirmed single-PC deployment), so user-scoped DPAPI is fine —
    // revisit if the app is ever installed under per-staff Windows accounts.
    private static byte[] Protect(string plaintext)
    {
        var bytes = Encoding.UTF8.GetBytes(plaintext);
        return ProtectedData.Protect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
    }

    private static string? Unprotect(byte[]? ciphertext)
    {
        if (ciphertext is null || ciphertext.Length == 0)
        {
            return null;
        }

        var bytes = ProtectedData.Unprotect(ciphertext, optionalEntropy: null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(bytes);
    }
}
