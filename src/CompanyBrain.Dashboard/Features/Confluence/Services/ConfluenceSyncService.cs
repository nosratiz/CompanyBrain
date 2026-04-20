using System.Text;
using System.Text.RegularExpressions;
using CompanyBrain.Dashboard.Features.Confluence.Data;
using CompanyBrain.Dashboard.Features.Confluence.Models;
using Microsoft.EntityFrameworkCore;

namespace CompanyBrain.Dashboard.Features.Confluence.Services;

/// <summary>
/// Orchestrates syncing Confluence spaces and pages to local markdown files.
/// </summary>
public sealed partial class ConfluenceSyncService(
    IDbContextFactory<ConfluenceDbContext> dbContextFactory,
    ConfluenceApiService apiService,
    ConfluenceSettingsProvider settingsProvider,
    ILogger<ConfluenceSyncService> logger)
{
    /// <summary>
    /// Configures a space for syncing. Creates the DB record and local directory.
    /// </summary>
    public async Task<ConfluenceSyncedSpace> ConfigureSyncSpaceAsync(
        ConfluenceSpace space,
        CancellationToken cancellationToken = default)
    {
        var opts = await settingsProvider.GetEffectiveOptionsAsync(cancellationToken);

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var existing = await db.SyncedSpaces
            .FirstOrDefaultAsync(s => s.SpaceId == space.Id, cancellationToken);

        if (existing is not null)
        {
            existing.IsEnabled = true;
            await db.SaveChangesAsync(cancellationToken);
            return existing;
        }

        var localPath = Path.Combine(opts.LocalBasePath, SanitizePath(space.Key));
        Directory.CreateDirectory(localPath);

        var record = new ConfluenceSyncedSpace
        {
            SpaceId = space.Id,
            SpaceKey = space.Key,
            SpaceName = space.Name,
            LocalPath = localPath
        };

        db.SyncedSpaces.Add(record);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Configured sync for Confluence space {Key} → {Path}", space.Key, localPath);
        return record;
    }

    /// <summary>
    /// Syncs all pages in a configured space to local markdown files.
    /// Only downloads pages that are new or have a higher version number.
    /// </summary>
    public async Task SyncSpaceAsync(int syncedSpaceId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var space = await db.SyncedSpaces
            .Include(s => s.SyncedPages)
            .FirstOrDefaultAsync(s => s.Id == syncedSpaceId, cancellationToken);

        if (space is null)
            throw new InvalidOperationException($"Synced space {syncedSpaceId} not found.");

        if (!space.IsEnabled)
        {
            logger.LogInformation("Skipping disabled space {Key}", space.SpaceKey);
            return;
        }

        logger.LogInformation("Starting sync for space {Key} ({SpaceId})", space.SpaceKey, space.SpaceId);
        space.LastSyncError = null;

        try
        {
            var remotePages = await apiService.GetAllPagesAsync(space.SpaceId, cancellationToken);
            logger.LogDebug("Space {Key}: fetched {Count} remote pages", space.SpaceKey, remotePages.Count);

            var (processed, skipped, totalBytes) = await SyncPagesAsync(
                remotePages, space, db, cancellationToken);

            var staleCount = RemoveStalePages(space, remotePages, db);

            UpdateSpaceStats(space, remotePages.Count, staleCount, totalBytes);
            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Space {Key}: synced {Processed} pages, skipped {Skipped} unchanged, removed {Stale} stale",
                space.SpaceKey, processed, skipped, staleCount);
        }
        catch (Exception ex)
        {
            space.LastSyncError = ex.Message;
            await db.SaveChangesAsync(cancellationToken);
            logger.LogError(ex, "Sync failed for space {Key}", space.SpaceKey);
            throw;
        }
    }

    private async Task<(int Processed, int Skipped, long TotalBytes)> SyncPagesAsync(
        IReadOnlyList<ConfluencePage> remotePages,
        ConfluenceSyncedSpace space,
        ConfluenceDbContext db,
        CancellationToken ct)
    {
        var existingByPageId = space.SyncedPages.ToDictionary(p => p.PageId);
        var processed = 0;
        var skipped = 0;
        long totalBytes = 0;

        foreach (var remotePage in remotePages)
        {
            ct.ThrowIfCancellationRequested();

            if (existingByPageId.TryGetValue(remotePage.Id, out var existing)
                && existing.RemoteVersion >= remotePage.Version)
            {
                skipped++;
                continue;
            }

            totalBytes += await ProcessPageAsync(remotePage, space, existing, db, ct);
            processed++;
        }

        return (processed, skipped, totalBytes);
    }

    private static int RemoveStalePages(
        ConfluenceSyncedSpace space,
        IReadOnlyList<ConfluencePage> remotePages,
        ConfluenceDbContext db)
    {
        var remoteIds = remotePages.Select(p => p.Id).ToHashSet();
        var stale = space.SyncedPages.Where(p => !remoteIds.Contains(p.PageId)).ToList();

        foreach (var stalePage in stale)
        {
            if (File.Exists(stalePage.LocalPath))
                File.Delete(stalePage.LocalPath);
            db.SyncedPages.Remove(stalePage);
        }

        return stale.Count;
    }

    private static void UpdateSpaceStats(
        ConfluenceSyncedSpace space,
        int remoteCount,
        int staleCount,
        long syncedBytes)
    {
        space.SyncedPageCount = remoteCount - staleCount;
        space.SyncedSizeBytes = syncedBytes;
        space.LastSyncedAtUtc = DateTimeOffset.UtcNow;
    }

    private async Task<long> ProcessPageAsync(
        ConfluencePage page,
        ConfluenceSyncedSpace space,
        ConfluenceSyncedPage? existing,
        ConfluenceDbContext db,
        CancellationToken cancellationToken)
    {
        var fileName = $"{SanitizePath(page.Title)}.md";
        var localPath = Path.Combine(space.LocalPath, fileName);

        var markdown = ConvertToMarkdown(page);
        var bytes = Encoding.UTF8.GetBytes(markdown);

        Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
        await File.WriteAllBytesAsync(localPath, bytes, cancellationToken);

        if (existing is not null)
        {
            existing.Title = page.Title;
            existing.LocalPath = localPath;
            existing.RemoteVersion = page.Version;
            existing.RemoteUpdatedAt = page.UpdatedAt;
            existing.LastSyncedAtUtc = DateTimeOffset.UtcNow;
        }
        else
        {
            db.SyncedPages.Add(new ConfluenceSyncedPage
            {
                SyncedSpaceId = space.Id,
                PageId = page.Id,
                Title = page.Title,
                LocalPath = localPath,
                RemoteVersion = page.Version,
                RemoteUpdatedAt = page.UpdatedAt
            });
        }

        logger.LogDebug("Wrote page '{Title}' → {Path} ({Bytes} bytes)", page.Title, localPath, bytes.Length);
        return bytes.Length;
    }

    /// <summary>
    /// Converts Confluence storage format (XML/HTML) to Markdown.
    /// Handles the most common elements: headings, bold, italic, links, code, lists.
    /// </summary>
    private static string ConvertToMarkdown(ConfluencePage page)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {page.Title}");
        sb.AppendLine();
        sb.AppendLine($"> Source: {page.WebUrl}  ");
        sb.AppendLine($"> Last updated: {page.UpdatedAt:yyyy-MM-dd HH:mm} UTC  ");
        sb.AppendLine($"> Version: {page.Version}");
        sb.AppendLine();

        if (string.IsNullOrWhiteSpace(page.BodyStorage))
        {
            sb.AppendLine("*This page has no content.*");
            return sb.ToString();
        }

        var markdown = StorageToMarkdown(page.BodyStorage);
        sb.Append(markdown);
        return sb.ToString();
    }

    private static string StorageToMarkdown(string html)
    {
        html = MacroCodeRegex().Replace(html, m =>
            $"\n```{m.Groups["lang"].Value.ToLowerInvariant()}\n{m.Groups["code"].Value.Trim()}\n```\n");

        html = ConvertBlockElements(html);
        html = ConvertInlineElements(html);
        html = ConvertTableElements(html);
        html = StripRemainingHtml(html);

        return html.Trim();
    }

    private static string ConvertBlockElements(string html)
    {
        for (var i = 6; i >= 1; i--)
            html = Regex.Replace(html, $@"<h{i}[^>]*>(.*?)</h{i}>",
                new string('#', i) + " $1\n", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        html = Regex.Replace(html, @"<p[^>]*>(.*?)</p>", "$1\n\n",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        html = Regex.Replace(html, @"<li[^>]*>(.*?)</li>", "- $1\n",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        html = Regex.Replace(html, @"<br\s*/?>", "  \n", RegexOptions.IgnoreCase);

        return html;
    }

    private static string ConvertInlineElements(string html)
    {
        html = Regex.Replace(html, @"<strong[^>]*>(.*?)</strong>", "**$1**",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        html = Regex.Replace(html, @"<b[^>]*>(.*?)</b>", "**$1**",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        html = Regex.Replace(html, @"<em[^>]*>(.*?)</em>", "*$1*",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        html = Regex.Replace(html, @"<i[^>]*>(.*?)</i>", "*$1*",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        html = Regex.Replace(html, @"<code[^>]*>(.*?)</code>", "`$1`",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        html = Regex.Replace(html, @"<a[^>]+href=""([^""]+)""[^>]*>(.*?)</a>", "[$2]($1)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        return html;
    }

    private static string ConvertTableElements(string html)
    {
        html = Regex.Replace(html, @"<th[^>]*>(.*?)</th>", "| **$1** ",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        html = Regex.Replace(html, @"<td[^>]*>(.*?)</td>", "| $1 ",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        html = Regex.Replace(html, @"</tr>", "|\n", RegexOptions.IgnoreCase);

        return html;
    }

    private static string StripRemainingHtml(string html)
    {
        html = Regex.Replace(html, @"<[^>]+>", string.Empty);

        html = html
            .Replace("&amp;", "&")
            .Replace("&lt;", "<")
            .Replace("&gt;", ">")
            .Replace("&quot;", "\"")
            .Replace("&#39;", "'")
            .Replace("&nbsp;", " ");

        return Regex.Replace(html, @"\n{3,}", "\n\n");
    }

    private static string SanitizePath(string input)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(input.Length);
        foreach (var c in input)
            sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
        return sb.ToString().Trim('.', ' ');
    }

    [GeneratedRegex(
        @"<ac:structured-macro[^>]+ac:name=""code""[^>]*>.*?<ac:parameter ac:name=""language"">(?<lang>[^<]*)</ac:parameter>.*?<ac:plain-text-body><!\[CDATA\[(?<code>.*?)\]\]></ac:plain-text-body>.*?</ac:structured-macro>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex MacroCodeRegex();
}
