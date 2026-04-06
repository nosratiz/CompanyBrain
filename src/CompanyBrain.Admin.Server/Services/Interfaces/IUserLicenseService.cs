using FluentResults;
using CompanyBrain.Admin.Server.Domain;
using CompanyBrain.Admin.Server.Domain.Enums;

namespace CompanyBrain.Admin.Server.Services.Interfaces;

public interface IUserLicenseService
{
    Task<IReadOnlyList<License>> GetUserLicensesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<License?> GetActiveLicenseAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Result<License>> PurchaseLicenseAsync(Guid userId, LicenseTier tier, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<License>> GetAllLicensesAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<int> GetTotalLicenseCountAsync(CancellationToken cancellationToken = default);
    Task<Result> RevokeLicenseAsync(Guid licenseId, CancellationToken cancellationToken = default);
    Task<Result<License>> AssignLicenseAsync(Guid userId, LicenseTier tier, CancellationToken cancellationToken = default);
    Task<Result<License>> UpdateLicenseTierAsync(Guid licenseId, LicenseTier newTier, CancellationToken cancellationToken = default);
}