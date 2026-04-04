using CompanyBrain.Admin.Server.Api.Contracts.User;
using CompanyBrain.Admin.Server.Domain.Enums;
using FluentValidation;

namespace CompanyBrain.Admin.Server.Api.Validation;

internal sealed class PurchaseLicenseRequestValidator : AbstractValidator<PurchaseLicenseRequest>
{
    public PurchaseLicenseRequestValidator()
    {
        RuleFor(request => request.Tier)
            .NotEmpty()
            .Must(BeValidTier)
            .WithMessage("Invalid license tier");
    }

    private static bool BeValidTier(string tier) =>
        Enum.TryParse<LicenseTier>(tier, ignoreCase: true, out _);
}