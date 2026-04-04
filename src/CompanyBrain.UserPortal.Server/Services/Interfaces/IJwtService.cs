using CompanyBrain.UserPortal.Server.Domain;

namespace CompanyBrain.UserPortal.Server.Services.Interfaces;

public interface IJwtService
{
    string GenerateToken(User user);
    Guid? ValidateToken(string token);
}