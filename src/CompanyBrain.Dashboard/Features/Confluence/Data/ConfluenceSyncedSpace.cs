namespace CompanyBrain.Dashboard.Features.Confluence.Data;

public sealed class ConfluenceSyncedSpace
{
    public int Id { get; set; }
    public string SpaceId { get; set; } = string.Empty;
    public string SpaceKey { get; set; } = string.Empty;
    public string SpaceName { get; set; } = string.Empty;
    public string LocalPath { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public int SyncedPageCount { get; set; }
    public long SyncedSizeBytes { get; set; }
    public DateTimeOffset? LastSyncedAtUtc { get; set; }
    public string? LastSyncError { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<ConfluenceSyncedPage> SyncedPages { get; set; } = [];
}
