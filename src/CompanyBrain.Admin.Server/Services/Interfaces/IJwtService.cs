using CompanyBrain.Admin.Server.Domain;

namespace CompanyBrain.Admin.Server.Services.Interfaces;

public interface IJwtService
{
    Task<string> GenerateTokenAsync(User user, CancellationToken cancellationToken = default);
    Task<Guid?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default);
}