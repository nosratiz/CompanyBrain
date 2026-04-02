using System.Text.Json;
using System.Text.Json.Serialization;
using CompanyBrain.Api.Contracts;
using CompanyBrain.Models;
using Microsoft.AspNetCore.Mvc;

namespace CompanyBrain.Api.Serialization;

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(IngestWikiRequest))]
[JsonSerializable(typeof(IngestPathRequest))]
[JsonSerializable(typeof(SearchRequest))]
[JsonSerializable(typeof(IngestResultResponse))]
[JsonSerializable(typeof(SearchResponse))]
[JsonSerializable(typeof(KnowledgeResourceContent))]
[JsonSerializable(typeof(KnowledgeResourceDescriptor))]
[JsonSerializable(typeof(IReadOnlyList<KnowledgeResourceDescriptor>))]
[JsonSerializable(typeof(List<KnowledgeResourceDescriptor>))]
[JsonSerializable(typeof(ProblemDetails))]
[JsonSerializable(typeof(HttpValidationProblemDetails))]
internal sealed partial class CompanyBrainJsonSerializerContext : JsonSerializerContext
{
}