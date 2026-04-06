using CompanyBrain.MultiTenant.Api.Contracts;
using FluentValidation;

namespace CompanyBrain.MultiTenant.Api.Validation;

internal sealed class CreateApiKeyRequestValidator : AbstractValidator<CreateApiKeyRequest>
{
    public CreateApiKeyRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(request => request.Scope)
            .IsInEnum();

        RuleFor(request => request.ExpiresAt)
            .Must(expiresAt => !expiresAt.HasValue || expiresAt.Value > DateTime.UtcNow)
            .WithMessage("Expiry date must be in the future.");
    }
}