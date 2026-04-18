namespace CompanyBrain.Dashboard.Features.Auth.Contracts;

public sealed record AuthResponse(
    string Token,
    UserInfo User);
