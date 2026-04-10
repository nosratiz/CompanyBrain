using CompanyBrain.Dashboard.Api.Contracts;
using FluentValidation;

namespace CompanyBrain.Dashboard.Api.Validation;

internal sealed class IngestDatabaseSchemaRequestValidator : AbstractValidator<IngestDatabaseSchemaRequest>
{
    public IngestDatabaseSchemaRequestValidator()
    {
        RuleFor(r => r.ConnectionString)
            .NotEmpty()
            .WithMessage("Connection string is required.");

        RuleFor(r => r.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(r => r.Provider)
            .NotEmpty()
            .Must(p => p is "SqlServer" or "PostgreSql" or "MySql")
            .WithMessage("Provider must be SqlServer, PostgreSql, or MySql.");
    }
}
