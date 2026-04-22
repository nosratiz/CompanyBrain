using CompanyBrain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CompanyBrain.Services;

public sealed class CollectionManagerService
{
    private const string CollectionUriPrefix = "mcp://internal/knowledge/";
    private readonly string rootPath;
    private readonly ILogger<CollectionManagerService> logger;

    public CollectionManagerService(string rootPath, ILogger<CollectionManagerService>? logger = null)
    {
        this.rootPath = rootPath;
        this.logger = logger ?? NullLogger<CollectionManagerService>.Instance;
    }

    public string RootPath => rootPath;

    public Task EnsureRootExistsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(rootPath);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<KnowledgeCollectionDescriptor>> ListCollectionsAsync(CancellationToken cancellationToken)
    {
        await EnsureRootExistsAsync(cancellationToken);

        var collectionDirectories = Directory
            .EnumerateDirectories(rootPath, "*", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var collections = new List<KnowledgeCollectionDescriptor>(collectionDirectories.Count);
        foreach (var collectionDirectory in collectionDirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var collectionId = Path.GetFileName(collectionDirectory);
            if (string.IsNullOrWhiteSpace(collectionId))
            {
                continue;
            }

            var documentCount = Directory.EnumerateFiles(collectionDirectory, "*.md", SearchOption.AllDirectories).Count();
            collections.Add(new KnowledgeCollectionDescriptor(
                collectionId,
                collectionId,
                collectionDirectory,
                ToCollectionUri(collectionId),
                documentCount));
        }

        if (collections.Count == 0)
        {
            var generalPath = EnsureCollectionFolder("General");
            collections.Add(new KnowledgeCollectionDescriptor("General", "General", generalPath, ToCollectionUri("General"), 0));
        }

        return collections;
    }

    public Task<IReadOnlyList<string>> ListCollectionDocumentsAsync(string collectionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var collectionPath = GetCollectionPath(collectionId);
        if (!Directory.Exists(collectionPath))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        var files = Directory
            .EnumerateFiles(collectionPath, "*.md", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(collectionPath, path).Replace(Path.DirectorySeparatorChar, '/'))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(files);
    }

    public string EnsureCollectionFolder(string collectionId)
    {
        var safeCollection = NormalizeCollectionId(collectionId);
        var path = Path.Combine(rootPath, safeCollection);
        Directory.CreateDirectory(path);
        return path;
    }

    public string ToCollectionUri(string collectionId)
        => CollectionUriPrefix + Uri.EscapeDataString(NormalizeCollectionId(collectionId));

    public string ToDocumentUri(string collectionId, string relativePath)
    {
        var encodedCollection = Uri.EscapeDataString(NormalizeCollectionId(collectionId));
        var cleanRelativePath = relativePath.Replace('\\', '/').TrimStart('/');
        var segments = cleanRelativePath.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.EscapeDataString);

        return CollectionUriPrefix + encodedCollection + "/" + string.Join('/', segments);
    }

    public bool TryResolveUri(string resourceUri, out string collectionId, out string? relativePath)
    {
        collectionId = string.Empty;
        relativePath = null;

        if (!resourceUri.StartsWith(CollectionUriPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var tail = resourceUri[CollectionUriPrefix.Length..].Trim('/');
        if (string.IsNullOrWhiteSpace(tail))
        {
            return false;
        }

        var slashIndex = tail.IndexOf('/');
        if (slashIndex < 0)
        {
            collectionId = NormalizeCollectionId(Uri.UnescapeDataString(tail));
            return true;
        }

        collectionId = NormalizeCollectionId(Uri.UnescapeDataString(tail[..slashIndex]));
        var encodedPath = tail[(slashIndex + 1)..];
        var decodedSegments = encodedPath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.UnescapeDataString);

        relativePath = string.Join('/', decodedSegments);
        return true;
    }

    public bool TryResolveDocumentPath(string collectionId, string relativePath, out string fullPath)
    {
        fullPath = string.Empty;

        var collectionPath = GetCollectionPath(collectionId);
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        var candidatePath = Path.GetFullPath(Path.Combine(collectionPath, normalized));

        if (!candidatePath.StartsWith(collectionPath, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Blocked collection path traversal attempt for collection '{CollectionId}'", collectionId);
            return false;
        }

        fullPath = candidatePath;
        return true;
    }

    public string GetCollectionPath(string collectionId)
        => Path.Combine(rootPath, NormalizeCollectionId(collectionId));

    private static string NormalizeCollectionId(string collectionId)
    {
        if (string.IsNullOrWhiteSpace(collectionId))
        {
            return "General";
        }

        var normalized = string.Concat(collectionId.Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_'));
        return string.IsNullOrWhiteSpace(normalized) ? "General" : normalized;
    }
}