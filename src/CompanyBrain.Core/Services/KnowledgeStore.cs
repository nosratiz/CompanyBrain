using System.Text;
using CompanyBrain.Constants;
using CompanyBrain.Application.Results;
using FluentResults;
using CompanyBrain.Models;
using CompanyBrain.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CompanyBrain.Services;

internal sealed class KnowledgeStore
{
    private readonly ILogger<KnowledgeStore> logger;
    private readonly string rootPath;

    public KnowledgeStore(string rootPath, ILogger<KnowledgeStore>? logger = null)
    {
        this.rootPath = rootPath;
        this.logger = logger ?? NullLogger<KnowledgeStore>.Instance;
    }

    public void EnsureFolderExists() => Directory.CreateDirectory(rootPath);

    public async Task<SavedKnowledgeDocument> SaveMarkdownAsync(string name, string markdown,
        CancellationToken cancellationToken)
    {
        EnsureFolderExists();

        var fileName = FileNameHelper.ToMarkdownFileName(name);
        var filePath = Path.Combine(rootPath, fileName);
        var existed = File.Exists(filePath);
        var normalized = MarkdownUtilities.Normalize(markdown);

        logger.LogInformation("Saving knowledge document '{Name}' to '{FilePath}'. Existed: {Existed}", name, filePath,
            existed);

        await File.WriteAllTextAsync(filePath, normalized, Encoding.UTF8, cancellationToken);

        return new SavedKnowledgeDocument(fileName, filePath, ToResourceUri(fileName), existed);
    }

    public Task<IReadOnlyList<KnowledgeResourceDescriptor>> ListResourcesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureFolderExists();

        logger.LogDebug("Listing knowledge resources from '{RootPath}'.", rootPath);

        var resources = Directory
            .EnumerateFiles(rootPath, "*.md", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Select(filePath =>
            {
                var fileInfo = new FileInfo(filePath);
                var fileName = fileInfo.Name;

                return new KnowledgeResourceDescriptor(
                    $"resources/{fileName}",
                    $"@resources/{fileName}",
                    ToResourceUri(fileName),
                    $"Knowledge document stored in {CompanyBrainConstants.KnowledgeFolderName}/{fileName}.",
                    "text/markdown",
                    fileInfo.Length);
            })
            .Cast<KnowledgeResourceDescriptor>()
            .ToList();

        return Task.FromResult<IReadOnlyList<KnowledgeResourceDescriptor>>(resources);
    }

    public Task<Result<KnowledgeResourceContent>> ReadResourceAsync(string resourceUri,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(ReadTextResource(resourceUri));
    }

    public Result<KnowledgeResourceContent> ReadTextResource(string resourceUri)
    {
        EnsureFolderExists();

        var fileName = TryGetFileName(resourceUri);
        if (fileName.IsFailed)
        {
            return Result.Fail<KnowledgeResourceContent>(fileName.Errors);
        }

        var filePath = Path.Combine(rootPath, fileName.Value);
        if (!File.Exists(filePath))
        {
            logger.LogWarning("Requested knowledge resource was not found: '{FilePath}'.", filePath);
            return Result.Fail<KnowledgeResourceContent>(new NotFoundAppError($"Resource not found: {filePath}"));
        }

        logger.LogDebug("Reading knowledge resource '{FileName}' from '{FilePath}'.", fileName.Value, filePath);

        return Result.Ok(new KnowledgeResourceContent(
            fileName.Value,
            ToResourceUri(fileName.Value),
            "text/markdown",
            File.ReadAllText(filePath, Encoding.UTF8)));
    }

    public async Task<Result<string>> SearchAsync(string query, int maxResults, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureFolderExists();

        logger.LogInformation("Searching knowledge store. Query: '{Query}', MaxResults: {MaxResults}", query, maxResults);

        if (string.IsNullOrWhiteSpace(query))
        {
            return Result.Fail<string>(new ValidationAppError("A non-empty query is required."));
        }

        maxResults = Math.Clamp(maxResults, 1, 20);
        var terms = SearchUtilities.Tokenize(query).ToArray();
        if (terms.Length == 0)
        {
            return Result.Fail<string>(new ValidationAppError("No searchable terms were found in the provided query."));
        }

        var matches = new List<SearchMatch>();

        foreach (var filePath in Directory.EnumerateFiles(rootPath, "*.md", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileName = Path.GetFileName(filePath);
            var content = await File.ReadAllTextAsync(filePath, cancellationToken);
            foreach (var snippet in SearchUtilities.ExtractSnippets(content))
            {
                var score = SearchUtilities.ScoreSnippet(fileName, snippet, query, terms);
                if (score <= 0)
                {
                    continue;
                }

                matches.Add(new SearchMatch(fileName, score, snippet));
            }
        }

        if (matches.Count == 0)
        {
            logger.LogInformation("Search completed with no matches for query '{Query}'.", query);
            return Result.Ok($"No matches found for '{query}' in {CompanyBrainConstants.KnowledgeFolderName}.");
        }

        var builder = new StringBuilder();
        builder.AppendLine($"Search results for '{query}':");
        builder.AppendLine();

        foreach (var match in matches
                     .OrderByDescending(match => match.Score)
                     .ThenBy(match => match.FileName, StringComparer.OrdinalIgnoreCase)
                     .Take(maxResults))
        {
            builder.AppendLine($"- @resources/{match.FileName} ({ToResourceUri(match.FileName)})");
            builder.AppendLine($"  {MarkdownUtilities.ToBlockQuote(match.Snippet)}");
        }

        logger.LogInformation("Search completed with {MatchCount} matches for query '{Query}'.", matches.Count, query);

        return Result.Ok(builder.ToString().TrimEnd());
    }

    public string ToResourceUri(string fileName) =>
        CompanyBrainConstants.ResourceScheme + Uri.EscapeDataString(fileName);

    private static Result<string> TryGetFileName(string resourceUri)
    {
        if (!resourceUri.StartsWith(CompanyBrainConstants.ResourceScheme, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Fail<string>(new ValidationAppError($"Unsupported resource URI: {resourceUri}"));
        }

        var encodedName = resourceUri[CompanyBrainConstants.ResourceScheme.Length..].Trim('/');
        var fileName = Uri.UnescapeDataString(encodedName);
        var normalized = Path.GetFileName(fileName);

        if (string.IsNullOrWhiteSpace(normalized) || !normalized.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Fail<string>(new ValidationAppError($"Unsupported knowledge resource: {resourceUri}"));
        }

        return Result.Ok(normalized);
    }
}