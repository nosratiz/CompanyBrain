namespace CompanyBrain.Admin.Server.Api.Contracts.Auth;

public sealed record RegisterRequest(string Email, string Password, string FullName);