using CompanyBrain.Admin.Server.Api.Contracts.Admin;
using FluentValidation;

namespace CompanyBrain.Admin.Server.Api.Validation;

internal sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(request => request)
            .Must(request => !string.IsNullOrWhiteSpace(request.FullName) || !string.IsNullOrWhiteSpace(request.Email))
            .WithMessage("At least one field must be provided.");

        RuleFor(request => request.FullName)
            .MaximumLength(256)
            .When(request => request.FullName is not null);

        RuleFor(request => request.Email)
            .EmailAddress()
            .When(request => !string.IsNullOrWhiteSpace(request.Email));
    }
}