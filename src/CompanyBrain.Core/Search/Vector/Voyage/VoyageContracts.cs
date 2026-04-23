using System.Text.Json.Serialization;

namespace CompanyBrain.Search.Vector.Voyage;

public sealed record VoyageEmbedRequest(
    [property: JsonPropertyName("input")] IReadOnlyList<string> Input,
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("output_dimension"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? OutputDimension,
    [property: JsonPropertyName("input_type"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? InputType);

public sealed record VoyageEmbedResponse(
    [property: JsonPropertyName("data")] IReadOnlyList<VoyageEmbeddingItem>? Data);

public sealed record VoyageEmbeddingItem(
    [property: JsonPropertyName("embedding")] IReadOnlyList<float> Embedding,
    [property: JsonPropertyName("index")] int Index);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(VoyageEmbedRequest))]
[JsonSerializable(typeof(VoyageEmbedResponse))]
[JsonSerializable(typeof(VoyageEmbeddingItem))]
internal sealed partial class VoyageJsonContext : JsonSerializerContext
{
}
