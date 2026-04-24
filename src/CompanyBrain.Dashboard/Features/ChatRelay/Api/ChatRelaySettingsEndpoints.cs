using CompanyBrain.Dashboard.Features.ChatRelay.Services;

namespace CompanyBrain.Dashboard.Features.ChatRelay.Api;

/// <summary>
/// REST endpoints for managing chat-relay settings and active conversation threads.
/// Consumed by the Bot Management Blazor component.
///
/// <list type="bullet">
///   <item><description><c>GET  /api/chat/settings</c></description></item>
///   <item><description><c>PUT  /api/chat/settings</c></description></item>
///   <item><description><c>GET  /api/chat/threads</c></description></item>
///   <item><description><c>DELETE /api/chat/threads/{id}</c></description></item>
///   <item><description><c>DELETE /api/chat/threads</c></description></item>
/// </list>
/// </summary>
public static class ChatRelaySettingsEndpoints
{
    public static IEndpointRouteBuilder MapChatRelaySettingsApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/chat").WithTags("Chat-Relay");

        group.MapGet("/settings", GetSettingsAsync).WithName("GetChatSettings");
        group.MapPut("/settings", PutSettingsAsync).WithName("PutChatSettings");

        group.MapGet("/threads", GetThreadsAsync).WithName("GetConversationThreads");
        group.MapDelete("/threads/{id:int}", DeleteThreadAsync).WithName("DeleteConversationThread");
        group.MapDelete("/threads", DeleteAllThreadsAsync).WithName("DeleteAllConversationThreads");

        return routes;
    }

    // ── Settings ──────────────────────────────────────────────────────────────

    private static async Task<IResult> GetSettingsAsync(
        ChatRelaySettingsService svc, CancellationToken ct)
    {
        var s = await svc.GetSettingsAsync(ct);
        // Return a safe view: never expose encrypted tokens.
        return Results.Ok(new
        {
            slackEnabled = s.SlackEnabled,
            hasSlackBotToken = !string.IsNullOrEmpty(s.EncryptedSlackBotToken),
            hasSlackSigningSecret = !string.IsNullOrEmpty(s.EncryptedSlackSigningSecret),
            teamsEnabled = s.TeamsEnabled,
            teamsAppId = s.TeamsAppId,
            hasTeamsAppPassword = !string.IsNullOrEmpty(s.EncryptedTeamsAppPassword),
            tunnelEnabled = s.TunnelEnabled,
            tunnelUrl = s.TunnelUrl,
        });
    }

    private static async Task<IResult> PutSettingsAsync(
        HttpRequest request,
        ChatRelaySettingsService svc,
        ILogger<ChatRelayService> logger,
        CancellationToken ct)
    {
        UpdateChatSettingsRequest? body;
        try
        {
            body = await request.ReadFromJsonAsync<UpdateChatSettingsRequest>(ct);
        }
        catch
        {
            return Results.BadRequest("Invalid request body.");
        }

        if (body is null) return Results.BadRequest("Request body is required.");

        await svc.UpdateAsync(
            slackEnabled: body.SlackEnabled,
            teamsEnabled: body.TeamsEnabled,
            tunnelEnabled: body.TunnelEnabled,
            slackBotToken: body.SlackBotToken,       // null = preserve
            slackSigningSecret: body.SlackSigningSecret, // null = preserve
            teamsAppId: body.TeamsAppId ?? string.Empty,
            teamsAppPassword: body.TeamsAppPassword, // null = preserve
            ct: ct);

        logger.LogInformation("Chat relay settings updated by API caller");
        return Results.NoContent();
    }

    // ── Threads ───────────────────────────────────────────────────────────────

    private static async Task<IResult> GetThreadsAsync(
        IConversationThreadRepository repo, CancellationToken ct)
    {
        var threads = await repo.GetActiveThreadsAsync(ct);
        return Results.Ok(threads.Select(t => new
        {
            t.Id,
            platform = t.Platform.ToString(),
            t.ExternalThreadId,
            t.ExternalChannelId,
            t.ServiceUrl,
            t.SessionId,
            t.CreatedAtUtc,
            t.LastActivityUtc,
        }));
    }

    private static async Task<IResult> DeleteThreadAsync(
        int id,
        IConversationThreadRepository repo,
        CancellationToken ct)
    {
        await repo.RevokeAsync(id, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteAllThreadsAsync(
        IConversationThreadRepository repo,
        CancellationToken ct)
    {
        await repo.RevokeAllAsync(platform: null, ct);
        return Results.NoContent();
    }

    // ── Request body contract ─────────────────────────────────────────────────

    public sealed record UpdateChatSettingsRequest(
        bool SlackEnabled,
        string? SlackBotToken,
        string? SlackSigningSecret,
        bool TeamsEnabled,
        string? TeamsAppId,
        string? TeamsAppPassword,
        bool TunnelEnabled);
}
