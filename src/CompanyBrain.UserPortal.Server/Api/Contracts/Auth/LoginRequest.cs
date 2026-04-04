namespace CompanyBrain.UserPortal.Server.Api.Contracts.Auth;

public sealed record LoginRequest(string Email, string Password);