using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using PrintavoShipper.Core.Interfaces;

namespace PrintavoShipper.Printavo;

public class PrintavoGraphQLException : Exception
{
    public PrintavoGraphQLException(string message) : base(message)
    {
    }
}

/// <summary>
/// POSTs GraphQL requests to Printavo's API v2. Handles the email/token auth
/// headers and Printavo's 10-requests-per-5-seconds rate limit: requests are
/// paced up front so bursts (e.g. paging the whole customer list) wait just
/// long enough to stay under the limit instead of hitting it, with a
/// retry-with-backoff on 429s as a fallback.
/// </summary>
public class GraphQLExecutor
{
    private const string Endpoint = "https://www.printavo.com/api/v2";

    private const int MaxRequestsPerWindow = 10;
    private static readonly TimeSpan RateWindow = TimeSpan.FromSeconds(5);

    // Static: the limit is per Printavo account, shared by every executor
    // instance in the process. Timestamps are Environment.TickCount64
    // (monotonic — unaffected by clock changes).
    private static readonly SemaphoreSlim RateGate = new(1, 1);
    private static readonly Queue<long> RecentSendTicks = new();

    private readonly HttpClient _httpClient;
    private readonly IPrintavoCredentialProvider _credentialProvider;
    private readonly ILogger<GraphQLExecutor> _logger;

    public GraphQLExecutor(HttpClient httpClient, IPrintavoCredentialProvider credentialProvider, ILogger<GraphQLExecutor> logger)
    {
        _httpClient = httpClient;
        _credentialProvider = credentialProvider;
        _logger = logger;
    }

    public async Task<JsonNode> ExecuteAsync(string query, object? variables = null, CancellationToken ct = default)
    {
        var credentials = await _credentialProvider.GetCredentialsAsync(ct)
            ?? throw new PrintavoGraphQLException("Printavo credentials are not configured yet — set them in App Settings.");

        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
            request.Headers.Add("email", credentials.Email);
            request.Headers.Add("token", credentials.ApiToken);
            request.Content = JsonContent.Create(new { query, variables });

            await WaitForRateLimitSlotAsync(ct);
            using var response = await _httpClient.SendAsync(request, ct);

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                if (attempt == maxAttempts)
                {
                    break;
                }

                var delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(2 * attempt);
                _logger.LogWarning("Printavo rate limit hit, retrying in {Delay} (attempt {Attempt}/{Max})", delay, attempt, maxAttempts);
                await Task.Delay(delay, ct);
                continue;
            }

            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync(ct);
            var json = JsonNode.Parse(body) ?? throw new PrintavoGraphQLException("Printavo returned an empty response.");

            if (json["errors"] is JsonArray errors && errors.Count > 0)
            {
                var message = string.Join("; ", errors.Select(e => e?["message"]?.ToString() ?? "Unknown error"));
                throw new PrintavoGraphQLException($"Printavo API error: {message}");
            }

            return json["data"] ?? throw new PrintavoGraphQLException("Printavo response had no 'data' field.");
        }

        throw new PrintavoGraphQLException("Printavo rate limit exceeded after retries.");
    }

    /// <summary>
    /// Sliding-window limiter: waits until fewer than MaxRequestsPerWindow
    /// requests have been sent in the last RateWindow, then records this one.
    /// Only ever actually waits during a burst — normal one-off calls go
    /// straight through.
    /// </summary>
    private static async Task WaitForRateLimitSlotAsync(CancellationToken ct)
    {
        await RateGate.WaitAsync(ct);
        try
        {
            var windowMs = (long)RateWindow.TotalMilliseconds;
            while (true)
            {
                var now = Environment.TickCount64;
                while (RecentSendTicks.Count > 0 && now - RecentSendTicks.Peek() >= windowMs)
                {
                    RecentSendTicks.Dequeue();
                }

                if (RecentSendTicks.Count < MaxRequestsPerWindow)
                {
                    RecentSendTicks.Enqueue(now);
                    return;
                }

                // Small extra margin so our clock and Printavo's don't
                // disagree about whether the oldest request has aged out.
                var waitMs = RecentSendTicks.Peek() + windowMs - now + 100;
                await Task.Delay(TimeSpan.FromMilliseconds(waitMs), ct);
            }
        }
        finally
        {
            RateGate.Release();
        }
    }
}
