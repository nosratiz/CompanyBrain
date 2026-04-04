using FluentResults;
using CompanyBrain.UserPortal.Server.Domain;
using CompanyBrain.UserPortal.Server.Domain.Enums;

namespace CompanyBrain.UserPortal.Server.Services.Interfaces;

public interface IUserLicenseService
{
    Task<IReadOnlyList<License>> GetUserLicensesAsync(Guid userId);
    Task<License?> GetActiveLicenseAsync(Guid userId);
    Task<Result<License>> PurchaseLicenseAsync(Guid userId, LicenseTier tier);
}