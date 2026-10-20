using PrintavoShipper.Core.Interfaces;
using PrintavoShipper.Core.Models;

namespace PrintavoShipper.Core.Services;

public class PrintavoCredentialProvider : IPrintavoCredentialProvider
{
    private readonly ISettingsService _settingsService;

    public PrintavoCredentialProvider(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task<PrintavoCredentials?> GetCredentialsAsync(CancellationToken ct = default)
    {
        var settings = await _settingsService.GetSettingsAsync(ct);
        var token = _settingsService.DecryptPrintavoApiToken(settings);

        if (string.IsNullOrWhiteSpace(settings.PrintavoEmail) || string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        return new PrintavoCredentials(settings.PrintavoEmail, token);
    }
}

public class UpsCredentialProvider : IUpsCredentialProvider
{
    private readonly ISettingsService _settingsService;

    public UpsCredentialProvider(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task<UpsCredentials?> GetCredentialsAsync(CancellationToken ct = default)
    {
        var settings = await _settingsService.GetSettingsAsync(ct);
        var clientId = _settingsService.DecryptUpsClientId(settings);
        var clientSecret = _settingsService.DecryptUpsClientSecret(settings);

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            return null;
        }

        return new UpsCredentials(clientId, clientSecret, settings.UpsEnvironment, settings.UpsMerchantId);
    }
}

public class FedExCredentialProvider : IFedExCredentialProvider
{
    private readonly ISettingsService _settingsService;

    public FedExCredentialProvider(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task<FedExCredentials?> GetCredentialsAsync(CancellationToken ct = default)
    {
        var settings = await _settingsService.GetSettingsAsync(ct);
        var clientId = _settingsService.DecryptFedExClientId(settings);
        var clientSecret = _settingsService.DecryptFedExClientSecret(settings);

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            return null;
        }

        return new FedExCredentials(clientId, clientSecret, settings.FedExEnvironment, settings.FedExAccountNumber);
    }
}
