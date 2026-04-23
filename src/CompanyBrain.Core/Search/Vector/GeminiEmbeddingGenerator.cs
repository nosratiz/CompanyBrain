using System.Net.Http.Json;
using System.Text.Json;
using CompanyBrain.Search.Vector.Gemini;
using Microsoft.Extensions.AI;

namespace CompanyBrain.Search.Vector;

/// <summary>
/// AOT-friendly Gemini implementation of <see cref="IEmbeddingGenerator{String, Embedding}"/>.
/// Uses Google AI Studio's <c>:embedContent</c> endpoint (no SDK reflection).
/// </summary>
public sealed class GeminiEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    private const string DefaultEndpoint = "https://generativelanguage.googleapis.com/v1beta";

    private readonly HttpClient httpClient;
    private readonly string apiKey;
    private readonly string model;
    private readonly int dimensions;
    private readonly string endpoint;

    public GeminiEmbeddingGenerator(HttpClient httpClient, string apiKey, string model, int dimensions, string? endpoint = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        this.httpClient = httpClient;
        this.apiKey = apiKey;
        this.model = model;
        this.dimensions = dimensions;
        this.endpoint = (endpoint ?? DefaultEndpoint).TrimEnd('/');

        metadata = new EmbeddingGeneratorMetadata("gemini", new Uri(this.endpoint), model, dimensions);
    }

    private readonly EmbeddingGeneratorMetadata metadata;

    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);

        var generated = new GeneratedEmbeddings<Embedding<float>>();
        foreach (var input in values)
        {
            var request = new GeminiEmbedRequest(
                Model: $"models/{model}",
                Content: new GeminiContent([new GeminiPart(input ?? string.Empty)]),
                OutputDimensionality: dimensions > 0 ? dimensions : null);

            var url = $"{endpoint}/models/{Uri.EscapeDataString(model)}:embedContent?key={Uri.EscapeDataString(apiKey)}";
            using var content = JsonContent.Create(request, GeminiJsonContext.Default.GeminiEmbedRequest);
            using var response = await httpClient.PostAsync(url, content, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException(
                    $"Gemini embedContent failed ({(int)response.StatusCode}): {body}");
            }

            var payload = await response.Content
                .ReadFromJsonAsync(GeminiJsonContext.Default.GeminiEmbedResponse, cancellationToken)
                .ConfigureAwait(false);

            var values_ = payload?.Embedding?.Values
                ?? throw new InvalidOperationException("Gemini returned no embedding values.");

            generated.Add(new Embedding<float>(values_));
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
        // HttpClient is owned by IHttpClientFactory.
    }
}
