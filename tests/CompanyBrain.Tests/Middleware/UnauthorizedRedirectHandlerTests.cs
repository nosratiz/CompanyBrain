using System.Net;
using CompanyBrain.Dashboard.Middleware;
using CompanyBrain.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CompanyBrain.Tests.Middleware;

public sealed class UnauthorizedRedirectHandlerTests
{
    private readonly ILogger<UnauthorizedRedirectHandler> _logger;

    public UnauthorizedRedirectHandlerTests()
    {
        _logger = Substitute.For<ILogger<UnauthorizedRedirectHandler>>();
    }

    private HttpClient CreateClient(HttpStatusCode responseStatus)
    {
        var innerHandler = FakeHttpMessageHandler.ReturningStatus(responseStatus);
        var handler = new UnauthorizedRedirectHandler(_logger)
        {
            InnerHandler = innerHandler
        };
        return new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
    }

    #region Pass-Through Tests

    [Fact]
    public async Task SendAsync_WhenResponseIs200_ShouldReturnResponse()
    {
        using var client = CreateClient(HttpStatusCode.OK);

        var response = await client.GetAsync("/api/test");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(HttpStatusCode.Created)]
    [InlineData(HttpStatusCode.NoContent)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task SendAsync_WhenResponseIsNot401_ShouldReturnResponseWithoutThrowing(HttpStatusCode status)
    {
        using var client = CreateClient(status);

        var response = await client.GetAsync("/api/test");

        response.StatusCode.Should().Be(status);
    }

    #endregion

    #region Unauthorized Tests

    [Fact]
    public async Task SendAsync_WhenResponseIs401_ShouldThrowUnauthorizedApiException()
    {
        using var client = CreateClient(HttpStatusCode.Unauthorized);

        var act = async () => await client.GetAsync("/api/test");

        await act.Should().ThrowAsync<UnauthorizedApiException>()
            .WithMessage("*401*");
    }

    [Fact]
    public async Task SendAsync_WhenResponseIs401_ShouldLogWarning()
    {
        using var client = CreateClient(HttpStatusCode.Unauthorized);

        try { await client.GetAsync("/api/test"); } catch { }

        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    #endregion
}
