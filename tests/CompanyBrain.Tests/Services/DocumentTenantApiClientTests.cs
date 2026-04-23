using System.Net;
using CompanyBrain.Dashboard.Features.DocumentTenant.Requests;
using CompanyBrain.Dashboard.Middleware;
using CompanyBrain.Dashboard.Services;
using CompanyBrain.Tests.TestHelpers;
using FluentAssertions;

namespace CompanyBrain.Tests.Services;

public sealed class DocumentTenantApiClientTests
{
    private static DocumentTenantApiClient CreateClient(string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = FakeHttpMessageHandler.ReturningJson(json, status);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        return new DocumentTenantApiClient(http);
    }

    private static DocumentTenantApiClient CreateClient(FakeHttpMessageHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        return new DocumentTenantApiClient(http);
    }

    #region GetAssignmentsByDocumentAsync Tests

    [Fact]
    public async Task GetAssignmentsByDocumentAsync_WhenSuccess_ShouldReturnResponse()
    {
        var json = """{"fileName":"doc.pdf","assignments":[]}""";
        var sut = CreateClient(json);

        var result = await sut.GetAssignmentsByDocumentAsync("doc.pdf");

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAssignmentsByDocumentAsync_WhenError_ShouldReturnNull()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new HttpRequestException("error"));
        var sut = CreateClient(handler);

        var result = await sut.GetAssignmentsByDocumentAsync("doc.pdf");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAssignmentsByDocumentAsync_WhenUnauthorized_ShouldRethrow()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new UnauthorizedApiException("401"));
        var sut = CreateClient(handler);

        var act = async () => await sut.GetAssignmentsByDocumentAsync("doc.pdf");

        await act.Should().ThrowAsync<UnauthorizedApiException>();
    }

    #endregion

    #region UpdateDocumentTenantsAsync Tests

    [Fact]
    public async Task UpdateDocumentTenantsAsync_WhenSuccess_ShouldReturnResponse()
    {
        var json = """{"fileName":"doc.pdf","assignments":[]}""";
        var handler = FakeHttpMessageHandler.ReturningJson(json);
        var sut = CreateClient(handler);

        var tenants = new List<TenantAssignment> { new(Guid.NewGuid(), "Tenant A") };
        var result = await sut.UpdateDocumentTenantsAsync("doc.pdf", tenants);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateDocumentTenantsAsync_WhenError_ShouldReturnNull()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new Exception("error"));
        var sut = CreateClient(handler);

        var result = await sut.UpdateDocumentTenantsAsync("doc.pdf", []);

        result.Should().BeNull();
    }

    #endregion

    #region AssignDocumentToTenantAsync Tests

    [Fact]
    public async Task AssignDocumentToTenantAsync_WhenSuccess_ShouldReturnResponse()
    {
        var json = """{"id":1,"fileName":"doc.pdf","tenantId":"00000000-0000-0000-0000-000000000001","tenantName":"Acme Corp"}""";
        var handler = FakeHttpMessageHandler.ReturningJson(json, HttpStatusCode.Created);
        var sut = CreateClient(handler);

        var result = await sut.AssignDocumentToTenantAsync("doc.pdf", Guid.NewGuid(), "Acme Corp");

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task AssignDocumentToTenantAsync_WhenError_ShouldReturnNull()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new Exception("error"));
        var sut = CreateClient(handler);

        var result = await sut.AssignDocumentToTenantAsync("doc.pdf", Guid.NewGuid(), "Acme Corp");

        result.Should().BeNull();
    }

    #endregion

    #region RemoveAssignmentAsync Tests

    [Fact]
    public async Task RemoveAssignmentAsync_WhenSuccess_ShouldReturnTrue()
    {
        var handler = FakeHttpMessageHandler.ReturningStatus(HttpStatusCode.NoContent);
        var sut = CreateClient(handler);

        var result = await sut.RemoveAssignmentAsync("doc.pdf", Guid.NewGuid());

        result.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveAssignmentAsync_WhenNotFound_ShouldReturnFalse()
    {
        var handler = FakeHttpMessageHandler.ReturningStatus(HttpStatusCode.NotFound);
        var sut = CreateClient(handler);

        var result = await sut.RemoveAssignmentAsync("doc.pdf", Guid.NewGuid());

        result.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveAssignmentAsync_WhenException_ShouldReturnFalse()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new Exception("error"));
        var sut = CreateClient(handler);

        var result = await sut.RemoveAssignmentAsync("doc.pdf", Guid.NewGuid());

        result.Should().BeFalse();
    }

    #endregion

    #region GetAvailableTenantsAsync Tests

    [Fact]
    public async Task GetAvailableTenantsAsync_WhenSuccess_ShouldReturnList()
    {
        var json = """[{"tenantId":"00000000-0000-0000-0000-000000000001","name":"Acme Corp","status":1}]""";
        var sut = CreateClient(json);

        var result = await sut.GetAvailableTenantsAsync();

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAvailableTenantsAsync_WhenError_ShouldReturnEmptyList()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new Exception("error"));
        var sut = CreateClient(handler);

        var result = await sut.GetAvailableTenantsAsync();

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    #endregion
}
