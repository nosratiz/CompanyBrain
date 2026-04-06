using CompanyBrain.MultiTenant.Api.Contracts;
using FluentValidation;

namespace CompanyBrain.MultiTenant.Api.Validation;

internal sealed class UpdateTenantPlanRequestValidator : AbstractValidator<UpdateTenantPlanRequest>
{
    public UpdateTenantPlanRequestValidator()
    {
        RuleFor(request => request.Plan)
            .IsInEnum();
    }
}