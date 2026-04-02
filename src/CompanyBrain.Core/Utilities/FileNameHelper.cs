using System.Text.RegularExpressions;

namespace CompanyBrain.Utilities;

internal static class FileNameHelper
{
    private static readonly Regex InvalidFileNameCharactersRegex = new("[^a-z0-9._-]+", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex DuplicateHyphenRegex = new("-{2,}", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string ToMarkdownFileName(string name)
    {
        var candidate = string.IsNullOrWhiteSpace(name) ? "document" : name.Trim();
        candidate = candidate.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(candidate)
            : candidate;

        var slug = InvalidFileNameCharactersRegex.Replace(candidate.ToLowerInvariant(), "-");
        slug = DuplicateHyphenRegex.Replace(slug, "-").Trim('-');

        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = $"document-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        }

        return $"{slug}.md";
    }
}