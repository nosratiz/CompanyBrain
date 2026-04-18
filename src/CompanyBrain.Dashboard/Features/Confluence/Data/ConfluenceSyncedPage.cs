namespace CompanyBrain.Dashboard.Features.Confluence.Data;

public sealed class ConfluenceSyncedPage
{
    public int Id { get; set; }
    public int SyncedSpaceId { get; set; }
    public string PageId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string LocalPath { get; set; } = string.Empty;
    public int RemoteVersion { get; set; }
    public DateTimeOffset RemoteUpdatedAt { get; set; }
    public DateTimeOffset LastSyncedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ConfluenceSyncedSpace SyncedSpace { get; set; } = null!;
}
