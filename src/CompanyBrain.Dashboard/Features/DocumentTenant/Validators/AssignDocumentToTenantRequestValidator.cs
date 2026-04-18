using CompanyBrain.Dashboard.Features.DocumentTenant.Requests;
using FluentValidation;

namespace CompanyBrain.Dashboard.Features.DocumentTenant.Validators;

/// <summary>
/// Validator for <see cref="AssignDocumentToTenantRequest"/>.
/// </summary>
public sealed class AssignDocumentToTenantRequestValidator : AbstractValidator<AssignDocumentToTenantRequest>
{
    public AssignDocumentToTenantRequestValidator()
    {
        RuleFor(x => x.FileName)
            .NotEmpty()
            .WithMessage("File name is required.")
            .MaximumLength(500)
            .WithMessage("File name must not exceed 500 characters.");

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
