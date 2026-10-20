using PrintavoShipper.Core.Models;

namespace PrintavoShipper.Core.Interfaces;

/// <summary>Resolves current, decrypted Printavo credentials from AppSettings for the API clients.</summary>
public interface IPrintavoCredentialProvider
{
    Task<PrintavoCredentials?> GetCredentialsAsync(CancellationToken ct = default);
}

/// <summary>Resolves current, decrypted UPS credentials from AppSettings for the API clients.</summary>
public interface IUpsCredentialProvider
{
    Task<UpsCredentials?> GetCredentialsAsync(CancellationToken ct = default);
}

/// <summary>Resolves current, decrypted FedEx credentials from AppSettings for the API clients.</summary>
public interface IFedExCredentialProvider
{
    Task<FedExCredentials?> GetCredentialsAsync(CancellationToken ct = default);
}
