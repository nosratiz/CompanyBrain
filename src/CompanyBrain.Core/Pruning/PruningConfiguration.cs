namespace CompanyBrain.Pruning;

/// <summary>
/// Configuration for the intelligent pruning engine.
/// Users can adjust these values to control how aggressively content is pruned
/// before being sent to the cloud LLM.
/// </summary>
public sealed class PruningConfiguration
{
    /// <summary>
    /// Minimum cosine similarity (or TF-IDF relevance score) for a chunk to be included.
    /// Range: 0.0 (include everything) to 1.0 (exact match only).
    /// Default: 0.3 for TF-IDF (lower than embedding-based similarity).
    /// </summary>
    public double RelevanceThreshold { get; set; } = 0.3;

    /// <summary>
    /// Maximum number of top-scoring chunks to return.
    /// </summary>
    public int MaxChunks { get; set; } = 3;

    /// <summary>
    /// Approximate token budget. Documents below this size are sent unmodified.
    /// 1 token ≈ 4 characters.
    /// </summary>
    public int TokenBudget { get; set; } = 2000;

    /// <summary>
    /// Target chunk size in characters for the semantic chunker.
    /// </summary>
    public int ChunkTargetSize { get; set; } = 400;

    /// <summary>
    /// Minimum chunk size in characters. Chunks below this are merged with neighbors.
    /// </summary>
    public int ChunkMinSize { get; set; } = 100;

    /// <summary>
    /// Maximum chunk size in characters.
    /// </summary>
    public int ChunkMaxSize { get; set; } = 600;

    /// <summary>
    /// Whether to require chunks to have passed PII masking before scoring.
    /// When true, the pruning service applies PII redaction before relevance scoring.
    /// </summary>
    public bool RequirePiiMaskedInput { get; set; } = true;

    /// <summary>
    /// Whether pruning is enabled at all. When false, content passes through unmodified.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
