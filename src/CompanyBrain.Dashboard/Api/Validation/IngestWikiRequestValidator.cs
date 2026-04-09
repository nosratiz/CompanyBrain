using CompanyBrain.Dashboard.Api.Contracts;
using FluentValidation;

namespace CompanyBrain.Dashboard.Api.Validation;

internal sealed class IngestWikiRequestValidator : AbstractValidator<IngestWikiRequest>
{
    public IngestWikiRequestValidator()
    {
        RuleFor(request => request.Url)
            .NotEmpty()
            .Must(BeAbsoluteHttpUrl)
            .WithMessage("Url must be an absolute http or https URL.");

        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(200);
    }

    private static bool BeAbsoluteHttpUrl(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri)
           && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
