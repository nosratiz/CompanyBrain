using CompanyBrain.MultiTenant.Abstractions;
using CompanyBrain.MultiTenant.Api;
using CompanyBrain.MultiTenant.Api.Contracts;
using CompanyBrain.MultiTenant.Data;
using CompanyBrain.MultiTenant.Domain;
using CompanyBrain.MultiTenant.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace CompanyBrain.MultiTenant.Tests.Integration;

public sealed class TenantApiIntegrationTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private TestServer _server = null!;
    private HttpClient _client = null!;
    private readonly string _dbName = $"TenantApiTests_{Guid.NewGuid():N}";

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();

        // Configure services
        builder.Services.AddDbContext<TenantDbContext>(options =>
            options.UseInMemoryDatabase(_dbName));

        builder.Services.AddSingleton<ITenantContextAccessor, TenantContextAccessor>();
        builder.Services.AddSingleton<ITenantContext>(sp => sp.GetRequiredService<ITenantContextAccessor>());
        builder.Services.AddScoped<TenantService>();
        builder.Services.AddScoped<ApiKeyService>();
        builder.Services.AddSingleton(sp => new TenantKnowledgeStoreFactory(
            Path.GetTempPath(),
            sp.GetRequiredService<ITenantContext>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>()));

        // Configure JSON for minimal APIs
        builder.Services.Configure<JsonOptions>(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        builder.WebHost.UseTestServer();

        _app = builder.Build();
        _app.MapTenantApi("http://localhost:5003");

        await _app.StartAsync();
        _server = _app.GetTestServer();
        _client = _server.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.StopAsync();
        _server.Dispose();
        await _app.DisposeAsync();
    }

    #region Create Tenant Tests

    [Fact]
    public async Task CreateTenant_WithValidRequest_ShouldReturnCreatedTenant()
    {
        // Arrange
        var request = new CreateTenantRequest("Test Tenant", "A test tenant desc", TenantPlan.Free);

        // Act
        var response = await _client.PostAsJsonAsync("/api/tenants", request, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var tenant = await response.Content.ReadFromJsonAsync<TenantResponse>(JsonOptions);
        tenant.Should().NotBeNull();
        tenant!.Name.Should().Be("Test Tenant");
        tenant.Slug.Should().Be("test-tenant");
        tenant.Plan.Should().Be(TenantPlan.Free);
        tenant.Status.Should().Be(TenantStatus.Active);
    }

    [Fact]
    public async Task CreateTenant_WithDuplicateName_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new CreateTenantRequest("Duplicate Tenant", null, TenantPlan.Free);
        await _client.PostAsJsonAsync("/api/tenants", request, JsonOptions);

        // Act - Try to create same tenant again
        var response = await _client.PostAsJsonAsync("/api/tenants", request, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateTenant_WithEnterprisePlan_ShouldCreateSuccessfully()
    {
        // Arrange
        var request = new CreateTenantRequest("Enterprise Client", "Big company", TenantPlan.Enterprise);

        // Act
        var response = await _client.PostAsJsonAsync("/api/tenants", request, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var tenant = await response.Content.ReadFromJsonAsync<TenantResponse>(JsonOptions);
        tenant!.Plan.Should().Be(TenantPlan.Enterprise);
    }

    #endregion

    #region List Tenants Tests

    [Fact]
    public async Task ListTenants_ShouldReturnAllActiveTenants()
    {
        // Arrange
        await _client.PostAsJsonAsync("/api/tenants",
            new CreateTenantRequest("Tenant A", null, TenantPlan.Free), JsonOptions);
        await _client.PostAsJsonAsync("/api/tenants",
            new CreateTenantRequest("Tenant B", null, TenantPlan.Professional), JsonOptions);

        // Act
        var response = await _client.GetFromJsonAsync<TenantListResponse>("/api/tenants", JsonOptions);

        // Assert
        response.Should().NotBeNull();
        response!.Tenants.Should().HaveCountGreaterThanOrEqualTo(2);
        response.Tenants.Should().Contain(t => t.Name == "Tenant A");
        response.Tenants.Should().Contain(t => t.Name == "Tenant B");
    }

    [Fact]
    public async Task ListTenants_ShouldIncludeSuspendedTenants()
    {
        // Arrange - create and suspend a tenant
        var createResponse = await _client.PostAsJsonAsync("/api/tenants",
            new CreateTenantRequest("Suspended Tenant", null, TenantPlan.Free), JsonOptions);
        var tenant = await createResponse.Content.ReadFromJsonAsync<TenantResponse>(JsonOptions);
        await _client.PostAsync($"/api/tenants/{tenant!.Id}/suspend", null);

        // Act
        var response = await _client.GetFromJsonAsync<TenantListResponse>("/api/tenants", JsonOptions);

        // Assert - Suspended tenants ARE included (only deleted are excluded)
        response!.Tenants.Should().Contain(t => t.Name == "Suspended Tenant");
        response.Tenants.Should().Contain(t => t.Status == TenantStatus.Suspended);
    }

    #endregion

    #region Get Tenant Tests

    [Fact]
    public async Task GetTenant_WithValidId_ShouldReturnTenant()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/tenants",
            new CreateTenantRequest("Get Test", null, TenantPlan.Professional), JsonOptions);
        var created = await createResponse.Content.ReadFromJsonAsync<TenantResponse>(JsonOptions);

        // Act
        var response = await _client.GetAsync($"/api/tenants/{created!.Id}");
        var tenant = await response.Content.ReadFromJsonAsync<TenantResponse>(JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        tenant!.Id.Should().Be(created.Id);
        tenant.Name.Should().Be("Get Test");
    }

    [Fact]
    public async Task GetTenant_WithInvalidId_ShouldReturnNotFound()
    {
        // Act
        var response = await _client.GetAsync($"/api/tenants/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Update Plan Tests

    [Fact]
    public async Task UpdatePlan_ShouldChangeTenantPlan()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/tenants",
            new CreateTenantRequest("Upgrade Me", null, TenantPlan.Free), JsonOptions);
        var tenant = await createResponse.Content.ReadFromJsonAsync<TenantResponse>(JsonOptions);

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/tenants/{tenant!.Id}/plan",
            new UpdateTenantPlanRequest(TenantPlan.Enterprise), JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<TenantResponse>(JsonOptions);
        updated!.Plan.Should().Be(TenantPlan.Enterprise);
    }

    #endregion

    #region Suspend Tenant Tests

    [Fact]
    public async Task SuspendTenant_ShouldMarkTenantAsSuspended()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/tenants",
            new CreateTenantRequest("Suspend Me", null, TenantPlan.Free), JsonOptions);
        var tenant = await createResponse.Content.ReadFromJsonAsync<TenantResponse>(JsonOptions);

        // Act
        var response = await _client.PostAsync($"/api/tenants/{tenant!.Id}/suspend", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it's suspended
        var getResponse = await _client.GetAsync($"/api/tenants/{tenant.Id}");
        var suspended = await getResponse.Content.ReadFromJsonAsync<TenantResponse>(JsonOptions);
        suspended!.Status.Should().Be(TenantStatus.Suspended);
    }

    #endregion

    #region API Key Tests

    [Fact]
    public async Task CreateApiKey_ShouldReturnKeyWithPlainText()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/tenants",
            new CreateTenantRequest("Key Test", null, TenantPlan.Free), JsonOptions);
        var tenant = await createResponse.Content.ReadFromJsonAsync<TenantResponse>(JsonOptions);

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/tenants/{tenant!.Id}/api-keys",
            new CreateApiKeyRequest("Production Key", ApiKeyScope.Admin, null), JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var key = await response.Content.ReadFromJsonAsync<ApiKeyCreatedResponse>(JsonOptions);
        key.Should().NotBeNull();
        key!.Name.Should().Be("Production Key");
        key.PlainKey.Should().StartWith("cb_");
        key.KeyPrefix.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ListApiKeys_ShouldReturnAllKeys()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/tenants",
            new CreateTenantRequest("Keys List Test", null, TenantPlan.Free), JsonOptions);
        var tenant = await createResponse.Content.ReadFromJsonAsync<TenantResponse>(JsonOptions);

        await _client.PostAsJsonAsync(
            $"/api/tenants/{tenant!.Id}/api-keys",
            new CreateApiKeyRequest("Key 1", ApiKeyScope.ReadOnly, null), JsonOptions);
        await _client.PostAsJsonAsync(
            $"/api/tenants/{tenant.Id}/api-keys",
            new CreateApiKeyRequest("Key 2", ApiKeyScope.Admin, null), JsonOptions);

        // Act
        var response = await _client.GetFromJsonAsync<ApiKeyListResponse>(
            $"/api/tenants/{tenant.Id}/api-keys?includeRevoked=false", JsonOptions);

        // Assert
        response.Should().NotBeNull();
        response!.ApiKeys.Should().HaveCount(2);
        response.ApiKeys.Should().Contain(k => k.Name == "Key 1");
        response.ApiKeys.Should().Contain(k => k.Name == "Key 2");
    }

    [Fact]
    public async Task RevokeApiKey_ShouldMarkKeyAsRevoked()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/tenants",
            new CreateTenantRequest("Revoke Test", null, TenantPlan.Free), JsonOptions);
        var tenant = await createResponse.Content.ReadFromJsonAsync<TenantResponse>(JsonOptions);

        var keyResponse = await _client.PostAsJsonAsync(
            $"/api/tenants/{tenant!.Id}/api-keys",
            new CreateApiKeyRequest("To Revoke", ApiKeyScope.Admin, null), JsonOptions);
        var key = await keyResponse.Content.ReadFromJsonAsync<ApiKeyCreatedResponse>(JsonOptions);

        // Act
        var response = await _client.DeleteAsync($"/api/tenants/{tenant.Id}/api-keys/{key!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it's revoked (not in default list)
        var listResponse = await _client.GetFromJsonAsync<ApiKeyListResponse>(
            $"/api/tenants/{tenant.Id}/api-keys?includeRevoked=false", JsonOptions);
        listResponse!.ApiKeys.Should().NotContain(k => k.Id == key.Id);
    }

    [Fact]
    public async Task RegenerateApiKey_ShouldReturnNewKey()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/tenants",
            new CreateTenantRequest("Regenerate Test", null, TenantPlan.Free), JsonOptions);
        var tenant = await createResponse.Content.ReadFromJsonAsync<TenantResponse>(JsonOptions);

        var keyResponse = await _client.PostAsJsonAsync(
            $"/api/tenants/{tenant!.Id}/api-keys",
            new CreateApiKeyRequest("To Regenerate", ApiKeyScope.Admin, null), JsonOptions);
        var originalKey = await keyResponse.Content.ReadFromJsonAsync<ApiKeyCreatedResponse>(JsonOptions);

        // Act
        var response = await _client.PostAsync(
            $"/api/tenants/{tenant.Id}/api-keys/{originalKey!.Id}/regenerate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var newKey = await response.Content.ReadFromJsonAsync<ApiKeyCreatedResponse>(JsonOptions);
        newKey!.PlainKey.Should().NotBe(originalKey.PlainKey);
        newKey.Name.Should().Be(originalKey.Name);
    }

    #endregion
}
