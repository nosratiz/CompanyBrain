namespace CompanyBrain.UserPortal.Server.Api.Contracts.User;

public sealed record CreateApiKeyRequest(string Name, string? Scope, DateTime? ExpiresAt);