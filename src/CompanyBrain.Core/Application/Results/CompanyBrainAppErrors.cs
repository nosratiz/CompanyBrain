using FluentResults;

namespace CompanyBrain.Application.Results;

internal abstract class CompanyBrainAppError : Error
{
    protected CompanyBrainAppError(string message, int statusCode, string code)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }

    public int StatusCode { get; }

    public string Code { get; }
}

internal sealed class ValidationAppError(string message)
    : CompanyBrainAppError(message, 400, "validation");

internal sealed class NotFoundAppError(string message)
    : CompanyBrainAppError(message, 404, "not_found");

internal sealed class UpstreamAppError(string message)
    : CompanyBrainAppError(message, 502, "upstream_failed");