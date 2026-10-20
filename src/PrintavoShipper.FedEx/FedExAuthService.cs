using System.Text.Json;
using PrintavoShipper.Core.Interfaces;
using PrintavoShipper.Core.Models;

namespace PrintavoShipper.FedEx;

public class FedExAuthException : Exception
{
    public FedExAuthException(string message) : base(message)
    {
    }
}

public interface IFedExAuthService
{
    /// <param name="forceRefresh">Skip the cached token and really contact FedEx — for Settings' "Test Connection", which must prove the credentials work right now.</param>
    Task<string> GetAccessTokenAsync(CancellationToken ct = default, bool forceRefresh = false);
}

/// <summary>
/// FedEx OAuth2 client_credentials flow. Mirrors UpsAuthService's caching
/// strategy exactly (one static in-memory token shared for the app's whole
/// session, refreshed a minute early, invalidated if credentials change),
/// but two real wire differences from UPS: credentials go in the
/// form-urlencoded body (not an HTTP Basic auth header), and expires_in is a
/// JSON number (not a string) — both confirmed against FedEx's published
/// Authorization API documentation this session.
/// </summary>
public class FedExAuthService : IFedExAuthService
{
    private readonly HttpClient _httpClient;
    private readonly IFedExCredentialProvider _credentialProvider;

    // Static for the same reason as UpsAuthService's: this is a transient
    // typed HttpClient, so a per-instance cache started empty on every order
    // opened.
    private static readonly SemaphoreSlim TokenGate = new(1, 1);
    private static string? _cachedToken;
    private static DateTimeOffset _expiresAt = DateTimeOffset.MinValue;
    private static string? _cachedCredentialsKey;

    public FedExAuthService(HttpClient httpClient, IFedExCredentialProvider credentialProvider)
    {
        _httpClient = httpClient;
        _credentialProvider = credentialProvider;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken ct = default, bool forceRefresh = false)
    {
        var credentials = await _credentialProvider.GetCredentialsAsync(ct)
            ?? throw new FedExAuthException("FedEx credentials are not configured yet — set them in App Settings.");

        var credentialsKey = $"{credentials.ClientId}|{credentials.ClientSecret}|{credentials.Environment}";

        await TokenGate.WaitAsync(ct);
        try
        {
            if (!forceRefresh && _cachedToken is not null && credentialsKey == _cachedCredentialsKey && DateTimeOffset.UtcNow < _expiresAt)
            {
                return _cachedToken;
            }

            return await RequestTokenAsync(credentials, credentialsKey, ct);
        }
        finally
        {
            TokenGate.Release();
        }
    }

    private async Task<string> RequestTokenAsync(FedExCredentials credentials, string credentialsKey, CancellationToken ct)
    {
        var baseUrl = credentials.Environment == FedExEnvironment.Production
            ? "https://apis.fedex.com"
            : "https://apis-sandbox.fedex.com";

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/oauth/token");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = credentials.ClientId,
            ["client_secret"] = credentials.ClientSecret
        });

        using var response = await _httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new FedExAuthException($"FedEx OAuth token request failed ({(int)response.StatusCode}): {body}");
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var accessToken = root.GetProperty("access_token").GetString()
            ?? throw new FedExAuthException("FedEx OAuth response had no access_token.");
        var expiresInSeconds = root.TryGetProperty("expires_in", out var expiresProp) ? expiresProp.GetInt32() : 3600;

        _cachedToken = accessToken;
        _cachedCredentialsKey = credentialsKey;
        _expiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds - 60);

        return _cachedToken;
    }
}
