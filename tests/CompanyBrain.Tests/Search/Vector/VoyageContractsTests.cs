using System.Text.Json;
using CompanyBrain.Search.Vector.Voyage;

namespace CompanyBrain.Tests.Search.Vector;

public sealed class VoyageContractsTests
{
    [Fact]
    public void Request_serializes_with_snake_case_and_omits_null_dimension()
    {
        var request = new VoyageEmbedRequest(
            Input: ["hello", "world"],
            Model: "voyage-3",
            OutputDimension: null,
            InputType: null);

        var json = JsonSerializer.Serialize(request, VoyageJsonContext.Default.VoyageEmbedRequest);

        Assert.Contains("\"input\":[\"hello\",\"world\"]", json);
        Assert.Contains("\"model\":\"voyage-3\"", json);
        Assert.DoesNotContain("output_dimension", json);
        Assert.DoesNotContain("input_type", json);
    }

    [Fact]
    public void Request_serializes_output_dimension_when_provided()
    {
        var request = new VoyageEmbedRequest(
            Input: ["hi"],
            Model: "voyage-3-large",
            OutputDimension: 512,
            InputType: "document");

        var json = JsonSerializer.Serialize(request, VoyageJsonContext.Default.VoyageEmbedRequest);

        Assert.Contains("\"output_dimension\":512", json);
        Assert.Contains("\"input_type\":\"document\"", json);
    }

    [Fact]
    public void Response_deserializes_floats_in_index_order()
    {
        const string payload = """
            {
              "data": [
                { "embedding": [0.1, 0.2, 0.3], "index": 1 },
                { "embedding": [0.4, 0.5, 0.6], "index": 0 }
              ]
            }
            """;

        var result = JsonSerializer.Deserialize(payload, VoyageJsonContext.Default.VoyageEmbedResponse);

        Assert.NotNull(result);
        Assert.Equal(2, result!.Data!.Count);
        Assert.Equal(1, result.Data[0].Index);
        Assert.Equal(0, result.Data[1].Index);
    }
}
