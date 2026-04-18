namespace CompanyBrain.Dashboard.Features.Auth.Contracts;

public sealed record LoginRequest(
    string Email,
    string Password);
