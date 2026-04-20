using System.Text.Json;
using System.Text.Json.Serialization;

namespace CompanyBrain.Dashboard.Services.Dtos;

public sealed class JsonRpcResponse<T>
{
    [JsonPropertyName("jsonrpc")] public string? Jsonrpc { get; set; }
    [JsonPropertyName("id")]      public int? Id { get; set; }
    [JsonPropertyName("result")]  public T? Result { get; set; }
    [JsonPropertyName("error")]   public JsonRpcError? Error { get; set; }
    [JsonIgnore]                  public string? SessionId { get; set; }
}
