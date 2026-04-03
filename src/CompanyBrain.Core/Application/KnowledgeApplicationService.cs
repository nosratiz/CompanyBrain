using CompanyBrain.Application.Results;
using FluentResults;
using CompanyBrain.Models;
using CompanyBrain.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CompanyBrain.Application;

internal sealed class KnowledgeApplicationService
{
    private readonly KnowledgeStore knowledgeStore;
    private readonly WikiIngester wikiIngester;
    private readonly ILogger<KnowledgeApplicationService> logger;

    public KnowledgeApplicationService(
        KnowledgeStore knowledgeStore,
        WikiIngester wikiIngester,
        ILogger<KnowledgeApplicationService>? logger = null)
    {
        this.knowledgeStore = knowledgeStore;
        this.wikiIngester = wikiIngester;
        this.logger = logger ?? NullLogger<KnowledgeApplicationService>.Instance;
    }

    public async Task<Result<SavedKnowledgeDocument>> IngestWikiAsync(string url, string name, CancellationToken cancellationToken)
    {
        logger.LogInformation("Ingesting wiki content from '{Url}' as '{Name}'.", url, name);

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return Result.Fail<SavedKnowledgeDocument>(new ValidationAppError("An absolute http or https URL is required."));
        }

        try
        {
            var markdown = await wikiIngester.IngestAsync(uri, cancellationToken);
            var document = await knowledgeStore.SaveMarkdownAsync(name, markdown, cancellationToken);
            return Result.Ok(document);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Failed to fetch wiki content from '{Url}'.", url);
            return Result.Fail<SavedKnowledgeDocument>(new UpstreamAppError($"Failed to fetch wiki content from '{url}'. {exception.Message}"));
        }
        catch (InvalidOperationException exception)
        {
            logger.LogWarning(exception, "Wiki content from '{Url}' could not be converted into meaningful markdown.", url);
            return Result.Fail<SavedKnowledgeDocument>(new ValidationAppError(exception.Message));
        }
    }

    public async Task<Result<WikiBatchIngestionResult>> IngestWikiBatchAsync(string baseUrl, string? linkSelector, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting batch wiki ingestion from '{Url}'.", baseUrl);

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return Result.Fail<WikiBatchIngestionResult>(new ValidationAppError("An absolute http or https URL is required."));
        }

        IReadOnlyList<DiscoveredWikiLink> discoveredLinks;
        try
        {
            discoveredLinks = await wikiIngester.DiscoverLinksAsync(uri, linkSelector, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Failed to discover wiki links from '{Url}'.", baseUrl);
            return Result.Fail<WikiBatchIngestionResult>(new UpstreamAppError($"Failed to discover wiki links from '{baseUrl}'. {exception.Message}"));
        }

        if (discoveredLinks.Count == 0)
        {
            logger.LogWarning("No wiki links discovered from '{Url}'.", baseUrl);
            return Result.Ok(new WikiBatchIngestionResult(0, 0, 0, []));
        }

        var results = new List<WikiBatchIngestionItemResult>();
        var successCount = 0;
        var failCount = 0;

        foreach (var link in discoveredLinks)
        {
            try
            {
                logger.LogInformation("Ingesting wiki page '{Name}' from '{Url}'.", link.Name, link.Url);

                var ingestResult = await IngestWikiAsync(link.Url, link.Name, cancellationToken);

                if (ingestResult.IsSuccess)
                {
                    var doc = ingestResult.Value;
                    results.Add(new WikiBatchIngestionItemResult(link.Url, link.Name, doc.FileName, doc.ResourceUri, true, null));
                    successCount++;
                }
                else
                {
                    var errorMessage = string.Join("; ", ingestResult.Errors.Select(e => e.Message));
                    results.Add(new WikiBatchIngestionItemResult(link.Url, link.Name, null, null, false, errorMessage));
                    failCount++;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Unexpected error ingesting wiki page '{Name}' from '{Url}'.", link.Name, link.Url);
                results.Add(new WikiBatchIngestionItemResult(link.Url, link.Name, null, null, false, exception.Message));
                failCount++;
            }
        }

        logger.LogInformation("Batch wiki ingestion from '{Url}' completed: {Success} succeeded, {Failed} failed.", baseUrl, successCount, failCount);

        return Result.Ok(new WikiBatchIngestionResult(discoveredLinks.Count, successCount, failCount, results));
    }

    public async Task<Result<SavedKnowledgeDocument>> IngestDocumentFromPathAsync(string localPath, string? name, CancellationToken cancellationToken)
    {
        logger.LogInformation("Ingesting document from local path '{LocalPath}' as '{Name}'.", localPath, name);

        var fullPath = Path.GetFullPath(localPath, Directory.GetCurrentDirectory());
        if (!File.Exists(fullPath))
        {
            logger.LogWarning("Document path does not exist: '{FullPath}'.", fullPath);
            return Result.Fail<SavedKnowledgeDocument>(new NotFoundAppError($"Document not found: {fullPath}"));
        }

        try
        {
            var markdown = await DocumentMarkdownConverter.ConvertAsync(fullPath, cancellationToken);
            var logicalName = string.IsNullOrWhiteSpace(name) ? Path.GetFileNameWithoutExtension(fullPath) : name;

            var document = await knowledgeStore.SaveMarkdownAsync(logicalName, markdown, cancellationToken);
            return Result.Ok(document);
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException or ArgumentException)
        {
            logger.LogWarning(exception, "Document ingestion failed for '{FullPath}'.", fullPath);
            return Result.Fail<SavedKnowledgeDocument>(new ValidationAppError(exception.Message));
        }
    }

    public async Task<Result<SavedKnowledgeDocument>> IngestUploadedDocumentAsync(Stream content, string originalFileName, string? name, CancellationToken cancellationToken)
    {
        logger.LogInformation("Ingesting uploaded document '{OriginalFileName}' as '{Name}'.", originalFileName, name);

        var extension = Path.GetExtension(originalFileName);
        var tempFilePath = Path.Combine(Path.GetTempPath(), $"company-brain-{Guid.NewGuid():N}{extension}");

        await using (var fileStream = File.Create(tempFilePath))
        {
            await content.CopyToAsync(fileStream, cancellationToken);
        }

        try
        {
            var markdown = await DocumentMarkdownConverter.ConvertAsync(tempFilePath, cancellationToken);
            var logicalName = string.IsNullOrWhiteSpace(name)
                ? Path.GetFileNameWithoutExtension(originalFileName)
                : name;

            var document = await knowledgeStore.SaveMarkdownAsync(logicalName, markdown, cancellationToken);
            return Result.Ok(document);
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException or ArgumentException)
        {
            logger.LogWarning(exception, "Uploaded document ingestion failed for '{OriginalFileName}'.", originalFileName);
            return Result.Fail<SavedKnowledgeDocument>(new ValidationAppError(exception.Message));
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }

    public Task<Result<string>> SearchAsync(string query, int maxResults, CancellationToken cancellationToken)
        => knowledgeStore.SearchAsync(query, maxResults, cancellationToken);

    public async Task<Result<IReadOnlyList<KnowledgeResourceDescriptor>>> ListResourcesAsync(CancellationToken cancellationToken)
        => Result.Ok(await knowledgeStore.ListResourcesAsync(cancellationToken));

    public Task<Result<KnowledgeResourceContent>> ReadResourceAsync(string resourceUri, CancellationToken cancellationToken)
        => knowledgeStore.ReadResourceAsync(resourceUri, cancellationToken);

    public Task<Result<KnowledgeResourceContent>> GetResourceAsync(string fileName, CancellationToken cancellationToken)
        => knowledgeStore.ReadResourceAsync(knowledgeStore.ToResourceUri(fileName), cancellationToken);
}