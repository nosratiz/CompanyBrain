using System.Net;
using System.Security.Cryptography;
using System.Text;
using CompanyBrain.Dashboard.Features.AutoSync.Models;
using CompanyBrain.Services;

namespace CompanyBrain.Dashboard.Features.AutoSync.Providers;

/// <summary>
/// Ingests GitHub Wiki pages via <see cref="WikiIngester"/>.
///
/// <para>
/// GitHub wikis are rendered as plain HTML at <c>https://github.com/{owner}/{repo}/wiki</c>
/// and individual pages at <c>/wiki/{Page-Name}</c>, making the standard HTML-to-markdown
/// pipeline a perfect fit.  Both the index (all pages) and individual page URLs are supported.
/// </para>
///
/// <para>Delta detection is identical to <see cref="WebWikiIngestionProvider"/>: SHA-256 of
/// the converted markdown is compared against <see cref="SyncSchedule.LastContentHash"/>.</para>
/// </summary>
internal sealed class GitHubWikiIngestionProvider(
    WikiIngester wikiIngester,
    KnowledgeStore knowledgeStore,
    ILogger<GitHubWikiIngestionProvider> logger) : IIngestionProvider
{
    public SourceType SourceType => SourceType.GitHub;

    public async Task<IngestionResult> SyncAsync(SyncSchedule schedule, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(schedule.SourceUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return IngestionResult.Failure($"Invalid or non-HTTP URL: {schedule.SourceUrl}");
        }

        string markdown;
        try
        {
            markdown = await wikiIngester.IngestAsync(uri, cancellationToken);
        }
        catch (HttpRequestException ex) when (
            ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return IngestionResult.Failure(
                $"HTTP {(int)ex.StatusCode!} — access denied for GitHub wiki {schedule.SourceUrl}: {ex.Message}");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return IngestionResult.Failure($"HTTP 404 — GitHub wiki page not found: {schedule.SourceUrl}");
        }
        catch (HttpRequestException ex)
        {
            return IngestionResult.Failure($"HTTP error fetching GitHub wiki {schedule.SourceUrl}: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            return IngestionResult.Failure($"Content extraction failed: {ex.Message}");
        }

        // ── Delta check ───────────────────────────────────────────────────────
        var hash = ComputeHash(markdown);

        if (hash == schedule.LastContentHash)
        {
            logger.LogDebug("GitHub wiki content unchanged for {Url} — skipping re-ingestion", schedule.SourceUrl);
            return IngestionResult.Unchanged(hash);
        }

        // ── Persist to knowledge store ────────────────────────────────────────
        var collection = string.IsNullOrWhiteSpace(schedule.CollectionName)
            ? "General"
            : schedule.CollectionName.Trim();

        var documentName = DeriveDocumentName(schedule.SourceUrl);

        await knowledgeStore.SaveMarkdownToCollectionAsync(collection, documentName, markdown, cancellationToken);

        logger.LogInformation(
            "GitHub wiki content updated for {Url} → collection '{Collection}' as '{Name}'",
            schedule.SourceUrl, collection, documentName);

        return IngestionResult.Succeeded(hash);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string ComputeHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes);
    }

    private static string DeriveDocumentName(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return url;

        var segments = uri.AbsolutePath.TrimEnd('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        // For GitHub wiki URLs like /owner/repo/wiki/Page-Name, pick the last segment
        var last = Array.FindLast(segments, s => !string.IsNullOrEmpty(s));
        return string.IsNullOrEmpty(last) ? uri.Host : last;
    }
}
