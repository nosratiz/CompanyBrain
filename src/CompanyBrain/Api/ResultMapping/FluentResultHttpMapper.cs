using CompanyBrain.Application.Results;
using FluentResults;

namespace CompanyBrain.Api.ResultMapping;

internal static class FluentResultHttpMapper
{
    public static IResult ToProblemResult(this ResultBase result)
    {
        var primaryError = result.Errors.OfType<CompanyBrainAppError>().FirstOrDefault();
        var statusCode = primaryError?.StatusCode ?? StatusCodes.Status400BadRequest;
        var title = statusCode switch
        {
            StatusCodes.Status404NotFound => "Resource not found",
            StatusCodes.Status502BadGateway => "Upstream request failed",
            _ => "Request failed",
        };

        var detail = string.Join(Environment.NewLine, result.Errors.Select(error => error.Message).Distinct());
        return TypedResults.Problem(title: title, detail: detail, statusCode: statusCode);
    }
}