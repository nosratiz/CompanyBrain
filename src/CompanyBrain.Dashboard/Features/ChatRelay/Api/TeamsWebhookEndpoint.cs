using System.Text.Json;
using CompanyBrain.Dashboard.Features.ChatRelay.Contracts;
using CompanyBrain.Dashboard.Features.ChatRelay.Models;
using CompanyBrain.Dashboard.Features.ChatRelay.Services;

namespace CompanyBrain.Dashboard.Features.ChatRelay.Api;

/// <summary>
/// Minimal API endpoints for the Microsoft Teams integration.
///
/// <list type="bullet">
///   <item><description>
///     <c>POST /api/chat/teams</c> — receives Bot Framework Activity callbacks.
///   </description></item>
///   <item><description>
///     <c>GET /api/chat/teams/status</c> — health check used by the Bot Management UI.
///   </description></item>
/// </list>
///
/// <para>
/// <b>Security note:</b> Full Bot Framework JWT validation requires
/// <c>Microsoft.Bot.Connector.Authentication</c> which is not NativeAOT-compatible.
/// As a practical alternative this endpoint checks that the <c>serviceUrl</c> begins
/// with a known Microsoft-owned prefix.  For production deployments, upgrade to the
/// full SDK once AOT support is available.
/// </para>
/// </summary>
public static class TeamsWebhookEndpoint
{
    // Known Microsoft Bot Framework serviceUrl prefixes.
    private static readonly HashSet<string> AllowedServiceUrlPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "https://smba.trafficmanager.net/",
        "https://directline.botframework.com/",
        "https://slack.botframework.com/",
        "https://skype.botframework.com/",
        "https://webchat.botframework.com/",
    };

    public static IEndpointRouteBuilder MapTeamsWebhook(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/chat/teams").WithTags("Chat-Relay");

        group.MapPost("/", HandleActivityAsync)
            .WithName("TeamsWebhook")
            .WithDescription("Receives Microsoft Teams Bot Framework Activity callbacks.");

        group.MapGet("/status", GetStatusAsync)
            .WithName("TeamsStatus")
            .WithDescription("Returns whether the Teams integration is active.");

        return routes;
    }

    // ── POST /api/chat/teams ──────────────────────────────────────────────────

    private static async Task<IResult> HandleActivityAsync(
        HttpRequest request,
        ChatRelayService relay,
        ChatRelaySettingsService settingsService,
        ILogger<ChatRelayService> logger,
        CancellationToken ct)
    {
        var settings = await settingsService.GetSettingsAsync(ct);

        // Guard: integration must be enabled.
        if (!settings.TeamsEnabled)
            return Results.Ok();

        // Deserialize the Bot Framework Activity.
        TeamsActivity? activity;
        try
        {
            activity = await JsonSerializer.DeserializeAsync(
                request.Body,
                ChatRelayJsonContext.Default.TeamsActivity,
                ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Teams: failed to parse Activity body");
            return Results.BadRequest();
        }

        if (activity is null)
            return Results.BadRequest();

        // Validate serviceUrl against known Microsoft prefixes.
        if (!AllowedServiceUrlPrefixes.Any(p => activity.ServiceUrl?.StartsWith(p, StringComparison.OrdinalIgnoreCase) == true))
        {
            logger.LogWarning("Teams: rejected Activity with unknown serviceUrl '{Url}'", activity.ServiceUrl);
            return Results.Unauthorized();
        }

        // Only handle message activities.
        if (activity.Type != "message" || string.IsNullOrWhiteSpace(activity.Text))
            return Results.Ok();

        var message = new IncomingChatMessage(
            Platform: ChatPlatform.Teams,
            UserId: activity.From?.Id ?? string.Empty,
            ChannelId: activity.ChannelId ?? string.Empty,
            ThreadId: activity.Conversation?.Id ?? activity.Id ?? string.Empty,
            Text: activity.Text,
            ServiceUrl: activity.ServiceUrl ?? string.Empty);

        _ = Task.Run(() => relay.ProcessAsync(message, CancellationToken.None), ct);

        return Results.Ok();
    }

    // ── GET /api/chat/teams/status ────────────────────────────────────────────

    private static async Task<IResult> GetStatusAsync(
        ChatRelaySettingsService settingsService,
        CancellationToken ct)
    {
        var s = await settingsService.GetSettingsAsync(ct);
        return Results.Ok(new
        {
            enabled = s.TeamsEnabled,
            hasAppId = !string.IsNullOrEmpty(s.TeamsAppId),
            hasPassword = !string.IsNullOrEmpty(s.EncryptedTeamsAppPassword),
        });
    }
}
