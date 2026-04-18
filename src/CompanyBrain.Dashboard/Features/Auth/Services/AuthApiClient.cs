using System.Net.Http.Json;
using CompanyBrain.Dashboard.Features.Auth.Contracts;
using CompanyBrain.Dashboard.Features.Auth.Interfaces;
using FluentResults;
using Microsoft.Extensions.Logging;

namespace CompanyBrain.Dashboard.Features.Auth.Services;

public sealed class AuthApiClient(HttpClient httpClient, ILogger<AuthApiClient> logger) : IAuthApiClient
{
    public async Task<Result<AuthResponse>> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync(
                "/api/auth/login",
                new LoginRequest(email, password),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Owner login failed for {Email} with status {StatusCode}", email, response.StatusCode);
                return Result.Fail<AuthResponse>("Invalid email or password.");
            }

            var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken);
            return authResponse is null
                ? Result.Fail<AuthResponse>("Failed to deserialize login response.")
                : Result.Ok(authResponse);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during owner login for {Email}", email);
            return Result.Fail<AuthResponse>("An unexpected error occurred. Please try again.");
        }
    }

    public async Task<Result> SendEmployeeLoginCodeAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync(
                "/api/employee-auth/send-code",
                new SendEmployeeLoginCodeRequest(email),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Send employee code failed for {Email} with status {StatusCode}", email, response.StatusCode);
            }

            // Always return OK to prevent email enumeration
            return Result.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error sending employee login code for {Email}", email);
            return Result.Fail("An unexpected error occurred. Please try again.");
        }
    }

    public async Task<Result<EmployeeAuthResponse>> VerifyEmployeeLoginCodeAsync(
        string email,
        string code,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync(
                "/api/employee-auth/verify-code",
                new VerifyEmployeeLoginCodeRequest(email, code),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Employee code verification failed for {Email} with status {StatusCode}", email, response.StatusCode);
                return Result.Fail<EmployeeAuthResponse>("Invalid email or code.");
            }

            var authResponse = await response.Content.ReadFromJsonAsync<EmployeeAuthResponse>(cancellationToken);
            return authResponse is null
                ? Result.Fail<EmployeeAuthResponse>("Failed to deserialize verification response.")
                : Result.Ok(authResponse);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error verifying employee code for {Email}", email);
            return Result.Fail<EmployeeAuthResponse>("An unexpected error occurred. Please try again.");
        }
    }
}
