using CompanyBrain.Admin.Server.Api.Contracts.Auth;
using FluentValidation;

namespace CompanyBrain.Admin.Server.Api.Validation;

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