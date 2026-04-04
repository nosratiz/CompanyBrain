using FluentResults;
using CompanyBrain.Admin.Server.Domain;

namespace CompanyBrain.Admin.Server.Services.Interfaces;

public interface IUserService
{
    Task<Result<User>> RegisterAsync(string email, string password, string fullName);
    Task<Result<User>> LoginAsync(string email, string password);
    Task<User?> GetByIdAsync(Guid userId);
    Task<bool> EmailExistsAsync(string email);
    Task<IReadOnlyList<User>> GetAllUsersAsync(int page, int pageSize);
    Task<int> GetTotalUserCountAsync();
    Task<Result> UpdateUserAsync(Guid userId, string? fullName, string? email);
    Task<Result> SetUserActiveStatusAsync(Guid userId, bool isActive);
    Task<Result> DeleteUserAsync(Guid userId);
}