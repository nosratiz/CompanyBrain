using System.Text.RegularExpressions;

namespace CompanyBrain.Utilities;

internal static class MarkdownUtilities
{
    private static readonly Regex ExcessBlankLinesRegex = new("\n{3,}", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string Normalize(string markdown)
    {
        var normalized = markdown.Replace("\r\n", "\n");
        normalized = ExcessBlankLinesRegex.Replace(normalized, "\n\n");
        return normalized.Trim();
    }

    public static string EscapeTableCell(string text) => text.Replace("|", "\\|").Replace("\n", " ").Trim();

    public static string ToBlockQuote(string text)
    {
        var lines = Normalize(text).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return string.Join("\n  ", lines.Select(line => $"> {line.Trim()}"));
    }
}