using System.Diagnostics;

namespace CompanyBrain.Dashboard.Middleware;

/// <summary>
/// Request logging middleware that logs execution time and status codes for every request.
/// Uses structured logging with Serilog.
/// </summary>
public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestPath = context.Request.Path;
        var requestMethod = context.Request.Method;

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            LogRequest(context, requestPath, requestMethod, stopwatch.ElapsedMilliseconds);
        }
    }

    private void LogRequest(HttpContext context, string path, string method, long elapsedMs)
    {
        var statusCode = context.Response.StatusCode;
        var logLevel = GetLogLevel(statusCode);

        _logger.Log(
            logLevel,
            "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms",
            method,
            path,
            statusCode,
            elapsedMs);
    }

    private static LogLevel GetLogLevel(int statusCode) => statusCode switch
    {
        >= 500 => LogLevel.Error,
        >= 400 => LogLevel.Warning,
        _ => LogLevel.Information
    };
}

/// <summary>
/// Extension methods for <see cref="RequestLoggingMiddleware"/>.
/// </summary>
public static class RequestLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<RequestLoggingMiddleware>();
    }
}
