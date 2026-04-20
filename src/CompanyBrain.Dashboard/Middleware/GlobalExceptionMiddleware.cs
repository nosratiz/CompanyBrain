using System.Net;
using System.Text.Json;
using FluentResults;
using Microsoft.AspNetCore.Mvc;

namespace CompanyBrain.Dashboard.Middleware;

/// <summary>
/// Global exception middleware for catching unhandled errors and logging them via Serilog.
/// Returns a consistent ProblemDetails response.
/// </summary>
public sealed class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        logger.LogError(
            exception,
            "Unhandled exception occurred. TraceId: {TraceId}, Path: {Path}, Method: {Method}",
            context.TraceIdentifier,
            context.Request.Path,
            context.Request.Method);

        var problemDetails = CreateProblemDetails(context, exception);
        
        context.Response.StatusCode = problemDetails.Status ?? (int)HttpStatusCode.InternalServerError;
        context.Response.ContentType = "application/problem+json";

        var json = JsonSerializer.Serialize(problemDetails, JsonSerializerOptions);
        await context.Response.WriteAsync(json);
    }

    private static ProblemDetails CreateProblemDetails(HttpContext context, Exception exception)
    {
        var statusCode = MapExceptionToStatusCode(exception);

        return new ProblemDetails
        {
            Status = statusCode,
            Title = GetTitle(statusCode),
            Detail = GetDetailMessage(statusCode),
            Instance = context.Request.Path,
            Extensions = { ["traceId"] = context.TraceIdentifier }
        };
    }

    private static int MapExceptionToStatusCode(Exception exception) => exception switch
    {
        ArgumentNullException => (int)HttpStatusCode.BadRequest,
        ArgumentException => (int)HttpStatusCode.BadRequest,
        UnauthorizedAccessException => (int)HttpStatusCode.Forbidden,
        KeyNotFoundException => (int)HttpStatusCode.NotFound,
        OperationCanceledException => 499,
        _ => (int)HttpStatusCode.InternalServerError
    };

    private static string GetTitle(int statusCode) => statusCode switch
    {
        400 => "Bad Request",
        403 => "Forbidden",
        404 => "Not Found",
        499 => "Client Closed Request",
        _ => "Internal Server Error"
    };

    private static string GetDetailMessage(int statusCode)
    {
        if (statusCode >= 500)
            return "An unexpected error occurred. Please try again later.";

        return "The request could not be processed.";
    }

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
}
