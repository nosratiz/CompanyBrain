namespace CompanyBrain.Models;

internal sealed record SavedKnowledgeDocument(string FileName, string FilePath, string ResourceUri, bool Existed);