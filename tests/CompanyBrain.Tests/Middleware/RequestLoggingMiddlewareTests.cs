using CompanyBrain.Dashboard.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CompanyBrain.Tests.Middleware;

public sealed class RequestLoggingMiddlewareTests
{
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddlewareTests()
    {
        _logger = Substitute.For<ILogger<RequestLoggingMiddleware>>();
    }

    private static DefaultHttpContext CreateContext(string method = "GET", string path = "/api/test")
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        return context;
    }

    #region Pipeline Tests

    [Fact]
    public async Task InvokeAsync_ShouldCallNext()
    {
        var context = CreateContext();
        var nextCalled = false;

        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new RequestLoggingMiddleware(next, _logger);
        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_ShouldLogAfterNextCompletes()
    {
        var context = CreateContext();
        var loggedAfterNext = false;
        var nextCompleted = false;

        RequestDelegate next = _ =>
        {
            nextCompleted = true;
            return Task.CompletedTask;
        };

        _logger.When(l => l.Log(
                Arg.Any<LogLevel>(),
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>()))
            .Do(_ => loggedAfterNext = nextCompleted);

        var middleware = new RequestLoggingMiddleware(next, _logger);
        await middleware.InvokeAsync(context);

        loggedAfterNext.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WhenNextThrows_ShouldStillLog()
    {
        var context = CreateContext();
        RequestDelegate next = _ => throw new InvalidOperationException("test error");

        var middleware = new RequestLoggingMiddleware(next, _logger);
        var act = async () => await middleware.InvokeAsync(context);

        await act.Should().ThrowAsync<InvalidOperationException>();

        _logger.ReceivedWithAnyArgs(1).Log<object>(default, default, default!, default, default!);
    }

    #endregion

    #region Log Level Tests

    [Fact]
    public async Task InvokeAsync_WhenStatusCode200_ShouldLogInformation()
    {
        var context = CreateContext();
        context.Response.StatusCode = 200;

        RequestDelegate next = _ => Task.CompletedTask;

        var middleware = new RequestLoggingMiddleware(next, _logger);
        await middleware.InvokeAsync(context);

        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task InvokeAsync_WhenStatusCode400_ShouldLogWarning()
    {
        var context = CreateContext();
        RequestDelegate next = ctx =>
        {
            ctx.Response.StatusCode = 400;
            return Task.CompletedTask;
        };

        var middleware = new RequestLoggingMiddleware(next, _logger);
        await middleware.InvokeAsync(context);

        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task InvokeAsync_WhenStatusCode500_ShouldLogError()
    {
        var context = CreateContext();
        RequestDelegate next = ctx =>
        {
            ctx.Response.StatusCode = 500;
            return Task.CompletedTask;
        };

        var middleware = new RequestLoggingMiddleware(next, _logger);
        await middleware.InvokeAsync(context);

        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WhenNextIsNull_ShouldThrow()
    {
        var act = () => new RequestLoggingMiddleware(null!, _logger);

        act.Should().Throw<ArgumentNullException>().WithParameterName("next");
    }

    [Fact]
    public void Constructor_WhenLoggerIsNull_ShouldThrow()
    {
        var act = () => new RequestLoggingMiddleware(_ => Task.CompletedTask, null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    #endregion
}
