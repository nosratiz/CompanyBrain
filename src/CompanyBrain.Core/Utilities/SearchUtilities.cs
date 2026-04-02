using System.Text.RegularExpressions;

namespace CompanyBrain.Utilities;

internal static class SearchUtilities
{
    private static readonly Regex SearchTermRegex = new("[A-Za-z0-9][A-Za-z0-9_-]*", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static IEnumerable<string> Tokenize(string query)
    {
        return SearchTermRegex
            .Matches(query)
            .Select(match => match.Value.ToLowerInvariant())
            .Where(term => term.Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    public static IEnumerable<string> ExtractSnippets(string markdown)
    {
        foreach (var snippet in markdown.Split(["\n\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (snippet.Length >= 40)
            {
                yield return snippet;
            }
        }
    }

    public static int ScoreSnippet(string fileName, string snippet, string phrase, IReadOnlyCollection<string> terms)
    {
        var lowerSnippet = snippet.ToLowerInvariant();
        var lowerFileName = fileName.ToLowerInvariant();
        var lowerPhrase = phrase.ToLowerInvariant();
        var score = 0;

        if (lowerSnippet.Contains(lowerPhrase, StringComparison.Ordinal))
        {
            score += 20;
        }

        foreach (var term in terms)
        {
            score += CountOccurrences(lowerSnippet, term) * 3;
            score += CountOccurrences(lowerFileName, term) * 5;
        }

        return score;
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;

        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}