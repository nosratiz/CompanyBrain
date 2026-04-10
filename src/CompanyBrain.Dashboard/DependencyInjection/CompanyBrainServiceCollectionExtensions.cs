using CompanyBrain.Dashboard.Api.Validation;
using CompanyBrain.Dashboard.Api.Contracts;
using CompanyBrain.DependencyInjection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyBrain.Dashboard.DependencyInjection;

internal static class CompanyBrainServiceCollectionExtensions
{
    public static IServiceCollection AddCompanyBrain(this IServiceCollection services, string contentRootPath)
    {
        services.AddCompanyBrainCore(contentRootPath);
        services.AddSingleton<IValidator<IngestWikiRequest>, IngestWikiRequestValidator>();
        services.AddSingleton<IValidator<IngestWikiBatchRequest>, IngestWikiBatchRequestValidator>();
        services.AddSingleton<IValidator<IngestPathRequest>, IngestPathRequestValidator>();
        services.AddSingleton<IValidator<SearchRequest>, SearchRequestValidator>();
        services.AddSingleton<IValidator<UploadDocumentRequest>, UploadDocumentRequestValidator>();
        services.AddSingleton<IValidator<IngestDatabaseSchemaRequest>, IngestDatabaseSchemaRequestValidator>();
        return services;
    }
}
