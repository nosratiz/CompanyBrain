using System.Text;
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
    private const int MaxSearchResults = 6;
    private const string ChatResultHeader = "Here's what I found for";

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
        var rawAnswer = await RunSearchAsync(query, ct);

        // ── Step 3: sovereign post-processing (PII + hostname redaction) ──────
        var safeAnswer = RunPostProcessor(rawAnswer);

        // ── Step 3b: reformat raw search-result markup into a readable reply ──
        var chatAnswer = FormatForChat(safeAnswer);

        // ── Step 3c: split into one message per result and send all at once ───
        var entries = SplitIntoResultEntries(chatAnswer);

        // ── Step 4: platform-specific reply — burst all entries in order ──────
        var sent = false;
        foreach (var entry in entries)
            sent |= await SendPlatformReplyAsync(settings, message, thread, entry, ct);

        // ── Step 5: update thread activity timestamp ──────────────────────────
        if (sent)
        {
            try { await threadRepository.TouchAsync(thread.Id, ct); }
            catch (Exception ex) { logger.LogWarning(ex, "Failed to touch conversation thread {Id}", thread.Id); }
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<string> RunSearchAsync(string query, CancellationToken ct)
    {
        try
        {
            var result = await knowledgeService.SearchAsync(query, MaxSearchResults, ct);
            if (result.IsFailed)
            {
                logger.LogWarning("SearchAsync failed for query '{Query}': {Reasons}", query, result.Reasons);
                return "I couldn't find relevant information in the knowledge base for that query.";
            }
            return result.Value;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "AskDocs search failed for query '{Query}'", query);
            return "An internal error occurred while searching the knowledge base. Please try again later.";
        }
    }

    private string RunPostProcessor(string rawAnswer)
    {
        try { return postProcessor.Process(rawAnswer); }
        catch (Exception ex)
        {
            logger.LogError(ex, "SovereignPostProcessor threw unexpectedly — sending sanitised fallback");
            return "The response could not be safely filtered. Please contact your administrator.";
        }
    }

    /// <summary>
    /// Splits a formatted chat reply into one message per document result.
    /// Returns a single-element list for non-search replies or single-match results.
    /// </summary>
    private static IReadOnlyList<string> SplitIntoResultEntries(string formatted)
    {
        if (!formatted.StartsWith(ChatResultHeader, StringComparison.Ordinal))
            return [formatted];

        // FormatForChat separates each result block with "\n\n"
        // → parts[0] = header line, parts[1..n] = "*file.md*\n> snippet"
        var parts = formatted.Split("\n\n");
        if (parts.Length <= 2)
            return [formatted]; // only one result

        var results = new List<string>(parts.Length - 1);
        results.Add($"{parts[0]}\n\n{parts[1]}"); // first message carries the header
        for (var i = 2; i < parts.Length; i++)
            results.Add(parts[i]);

        return results;
    }

    private Task<bool> SendPlatformReplyAsync(
        Models.ChatBotSettings settings,
        IncomingChatMessage message,
        ConversationThread thread,
        string reply,
        CancellationToken ct)
        => message.Platform switch
        {
            ChatPlatform.Slack => SendSlackReplyAsync(settings, message, reply, ct),
            ChatPlatform.Teams => SendTeamsReplyAsync(settings, message, thread, reply, ct),
            _ => Task.FromResult(false),
        };

    private static string CleanQuery(IncomingChatMessage message)
    {
        var text = message.Text ?? string.Empty;

        // Strip Slack mention markup (<@U1234567>)
        if (message.Platform == ChatPlatform.Slack)
            text = SlackMentionRegex().Replace(text, string.Empty);

        return text.Trim();
    }

    /// <summary>
    /// Converts the raw search-result string produced by <see cref="KnowledgeStore"/> (which is
    /// formatted for LLM / MCP consumption) into a human-readable Slack/Teams message.
    ///
    /// <para>
    /// Raw format:
    /// <code>
    /// Search results for 'query':
    ///
    /// - @resources/file.md (resources://file.md)
    ///   > snippet line 1
    ///   > snippet line 2
    /// </code>
    /// </para>
    /// </summary>
    private static string FormatForChat(string rawAnswer)
    {
        // "No matches found for 'X' in Y." → friendly message
        const string noMatchPrefix = "No matches found for '";
        if (rawAnswer.StartsWith(noMatchPrefix, StringComparison.Ordinal))
        {
            var queryStart = noMatchPrefix.Length;
            var queryEnd = rawAnswer.IndexOf('\'', queryStart);
            var query = queryEnd > queryStart ? rawAnswer[queryStart..queryEnd] : "your query";
            return $"I couldn't find anything relevant for \"{query}\" in the knowledge base.";
        }

        // Pass non-search-result strings through unchanged.
        const string searchPrefix = "Search results for '";
        if (!rawAnswer.StartsWith(searchPrefix, StringComparison.Ordinal))
            return rawAnswer;

        var lines = rawAnswer.Split('\n');
        var sb = new StringBuilder();

        foreach (var rawLine in lines)
        {
            // Header: "Search results for 'X':" → "Here's what I found for "X":"
            if (rawLine.StartsWith(searchPrefix, StringComparison.Ordinal))
            {
                var innerStart = searchPrefix.Length;
                var innerEnd = rawLine.IndexOf("':", innerStart, StringComparison.Ordinal);
                var query = innerEnd > innerStart
                    ? rawLine[innerStart..innerEnd]
                    : rawLine[innerStart..].TrimEnd(':');
                sb.AppendLine($"Here's what I found for \"{query}\":");
                continue;
            }

            // Resource line: "- @resources/file.md (resources://...)" → "*file.md*"
            const string resourcePrefix = "- @resources/";
            if (rawLine.StartsWith(resourcePrefix, StringComparison.Ordinal))
            {
                var fileStart = resourcePrefix.Length;
                var fileEnd = rawLine.IndexOfAny([' ', '('], fileStart);
                var fileName = fileEnd > fileStart ? rawLine[fileStart..fileEnd] : rawLine[fileStart..];
                sb.AppendLine();
                sb.Append($"*{fileName}*");
                continue;
            }

            // Snippet line: "  > text" → "> text"
            if (rawLine.StartsWith("  >", StringComparison.Ordinal))
            {
                sb.AppendLine();
                sb.Append(rawLine.TrimStart());
                continue;
            }

            // Blank lines — suppress (we add our own spacing above)
            if (!string.IsNullOrWhiteSpace(rawLine))
                sb.AppendLine(rawLine);
        }

        return sb.ToString().TrimEnd();
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
