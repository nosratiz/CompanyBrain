using System.Text.Json;
using CompanyBrain.Dashboard.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CompanyBrain.Tests.Middleware;

public sealed class GlobalExceptionMiddlewareTests
{
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddlewareTests()
    {
        _logger = Substitute.For<ILogger<GlobalExceptionMiddleware>>();
    }

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<string> ReadResponseBodyAsync(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync();
    }

    #region Successful Pipeline Tests

    [Fact]
    public async Task InvokeAsync_WhenNextSucceeds_ShouldNotWriteErrorResponse()
    {
        var context = CreateContext();
        var nextCalled = false;

        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new GlobalExceptionMiddleware(next, _logger);
        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(200);
    }

    #endregion

    #region Exception Mapping Tests

    [Fact]
    public async Task InvokeAsync_WhenArgumentNullExceptionThrown_ShouldReturn400()
    {
        var context = CreateContext();
        RequestDelegate next = _ => throw new ArgumentNullException("param");

        var middleware = new GlobalExceptionMiddleware(next, _logger);
        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(400);
        context.Response.ContentType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task InvokeAsync_WhenArgumentExceptionThrown_ShouldReturn400()
    {
        var context = CreateContext();
        RequestDelegate next = _ => throw new ArgumentException("bad arg");

        var middleware = new GlobalExceptionMiddleware(next, _logger);
        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task InvokeAsync_WhenUnauthorizedAccessExceptionThrown_ShouldReturn403()
    {
        var context = CreateContext();
        RequestDelegate next = _ => throw new UnauthorizedAccessException();

        var middleware = new GlobalExceptionMiddleware(next, _logger);
        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task InvokeAsync_WhenKeyNotFoundExceptionThrown_ShouldReturn404()
    {
        var context = CreateContext();
        RequestDelegate next = _ => throw new KeyNotFoundException("not found");

        var middleware = new GlobalExceptionMiddleware(next, _logger);
        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task InvokeAsync_WhenOperationCanceledExceptionThrown_ShouldReturn499()
    {
        var context = CreateContext();
        RequestDelegate next = _ => throw new OperationCanceledException();

        var middleware = new GlobalExceptionMiddleware(next, _logger);
        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(499);
    }

    [Fact]
    public async Task InvokeAsync_WhenUnhandledExceptionThrown_ShouldReturn500()
    {
        var context = CreateContext();
        RequestDelegate next = _ => throw new InvalidOperationException("unexpected");

        var middleware = new GlobalExceptionMiddleware(next, _logger);
        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(500);
    }

    #endregion

    #region Response Body Tests

    [Fact]
    public async Task InvokeAsync_WhenExceptionThrown_ShouldWriteValidJson()
    {
        var context = CreateContext();
        RequestDelegate next = _ => throw new KeyNotFoundException("resource not found");

        var middleware = new GlobalExceptionMiddleware(next, _logger);
        await middleware.InvokeAsync(context);

        var body = await ReadResponseBodyAsync(context);
        body.Should().NotBeEmpty();

        var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("status").GetInt32().Should().Be(404);
        doc.RootElement.GetProperty("title").GetString().Should().Be("Not Found");
    }

    [Fact]
    public async Task InvokeAsync_WhenExceptionThrown_ShouldWriteProblemJson()
    {
        var context = CreateContext();
        context.TraceIdentifier = "test-trace-123";
        context.Request.Path = "/api/items/1";
        RequestDelegate next = _ => throw new InvalidOperationException("oops");

        var middleware = new GlobalExceptionMiddleware(next, _logger);
        await middleware.InvokeAsync(context);

        var body = await ReadResponseBodyAsync(context);
        var doc = JsonDocument.Parse(body);

        doc.RootElement.TryGetProperty("status", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("title", out _).Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WhenServerError_ShouldReturnGenericMessage()
    {
        var context = CreateContext();
        RequestDelegate next = _ => throw new InvalidOperationException("internal details");

        var middleware = new GlobalExceptionMiddleware(next, _logger);
        await middleware.InvokeAsync(context);

        var body = await ReadResponseBodyAsync(context);
        body.Should().Contain("unexpected error");
        body.Should().NotContain("internal details");
    }

    [Fact]
    public async Task InvokeAsync_WhenClientError_ShouldReturnRequestMessage()
    {
        var context = CreateContext();
        RequestDelegate next = _ => throw new ArgumentException("bad request");

        var middleware = new GlobalExceptionMiddleware(next, _logger);
        await middleware.InvokeAsync(context);

        var body = await ReadResponseBodyAsync(context);
        body.Should().Contain("could not be processed");
    }

    #endregion
}
