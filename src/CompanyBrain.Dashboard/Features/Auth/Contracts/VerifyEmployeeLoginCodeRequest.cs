namespace CompanyBrain.Dashboard.Features.Auth.Contracts;

public sealed record VerifyEmployeeLoginCodeRequest(string Email, string Code);
