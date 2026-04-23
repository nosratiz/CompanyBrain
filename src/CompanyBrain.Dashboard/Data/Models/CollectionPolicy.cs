namespace CompanyBrain.Dashboard.Data.Models;

/// <summary>
/// Stores per-collection privacy and synchronization policy.
/// </summary>
public sealed class CollectionPolicy
{
    public int Id { get; set; }
    public required string CollectionId { get; set; }
    public required string Department { get; set; }
    public int PrivacyAggressionPercent { get; set; } = 50;
    public bool IsSyncing { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}