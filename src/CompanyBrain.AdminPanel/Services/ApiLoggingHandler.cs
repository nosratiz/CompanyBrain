using Microsoft.Extensions.Logging;

namespace CompanyBrain.AdminPanel.Services;

public sealed class ApiLoggingHandler(ILogger<ApiLoggingHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var requestUri = request.RequestUri?.ToString() ?? "<unknown>";
        logger.LogInformation("Sending HTTP {Method} {RequestUri}", request.Method, requestUri);

        try
        {
            var response = await base.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation(
                    "Received HTTP {StatusCode} for {Method} {RequestUri}",
                    (int)response.StatusCode,
                    request.Method,
                    requestUri);
            }
            else
            {
                logger.LogWarning(
                    "Received HTTP {StatusCode} for {Method} {RequestUri}",
                    (int)response.StatusCode,
                    request.Method,
                    requestUri);
            }

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "HTTP request failed for {Method} {RequestUri}", request.Method, requestUri);
            throw;
        }
    }
}