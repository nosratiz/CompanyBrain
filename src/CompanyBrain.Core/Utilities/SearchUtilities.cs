using System.Text.RegularExpressions;

namespace CompanyBrain.Utilities;

internal static class SearchUtilities
{
    private static readonly Regex SearchTermRegex = new("[A-Za-z0-9][A-Za-z0-9_-]*", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // Common English words that appear in almost every document and add no discriminative value.
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        // Articles, conjunctions, particles
        "an", "the", "and", "but", "or", "nor", "so", "yet",
        // Prepositions
        "in", "on", "at", "to", "for", "of", "by", "as", "up",
        "from", "into", "with", "about", "over", "after", "before",
        "between", "during", "out",
        // Pronouns
        "me", "my", "we", "us", "our", "it", "its",
        "he", "him", "his", "she", "her", "they", "them", "their",
        "you", "your",
        // Auxiliary verbs
        "is", "am", "are", "was", "were", "be", "been", "being",
        "do", "did", "does", "have", "has", "had",
        "will", "would", "could", "should", "may", "might", "can", "shall",
        // Question words
        "what", "which", "who", "whom", "whose", "where", "when", "why", "how",
        // Demonstratives and quantifiers
        "this", "that", "these", "those",
        "all", "any", "each", "few", "more", "most", "some", "such",
        "no", "not", "only", "same", "than", "too", "very",
        // Common filler words
        "if", "then", "also", "just",
    };

    public static IEnumerable<string> Tokenize(string query)
    {
        return SearchTermRegex
            .Matches(query)
            .Select(match => match.Value.ToLowerInvariant())
            .Where(term => term.Length >= 2 && !StopWords.Contains(term))
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
            // Require word boundaries so that e.g. "is" does not match inside
            // "IsNullable", "in" does not match inside "integer", etc.
            var before = index == 0
                || !(char.IsLetterOrDigit(haystack[index - 1]) || haystack[index - 1] == '_');
            var end = index + needle.Length;
            var after = end >= haystack.Length
                || !(char.IsLetterOrDigit(haystack[end]) || haystack[end] == '_');

            if (before && after)
                count++;

            index += needle.Length;
        }

        return count;
    }
}