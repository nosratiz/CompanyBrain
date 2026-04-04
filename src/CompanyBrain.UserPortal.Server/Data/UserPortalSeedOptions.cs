namespace CompanyBrain.UserPortal.Server.Data;

internal sealed class UserPortalSeedOptions
{
    public const string SectionName = "SeedData";

    public bool Enabled { get; init; } = true;
    public string Email { get; init; } = "demo@companybrain.local";
    public string Password { get; init; } = "Password123!";
    public string FullName { get; init; } = "Demo User";
}