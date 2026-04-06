using CompanyBrain.Admin.Server.Api.Contracts.Admin;
using CompanyBrain.Admin.Server.Domain.Enums;
using FluentValidation;

namespace CompanyBrain.Admin.Server.Api.Validation;

internal sealed class AssignLicenseRequestValidator : AbstractValidator<AssignLicenseRequest>
{
    public AssignLicenseRequestValidator()
    {
        RuleFor(request => request.UserId)
            .NotEmpty();

        RuleFor(request => request.Tier)
            .NotEmpty()
            .Must(tier => Enum.TryParse<LicenseTier>(tier, ignoreCase: true, out _))
            .WithMessage("Invalid license tier");
    }
}