using System.Text;
using CompanyBrain.Dashboard.Features.Notion.Api;

namespace CompanyBrain.Dashboard.Features.Notion.Converters;

/// <summary>
/// Converts a list of Notion blocks to Markdown text.
/// </summary>
public static class NotionBlockConverter
{
    /// <summary>
    /// Converts a flat list of Notion blocks to a Markdown string.
    /// </summary>
    /// <param name="blocks">Blocks at the current nesting level.</param>
    /// <param name="depth">Indentation depth (0 = top level).</param>
    public static string ToMarkdown(IReadOnlyList<NotionBlock> blocks, int depth = 0)
    {
        var sb = new StringBuilder();
        var indent = new string(' ', depth * 2);

        for (int i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            var line = RenderBlock(block, indent);

            if (line is not null)
                sb.AppendLine(line);
        }

        return sb.ToString();
    }

    // ── Block rendering ───────────────────────────────────────────────────────

    private static string? RenderBlock(NotionBlock block, string indent) =>
        block.Type switch
        {
            "paragraph"
                => $"{indent}{ExtractText(block.Paragraph?.RichText)}",

            "heading_1"
                => $"# {ExtractText(block.Heading1?.RichText)}",

            "heading_2"
                => $"## {ExtractText(block.Heading2?.RichText)}",

            "heading_3"
                => $"### {ExtractText(block.Heading3?.RichText)}",

            "bulleted_list_item"
                => $"{indent}- {ExtractText(block.BulletedListItem?.RichText)}",

            "numbered_list_item"
                => $"{indent}1. {ExtractText(block.NumberedListItem?.RichText)}",

            "code"
                => RenderCode(block.Code),

            "quote"
                => $"> {ExtractText(block.Quote?.RichText)}",

            "callout"
                => $"> 📌 {ExtractText(block.Callout?.RichText)}",

            "table"
                => null, // table rows are emitted as children; the table block itself has no content

            "table_row"
                => RenderTableRow(block.TableRow),

            "child_page"
                => $"## {block.ChildPage?.Title}",

            "divider"
                => "---",

            _ => null // skip silently
        };

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string ExtractText(IReadOnlyList<NotionRichText>? richText)
    {
        if (richText is null || richText.Count == 0)
            return string.Empty;

        return string.Concat(richText.Select(r => r.PlainText));
    }

    private static string RenderCode(NotionCode? code)
    {
        if (code is null) return string.Empty;

        var language = string.IsNullOrEmpty(code.Language) ? string.Empty : code.Language.ToLowerInvariant();
        var text = ExtractText(code.RichText);
        return $"```{language}\n{text}\n```";
    }

    private static string? RenderTableRow(NotionTableRow? row)
    {
        if (row is null) return null;

        var cells = row.Cells.Select(cell =>
            string.Concat(cell.Select(r => r.PlainText)));

        return string.Join(" | ", cells);
    }
}
