namespace CompanyBrain.Dashboard.Middleware;

/// <summary>
/// Security headers middleware that adds OWASP-compliant security headers to all responses.
/// Adds Content-Security-Policy, X-Content-Type-Options, and Strict-Transport-Security headers.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        AddSecurityHeaders(context.Response.Headers);
        await _next(context);
    }

    private static void AddSecurityHeaders(IHeaderDictionary headers)
    {
        // Prevent MIME type sniffing
        headers.TryAdd("X-Content-Type-Options", "nosniff");

        // Prevent clickjacking
        headers.TryAdd("X-Frame-Options", "DENY");

        // Enable XSS filter in browsers
        headers.TryAdd("X-XSS-Protection", "1; mode=block");

        // Referrer policy
        headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");

        // Permissions policy (formerly Feature-Policy)
        headers.TryAdd("Permissions-Policy", "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()");

        // Content Security Policy
        headers.TryAdd("Content-Security-Policy", BuildContentSecurityPolicy());

        // HTTP Strict Transport Security (only applied over HTTPS)
        headers.TryAdd("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
    }

    private static string BuildContentSecurityPolicy()
    {
        return string.Join("; ",
            "default-src 'self'",
            "script-src 'self' 'unsafe-inline' 'unsafe-eval'", // Required for Blazor
            "style-src 'self' 'unsafe-inline'", // Required for MudBlazor
            "img-src 'self' data: https:",
            "font-src 'self' data:",
            "connect-src 'self' ws: wss:", // Required for Blazor SignalR
            "frame-ancestors 'none'",
            "form-action 'self'",
            "base-uri 'self'"
        );
    }
}

/// <summary>
/// Extension methods for <see cref="SecurityHeadersMiddleware"/>.
/// </summary>
public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
