using CompanyBrain.MultiTenant.Api;
using FluentValidation;

namespace CompanyBrain.MultiTenant.Api.Validation;

internal sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(request => request.CurrentPassword)
            .NotEmpty();

        RuleFor(request => request.NewPassword)
            .NotEmpty()
            .MinimumLength(8)
            .NotEqual(request => request.CurrentPassword);
    }
}