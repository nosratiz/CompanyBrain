using CompanyBrain.Dashboard.Api.Contracts;
using FluentValidation;

namespace CompanyBrain.Dashboard.Api.Validation;

internal sealed class IngestWikiBatchRequestValidator : AbstractValidator<IngestWikiBatchRequest>
{
    public IngestWikiBatchRequestValidator()
    {
        RuleFor(request => request.Url)
            .NotEmpty()
            .Must(BeAbsoluteHttpUrl)
            .WithMessage("Url must be an absolute http or https URL.");

        RuleFor(request => request.LinkSelector)
            .MaximumLength(500)
            .When(request => request.LinkSelector is not null);
    }

    private static bool BeAbsoluteHttpUrl(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri)
           && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
