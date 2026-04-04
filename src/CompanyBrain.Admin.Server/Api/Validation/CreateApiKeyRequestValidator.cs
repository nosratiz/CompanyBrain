using CompanyBrain.Admin.Server.Api.Contracts.User;
using FluentValidation;

namespace CompanyBrain.Admin.Server.Api.Validation;

internal sealed class CreateApiKeyRequestValidator : AbstractValidator<CreateApiKeyRequest>
{
    public CreateApiKeyRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty();
    }
}