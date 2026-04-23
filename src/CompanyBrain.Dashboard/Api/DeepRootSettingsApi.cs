using CompanyBrain.Dashboard.Api.Contracts;
using CompanyBrain.Dashboard.Services;
using CompanyBrain.Search.Vector;
using Microsoft.AspNetCore.Mvc;

namespace CompanyBrain.Dashboard.Api;

internal static class DeepRootSettingsApi
{
    public static IEndpointRouteBuilder MapDeepRootSettingsApi(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/deeproot/settings")
            .WithTags("DeepRoot Settings");

        group.MapGet("/", GetAsync)
            .WithName("GetDeepRootSettings")
            .Produces<DeepRootSettingsResponse>(StatusCodes.Status200OK);

        group.MapPut("/", UpdateAsync)
            .WithName("UpdateDeepRootSettings")
            .Produces<DeepRootSettingsResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        [FromServices] DeepRootSettingsService settings,
        [FromServices] EmbeddingProviderFactory factory,
        CancellationToken cancellationToken)
    {
        var row = await settings.GetSettingsAsync(cancellationToken);

        // Touch the factory to populate Resolved* values from the current snapshot.
        _ = factory.GetGeneratorOrNull();

        return TypedResults.Ok(new DeepRootSettingsResponse(
            row.Provider,
            row.Model,
            row.Dimensions,
            HasApiKey: !string.IsNullOrEmpty(row.EncryptedApiKey),
            row.Endpoint,
            row.DatabasePath,
            row.UpdatedAtUtc,
            ResolvedModel: factory.ResolvedModel,
            ResolvedDimensions: factory.ResolvedDimensions,
            ProviderActive: factory.ResolvedProvider != EmbeddingProviderType.None));
    }

    private static async Task<IResult> UpdateAsync(
        DeepRootSettingsUpdateRequest request,
        [FromServices] DeepRootSettingsService settings,
        [FromServices] DatabaseEmbeddingOptionsAccessor accessor,
        [FromServices] EmbeddingProviderFactory factory,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Provider))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["provider"] = ["Provider is required (None, OpenAI, Gemini, or Voyage)."],
            });
        }

        if (!Enum.TryParse<EmbeddingProviderType>(request.Provider, ignoreCase: true, out _))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["provider"] = [$"Unknown provider '{request.Provider}'. Allowed: None, OpenAI, Gemini, Voyage."],
            });
        }

        var row = await settings.UpdateAsync(
            request.Provider,
            request.Model ?? string.Empty,
            request.Dimensions ?? 0,
            request.ApiKey,
            request.Endpoint ?? string.Empty,
            request.DatabasePath ?? string.Empty,
            cancellationToken);

        accessor.Invalidate();
        _ = factory.GetGeneratorOrNull();

        return TypedResults.Ok(new DeepRootSettingsResponse(
            row.Provider,
            row.Model,
            row.Dimensions,
            HasApiKey: !string.IsNullOrEmpty(row.EncryptedApiKey),
            row.Endpoint,
            row.DatabasePath,
            row.UpdatedAtUtc,
            ResolvedModel: factory.ResolvedModel,
            ResolvedDimensions: factory.ResolvedDimensions,
            ProviderActive: factory.ResolvedProvider != EmbeddingProviderType.None));
    }
}
