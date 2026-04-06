using FluentResults;
using CompanyBrain.Admin.Server.Domain;
using CompanyBrain.Admin.Server.Domain.Enums;

namespace CompanyBrain.Admin.Server.Services.Interfaces;

public interface IUserApiKeyService
{
    Task<IReadOnlyList<UserApiKey>> GetUserApiKeysAsync(Guid userId, bool includeRevoked = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserApiKey>> GetAllApiKeysAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<int> GetTotalApiKeyCountAsync(CancellationToken cancellationToken = default);
    Task<Result<(string PlainKey, UserApiKey ApiKey)>> CreateApiKeyAsync(Guid userId, string name, ApiKeyScope scope, DateTime? expiresAt, CancellationToken cancellationToken = default);
    Task<Result> RevokeApiKeyAsync(Guid userId, Guid keyId, CancellationToken cancellationToken = default);
    Task<Result> AdminRevokeApiKeyAsync(Guid keyId, CancellationToken cancellationToken = default);
    Task<Result<(Guid UserId, ApiKeyScope Scope)>> ValidateApiKeyAsync(string plainKey, CancellationToken cancellationToken = default);
}