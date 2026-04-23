namespace CompanyBrain.Dashboard.Data.Models;

/// <summary>
/// DeepRoot vector-search configuration. Singleton row pattern (one row per installation).
/// API keys are stored encrypted with ASP.NET Data Protection — see
/// <c>DeepRootSettingsService</c> for the encrypt/decrypt round-trip.
/// </summary>
public sealed class DeepRootEmbeddingSettings
{
    public Guid Id { get; set; } = DeepRootEmbeddingSettingsConstants.SingletonId;

    /// <summary>
    /// One of <c>None</c>, <c>OpenAI</c>, <c>Gemini</c>, or <c>Voyage</c> (Claude family).
    /// Stored as a string for forward-compatibility with new providers.
    /// </summary>
    public string Provider { get; set; } = "None";

    /// <summary>
    /// Provider-specific model id (e.g. <c>text-embedding-3-small</c>, <c>voyage-3</c>).
    /// Empty falls back to the provider default.
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Vector dimensions. <c>0</c> falls back to the model default.
    /// </summary>
    public int Dimensions { get; set; }

    /// <summary>
    /// Encrypted API key (ASP.NET Data Protection ciphertext) — never plaintext on disk.
    /// </summary>
    public string EncryptedApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Optional override for the provider endpoint (Azure OpenAI, Vertex, proxy, …).
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Optional override for the sqlite-vec database path.
    /// </summary>
    public string DatabasePath { get; set; } = string.Empty;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public static class DeepRootEmbeddingSettingsConstants
{
    public static readonly Guid SingletonId = new("a1b2c3d4-e5f6-4789-9abc-def012345678");
}
