namespace CompanyBrain.Search.Vector;

/// <summary>
/// Bound from <c>DeepRoot:Embeddings</c> in appsettings.
/// </summary>
public sealed class EmbeddingOptions
{
    public const string SectionName = "DeepRoot:Embeddings";

    /// <summary>
    /// "OpenAI", "Gemini", "Voyage", or "None" (falls back to legacy keyword search).
    /// "Voyage" is the embedding family Anthropic recommends for Claude RAG pipelines.
    /// </summary>
    public EmbeddingProviderType Provider { get; set; } = EmbeddingProviderType.None;

    /// <summary>
    /// Provider-specific model id. Defaults are applied if empty:
    /// OpenAI -> "text-embedding-3-small" (1536 dims),
    /// Gemini -> "text-embedding-004" (768 dims).
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Vector dimensions written into the sqlite-vec virtual table.
    /// Must match the model's native output (or its truncated dimension for OpenAI v3).
    /// </summary>
    public int Dimensions { get; set; }

    /// <summary>
    /// API key for the selected provider. May be sourced from environment variable.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Optional override for the provider endpoint (Azure OpenAI / proxy / Vertex AI gateway).
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Path to the local sqlite-vec database. Defaults to <c>InternalKnowledge/.deeproot/vectors.db</c>.
    /// </summary>
    public string? DatabasePath { get; set; }
}

public enum EmbeddingProviderType
{
    None = 0,
    OpenAI = 1,
    Gemini = 2,

    /// <summary>
    /// Voyage AI (the embedding family Anthropic owns and recommends for Claude RAG).
    /// Surfaced in the UI as "Claude (Voyage)".
    /// </summary>
    Voyage = 3,
}
