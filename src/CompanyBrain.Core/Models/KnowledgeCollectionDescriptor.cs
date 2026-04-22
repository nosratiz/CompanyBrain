namespace CompanyBrain.Models;

public sealed record KnowledgeCollectionDescriptor(
    string CollectionId,
    string DisplayName,
    string FolderPath,
    string ResourceUri,
    int DocumentCount);