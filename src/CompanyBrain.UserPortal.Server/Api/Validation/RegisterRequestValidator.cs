using CompanyBrain.UserPortal.Server.Api.Contracts.Auth;
using FluentValidation;

namespace CompanyBrain.UserPortal.Server.Api.Validation;

internal sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(request => request.Password)
            .NotEmpty()
            .MinimumLength(8);

        RuleFor(request => request.FullName)
            .NotEmpty();
    }
}