using System.Net;
using System.Security.Cryptography;
using System.Text;
using CompanyBrain.Dashboard.Features.AutoSync.Models;
using CompanyBrain.Services;

namespace CompanyBrain.Dashboard.Features.AutoSync.Providers;

/// <summary>
/// Ingests content from any public HTTP/HTTPS wiki page (Confluence exported pages,
/// internal documentation sites, etc.) using the existing <see cref="WikiIngester"/>.
///
/// <para>
/// Delta detection: a SHA-256 hash of the converted markdown is compared against
/// <see cref="SyncSchedule.LastContentHash"/>. Unchanged content skips the
/// <c>KnowledgeStore.SaveMarkdownToCollectionAsync</c> call, preventing unnecessary
/// re-embedding and cloud API token consumption.
/// </para>
/// </summary>
internal sealed class WebWikiIngestionProvider(
    WikiIngester wikiIngester,
    KnowledgeStore knowledgeStore,
    ILogger<WebWikiIngestionProvider> logger) : IIngestionProvider
{
    public SourceType SourceType => SourceType.WebWiki;

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
                $"HTTP {(int)ex.StatusCode!} — access denied for {schedule.SourceUrl}: {ex.Message}");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return IngestionResult.Failure($"HTTP 404 — URL not found: {schedule.SourceUrl}");
        }
        catch (HttpRequestException ex)
        {
            return IngestionResult.Failure($"HTTP error fetching {schedule.SourceUrl}: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            // WikiIngester throws when no meaningful content could be extracted
            return IngestionResult.Failure($"Content extraction failed: {ex.Message}");
        }

        // ── Delta check ───────────────────────────────────────────────────────
        var hash = ComputeHash(markdown);

        if (hash == schedule.LastContentHash)
        {
            logger.LogDebug("Content unchanged for {Url} — skipping re-ingestion", schedule.SourceUrl);
            return IngestionResult.Unchanged(hash);
        }

        // ── Persist to knowledge store ────────────────────────────────────────
        var collection = NormalizeCollection(schedule.CollectionName);
        var documentName = DeriveDocumentName(schedule.SourceUrl);

        await knowledgeStore.SaveMarkdownToCollectionAsync(collection, documentName, markdown, cancellationToken);

        logger.LogInformation(
            "WebWiki content updated for {Url} → collection '{Collection}' as '{Name}'",
            schedule.SourceUrl, collection, documentName);

        return IngestionResult.Succeeded(hash);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string ComputeHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes); // 64-char uppercase hex, no hyphens
    }

    private static string NormalizeCollection(string? name)
        => string.IsNullOrWhiteSpace(name) ? "General" : name.Trim();

    private static string DeriveDocumentName(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return url;

        // Use the last non-empty path segment as the document name
        var segments = uri.AbsolutePath.TrimEnd('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var last = Array.FindLast(segments, s => !string.IsNullOrEmpty(s));
        return string.IsNullOrEmpty(last) ? uri.Host : last;
    }
}
