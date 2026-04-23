using Microsoft.Extensions.Logging;

namespace CompanyBrain.Pruning;

/// <summary>
/// Orchestrates intelligent pruning of document content before it reaches the cloud LLM.
/// Combines semantic chunking, relevance scoring (TF-IDF), and context budget management
/// to return only the most relevant portions of large documents.
/// </summary>
public sealed class IntelligentPruningService(
    IRelevanceScoringStrategy scoringStrategy,
    PruningConfiguration configuration,
    ILogger<IntelligentPruningService> logger)
{
    /// <summary>
    /// Prunes <paramref name="text"/> relative to <paramref name="query"/>.
    /// Small documents (below the token budget) are returned as-is.
    /// Large documents are chunked, scored, and the top-N relevant chunks returned.
    /// </summary>
    public async ValueTask<PruningResult> PruneAsync(
        string text,
        string query,
        CancellationToken cancellationToken = default)
    {
        if (!configuration.Enabled || string.IsNullOrWhiteSpace(text))
        {
            return PassThrough(text ?? string.Empty);
        }

        var originalTokens = ContextBudgetManager.EstimateTokens(text);

        if (ContextBudgetManager.FitsWithinBudget(text, configuration.TokenBudget))
        {
            logger.LogDebug(
                "Document fits within budget ({Tokens} tokens ≤ {Budget}), skipping pruning",
                originalTokens,
                configuration.TokenBudget);

            return PassThrough(text);
        }

        return await PruneContentAsync(text, query, originalTokens, cancellationToken);
    }

    private async ValueTask<PruningResult> PruneContentAsync(
        string text,
        string query,
        int originalTokens,
        CancellationToken cancellationToken)
    {
        var chunks = SemanticChunker.Chunk(
            text,
            configuration.ChunkTargetSize,
            configuration.ChunkMinSize,
            configuration.ChunkMaxSize);

        logger.LogDebug(
            "Chunked document into {ChunkCount} chunks for query: {Query}",
            chunks.Count,
            TruncateForLog(query));

        var scoredChunks = await scoringStrategy.ScoreAsync(query, chunks, cancellationToken);

        var selected = ContextBudgetManager.SelectChunks(
            scoredChunks,
            configuration.MaxChunks,
            configuration.TokenBudget,
            configuration.RelevanceThreshold);

        if (selected.Count == 0)
        {
            logger.LogDebug("No chunks met the relevance threshold; returning top chunk as fallback");
            return FallbackToTopChunk(scoredChunks, originalTokens);
        }

        var prunedText = string.Join("\n\n---\n\n", selected.Select(c => c.Text));
        var prunedTokens = ContextBudgetManager.EstimateTokens(prunedText);

        logger.LogInformation(
            "Pruned document from {Original} to {Pruned} tokens ({Selected}/{Total} chunks)",
            originalTokens,
            prunedTokens,
            selected.Count,
            chunks.Count);

        return new PruningResult(
            prunedText,
            WasPruned: true,
            originalTokens,
            prunedTokens,
            chunks.Count,
            selected.Count);
    }

    private static PruningResult FallbackToTopChunk(
        IReadOnlyList<ScoredChunk> scoredChunks,
        int originalTokens)
    {
        if (scoredChunks.Count == 0)
        {
            return PassThrough(string.Empty);
        }

        var best = scoredChunks.OrderByDescending(c => c.Score).First();
        var prunedTokens = ContextBudgetManager.EstimateTokens(best.Text);

        return new PruningResult(
            best.Text,
            WasPruned: true,
            originalTokens,
            prunedTokens,
            scoredChunks.Count,
            ChunksSelected: 1);
    }

    private static PruningResult PassThrough(string text)
    {
        var tokens = ContextBudgetManager.EstimateTokens(text);

        return new PruningResult(
            text,
            WasPruned: false,
            tokens,
            tokens,
            ChunksEvaluated: 0,
            ChunksSelected: 0);
    }

    private static string TruncateForLog(string text)
    {
        return text.Length <= 80 ? text : string.Concat(text.AsSpan(0, 77), "...");
    }
}
