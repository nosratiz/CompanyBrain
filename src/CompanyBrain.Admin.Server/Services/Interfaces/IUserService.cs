using FluentResults;
using CompanyBrain.Admin.Server.Domain;

namespace CompanyBrain.Admin.Server.Services.Interfaces;

public interface IUserService
{
    Task<Result<User>> RegisterAsync(string email, string password, string fullName, CancellationToken cancellationToken = default);
    Task<Result<User>> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetAllUsersAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<int> GetTotalUserCountAsync(CancellationToken cancellationToken = default);
    Task<Result> UpdateUserAsync(Guid userId, string? fullName, string? email, CancellationToken cancellationToken = default);
    Task<Result> SetUserActiveStatusAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default);
    Task<Result> DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default);
}