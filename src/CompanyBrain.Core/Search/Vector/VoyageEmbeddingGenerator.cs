using System.Net.Http.Headers;
using System.Net.Http.Json;
using CompanyBrain.Search.Vector.Voyage;
using Microsoft.Extensions.AI;

namespace CompanyBrain.Search.Vector;

/// <summary>
/// AOT-friendly Voyage AI implementation of <see cref="IEmbeddingGenerator{String, Embedding}"/>.
/// Voyage is the embedding family Anthropic acquired and recommends for Claude RAG.
/// REST endpoint: <c>POST https://api.voyageai.com/v1/embeddings</c>.
/// </summary>
public sealed class VoyageEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    private const string DefaultEndpoint = "https://api.voyageai.com/v1";

    private readonly HttpClient httpClient;
    private readonly string apiKey;
    private readonly string model;
    private readonly int dimensions;
    private readonly string endpoint;
    private readonly EmbeddingGeneratorMetadata metadata;

    public VoyageEmbeddingGenerator(HttpClient httpClient, string apiKey, string model, int dimensions, string? endpoint = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        this.httpClient = httpClient;
        this.apiKey = apiKey;
        this.model = model;
        this.dimensions = dimensions;
        this.endpoint = (endpoint ?? DefaultEndpoint).TrimEnd('/');

        metadata = new EmbeddingGeneratorMetadata("voyage", new Uri(this.endpoint), model, dimensions);
    }

    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);

        var inputs = values.Select(static v => v ?? string.Empty).ToArray();
        if (inputs.Length == 0)
        {
            return [];
        }

        var request = new VoyageEmbedRequest(
            Input: inputs,
            Model: model,
            OutputDimension: dimensions > 0 && IsConfigurableDimensionModel(model) ? dimensions : null,
            InputType: null);

        using var content = JsonContent.Create(request, VoyageJsonContext.Default.VoyageEmbedRequest);
        using var message = new HttpRequestMessage(HttpMethod.Post, $"{endpoint}/embeddings") { Content = content };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException(
                $"Voyage embeddings failed ({(int)response.StatusCode}): {body}");
        }

        var payload = await response.Content
            .ReadFromJsonAsync(VoyageJsonContext.Default.VoyageEmbedResponse, cancellationToken)
            .ConfigureAwait(false);

        var data = payload?.Data
            ?? throw new InvalidOperationException("Voyage returned no embedding data.");

        var generated = new GeneratedEmbeddings<Embedding<float>>(data.Count);
        foreach (var item in data.OrderBy(static d => d.Index))
        {
            generated.Add(new Embedding<float>(item.Embedding.ToArray()));
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

    /// <summary>
    /// Only the larger Voyage models accept a custom <c>output_dimension</c>; the others reject
    /// the field entirely. Keep the request minimal for the default model.
    /// </summary>
    private static bool IsConfigurableDimensionModel(string model) =>
        model.StartsWith("voyage-3-large", StringComparison.OrdinalIgnoreCase) ||
        model.StartsWith("voyage-code-3", StringComparison.OrdinalIgnoreCase);
}
