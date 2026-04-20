using System.Text.Json.Serialization;

namespace CompanyBrain.Dashboard.Services.Dtos;

public sealed record JsonRpcError(
    [property: JsonPropertyName("code")]    int Code,
    [property: JsonPropertyName("message")] string Message);
