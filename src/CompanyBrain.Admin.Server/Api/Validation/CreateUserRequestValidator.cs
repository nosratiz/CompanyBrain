using CompanyBrain.Admin.Server.Api.Contracts.Admin;
using FluentValidation;

namespace CompanyBrain.Admin.Server.Api.Validation;

internal sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(request => request.Password)
            .NotEmpty()
            .MinimumLength(8);

        RuleFor(request => request.FullName)
            .NotEmpty()
            .MaximumLength(256);
    }
}