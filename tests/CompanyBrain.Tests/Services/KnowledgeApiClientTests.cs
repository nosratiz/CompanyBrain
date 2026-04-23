using System.Net;
using System.Text.Json;
using CompanyBrain.Dashboard.Middleware;
using CompanyBrain.Dashboard.Services;
using CompanyBrain.Tests.TestHelpers;
using FluentAssertions;

namespace CompanyBrain.Tests.Services;

public sealed class KnowledgeApiClientTests
{
    private static KnowledgeApiClient CreateClient(string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = FakeHttpMessageHandler.ReturningJson(json, status);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        return new KnowledgeApiClient(http);
    }

    private static KnowledgeApiClient CreateClient(FakeHttpMessageHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        return new KnowledgeApiClient(http);
    }

    #region ListResourcesAsync Tests

    [Fact]
    public async Task ListResourcesAsync_WhenSuccess_ShouldReturnList()
    {
        var json = """[{"fileName":"test.md","mimeType":"text/markdown","uri":"resources/test.md"}]""";
        var sut = CreateClient(json);

        var result = await sut.ListResourcesAsync();

        result.Should().NotBeNull();
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task ListResourcesAsync_WhenServerError_ShouldReturnEmptyList()
    {
        var sut = CreateClient("", HttpStatusCode.InternalServerError);

        var result = await sut.ListResourcesAsync();

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ListResourcesAsync_WhenNetworkError_ShouldReturnEmptyList()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new HttpRequestException("network error"));
        var sut = CreateClient(handler);

        var result = await sut.ListResourcesAsync();

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ListResourcesAsync_WhenUnauthorized_ShouldRethrow()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new UnauthorizedApiException("401"));
        var sut = CreateClient(handler);

        var act = async () => await sut.ListResourcesAsync();

        await act.Should().ThrowAsync<UnauthorizedApiException>();
    }

    #endregion

    #region GetResourceAsync Tests

    [Fact]
    public async Task GetResourceAsync_WhenSuccess_ShouldReturnContent()
    {
        var json = """{"uri":"resources/test.md","mimeType":"text/markdown","text":"# Test"}""";
        var sut = CreateClient(json);

        var result = await sut.GetResourceAsync("test.md");

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetResourceAsync_WhenNotFound_ShouldReturnNull()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new HttpRequestException("not found"));
        var sut = CreateClient(handler);

        var result = await sut.GetResourceAsync("nonexistent.md");

        result.Should().BeNull();
    }

    #endregion

    #region SearchAsync Tests

    [Fact]
    public async Task SearchAsync_WhenSuccess_ShouldReturnResponse()
    {
        var json = """{"matches":[],"totalCount":0}""";
        var sut = CreateClient(json);

        var result = await sut.SearchAsync("test query");

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task SearchAsync_WithMaxResults_ShouldIncludeItInUrl()
    {
        string? capturedUrl = null;
        var handler = new FakeHttpMessageHandler(req =>
        {
            capturedUrl = req.RequestUri?.ToString();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"matches":[],"totalCount":0}""",
                    System.Text.Encoding.UTF8, "application/json")
            };
        });
        var sut = CreateClient(handler);

        await sut.SearchAsync("query", maxResults: 5);

        capturedUrl.Should().Contain("maxResults=5");
    }

    [Fact]
    public async Task SearchAsync_WhenError_ShouldReturnNull()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new HttpRequestException("error"));
        var sut = CreateClient(handler);

        var result = await sut.SearchAsync("query");

        result.Should().BeNull();
    }

    #endregion

    #region DeleteResourceAsync Tests

    [Fact]
    public async Task DeleteResourceAsync_WhenSuccess_ShouldReturnTrue()
    {
        var handler = FakeHttpMessageHandler.ReturningStatus(HttpStatusCode.NoContent);
        var sut = CreateClient(handler);

        var result = await sut.DeleteResourceAsync("test.md");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteResourceAsync_WhenNotFound_ShouldReturnFalse()
    {
        var handler = FakeHttpMessageHandler.ReturningStatus(HttpStatusCode.NotFound);
        var sut = CreateClient(handler);

        var result = await sut.DeleteResourceAsync("missing.md");

        result.Should().BeFalse();
    }

    #endregion

    #region ListTemplatesAsync Tests

    [Fact]
    public async Task ListTemplatesAsync_WhenSuccess_ShouldReturnList()
    {
        var json = """[{"name":"my-template","displayName":"My Template"}]""";
        var sut = CreateClient(json);

        var result = await sut.ListTemplatesAsync();

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ListTemplatesAsync_WhenError_ShouldReturnEmptyList()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new HttpRequestException("error"));
        var sut = CreateClient(handler);

        var result = await sut.ListTemplatesAsync();

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    #endregion

    #region DeleteTemplateAsync Tests

    [Fact]
    public async Task DeleteTemplateAsync_WhenSuccess_ShouldReturnTrue()
    {
        var handler = FakeHttpMessageHandler.ReturningStatus(HttpStatusCode.OK);
        var sut = CreateClient(handler);

        var result = await sut.DeleteTemplateAsync("my-template");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteTemplateAsync_WhenError_ShouldReturnFalse()
    {
        var handler = FakeHttpMessageHandler.ReturningStatus(HttpStatusCode.InternalServerError);
        var sut = CreateClient(handler);

        var result = await sut.DeleteTemplateAsync("my-template");

        result.Should().BeFalse();
    }

    #endregion
}
