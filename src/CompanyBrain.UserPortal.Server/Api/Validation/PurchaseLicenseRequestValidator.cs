using CompanyBrain.UserPortal.Server.Api.Contracts.User;
using CompanyBrain.UserPortal.Server.Domain.Enums;
using FluentValidation;

namespace CompanyBrain.UserPortal.Server.Api.Validation;

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