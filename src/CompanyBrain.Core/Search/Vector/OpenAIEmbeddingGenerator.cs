using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Embeddings;
using SdkEmbeddingOptions = OpenAI.Embeddings.EmbeddingGenerationOptions;
using AiEmbeddingOptions = Microsoft.Extensions.AI.EmbeddingGenerationOptions;

namespace CompanyBrain.Search.Vector;

/// <summary>
/// AOT-friendly OpenAI implementation of <see cref="IEmbeddingGenerator{String, Embedding}"/>.
/// Uses the OpenAI SDK directly to keep dimension enforcement explicit.
/// </summary>
public sealed class OpenAIEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly EmbeddingClient client;
    private readonly string model;
    private readonly int dimensions;

    public OpenAIEmbeddingGenerator(string apiKey, string model, int dimensions, Uri? endpoint = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        this.model = model;
        this.dimensions = dimensions;

        var openAiOptions = endpoint is null ? null : new OpenAIClientOptions { Endpoint = endpoint };
        var openAi = new OpenAIClient(new System.ClientModel.ApiKeyCredential(apiKey), openAiOptions);
        client = openAi.GetEmbeddingClient(model);

        metadata = new EmbeddingGeneratorMetadata("openai", endpoint, model, dimensions);
    }

    private readonly EmbeddingGeneratorMetadata metadata;

    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        AiEmbeddingOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);

        var inputs = values as IReadOnlyList<string> ?? [.. values];
        var sdkOptions = new SdkEmbeddingOptions
        {
            Dimensions = options?.Dimensions ?? (dimensions > 0 ? dimensions : null),
        };

        var sdkResult = await client.GenerateEmbeddingsAsync(inputs, sdkOptions, cancellationToken).ConfigureAwait(false);

        var generated = new GeneratedEmbeddings<Embedding<float>>();
        foreach (var item in sdkResult.Value)
        {
            var floats = item.ToFloats().ToArray();
            generated.Add(new Embedding<float>(floats));
        }
        return generated;
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        if (serviceKey is not null) return null;
        if (serviceType == typeof(EmbeddingGeneratorMetadata)) return metadata;
        return serviceType?.IsInstanceOfType(this) == true ? this : null;
    }

    public void Dispose()
    {
        // EmbeddingClient is owned by OpenAIClient (no IDisposable surface).
    }
}
