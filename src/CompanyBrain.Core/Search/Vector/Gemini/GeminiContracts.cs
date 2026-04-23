using System.Text.Json.Serialization;

namespace CompanyBrain.Search.Vector.Gemini;

internal sealed record GeminiEmbedRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("content")] GeminiContent Content,
    [property: JsonPropertyName("outputDimensionality"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? OutputDimensionality);

internal sealed record GeminiContent(
    [property: JsonPropertyName("parts")] IReadOnlyList<GeminiPart> Parts);

internal sealed record GeminiPart(
    [property: JsonPropertyName("text")] string Text);

internal sealed record GeminiEmbedResponse(
    [property: JsonPropertyName("embedding")] GeminiEmbeddingPayload? Embedding);

internal sealed record GeminiEmbeddingPayload(
    [property: JsonPropertyName("values")] float[] Values);

[JsonSerializable(typeof(GeminiEmbedRequest))]
[JsonSerializable(typeof(GeminiEmbedResponse))]
internal sealed partial class GeminiJsonContext : JsonSerializerContext;
