using FluentResults;
using CompanyBrain.Admin.Server.Domain;
using CompanyBrain.Admin.Server.Domain.Enums;

namespace CompanyBrain.Admin.Server.Services.Interfaces;

public interface IUserLicenseService
{
    Task<IReadOnlyList<License>> GetUserLicensesAsync(Guid userId);
    Task<License?> GetActiveLicenseAsync(Guid userId);
    Task<Result<License>> PurchaseLicenseAsync(Guid userId, LicenseTier tier);
    Task<IReadOnlyList<License>> GetAllLicensesAsync(int page, int pageSize);
    Task<int> GetTotalLicenseCountAsync();
    Task<Result> RevokeLicenseAsync(Guid licenseId);
    Task<Result<License>> AssignLicenseAsync(Guid userId, LicenseTier tier);
}