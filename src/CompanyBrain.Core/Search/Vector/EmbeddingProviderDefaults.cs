namespace CompanyBrain.Search.Vector;

/// <summary>
/// Resolved per-provider defaults for model id + dimensions.
/// </summary>
internal static class EmbeddingProviderDefaults
{
    public const string OpenAIDefaultModel = "text-embedding-3-small";
    public const int OpenAIDefaultDimensions = 1536;

    public const string GeminiDefaultModel = "text-embedding-004";
    public const int GeminiDefaultDimensions = 768;

    public const string VoyageDefaultModel = "voyage-3";
    public const int VoyageDefaultDimensions = 1024;

    public static (string Model, int Dimensions) Resolve(EmbeddingProviderType provider, string model, int dimensions)
    {
        return provider switch
        {
            EmbeddingProviderType.OpenAI => (
                string.IsNullOrWhiteSpace(model) ? OpenAIDefaultModel : model,
                dimensions > 0 ? dimensions : OpenAIDefaultDimensions),
            EmbeddingProviderType.Gemini => (
                string.IsNullOrWhiteSpace(model) ? GeminiDefaultModel : model,
                dimensions > 0 ? dimensions : GeminiDefaultDimensions),
            EmbeddingProviderType.Voyage => (
                string.IsNullOrWhiteSpace(model) ? VoyageDefaultModel : model,
                dimensions > 0 ? dimensions : VoyageDefaultDimensions),
            _ => (model ?? string.Empty, dimensions),
        };
    }
}
