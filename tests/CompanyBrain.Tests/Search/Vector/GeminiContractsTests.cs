using System.Text.Json;
using CompanyBrain.Search.Vector.Gemini;

namespace CompanyBrain.Tests.Search.Vector;

public sealed class GeminiContractsTests
{
    [Fact]
    public void Request_serializes_with_source_generated_context()
    {
        var request = new GeminiEmbedRequest(
            Model: "models/text-embedding-004",
            Content: new GeminiContent([new GeminiPart("hello world")]),
            OutputDimensionality: 768);

        var json = JsonSerializer.Serialize(request, GeminiJsonContext.Default.GeminiEmbedRequest);

        Assert.Contains("\"model\":\"models/text-embedding-004\"", json);
        Assert.Contains("\"text\":\"hello world\"", json);
        Assert.Contains("\"outputDimensionality\":768", json);
    }

    [Fact]
    public void Request_omits_dimensionality_when_null()
    {
        var request = new GeminiEmbedRequest(
            "models/text-embedding-004",
            new GeminiContent([new GeminiPart("x")]),
            OutputDimensionality: null);

        var json = JsonSerializer.Serialize(request, GeminiJsonContext.Default.GeminiEmbedRequest);

        Assert.DoesNotContain("outputDimensionality", json);
    }

    [Fact]
    public void Response_deserializes_embedding_values()
    {
        const string body = """{ "embedding": { "values": [0.1, 0.2, -0.3] } }""";
        var parsed = JsonSerializer.Deserialize(body, GeminiJsonContext.Default.GeminiEmbedResponse);

        Assert.NotNull(parsed);
        Assert.NotNull(parsed!.Embedding);
        Assert.Equal(new[] { 0.1f, 0.2f, -0.3f }, parsed.Embedding!.Values);
    }
}
