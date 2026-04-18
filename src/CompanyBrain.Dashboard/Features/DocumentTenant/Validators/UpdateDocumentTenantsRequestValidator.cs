using CompanyBrain.Dashboard.Features.DocumentTenant.Requests;
using FluentValidation;

namespace CompanyBrain.Dashboard.Features.DocumentTenant.Validators;

/// <summary>
/// Validator for <see cref="UpdateDocumentTenantsRequest"/>.
/// </summary>
public sealed class UpdateDocumentTenantsRequestValidator : AbstractValidator<UpdateDocumentTenantsRequest>
{
    public UpdateDocumentTenantsRequestValidator()
    {
        RuleFor(x => x.FileName)
            .NotEmpty()
            .WithMessage("File name is required.")
            .MaximumLength(500)
            .WithMessage("File name must not exceed 500 characters.");

        RuleFor(x => x.Tenants)
            .NotNull()
            .WithMessage("Tenants list is required.");

        RuleForEach(x => x.Tenants)
            .SetValidator(new TenantAssignmentValidator());
    }
}
