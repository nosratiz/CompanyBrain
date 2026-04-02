using CompanyBrain.Application;
using CompanyBrain.Constants;
using CompanyBrain.Hosting;
using CompanyBrain.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CompanyBrain.DependencyInjection;

public static class CompanyBrainCoreServiceCollectionExtensions
{
    public static IServiceCollection AddCompanyBrainCore(this IServiceCollection services, string contentRootPath)
    {
        var knowledgeRoot = ResolveKnowledgeRoot(contentRootPath);

        services.AddSingleton(sp => new KnowledgeStore(
            knowledgeRoot,
            sp.GetRequiredService<ILogger<KnowledgeStore>>()));

        services.AddSingleton(sp => new HttpClient(new SocketsHttpHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
        }));

        services.AddSingleton(sp => new WikiIngester(
            sp.GetRequiredService<HttpClient>(),
            sp.GetRequiredService<ILogger<WikiIngester>>()));

        services.AddSingleton(sp => new KnowledgeApplicationService(
            sp.GetRequiredService<KnowledgeStore>(),
            sp.GetRequiredService<WikiIngester>(),
            sp.GetRequiredService<ILogger<KnowledgeApplicationService>>()));

        services.AddHostedService(sp => new KnowledgeFolderBootstrapper(
            sp.GetRequiredService<KnowledgeStore>(),
            sp.GetRequiredService<ILogger<KnowledgeFolderBootstrapper>>()));

        return services;
    }

    private static string ResolveKnowledgeRoot(string startPath)
    {
        var current = new DirectoryInfo(Path.GetFullPath(startPath));

        while (current is not null)
        {
            var srcDirectory = Path.Combine(current.FullName, "src");
            if (Directory.Exists(srcDirectory))
            {
                return Path.Combine(current.FullName, CompanyBrainConstants.KnowledgeFolderName);
            }

            current = current.Parent;
        }

        return Path.Combine(Path.GetFullPath(startPath), CompanyBrainConstants.KnowledgeFolderName);
    }
}