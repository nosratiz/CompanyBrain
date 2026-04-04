namespace CompanyBrain.Admin.Server.Api.Contracts.Auth;

public sealed record LoginRequest(string Email, string Password);