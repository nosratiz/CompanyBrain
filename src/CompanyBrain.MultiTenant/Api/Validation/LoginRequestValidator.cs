using CompanyBrain.MultiTenant.Api;
using FluentValidation;

namespace CompanyBrain.MultiTenant.Api.Validation;

internal sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(request => request.Password)
            .NotEmpty();
    }
}