using CompanyBrain.Api.Contracts;
using FluentValidation;

namespace CompanyBrain.Api.Validation;

internal sealed class SearchRequestValidator : AbstractValidator<SearchRequest>
{
    public SearchRequestValidator()
    {
        RuleFor(request => request.Query)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(request => request.MaxResults)
            .InclusiveBetween(1, 20)
            .When(request => request.MaxResults.HasValue);
    }
}