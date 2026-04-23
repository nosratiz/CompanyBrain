using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CompanyBrain.Dashboard.Features.ChatRelay.Contracts;
using CompanyBrain.Dashboard.Features.ChatRelay.Models;
using CompanyBrain.Dashboard.Features.ChatRelay.Services;
using Microsoft.AspNetCore.Mvc;

namespace CompanyBrain.Dashboard.Features.ChatRelay.Api;

/// <summary>
/// Minimal API endpoints for the Slack integration.
///
/// <list type="bullet">
///   <item><description>
///     <c>POST /api/chat/slack</c> — receives Slack Event Subscriptions callbacks.
///     Handles both the initial URL-verification challenge and live <c>app_mention</c> events.
///   </description></item>
///   <item><description>
///     <c>GET /api/chat/slack/status</c> — health check used by the Bot Management UI.
///   </description></item>
/// </list>
/// </summary>
public static class SlackWebhookEndpoint
{
    public static IEndpointRouteBuilder MapSlackWebhook(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/chat/slack").WithTags("Chat-Relay");

        group.MapPost("/", HandleEventAsync)
            .WithName("SlackWebhook")
            .WithDescription("Receives Slack Event Subscriptions callbacks.")
            .Accepts<SlackEventCallback>("application/json");

        group.MapGet("/status", GetStatusAsync)
            .WithName("SlackStatus")
            .WithDescription("Returns whether the Slack integration is active.");

        return routes;
    }

    // ── POST /api/chat/slack ──────────────────────────────────────────────────

    private static async Task<IResult> HandleEventAsync(
        HttpRequest request,
        ChatRelayService relay,
        ChatRelaySettingsService settingsService,
        ILogger<ChatRelayService> logger,
        CancellationToken ct)
    {
        // 1. Buffer the raw body for HMAC verification (must happen before any deserialization).
        request.EnableBuffering();
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync(ct);
        request.Body.Position = 0;

        // 2. Deserialize enough to detect the type before running the HMAC.
        //    (The url_verification challenge must be answered quickly — Slack retries.)
        SlackEventCallback? payload = null;
        SlackUrlVerification? challenge = null;

        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var type = doc.RootElement.GetProperty("type").GetString();

            if (type == "url_verification")
            {
                challenge = JsonSerializer.Deserialize(rawBody, ChatRelayJsonContext.Default.SlackUrlVerification);
            }
            else
            {
                payload = JsonSerializer.Deserialize(rawBody, ChatRelayJsonContext.Default.SlackEventCallback);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Slack: failed to parse request body");
            return Results.BadRequest();
        }

        // 3. Verify the Slack request signature (HMAC-SHA256).
        //    Skip for url_verification challenges that arrive without a signature during initial setup.
        var settings = await settingsService.GetSettingsAsync(ct);
        var (_, signingSecret) = await settingsService.GetSlackCredentialsAsync(ct);

        if (!string.IsNullOrEmpty(signingSecret))
        {
            if (!VerifySlackSignature(request, rawBody, signingSecret))
            {
                logger.LogWarning("Slack: request signature verification failed");
                return Results.Unauthorized();
            }
        }
        else
        {
            logger.LogWarning("Slack signing secret is not configured — signature check skipped");
        }

        // 4. Handle URL verification (initial app setup).
        if (challenge is not null)
        {
            return Results.Ok(new SlackChallengeResponse { Challenge = challenge.Challenge });
        }

        // 5. Guard: integration must be enabled.
        if (!settings.SlackEnabled)
            return Results.Ok(); // Silently ignore — return 200 so Slack doesn't retry.

        // 6. Route app_mention events to the relay.
        if (payload?.Event?.Type == "app_mention")
        {
            var evt = payload.Event;
            var threadTs = string.IsNullOrEmpty(evt.ThreadTs) ? evt.Ts : evt.ThreadTs;

            var message = new IncomingChatMessage(
                Platform: ChatPlatform.Slack,
                UserId: evt.User,
                ChannelId: evt.Channel,
                ThreadId: threadTs,
                Text: evt.Text,
                ServiceUrl: string.Empty);

            // Fire-and-forget: Slack expects a 200 within 3 seconds.
            _ = Task.Run(() => relay.ProcessAsync(message, CancellationToken.None), ct);
        }

        // Always return 200 OK to prevent Slack retry storms.
        return Results.Ok();
    }

    // ── GET /api/chat/slack/status ────────────────────────────────────────────

    private static async Task<IResult> GetStatusAsync(
        ChatRelaySettingsService settingsService,
        CancellationToken ct)
    {
        var s = await settingsService.GetSettingsAsync(ct);
        return Results.Ok(new { enabled = s.SlackEnabled, hasToken = !string.IsNullOrEmpty(s.EncryptedSlackBotToken) });
    }

    // ── HMAC-SHA256 signature verification ───────────────────────────────────

    private static bool VerifySlackSignature(HttpRequest request, string rawBody, string signingSecret)
    {
        var timestamp = request.Headers["X-Slack-Request-Timestamp"].FirstOrDefault();
        var signature = request.Headers["X-Slack-Signature"].FirstOrDefault();

        if (string.IsNullOrEmpty(timestamp) || string.IsNullOrEmpty(signature))
            return false;

        // Reject requests older than 5 minutes to defend against replay attacks.
        if (long.TryParse(timestamp, out var ts))
        {
            var requestTime = DateTimeOffset.FromUnixTimeSeconds(ts);
            if (DateTimeOffset.UtcNow - requestTime > TimeSpan.FromMinutes(5))
                return false;
        }

        var baseString = $"v0:{timestamp}:{rawBody}";
        var keyBytes = Encoding.UTF8.GetBytes(signingSecret);
        var dataBytes = Encoding.UTF8.GetBytes(baseString);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(dataBytes);
        var computed = $"v0={Convert.ToHexString(hashBytes).ToLowerInvariant()}";

        // Constant-time comparison to prevent timing attacks.
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computed),
            Encoding.UTF8.GetBytes(signature));
    }
}
