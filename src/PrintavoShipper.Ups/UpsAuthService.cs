using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using PrintavoShipper.Core.Interfaces;
using PrintavoShipper.Core.Models;

namespace PrintavoShipper.Ups;

public class UpsAuthException : Exception
{
    public UpsAuthException(string message) : base(message)
    {
    }
}

public interface IUpsAuthService
{
    /// <param name="forceRefresh">Skip the cached token and really contact UPS — for Settings' "Test Connection", which must prove the credentials work right now.</param>
    Task<string> GetAccessTokenAsync(CancellationToken ct = default, bool forceRefresh = false);
}

/// <summary>
/// UPS OAuth2 client_credentials flow. No refresh token — just re-requests
/// a new bearer token when the cached one is close to expiry.
/// </summary>
public class UpsAuthService : IUpsAuthService
{
    private readonly HttpClient _httpClient;
    private readonly IUpsCredentialProvider _credentialProvider;

    // Static, i.e. one token for the whole app session: this service is a
    // transient typed HttpClient, so every OrderDetailViewModel (a new one
    // per order opened) got a fresh instance with an empty cache and paid an
    // extra OAuth round trip before its first UPS call. The gate makes
    // concurrent callers (e.g. Get Rates across several accounts at once)
    // share a single refresh instead of each fetching their own.
    private static readonly SemaphoreSlim TokenGate = new(1, 1);
    private static string? _cachedToken;
    private static DateTimeOffset _expiresAt = DateTimeOffset.MinValue;
    private static string? _cachedCredentialsKey;

    public UpsAuthService(HttpClient httpClient, IUpsCredentialProvider credentialProvider)
    {
        _httpClient = httpClient;
        _credentialProvider = credentialProvider;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken ct = default, bool forceRefresh = false)
    {
        var credentials = await _credentialProvider.GetCredentialsAsync(ct)
            ?? throw new UpsAuthException("UPS credentials are not configured yet — set them in App Settings.");

        // The cache lives for the whole app session, so a token cached under
        // the old credentials would otherwise keep being returned as a false
        // "still working" signal for up to an hour after the user changes UPS
        // Client ID/Secret in Settings and clicks "Test UPS Connection" again.
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

    private async Task<string> RequestTokenAsync(UpsCredentials credentials, string credentialsKey, CancellationToken ct)
    {
        var baseUrl = credentials.Environment == UpsEnvironment.Production
            ? "https://onlinetools.ups.com"
            : "https://wwwcie.ups.com";

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/security/v1/oauth/token");
        var basicAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credentials.ClientId}:{credentials.ClientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);
        if (!string.IsNullOrWhiteSpace(credentials.MerchantId))
        {
            request.Headers.Add("x-merchant-id", credentials.MerchantId);
        }

        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials"
        });

        using var response = await _httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new UpsAuthException($"UPS OAuth token request failed ({(int)response.StatusCode}): {body}");
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var accessToken = root.GetProperty("access_token").GetString()
            ?? throw new UpsAuthException("UPS OAuth response had no access_token.");
        var expiresInSeconds = int.Parse(root.GetProperty("expires_in").GetString() ?? "3600");

        _cachedToken = accessToken;
        _cachedCredentialsKey = credentialsKey;
        // Refresh a minute early so an in-flight shipment call never races an expiring token.
        _expiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds - 60);

        return _cachedToken;
    }
}
