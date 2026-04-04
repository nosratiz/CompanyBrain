using CompanyBrain.Admin.Server.Domain;

namespace CompanyBrain.Admin.Server.Services.Interfaces;

public interface IJwtService
{
    string GenerateToken(User user);
    Guid? ValidateToken(string token);
}