using System.Net;
using CompanyBrain.Dashboard.Features.Auth.Interfaces;
using CompanyBrain.Dashboard.Features.Auth.Services;
using CompanyBrain.Dashboard.Middleware;
using CompanyBrain.Dashboard.Services;
using CompanyBrain.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CompanyBrain.Tests.Services;

public sealed class ExternalTenantApiClientTests
{
    private readonly ILogger<ExternalTenantApiClient> _logger;
    private readonly AuthTokenStore _tokenStore;

    public ExternalTenantApiClientTests()
    {
        _logger = NullLogger<ExternalTenantApiClient>.Instance;
        var storage = Substitute.For<IAuthSessionStorage>();
        _tokenStore = new AuthTokenStore(storage);
    }

    private ExternalTenantApiClient CreateClient(FakeHttpMessageHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        return new ExternalTenantApiClient(http, _tokenStore, _logger);
    }

    #region GetTenantsAsync Tests

    [Fact]
    public async Task GetTenantsAsync_WhenSuccess_ShouldReturnTenants()
    {
        var json = """{"tenants":[{"tenantId":"00000000-0000-0000-0000-000000000001","name":"Acme","status":1,"plan":2}]}""";
        var sut = CreateClient(FakeHttpMessageHandler.ReturningJson(json));

        var result = await sut.GetTenantsAsync();

        result.Should().NotBeNull();
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetTenantsAsync_WhenHttpError_ShouldReturnEmptyList()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new HttpRequestException("network error"));
        var sut = CreateClient(handler);

        var result = await sut.GetTenantsAsync();

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTenantsAsync_WhenGenericException_ShouldReturnEmptyList()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("unexpected"));
        var sut = CreateClient(handler);

        var result = await sut.GetTenantsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTenantsAsync_WhenUnauthorized_ShouldRethrow()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new UnauthorizedApiException("401"));
        var sut = CreateClient(handler);

        var act = async () => await sut.GetTenantsAsync();

        await act.Should().ThrowAsync<UnauthorizedApiException>();
    }

    [Fact]
    public async Task GetTenantsAsync_WhenTokenIsSet_ShouldIncludeAuthorizationHeader()
    {
        _tokenStore.SetOwnerSession("my-token", "User", "user@test.com", "Admin");

        string? capturedAuth = null;
        var handler = new FakeHttpMessageHandler(req =>
        {
            capturedAuth = req.Headers.Authorization?.ToString();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"tenants":[]}""", System.Text.Encoding.UTF8, "application/json")
            };
        });
        var sut = CreateClient(handler);

        await sut.GetTenantsAsync();

        capturedAuth.Should().Contain("Bearer my-token");
    }

    [Fact]
    public async Task GetTenantsAsync_WhenNullResponse_ShouldReturnEmptyList()
    {
        var handler = FakeHttpMessageHandler.ReturningJson("null");
        var sut = CreateClient(handler);

        var result = await sut.GetTenantsAsync();

        result.Should().BeEmpty();
    }

    #endregion

    #region GetTenantByIdAsync Tests

    [Fact]
    public async Task GetTenantByIdAsync_WhenSuccess_ShouldReturnTenant()
    {
        var tenantId = Guid.NewGuid();
        var json = $$"""{ "tenantId":"{{tenantId}}","name":"Acme","status":1,"plan":2 }""";
        var sut = CreateClient(FakeHttpMessageHandler.ReturningJson(json));

        var result = await sut.GetTenantByIdAsync(tenantId);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetTenantByIdAsync_WhenNotFound_ShouldReturnNull()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            throw new HttpRequestException("not found", null, HttpStatusCode.NotFound));
        var sut = CreateClient(handler);

        var result = await sut.GetTenantByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetTenantByIdAsync_WhenGenericException_ShouldReturnNull()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("error"));
        var sut = CreateClient(handler);

        var result = await sut.GetTenantByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    #endregion
}
