using CompanyBrain.UserPortal.Server.Api.Contracts.Auth;
using FluentValidation;

namespace CompanyBrain.UserPortal.Server.Api.Validation;

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