using CompanyBrain.MultiTenant.Abstractions;
using CompanyBrain.MultiTenant.Data;
using CompanyBrain.MultiTenant.Domain;
using CompanyBrain.MultiTenant.Middleware;
using CompanyBrain.MultiTenant.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CompanyBrain.MultiTenant.Tests;

public sealed class ApiKeyAuthenticationMiddlewareTests : IAsyncDisposable
{
    private readonly TenantDbContext _dbContext;
    private readonly TenantService _tenantService;
    private readonly ApiKeyService _apiKeyService;
    private readonly ITenantContextAccessor _tenantContextAccessor;

    public ApiKeyAuthenticationMiddlewareTests()
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseInMemoryDatabase(databaseName: $"MiddlewareTests_{Guid.NewGuid():N}")
            .Options;

        _dbContext = new TenantDbContext(options);
        _tenantService = new TenantService(_dbContext, NullLogger<TenantService>.Instance);
        _apiKeyService = new ApiKeyService(_dbContext, NullLogger<ApiKeyService>.Instance);
        _tenantContextAccessor = Substitute.For<ITenantContextAccessor>();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    private async Task<(Tenant Tenant, string PlainKey)> SetupTenantWithKeyAsync()
    {
        var tenantResult = await _tenantService.CreateTenantAsync("Test Tenant");
        var tenant = tenantResult.Value;
        var keyResult = await _apiKeyService.CreateApiKeyAsync(tenant.Id, "Test Key");
        return (tenant, keyResult.Value.PlainKey);
    }

    #region API Key Extraction Tests

    [Fact]
    public async Task InvokeAsync_WithValidXApiKeyHeader_ShouldAuthenticate()
    {
        // Arrange
        var (tenant, plainKey) = await SetupTenantWithKeyAsync();
        var nextCalled = false;

        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };
        var middleware = new ApiKeyAuthenticationMiddleware(
            next,
            _tenantContextAccessor,
            NullLogger<ApiKeyAuthenticationMiddleware>.Instance);

        var context = CreateHttpContext();
        context.Request.Headers["X-API-Key"] = plainKey;

        // Act
        await middleware.InvokeAsync(context, _apiKeyService);

        // Assert
        nextCalled.Should().BeTrue();
        _tenantContextAccessor.Received(1).SetTenant(tenant.Id, tenant.Slug);
        context.Items["TenantId"].Should().Be(tenant.Id);
    }

    [Fact]
    public async Task InvokeAsync_WithValidBearerToken_ShouldAuthenticate()
    {
        // Arrange
        var (tenant, plainKey) = await SetupTenantWithKeyAsync();
        var nextCalled = false;

        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };
        var middleware = new ApiKeyAuthenticationMiddleware(
            next,
            _tenantContextAccessor,
            NullLogger<ApiKeyAuthenticationMiddleware>.Instance);

        var context = CreateHttpContext();
        context.Request.Headers["Authorization"] = $"Bearer {plainKey}";

        // Act
        await middleware.InvokeAsync(context, _apiKeyService);

        // Assert
        nextCalled.Should().BeTrue();
        _tenantContextAccessor.Received(1).SetTenant(tenant.Id, tenant.Slug);
    }

    [Fact]
    public async Task InvokeAsync_WithNoApiKey_ShouldPassThrough()
    {
        // Arrange - No setup needed, no key provided
        var nextCalled = false;

        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };
        var middleware = new ApiKeyAuthenticationMiddleware(
            next,
            _tenantContextAccessor,
            NullLogger<ApiKeyAuthenticationMiddleware>.Instance);

        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context, _apiKeyService);

        // Assert - Should pass through without setting tenant
        nextCalled.Should().BeTrue();
        _tenantContextAccessor.DidNotReceive().SetTenant(Arg.Any<Guid>(), Arg.Any<string>());
    }

    #endregion

    #region Invalid Key Tests

    [Fact]
    public async Task InvokeAsync_WithInvalidApiKey_ShouldReturn401()
    {
        // Arrange
        RequestDelegate next = _ => Task.CompletedTask;
        var middleware = new ApiKeyAuthenticationMiddleware(
            next,
            _tenantContextAccessor,
            NullLogger<ApiKeyAuthenticationMiddleware>.Instance);

        var context = CreateHttpContext();
        context.Request.Headers["X-API-Key"] = "cb_invalid_key_here";

        // Act
        await middleware.InvokeAsync(context, _apiKeyService);

        // Assert
        context.Response.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task InvokeAsync_WithRevokedKey_ShouldReturn401()
    {
        // Arrange
        var (tenant, plainKey) = await SetupTenantWithKeyAsync();
        var keys = await _apiKeyService.ListApiKeysAsync(tenant.Id);
        await _apiKeyService.RevokeApiKeyAsync(tenant.Id, keys[0].Id);

        RequestDelegate next = _ => Task.CompletedTask;
        var middleware = new ApiKeyAuthenticationMiddleware(
            next,
            _tenantContextAccessor,
            NullLogger<ApiKeyAuthenticationMiddleware>.Instance);

        var context = CreateHttpContext();
        context.Request.Headers["X-API-Key"] = plainKey;

        // Act
        await middleware.InvokeAsync(context, _apiKeyService);

        // Assert
        context.Response.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task InvokeAsync_WithSuspendedTenant_ShouldReturn401()
    {
        // Arrange
        var (tenant, plainKey) = await SetupTenantWithKeyAsync();
        await _tenantService.SuspendTenantAsync(tenant.Id);

        RequestDelegate next = _ => Task.CompletedTask;
        var middleware = new ApiKeyAuthenticationMiddleware(
            next,
            _tenantContextAccessor,
            NullLogger<ApiKeyAuthenticationMiddleware>.Instance);

        var context = CreateHttpContext();
        context.Request.Headers["X-API-Key"] = plainKey;

        // Act
        await middleware.InvokeAsync(context, _apiKeyService);

        // Assert
        context.Response.StatusCode.Should().Be(401);
    }

    #endregion

    #region Scope Tests

    [Theory]
    [InlineData(ApiKeyScope.ReadOnly)]
    [InlineData(ApiKeyScope.WriteDocuments)]
    [InlineData(ApiKeyScope.ManageResources)]
    [InlineData(ApiKeyScope.Admin)]
    public async Task InvokeAsync_ShouldSetApiKeyScope(ApiKeyScope scope)
    {
        // Arrange
        var tenantResult = await _tenantService.CreateTenantAsync("Scope Test Tenant");
        var keyResult = await _apiKeyService.CreateApiKeyAsync(
            tenantResult.Value.Id, "Scoped Key", scope);

        RequestDelegate next = _ => Task.CompletedTask;
        var middleware = new ApiKeyAuthenticationMiddleware(
            next,
            _tenantContextAccessor,
            NullLogger<ApiKeyAuthenticationMiddleware>.Instance);

        var context = CreateHttpContext();
        context.Request.Headers["X-API-Key"] = keyResult.Value.PlainKey;

        // Act
        await middleware.InvokeAsync(context, _apiKeyService);

        // Assert
        context.Items["ApiKeyScope"].Should().Be(scope);
    }

    #endregion

    private static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }
}
