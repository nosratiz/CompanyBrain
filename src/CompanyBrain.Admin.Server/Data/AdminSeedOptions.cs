namespace CompanyBrain.Admin.Server.Data;

internal sealed class AdminSeedOptions
{
    public const string SectionName = "SeedData";

    public bool Enabled { get; init; } = true;
    public string Email { get; init; } = "demo@companybrain.local";
    public string Password { get; init; } = "Password123!";
    public string FullName { get; init; } = "Demo User";
}