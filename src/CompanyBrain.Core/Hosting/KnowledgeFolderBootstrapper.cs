using CompanyBrain.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CompanyBrain.Hosting;

internal sealed class KnowledgeFolderBootstrapper : IHostedService
{
    private readonly KnowledgeStore knowledgeStore;
    private readonly ILogger<KnowledgeFolderBootstrapper> logger;

    public KnowledgeFolderBootstrapper(KnowledgeStore knowledgeStore, ILogger<KnowledgeFolderBootstrapper>? logger = null)
    {
        this.knowledgeStore = knowledgeStore;
        this.logger = logger ?? NullLogger<KnowledgeFolderBootstrapper>.Instance;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Ensuring knowledge folder exists during startup.");
        knowledgeStore.EnsureFolderExists();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}