using System.Net.Http.Headers;
using System.Text.Json;
using CompanyBrain.Dashboard.Features.License;
using Microsoft.AspNetCore.Http;

namespace CompanyBrain.Dashboard.Mcp.Collections;

public sealed class CollectionEntitlementsService(
    IHttpClientFactory httpClientFactory,
    CollectionEntitlementsStore protectedStore,
    ILogger<CollectionEntitlementsService> logger)
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _lock = new(1, 1);

    private CollectionEntitlementsManifest? _cached;
    private DateTimeOffset _cacheExpiresUtc;

    public async Task<CollectionEntitlementsManifest> GetManifestAsync(
        HttpContext? httpContext,
        CancellationToken cancellationToken)
    {
        if (_cached is not null && DateTimeOffset.UtcNow < _cacheExpiresUtc)
        {
            return _cached;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_cached is not null && DateTimeOffset.UtcNow < _cacheExpiresUtc)
            {
                return _cached;
            }

            var loaded = await protectedStore.TryLoadAsync(cancellationToken);
            if (loaded is not null && DateTimeOffset.UtcNow - loaded.RetrievedAtUtc < CacheDuration)
            {
                _cached = loaded;
                _cacheExpiresUtc = DateTimeOffset.UtcNow.Add(CacheDuration);
                return loaded;
            }

            var fetched = await TryFetchFromCloudAsync(httpContext, cancellationToken)
                ?? BuildFallbackManifest(httpContext);

            _cached = fetched;
            _cacheExpiresUtc = DateTimeOffset.UtcNow.Add(CacheDuration);
            await protectedStore.SaveAsync(fetched, cancellationToken);

            return fetched;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<CollectionEntitlementsManifest?> TryFetchFromCloudAsync(
        HttpContext? httpContext,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient("LicenseEntitlementsHttpClient");
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/licenses/current");

            var authHeader = httpContext?.Request.Headers.Authorization.ToString();
            if (!string.IsNullOrWhiteSpace(authHeader)
                && AuthenticationHeaderValue.TryParse(authHeader, out var authValue))
            {
                request.Headers.Authorization = authValue;
            }

            var apiKey = httpContext?.Request.Headers["X-Api-Key"].ToString();
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                request.Headers.TryAddWithoutValidation("X-Api-Key", apiKey);
            }

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var license = await JsonSerializer.DeserializeAsync<LicenseInfo>(stream, JsonOptions, cancellationToken);
            if (license is null)
            {
                return null;
            }

            var team = httpContext?.Request.Headers["X-Team"].ToString();
            return new CollectionEntitlementsManifest(
                license.Tier,
                string.IsNullOrWhiteSpace(team) ? null : team,
                DefaultAllowedCollectionsFor(license.Tier),
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
                DateTimeOffset.UtcNow);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not fetch license from licensing layer.");
            return null;
        }
    }

    private static IReadOnlyList<string> DefaultAllowedCollectionsFor(LicenseTier tier) => tier switch
    {
        LicenseTier.Professional => new[] { "General", "Engineering", "Sales", "HR" },
        LicenseTier.Enterprise => new[] { "*" },
        _ => new[] { "General" },
    };

    private static CollectionEntitlementsManifest BuildFallbackManifest(HttpContext? httpContext)
    {
        var tierHeader = httpContext?.Request.Headers["X-License-Tier"].ToString();
        var team = httpContext?.Request.Headers["X-Team"].ToString();

        var tier = tierHeader?.ToLowerInvariant() switch
        {
            "enterprise" => LicenseTier.Enterprise,
            "pro" => LicenseTier.Professional,
            "professional" => LicenseTier.Professional,
            "starter" => LicenseTier.Starter,
            _ => LicenseTier.Free,
        };

        return new CollectionEntitlementsManifest(
            tier,
            string.IsNullOrWhiteSpace(team) ? null : team,
            DefaultAllowedCollectionsFor(tier),
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
            DateTimeOffset.UtcNow);
    }
}