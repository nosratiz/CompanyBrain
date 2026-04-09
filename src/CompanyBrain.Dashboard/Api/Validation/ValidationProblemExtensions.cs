using FluentValidation.Results;

namespace CompanyBrain.Dashboard.Api.Validation;

internal static class ValidationProblemExtensions
{
    public static IResult ToValidationProblem(this ValidationResult validationResult)
    {
        var errors = validationResult.Errors
            .GroupBy(failure => string.IsNullOrWhiteSpace(failure.PropertyName) ? "request" : failure.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).Distinct().ToArray());

        return TypedResults.ValidationProblem(errors);
    }
}
