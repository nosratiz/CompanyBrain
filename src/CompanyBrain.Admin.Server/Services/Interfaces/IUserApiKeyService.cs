using FluentResults;
using CompanyBrain.Admin.Server.Domain;
using CompanyBrain.Admin.Server.Domain.Enums;

namespace CompanyBrain.Admin.Server.Services.Interfaces;

public interface IUserApiKeyService
{
    Task<IReadOnlyList<UserApiKey>> GetUserApiKeysAsync(Guid userId, bool includeRevoked = false);
    Task<Result<(string PlainKey, UserApiKey ApiKey)>> CreateApiKeyAsync(Guid userId, string name, ApiKeyScope scope, DateTime? expiresAt);
    Task<Result> RevokeApiKeyAsync(Guid userId, Guid keyId);
    Task<Result<(Guid UserId, ApiKeyScope Scope)>> ValidateApiKeyAsync(string plainKey);
}