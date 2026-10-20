using PrintavoShipper.Core.Models;

namespace PrintavoShipper.Core.Interfaces;

/// <summary>
/// Reads/writes the single AppSettings row. Secret fields (Printavo API
/// token, UPS client id/secret) are encrypted at rest via Windows DPAPI and
/// decrypted transparently by this service — callers never see ciphertext.
/// Connection tests live at the composition-root/ViewModel layer, since they
/// need the Printavo/UPS clients, which sit above Core and would create a
/// circular reference if called from here.
/// </summary>
public interface ISettingsService
{
    Task<AppSettings> GetSettingsAsync(CancellationToken ct = default);

    Task SaveAsync(
        AppSettings settings,
        string? printavoApiToken,
        string? upsClientId,
        string? upsClientSecret,
        string? fedExClientId = null,
        string? fedExClientSecret = null,
        CancellationToken ct = default);

    string? DecryptPrintavoApiToken(AppSettings settings);
    string? DecryptUpsClientId(AppSettings settings);
    string? DecryptUpsClientSecret(AppSettings settings);
    string? DecryptFedExClientId(AppSettings settings);
    string? DecryptFedExClientSecret(AppSettings settings);
}
