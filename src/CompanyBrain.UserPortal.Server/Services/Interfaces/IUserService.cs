using FluentResults;
using CompanyBrain.UserPortal.Server.Domain;

namespace CompanyBrain.UserPortal.Server.Services.Interfaces;

public interface IUserService
{
    Task<Result<User>> RegisterAsync(string email, string password, string fullName);
    Task<Result<User>> LoginAsync(string email, string password);
    Task<User?> GetByIdAsync(Guid userId);
    Task<bool> EmailExistsAsync(string email);
}