using System.Text;
using CompanyBrain.Constants;
using CompanyBrain.Application.Results;
using FluentResults;
using CompanyBrain.Models;
using CompanyBrain.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CompanyBrain.Services;

public sealed class KnowledgeStore
{
    private const string DefaultCollection = "General";
    private readonly ILogger<KnowledgeStore> logger;
    private readonly string rootPath;

    public KnowledgeStore(string rootPath, ILogger<KnowledgeStore>? logger = null)
    {
        this.rootPath = rootPath;
        this.logger = logger ?? NullLogger<KnowledgeStore>.Instance;
    }

    public Task EnsureFolderExistsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(rootPath);
        Directory.CreateDirectory(Path.Combine(rootPath, DefaultCollection));
        return Task.CompletedTask;
    }

    public Task<SavedKnowledgeDocument> SaveMarkdownAsync(string name, string markdown,
        CancellationToken cancellationToken)
        => SaveMarkdownToCollectionAsync(DefaultCollection, name, markdown, cancellationToken);

    public async Task<SavedKnowledgeDocument> SaveMarkdownToCollectionAsync(
        string collectionId,
        string name,
        string markdown,
        CancellationToken cancellationToken)
    {
        await EnsureFolderExistsAsync(cancellationToken);

        var fileName = FileNameHelper.ToMarkdownFileName(name);
        var safeCollection = NormalizeCollectionId(collectionId);
        var collectionPath = Path.Combine(rootPath, safeCollection);
        Directory.CreateDirectory(collectionPath);
        var filePath = Path.Combine(collectionPath, fileName);
        var existed = File.Exists(filePath);
        var normalized = MarkdownUtilities.Normalize(markdown);

        logger.LogInformation("Saving knowledge document '{Name}' to '{FilePath}'. Existed: {Existed}", name, filePath,
            existed);

        await File.WriteAllTextAsync(filePath, normalized, Encoding.UTF8, cancellationToken);

        var resourcePath = $"{safeCollection}/{fileName}";
        return new SavedKnowledgeDocument(resourcePath, filePath, ToResourceUri(resourcePath), existed);
    }

    public Task<IReadOnlyList<KnowledgeResourceDescriptor>> ListResourcesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(rootPath);

        logger.LogDebug("Listing knowledge resources from '{RootPath}'.", rootPath);

        var resources = Directory
            .EnumerateFiles(rootPath, "*.md", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(filePath =>
            {
                var fileInfo = new FileInfo(filePath);
                var relativePath = Path.GetRelativePath(rootPath, filePath)
                    .Replace(Path.DirectorySeparatorChar, '/');
                var fileName = relativePath;
                var title = fileInfo.Name;
                var collection = fileName.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? DefaultCollection;

                return new KnowledgeResourceDescriptor(
                    $"resources/{fileName}",
                    $"@resources/{title}",
                    ToResourceUri(fileName),
                    $"Knowledge document stored in {CompanyBrainConstants.KnowledgeFolderName}/{collection}/{title}.",
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

        Directory.CreateDirectory(rootPath);

        var fileName = TryGetFileName(resourceUri);
        if (fileName.IsFailed)
        {
            return Task.FromResult(Result.Fail<KnowledgeResourceContent>(fileName.Errors));
        }

        var filePath = Path.Combine(rootPath, fileName.Value.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(filePath))
        {
            logger.LogWarning("Requested knowledge resource was not found: '{FilePath}'.", filePath);
            return Task.FromResult(Result.Fail<KnowledgeResourceContent>(new NotFoundAppError($"Resource not found: {filePath}")));
        }

        logger.LogDebug("Reading knowledge resource '{FileName}' from '{FilePath}'.", fileName.Value, filePath);

        return ReadTextResourceInternalAsync(fileName.Value, filePath, cancellationToken);
    }

    private async Task<Result<KnowledgeResourceContent>> ReadTextResourceInternalAsync(string fileName, string filePath, CancellationToken cancellationToken)
    {
        var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8, cancellationToken);

        return Result.Ok(new KnowledgeResourceContent(
            fileName,
            ToResourceUri(fileName),
            "text/markdown",
            content));
    }

    public Task<Result<string>> SearchAsync(string query, int maxResults, CancellationToken cancellationToken)
        => SearchAsync(query, maxResults, null, cancellationToken);

    public async Task<Result<string>> SearchAsync(
        string query,
        int maxResults,
        string? collectionId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(rootPath);

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

        var searchRoot = string.IsNullOrWhiteSpace(collectionId)
            ? rootPath
            : Path.Combine(rootPath, NormalizeCollectionId(collectionId));

        if (!Directory.Exists(searchRoot))
        {
            return Result.Ok($"No matches found for '{query}' in collection '{collectionId}'.");
        }

        foreach (var filePath in Directory.EnumerateFiles(searchRoot, "*.md", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileName = Path.GetRelativePath(rootPath, filePath).Replace(Path.DirectorySeparatorChar, '/');
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
            var scope = string.IsNullOrWhiteSpace(collectionId)
                ? CompanyBrainConstants.KnowledgeFolderName
                : $"{CompanyBrainConstants.KnowledgeFolderName}/{NormalizeCollectionId(collectionId)}";

            return Result.Ok($"No matches found for '{query}' in {scope}.");
        }

        var builder = new StringBuilder();
        builder.AppendLine($"Search results for '{query}':");
        builder.AppendLine();

        foreach (var match in matches
                     .GroupBy(m => m.FileName, StringComparer.OrdinalIgnoreCase)
                     .Select(g => g.OrderByDescending(m => m.Score).First())
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

    public Result DeleteResource(string fileName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalized = NormalizeRelativeMarkdownPath(fileName);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return Result.Fail(new ValidationAppError($"Invalid resource file name: {fileName}"));
        }

        var filePath = Path.Combine(rootPath, normalized.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(filePath))
        {
            logger.LogWarning("Attempted to delete non-existent resource: '{FilePath}'.", filePath);
            return Result.Fail(new NotFoundAppError($"Resource not found: {normalized}"));
        }

        logger.LogInformation("Deleting knowledge resource '{FileName}' at '{FilePath}'.", normalized, filePath);
        File.Delete(filePath);
        return Result.Ok();
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
        var normalized = NormalizeRelativeMarkdownPath(fileName);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return Result.Fail<string>(new ValidationAppError($"Unsupported knowledge resource: {resourceUri}"));
        }

        return Result.Ok(normalized);
    }

    private static string NormalizeRelativeMarkdownPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var segments = path
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)
            .Select(Path.GetFileName)
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToArray();

        if (segments.Length == 0)
        {
            return string.Empty;
        }

        var normalized = string.Join('/', segments);
        return normalized.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? normalized : string.Empty;
    }

    private static string NormalizeCollectionId(string collectionId)
    {
        if (string.IsNullOrWhiteSpace(collectionId))
        {
            return DefaultCollection;
        }

        var normalized = string.Concat(collectionId.Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_'));
        return string.IsNullOrWhiteSpace(normalized) ? DefaultCollection : normalized;
    }
}