using System.Text.RegularExpressions;
using CompanyBrain.Application;
using CompanyBrain.Dashboard.Features.ChatRelay.Models;

namespace CompanyBrain.Dashboard.Features.ChatRelay.Services;

/// <summary>
/// Sovereign Chat Relay — core orchestration service.
///
/// <para>
/// Pipeline for each incoming message:
/// <list type="number">
///   <item><description>Look up (or create) a <see cref="ConversationThread"/> mapping for the thread.</description></item>
///   <item><description>Invoke the internal AskDocs RAG engine (<see cref="KnowledgeApplicationService.SearchAsync"/>).</description></item>
///   <item><description>Pass the raw answer through <see cref="SovereignPostProcessor"/> (PII + hostname redaction).</description></item>
///   <item><description>Post the sanitised reply back to the originating chat platform in-thread.</description></item>
///   <item><description>Update <see cref="ConversationThread.LastActivityUtc"/>.</description></item>
/// </list>
/// </para>
/// </summary>
internal sealed partial class ChatRelayService(
    KnowledgeApplicationService knowledgeService,
    ChatRelaySettingsService settingsService,
    IConversationThreadRepository threadRepository,
    SovereignPostProcessor postProcessor,
    SlackOutboundClient slackClient,
    TeamsOutboundClient teamsClient,
    ILogger<ChatRelayService> logger)
{
    private const int MaxSearchResults = 5;

    // Strip Slack-style mention markup: <@U1234567> or <@BOTID>
    [GeneratedRegex(@"<@[A-Z0-9]+>", RegexOptions.None, matchTimeoutMilliseconds: 200)]
    private static partial Regex SlackMentionRegex();

    /// <summary>
    /// Processes an incoming chat message end-to-end (search → filter → reply).
    /// This method is intentionally fire-and-forget-safe: it catches all exceptions
    /// and logs them so the webhook handler can always return 200 OK to the platform.
    /// </summary>
    public async Task ProcessAsync(IncomingChatMessage message, CancellationToken ct = default)
    {
        var settings = await settingsService.GetSettingsAsync(ct);

        // Guard: refuse to process if the relevant platform is disabled.
        if (message.Platform == ChatPlatform.Slack && !settings.SlackEnabled)
        {
            logger.LogDebug("Ignoring Slack message — Slack integration is disabled");
            return;
        }
        if (message.Platform == ChatPlatform.Teams && !settings.TeamsEnabled)
        {
            logger.LogDebug("Ignoring Teams message — Teams integration is disabled");
            return;
        }

        var query = CleanQuery(message);
        if (string.IsNullOrWhiteSpace(query))
        {
            logger.LogDebug("Ignoring empty query from {Platform} user {User}", message.Platform, message.UserId);
            return;
        }

        // ── Step 1: resolve/create the conversation thread mapping ────────────
        ConversationThread thread;
        try
        {
            thread = await threadRepository.GetOrCreateAsync(
                message.Platform,
                message.ThreadId,
                message.ChannelId,
                message.ServiceUrl,
                ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get/create conversation thread mapping");
            return;
        }

        // ── Step 2: RAG search ────────────────────────────────────────────────
        string rawAnswer;
        try
        {
            var result = await knowledgeService.SearchAsync(query, MaxSearchResults, ct);

            if (result.IsFailed)
            {
                rawAnswer = "I couldn't find relevant information in the knowledge base for that query.";
                logger.LogWarning("SearchAsync failed for query '{Query}': {Reasons}", query, result.Reasons);
            }
            else
            {
                rawAnswer = result.Value;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "AskDocs search failed for query '{Query}'", query);
            rawAnswer = "An internal error occurred while searching the knowledge base. Please try again later.";
        }

        // ── Step 3: sovereign post-processing (PII + hostname redaction) ──────
        string safeAnswer;
        try
        {
            safeAnswer = postProcessor.Process(rawAnswer);
        }
        catch (Exception ex)
        {
            // Should never happen, but don't block the reply on a filter bug.
            logger.LogError(ex, "SovereignPostProcessor threw unexpectedly — sending sanitised fallback");
            safeAnswer = "The response could not be safely filtered. Please contact your administrator.";
        }

        // ── Step 4: platform-specific reply ──────────────────────────────────
        var sent = message.Platform switch
        {
            ChatPlatform.Slack => await SendSlackReplyAsync(settings, message, safeAnswer, ct),
            ChatPlatform.Teams => await SendTeamsReplyAsync(settings, message, thread, safeAnswer, ct),
            _ => false,
        };

        // ── Step 5: update thread activity timestamp ──────────────────────────
        if (sent)
        {
            try { await threadRepository.TouchAsync(thread.Id, ct); }
            catch (Exception ex) { logger.LogWarning(ex, "Failed to touch conversation thread {Id}", thread.Id); }
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static string CleanQuery(IncomingChatMessage message)
    {
        var text = message.Text ?? string.Empty;

        // Strip Slack mention markup (<@U1234567>)
        if (message.Platform == ChatPlatform.Slack)
            text = SlackMentionRegex().Replace(text, string.Empty);

        return text.Trim();
    }

    private async Task<bool> SendSlackReplyAsync(
        Models.ChatBotSettings settings,
        IncomingChatMessage message,
        string safeAnswer,
        CancellationToken ct)
    {
        var (botToken, _) = await settingsService.GetSlackCredentialsAsync(ct);

        if (string.IsNullOrEmpty(botToken))
        {
            logger.LogWarning("Slack Bot Token is not configured — reply skipped");
            return false;
        }

        return await slackClient.PostThreadedReplyAsync(
            botToken,
            message.ChannelId,
            message.ThreadId,
            safeAnswer,
            ct);
    }

    private async Task<bool> SendTeamsReplyAsync(
        Models.ChatBotSettings settings,
        IncomingChatMessage message,
        ConversationThread thread,
        string safeAnswer,
        CancellationToken ct)
    {
        var appPassword = await settingsService.GetTeamsAppPasswordAsync(ct);

        if (string.IsNullOrEmpty(settings.TeamsAppId) || string.IsNullOrEmpty(appPassword))
        {
            logger.LogWarning("Teams App ID or App Password is not configured — reply skipped");
            return false;
        }

        return await teamsClient.PostReplyAsync(
            thread.ServiceUrl,
            thread.ExternalThreadId,
            settings.TeamsAppId,
            appPassword,
            safeAnswer,
            ct);
    }
}
