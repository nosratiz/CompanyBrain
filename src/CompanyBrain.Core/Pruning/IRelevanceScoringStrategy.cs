namespace CompanyBrain.Pruning;

/// <summary>
/// Scores text chunks against a query for relevance.
/// Implementations may use TF-IDF, embeddings, or other strategies.
/// </summary>
public interface IRelevanceScoringStrategy
{
    /// <summary>
    /// Scores each chunk against the query. Returned scores should be in [0.0, 1.0].
    /// </summary>
    ValueTask<IReadOnlyList<ScoredChunk>> ScoreAsync(
        string query,
        IReadOnlyList<string> chunks,
        CancellationToken cancellationToken = default);
}
