using CompanyBrain.UserPortal.Server.Api.Contracts.User;
using FluentValidation;

namespace CompanyBrain.UserPortal.Server.Api.Validation;

internal sealed class CreateApiKeyRequestValidator : AbstractValidator<CreateApiKeyRequest>
{
    public CreateApiKeyRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty();
    }
}