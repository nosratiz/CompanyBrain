using CompanyBrain.MultiTenant.Api.Contracts;
using FluentValidation;

namespace CompanyBrain.MultiTenant.Api.Validation;

internal sealed class CreateTenantRequestValidator : AbstractValidator<CreateTenantRequest>
{
    public CreateTenantRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(request => request.Description)
            .MaximumLength(1000)
            .When(request => request.Description is not null);

        RuleFor(request => request.Plan)
            .IsInEnum();
    }
}