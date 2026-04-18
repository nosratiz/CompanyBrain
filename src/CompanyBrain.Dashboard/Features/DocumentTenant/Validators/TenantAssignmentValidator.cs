using CompanyBrain.Dashboard.Features.DocumentTenant.Requests;
using FluentValidation;

namespace CompanyBrain.Dashboard.Features.DocumentTenant.Validators;

/// <summary>
/// Validator for <see cref="TenantAssignment"/>.
/// </summary>
public sealed class TenantAssignmentValidator : AbstractValidator<TenantAssignment>
{
    public TenantAssignmentValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty()
            .WithMessage("Tenant ID is required.");

        RuleFor(x => x.TenantName)
            .NotEmpty()
            .WithMessage("Tenant name is required.")
            .MaximumLength(200)
            .WithMessage("Tenant name must not exceed 200 characters.");
    }
}
