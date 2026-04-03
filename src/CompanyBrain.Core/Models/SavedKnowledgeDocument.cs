namespace CompanyBrain.Models;

public sealed record SavedKnowledgeDocument(string FileName, string FilePath, string ResourceUri, bool Existed);