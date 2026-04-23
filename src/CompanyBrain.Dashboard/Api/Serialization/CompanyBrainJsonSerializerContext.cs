using System.Text.Json;
using System.Text.Json.Serialization;
using CompanyBrain.Dashboard.Api.Contracts;
using CompanyBrain.Models;
using Microsoft.AspNetCore.Mvc;

namespace CompanyBrain.Dashboard.Api.Serialization;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(IngestWikiRequest))]
[JsonSerializable(typeof(IngestWikiBatchRequest))]
[JsonSerializable(typeof(IngestPathRequest))]
[JsonSerializable(typeof(SearchRequest))]
[JsonSerializable(typeof(IngestResultResponse))]
[JsonSerializable(typeof(IngestWikiBatchResponse))]
[JsonSerializable(typeof(IngestWikiBatchItemResult))]
[JsonSerializable(typeof(IReadOnlyList<IngestWikiBatchItemResult>))]
[JsonSerializable(typeof(List<IngestWikiBatchItemResult>))]
[JsonSerializable(typeof(SearchResponse))]
[JsonSerializable(typeof(KnowledgeResourceContent))]
[JsonSerializable(typeof(KnowledgeResourceDescriptor))]
[JsonSerializable(typeof(IReadOnlyList<KnowledgeResourceDescriptor>))]
[JsonSerializable(typeof(List<KnowledgeResourceDescriptor>))]
[JsonSerializable(typeof(DeepRootSettingsResponse))]
[JsonSerializable(typeof(DeepRootSettingsUpdateRequest))]
[JsonSerializable(typeof(ProblemDetails))]
[JsonSerializable(typeof(HttpValidationProblemDetails))]
internal sealed partial class CompanyBrainJsonSerializerContext : JsonSerializerContext
{
}
