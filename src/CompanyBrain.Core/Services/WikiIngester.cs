using System.Net;
using CompanyBrain.Utilities;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CompanyBrain.Services;

internal sealed class WikiIngester
{
    private readonly HttpClient httpClient;
    private readonly ILogger<WikiIngester> logger;

    public WikiIngester(HttpClient httpClient, ILogger<WikiIngester>? logger = null)
    {
        this.httpClient = httpClient;
        this.logger = logger ?? NullLogger<WikiIngester>.Instance;
    }

    public async Task<string> IngestAsync(Uri url, CancellationToken cancellationToken)
    {
        logger.LogInformation("Fetching wiki content from '{Url}'.", url);

        using var response = await httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var document = new HtmlDocument();
        document.LoadHtml(html);

        RemoveBoilerplate(document);

        var mainNode = SelectPrimaryContentNode(document);
        var markdown = HtmlMarkdownConverter.Convert(mainNode, url);

        if (string.IsNullOrWhiteSpace(markdown))
        {
            logger.LogWarning("No meaningful content was extracted from '{Url}'.", url);
            throw new InvalidOperationException($"No meaningful content could be extracted from {url}.");
        }

        logger.LogInformation("Successfully converted wiki content from '{Url}' to markdown.", url);

        return markdown;
    }

    private static void RemoveBoilerplate(HtmlDocument document)
    {
        var boilerplateNodes = document.DocumentNode.SelectNodes("//script|//style|//nav|//footer|//header|//aside|//form|//noscript|//svg");
        if (boilerplateNodes is null)
        {
            return;
        }

        foreach (var node in boilerplateNodes)
        {
            node.Remove();
        }
    }

    private static HtmlNode SelectPrimaryContentNode(HtmlDocument document)
    {
        var directMatch = document.DocumentNode.SelectSingleNode("//main")
            ?? document.DocumentNode.SelectSingleNode("//article");

        if (directMatch is not null)
        {
            return directMatch;
        }

        return document.DocumentNode
            .SelectNodes("//section|//article|//div|//body")?
            .OrderByDescending(VisibleTextLength)
            .FirstOrDefault(node => VisibleTextLength(node) > 400)
            ?? document.DocumentNode.SelectSingleNode("//body")
            ?? document.DocumentNode;
    }

    private static int VisibleTextLength(HtmlNode node)
    {
        var text = WebUtility.HtmlDecode(node.InnerText);
        return text.Count(static character => !char.IsWhiteSpace(character));
    }

    /// <summary>
    /// Discovers all wiki links from a base URL.
    /// </summary>
    /// <param name="baseUrl">The base URL to discover links from.</param>
    /// <param name="linkSelector">Optional CSS selector or XPath for finding links. If null, discovers all internal links.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of discovered wiki page URLs with their names.</returns>
    public async Task<IReadOnlyList<DiscoveredWikiLink>> DiscoverLinksAsync(Uri baseUrl, string? linkSelector, CancellationToken cancellationToken)
    {
        logger.LogInformation("Discovering wiki links from '{Url}' with selector '{Selector}'.", baseUrl, linkSelector ?? "(auto)");

        using var response = await httpClient.GetAsync(baseUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var document = new HtmlDocument();
        document.LoadHtml(html);

        RemoveBoilerplate(document);

        var links = new List<DiscoveredWikiLink>();
        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Select anchor elements based on selector or default
        var selector = string.IsNullOrWhiteSpace(linkSelector) ? "//a[@href]" : linkSelector;
        var anchorNodes = document.DocumentNode.SelectNodes(selector);

        if (anchorNodes is null)
        {
            logger.LogWarning("No links found at '{Url}' with selector '{Selector}'.", baseUrl, selector);
            return links;
        }

        foreach (var anchor in anchorNodes)
        {
            var href = anchor.GetAttributeValue("href", null);
            if (string.IsNullOrWhiteSpace(href))
            {
                continue;
            }

            // Resolve relative URLs
            if (!Uri.TryCreate(baseUrl, href, out var resolvedUrl))
            {
                continue;
            }

            // Only include http/https links
            if (resolvedUrl.Scheme != Uri.UriSchemeHttp && resolvedUrl.Scheme != Uri.UriSchemeHttps)
            {
                continue;
            }

            // Skip fragments and query-only links to the same page
            var normalizedUrl = new UriBuilder(resolvedUrl) { Fragment = string.Empty }.Uri.ToString().TrimEnd('/');

            if (!seenUrls.Add(normalizedUrl))
            {
                continue;
            }

            // Skip common non-content links
            if (IsNonContentLink(resolvedUrl))
            {
                continue;
            }

            // Extract name from link text or URL
            var linkText = WebUtility.HtmlDecode(anchor.InnerText)?.Trim();
            var name = !string.IsNullOrWhiteSpace(linkText)
                ? SanitizeName(linkText)
                : ExtractNameFromUrl(resolvedUrl);

            if (string.IsNullOrWhiteSpace(name))
            {
                name = $"page-{links.Count + 1}";
            }

            links.Add(new DiscoveredWikiLink(resolvedUrl.ToString(), name));
        }

        logger.LogInformation("Discovered {Count} wiki links from '{Url}'.", links.Count, baseUrl);
        return links;
    }

    private static bool IsNonContentLink(Uri url)
    {
        var path = url.AbsolutePath.ToLowerInvariant();

        // Skip common non-content paths
        return path.EndsWith(".css", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".js", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".woff", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".woff2", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/login", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/logout", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/signup", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/register", StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeName(string text)
    {
        // Remove invalid filename characters and limit length
        var sanitized = string.Join("", text.Split(Path.GetInvalidFileNameChars()));
        return sanitized.Length > 100 ? sanitized[..100] : sanitized;
    }

    private static string ExtractNameFromUrl(Uri url)
    {
        var path = url.AbsolutePath.Trim('/');
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return url.Host;
        }

        var lastSegment = segments[^1];
        // Remove file extension if present
        var dotIndex = lastSegment.LastIndexOf('.');
        if (dotIndex > 0)
        {
            lastSegment = lastSegment[..dotIndex];
        }

        return WebUtility.UrlDecode(lastSegment);
    }
}

/// <summary>
/// Represents a discovered wiki link.
/// </summary>
/// <param name="Url">The full URL of the wiki page.</param>
/// <param name="Name">The suggested name for the document.</param>
public sealed record DiscoveredWikiLink(string Url, string Name);