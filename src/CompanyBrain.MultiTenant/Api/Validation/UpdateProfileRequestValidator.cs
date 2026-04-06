using CompanyBrain.MultiTenant.Api;
using FluentValidation;

namespace CompanyBrain.MultiTenant.Api.Validation;

internal sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(request => request.DisplayName)
            .NotEmpty()
            .MaximumLength(200);
    }
}