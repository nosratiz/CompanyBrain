using CompanyBrain.Api.Contracts;
using FluentValidation;

namespace CompanyBrain.Api.Validation;

internal sealed class UploadDocumentRequestValidator : AbstractValidator<UploadDocumentRequest>
{
    public UploadDocumentRequestValidator()
    {
        RuleFor(request => request.File)
            .NotNull()
            .WithMessage("A file upload is required under the 'file' form field.");

        RuleFor(request => request.File!.Length)
            .GreaterThan(0)
            .WithMessage("The uploaded file must not be empty.")
            .When(request => request.File is not null);

        RuleFor(request => request.Name)
            .MaximumLength(200)
            .When(request => !string.IsNullOrWhiteSpace(request.Name));
    }
}