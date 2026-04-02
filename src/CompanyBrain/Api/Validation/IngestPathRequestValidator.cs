using CompanyBrain.Api.Contracts;
using FluentValidation;

namespace CompanyBrain.Api.Validation;

internal sealed class IngestPathRequestValidator : AbstractValidator<IngestPathRequest>
{
    public IngestPathRequestValidator()
    {
        RuleFor(request => request.LocalPath)
            .NotEmpty()
            .MaximumLength(1024);

        RuleFor(request => request.Name)
            .MaximumLength(200)
            .When(request => !string.IsNullOrWhiteSpace(request.Name));
    }
}