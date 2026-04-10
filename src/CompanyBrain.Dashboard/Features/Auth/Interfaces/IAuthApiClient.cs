using CompanyBrain.Dashboard.Features.Auth.Contracts;
using FluentResults;

namespace CompanyBrain.Dashboard.Features.Auth.Interfaces;

public interface IAuthApiClient
{
    Task<Result<AuthResponse>> LoginAsync(string email, string password, CancellationToken cancellationToken = default);

    Task<Result> SendEmployeeLoginCodeAsync(string email, CancellationToken cancellationToken = default);

    Task<Result<EmployeeAuthResponse>> VerifyEmployeeLoginCodeAsync(string email, string code, CancellationToken cancellationToken = default);
}
