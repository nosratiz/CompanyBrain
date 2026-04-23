using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CompanyBrain.Dashboard.Features.ChatRelay.Contracts;

namespace CompanyBrain.Dashboard.Features.ChatRelay.Services;

/// <summary>
/// Sends replies to Microsoft Teams via the Bot Framework REST API.
/// Obtains and caches a short-lived OAuth2 token from the Bot Framework
/// token endpoint before each outbound call.
/// </summary>
public sealed class TeamsOutboundClient(
    IHttpClientFactory httpClientFactory,
    ILogger<TeamsOutboundClient> logger)
{
    // Known Microsoft Bot Framework service-URL prefixes — used to restrict
    // outbound calls to trusted Microsoft endpoints only (defense-in-depth).
    private static readonly HashSet<string> KnownServiceUrlPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "https://smba.trafficmanager.net/",
        "https://directline.botframework.com/",
        "https://slack.botframework.com/",
        "https://skype.botframework.com/",
        "https://webchat.botframework.com/",
    };

    private const string TokenEndpoint =
        "https://login.microsoftonline.com/botframework.com/oauth2/v2.0/token";

    private const string BotFrameworkScope = "https://api.botframework.com/.default";

    // Simple in-process token cache keyed by appId.
    private readonly ConcurrentDictionary<string, CachedToken> _tokenCache = new();

    /// <summary>
    /// Posts <paramref name="text"/> as a reply into the Teams conversation.
    /// </summary>
    /// <param name="serviceUrl">
    /// The <c>serviceUrl</c> from the incoming Activity.
    /// Must start with a known Microsoft Bot Framework prefix.
    /// </param>
    /// <param name="conversationId">The <c>conversation.id</c> from the incoming Activity.</param>
    /// <param name="appId">The Teams bot App ID.</param>
    /// <param name="appPassword">Decrypted App Password (client secret).</param>
    /// <param name="text">PII-redacted answer text.</param>
    public async Task<bool> PostReplyAsync(
        string serviceUrl,
        string conversationId,
        string appId,
        string appPassword,
        string text,
        CancellationToken ct = default)
    {
        // Validate service URL against known Microsoft endpoints.
        if (!KnownServiceUrlPrefixes.Any(p => serviceUrl.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            logger.LogWarning(
                "Rejected Teams reply: serviceUrl '{Url}' does not match known Bot Framework prefixes",
                serviceUrl);
            return false;
        }

        var token = await GetOrRefreshTokenAsync(appId, appPassword, ct);
        if (token is null) return false;

        var url = $"{serviceUrl.TrimEnd('/')}/v3/conversations/{Uri.EscapeDataString(conversationId)}/activities";

        var activity = new TeamsReplyActivity { Type = "message", Text = text };

        var client = httpClientFactory.CreateClient("teams");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            using var response = await client.PostAsJsonAsync(
                url,
                activity,
                ChatRelayJsonContext.Default.TeamsReplyActivity,
                ct);

            if (response.IsSuccessStatusCode)
            {
                logger.LogDebug("Teams reply posted to conversation {ConversationId}", conversationId);
                return true;
            }

            var errorBody = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning(
                "Teams reply failed: HTTP {Status} — {Body}",
                (int)response.StatusCode, errorBody);
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to post Teams reply to conversation {ConversationId}", conversationId);
            return false;
        }
    }

    // ── Token management ──────────────────────────────────────────────────────

    private async Task<string?> GetOrRefreshTokenAsync(string appId, string appPassword, CancellationToken ct)
    {
        if (_tokenCache.TryGetValue(appId, out var cached) && !cached.IsExpired)
            return cached.Token;

        var client = httpClientFactory.CreateClient("teams");

        var formData = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = appId,
            ["client_secret"] = appPassword,
            ["scope"] = BotFrameworkScope,
        };

        try
        {
            using var response = await client.PostAsync(
                TokenEndpoint,
                new FormUrlEncodedContent(formData),
                ct);

            response.EnsureSuccessStatusCode();

            var tokenResponse = await response.Content.ReadFromJsonAsync(
                ChatRelayJsonContext.Default.BotFrameworkTokenResponse, ct);

            if (tokenResponse is null || string.IsNullOrEmpty(tokenResponse.AccessToken))
            {
                logger.LogWarning("Bot Framework token endpoint returned an empty token");
                return null;
            }

            // Cache with a 5-minute buffer before the actual expiry.
            var expiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 300);
            _tokenCache[appId] = new CachedToken(tokenResponse.AccessToken, expiresAt);

            logger.LogDebug("Bot Framework access token refreshed (expires in ~{Sec}s)", tokenResponse.ExpiresIn);
            return tokenResponse.AccessToken;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to obtain Bot Framework access token for app {AppId}", appId);
            return null;
        }
    }

    private sealed record CachedToken(string Token, DateTime ExpiresAt)
    {
        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    }
}
