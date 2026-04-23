using System.Text;
using System.Text.RegularExpressions;
using CompanyBrain.Dashboard.Features.AutoSync.Models;
using CompanyBrain.Dashboard.Features.Notion.Api;
using CompanyBrain.Dashboard.Features.Notion.Converters;
using CompanyBrain.Dashboard.Features.Notion.Services;
using CompanyBrain.Services;

namespace CompanyBrain.Dashboard.Features.AutoSync.Providers;

/// <summary>
/// Ingests Notion pages into the DeepRoot knowledge store via the Notion REST API.
///
/// <para>
/// Supports three source-URL modes:
/// <list type="bullet">
///   <item>A notion.so URL containing a UUID — syncs that single page.</item>
///   <item>A raw UUID (dashes optional) — syncs that single page.</item>
///   <item>Empty string — discovers all pages shared with the integration via POST /search.</item>
/// </list>
/// </para>
/// </summary>
internal sealed class NotionIngestionProvider(
    NotionApiClient apiClient,
    NotionSettingsProvider settingsProvider,
    KnowledgeStore knowledgeStore,
    ILogger<NotionIngestionProvider> logger) : IIngestionProvider
{
    private const int MaxRecursionDepth = 3;
    private const int RateLimitDelayMs = 350;
    private static readonly Regex UuidPattern =
        new(@"[0-9a-f]{8}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{12}",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public SourceType SourceType => SourceType.Notion;

    public async Task<IngestionResult> SyncAsync(SyncSchedule schedule, CancellationToken cancellationToken)
    {
        // ── 1. Resolve token ─────────────────────────────────────────────────
        var token = await settingsProvider.GetDecryptedTokenAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(token))
        {
            return IngestionResult.Failure("Notion API token not configured.");
        }

        apiClient.SetToken(token);

        // ── 2. Determine pages to sync ───────────────────────────────────────
        var pageIds = ResolvePageIds(schedule.SourceUrl);
        List<NotionPageObject> pages;

        if (pageIds.Count > 0)
        {
            // Caller supplied specific page IDs — create lightweight stubs for iteration.
            // Actual content is fetched block-by-block; LastEditedTime is unknown so we
            // always do a content fetch. The hash comparison handles idempotency.
            pages = pageIds.Select(id => new NotionPageObject { Id = id }).ToList();
        }
        else
        {
            // Discover all pages shared with the integration.
            var searchResult = await SearchAllPagesAsync(cancellationToken).ConfigureAwait(false);
            if (searchResult.IsFailed)
                return IngestionResult.Failure(searchResult.Errors[0].Message);

            pages = searchResult.Value;
        }

        if (pages.Count == 0)
        {
            logger.LogInformation("No Notion pages found for schedule {Id}", schedule.Id);
            return IngestionResult.Unchanged(schedule.LastContentHash);
        }

        // ── 3. Sync each page ────────────────────────────────────────────────
        var collection = NormalizeCollection(schedule.CollectionName);
        var anyChanged = false;
        var errors = new List<string>();
        string? lastHash = null;

        foreach (var page in pages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await SyncPageAsync(page, collection, schedule.LastContentHash, cancellationToken)
                .ConfigureAwait(false);

            if (!result.Success)
            {
                errors.Add(result.ErrorMessage ?? "Unknown error");
                logger.LogWarning("Notion page {Id} failed: {Error}", page.Id, result.ErrorMessage);
            }
            else
            {
                if (result.ContentChanged) anyChanged = true;
                lastHash = result.ContentHash;
            }

            // Respect Notion's ~3 req/s rate limit
            await Task.Delay(RateLimitDelayMs, cancellationToken).ConfigureAwait(false);
        }

        if (errors.Count == pages.Count)
            return IngestionResult.Failure($"All {errors.Count} page(s) failed. First error: {errors[0]}");

        if (errors.Count > 0)
            logger.LogWarning(
                "Notion sync for schedule {Id}: {Success} succeeded, {Failed} failed",
                schedule.Id, pages.Count - errors.Count, errors.Count);

        return anyChanged
            ? IngestionResult.Succeeded(lastHash)
            : IngestionResult.Unchanged(lastHash ?? schedule.LastContentHash);
    }

    // ── Page-level sync ───────────────────────────────────────────────────────

    private async Task<IngestionResult> SyncPageAsync(
        NotionPageObject page,
        string collection,
        string? previousHash,
        CancellationToken ct)
    {
        // Fetch all blocks recursively
        var blocks = await FetchBlocksRecursiveAsync(page.Id, depth: 0, ct).ConfigureAwait(false);
        if (blocks.IsFailed)
            return IngestionResult.Failure(blocks.Errors[0].Message);

        // Convert to Markdown
        var bodyMd = NotionBlockConverter.ToMarkdown(blocks.Value);
        var title = page.GetTitle();
        var fullMarkdown = $"# {title}\n\n{bodyMd}".TrimEnd();

        // Delta check — use last-edited timestamp as the hash (sufficient for MVP)
        var contentHash = page.LastEditedTime != default
            ? page.LastEditedTime.ToString("O")
            : ComputeStringHash(fullMarkdown);

        if (contentHash == previousHash)
        {
            logger.LogDebug("Notion page {Id} ('{Title}') unchanged — skipping", page.Id, title);
            return IngestionResult.Unchanged(contentHash);
        }

        // Persist
        var documentName = SanitizeDocumentName(title);
        await knowledgeStore.SaveMarkdownToCollectionAsync(collection, documentName, fullMarkdown, ct)
            .ConfigureAwait(false);

        logger.LogInformation(
            "Notion page {Id} ('{Title}') synced → collection '{Collection}' as '{Name}'",
            page.Id, title, collection, documentName);

        return IngestionResult.Succeeded(contentHash);
    }

    // ── Block fetching ────────────────────────────────────────────────────────

    private async Task<FluentResults.Result<List<NotionBlock>>> FetchBlocksRecursiveAsync(
        string blockId,
        int depth,
        CancellationToken ct)
    {
        var allBlocks = new List<NotionBlock>();
        string? cursor = null;

        do
        {
            var result = await apiClient.GetBlockChildrenAsync(blockId, cursor, ct).ConfigureAwait(false);
            if (result.IsFailed)
                return FluentResults.Result.Fail<List<NotionBlock>>(result.Errors[0].Message);

            foreach (var block in result.Value.Results)
            {
                allBlocks.Add(block);

                // Recurse into children up to max depth
                if (block.HasChildren && depth < MaxRecursionDepth)
                {
                    await Task.Delay(RateLimitDelayMs, ct).ConfigureAwait(false);

                    var childResult = await FetchBlocksRecursiveAsync(block.Id, depth + 1, ct)
                        .ConfigureAwait(false);

                    if (childResult.IsSuccess && childResult.Value.Count > 0)
                    {
                        // Append child blocks inline (the converter renders them at the same level)
                        allBlocks.AddRange(childResult.Value);
                    }
                }
            }

            cursor = result.Value.HasMore ? result.Value.NextCursor : null;

        } while (cursor is not null);

        return FluentResults.Result.Ok(allBlocks);
    }

    // ── Page discovery ────────────────────────────────────────────────────────

    private async Task<FluentResults.Result<List<NotionPageObject>>> SearchAllPagesAsync(CancellationToken ct)
    {
        var allPages = new List<NotionPageObject>();
        string? cursor = null;

        do
        {
            var result = await apiClient.SearchPagesAsync(null, cursor, ct).ConfigureAwait(false);
            if (result.IsFailed)
                return FluentResults.Result.Fail<List<NotionPageObject>>(result.Errors[0].Message);

            allPages.AddRange(result.Value.Results);
            cursor = result.Value.HasMore ? result.Value.NextCursor : null;

            if (cursor is not null)
                await Task.Delay(RateLimitDelayMs, ct).ConfigureAwait(false);

        } while (cursor is not null);

        return FluentResults.Result.Ok(allPages);
    }

    // ── URL parsing ───────────────────────────────────────────────────────────

    private static List<string> ResolvePageIds(string sourceUrl)
    {
        if (string.IsNullOrWhiteSpace(sourceUrl))
            return [];

        var match = UuidPattern.Match(sourceUrl);
        if (!match.Success)
            return [];

        // Normalise to dashed UUID format
        var raw = match.Value.Replace("-", "");
        if (raw.Length != 32)
            return [];

        var dashed = $"{raw[..8]}-{raw[8..12]}-{raw[12..16]}-{raw[16..20]}-{raw[20..]}";
        return [dashed];
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string NormalizeCollection(string? name)
        => string.IsNullOrWhiteSpace(name) ? "Notion" : name.Trim();

    private static string SanitizeDocumentName(string title)
        => string.IsNullOrWhiteSpace(title) ? "notion-page" : title.Trim();

    private static string ComputeStringHash(string content)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes);
    }
}

