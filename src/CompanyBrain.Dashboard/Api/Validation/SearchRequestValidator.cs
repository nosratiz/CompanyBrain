using CompanyBrain.Dashboard.Api.Contracts;
using FluentValidation;

namespace CompanyBrain.Dashboard.Api.Validation;

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

        RuleFor(request => request.CollectionId)
            .MaximumLength(80)
            .When(request => !string.IsNullOrWhiteSpace(request.CollectionId));
    }
}
