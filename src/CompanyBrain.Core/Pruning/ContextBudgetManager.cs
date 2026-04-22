namespace CompanyBrain.Pruning;

/// <summary>
/// Manages the token budget for pruned content. Decides whether a document
/// is small enough to send whole, and selects the optimal set of chunks
/// that fit within the budget.
/// </summary>
public sealed class ContextBudgetManager
{
    private const int CharsPerToken = 4;

    /// <summary>
    /// Returns true when the text is small enough to send without pruning.
    /// </summary>
    public static bool FitsWithinBudget(string text, int tokenBudget)
    {
        return EstimateTokens(text) <= tokenBudget;
    }

    /// <summary>
    /// Selects the top-scoring chunks that fit within the token budget,
    /// preserving their original document order in the output.
    /// </summary>
    public static IReadOnlyList<ScoredChunk> SelectChunks(
        IReadOnlyList<ScoredChunk> scoredChunks,
        int maxChunks,
        int tokenBudget,
        double relevanceThreshold)
    {
        var candidates = scoredChunks
            .Where(c => c.Score >= relevanceThreshold)
            .OrderByDescending(c => c.Score)
            .Take(maxChunks)
            .ToList();

        var selected = new List<ScoredChunk>();
        var usedTokens = 0;

        foreach (var chunk in candidates)
        {
            var chunkTokens = EstimateTokens(chunk.Text);

            if (usedTokens + chunkTokens > tokenBudget)
            {
                continue;
            }

            selected.Add(chunk);
            usedTokens += chunkTokens;
        }

        // Restore original document order for coherent output
        selected.Sort((a, b) => a.Index.CompareTo(b.Index));

        return selected;
    }

    /// <summary>
    /// Rough token estimate: 1 token ≈ 4 characters.
    /// </summary>
    public static int EstimateTokens(string text)
    {
        return (text.Length + CharsPerToken - 1) / CharsPerToken;
    }
}
