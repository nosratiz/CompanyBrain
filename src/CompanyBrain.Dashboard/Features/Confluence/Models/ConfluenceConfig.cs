namespace CompanyBrain.Dashboard.Features.Confluence.Models;

public sealed class ConfluenceSyncOptions
{
    public const string SectionName = "ConfluenceSync";

    public string Domain { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ApiToken { get; set; } = string.Empty;
    public string LocalBasePath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "CompanyBrain", "Confluence");
    public int SyncIntervalMinutes { get; set; } = 60;
}
