using System.Net;

namespace CompanyBrain.Tests.TestHelpers;

/// <summary>
/// A fake HttpMessageHandler for unit testing HTTP-based services without real network calls.
/// </summary>
public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    public FakeHttpMessageHandler(HttpResponseMessage response)
        : this(_ => response)
    {
    }

    public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handler = handler;
    }

    public static FakeHttpMessageHandler ReturningJson(string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        return new FakeHttpMessageHandler(new HttpResponseMessage(status)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        });
    }

    public static FakeHttpMessageHandler ReturningStatus(HttpStatusCode status)
    {
        return new FakeHttpMessageHandler(new HttpResponseMessage(status));
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(_handler(request));
    }
}
