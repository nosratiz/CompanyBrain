using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace CompanyBrain.Utilities;

internal static class HtmlMarkdownConverter
{
    private static readonly Regex WhitespaceRegex = new("\\s+", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string Convert(HtmlNode root, Uri baseUri)
    {
        var builder = new StringBuilder();
        foreach (var child in root.ChildNodes)
        {
            AppendNode(builder, child, baseUri, 0);
        }

        return MarkdownUtilities.Normalize(builder.ToString());
    }

    private static void AppendNode(StringBuilder builder, HtmlNode node, Uri baseUri, int listDepth)
    {
        if (node.NodeType == HtmlNodeType.Comment)
        {
            return;
        }

        if (node.NodeType == HtmlNodeType.Text)
        {
            var text = NormalizeInlineText(node.InnerText);
            if (!string.IsNullOrWhiteSpace(text))
            {
                builder.Append(text);
            }

            return;
        }

        var name = node.Name.ToLowerInvariant();
        switch (name)
        {
            case "h1":
            case "h2":
            case "h3":
            case "h4":
            case "h5":
            case "h6":
                builder.AppendLine();
                builder.AppendLine($"{new string('#', int.Parse(name[1..]))} {NormalizeInlineText(node.InnerText)}");
                builder.AppendLine();
                return;

            case "p":
                AppendChildren(builder, node, baseUri, listDepth);
                builder.AppendLine();
                builder.AppendLine();
                return;

            case "br":
                builder.AppendLine();
                return;

            case "ul":
            case "ol":
                builder.AppendLine();
                foreach (var child in node.ChildNodes)
                {
                    AppendNode(builder, child, baseUri, listDepth + 1);
                }
                builder.AppendLine();
                return;

            case "li":
                builder.Append(new string(' ', Math.Max(0, listDepth - 1) * 2));
                builder.Append("- ");
                AppendChildren(builder, node, baseUri, listDepth);
                builder.AppendLine();
                return;

            case "a":
                var href = node.GetAttributeValue("href", string.Empty);
                var text = NormalizeInlineText(node.InnerText);
                if (string.IsNullOrWhiteSpace(text))
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(href) && Uri.TryCreate(baseUri, href, out var resolved))
                {
                    builder.Append($"[{text}]({resolved})");
                }
                else
                {
                    builder.Append(text);
                }

                return;

            case "pre":
                builder.AppendLine();
                builder.AppendLine("```text");
                builder.AppendLine(HtmlEntity.DeEntitize(node.InnerText).TrimEnd());
                builder.AppendLine("```");
                builder.AppendLine();
                return;

            case "code":
                builder.Append($"`{NormalizeInlineText(node.InnerText)}`");
                return;

            case "table":
                AppendTable(builder, node);
                builder.AppendLine();
                return;

            default:
                AppendChildren(builder, node, baseUri, listDepth);
                if (name is "div" or "section" or "article")
                {
                    builder.AppendLine();
                }
                return;
        }
    }

    private static void AppendChildren(StringBuilder builder, HtmlNode node, Uri baseUri, int listDepth)
    {
        foreach (var child in node.ChildNodes)
        {
            AppendNode(builder, child, baseUri, listDepth);
        }
    }

    private static void AppendTable(StringBuilder builder, HtmlNode table)
    {
        var rows = table.SelectNodes(".//tr")?
            .Select(row => row.SelectNodes("./th|./td")?
                .Select(cell => MarkdownUtilities.EscapeTableCell(NormalizeInlineText(cell.InnerText)))
                .ToList() ?? [])
            .Where(row => row.Count > 0)
            .ToList() ?? [];

        if (rows.Count == 0)
        {
            return;
        }

        var width = rows.Max(row => row.Count);
        foreach (var row in rows)
        {
            while (row.Count < width)
            {
                row.Add(string.Empty);
            }
        }

        builder.AppendLine($"| {string.Join(" | ", rows[0])} |");
        builder.AppendLine($"| {string.Join(" | ", Enumerable.Repeat("---", width))} |");
        foreach (var row in rows.Skip(1))
        {
            builder.AppendLine($"| {string.Join(" | ", row)} |");
        }
    }

    private static string NormalizeInlineText(string input)
    {
        var decoded = HtmlEntity.DeEntitize(input);
        return WhitespaceRegex.Replace(decoded, " ").Trim();
    }
}