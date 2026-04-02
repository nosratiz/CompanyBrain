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
}