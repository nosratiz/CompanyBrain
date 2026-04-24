using CompanyBrain.Dashboard.Data;
using CompanyBrain.Dashboard.Features.ChatRelay.Models;
using Microsoft.EntityFrameworkCore;

namespace CompanyBrain.Dashboard.Features.ChatRelay.Services;

/// <summary>
/// SQLite-backed implementation of <see cref="IConversationThreadRepository"/>.
/// Each call opens a fresh <see cref="DocumentAssignmentDbContext"/> so this class
/// is safe to register as a singleton and call from background workers.
/// </summary>
public sealed class ConversationThreadRepository(
    IDbContextFactory<DocumentAssignmentDbContext> dbContextFactory,
    ILogger<ConversationThreadRepository> logger) : IConversationThreadRepository
{
    public async Task<ConversationThread?> FindAsync(
        ChatPlatform platform,
        string externalThreadId,
        CancellationToken ct)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        return await db.ConversationThreads
            .AsNoTracking()
            .FirstOrDefaultAsync(
                t => t.Platform == platform && t.ExternalThreadId == externalThreadId && t.IsActive,
                ct);
    }

    public async Task<ConversationThread> GetOrCreateAsync(
        ChatPlatform platform,
        string externalThreadId,
        string channelId,
        string serviceUrl,
        CancellationToken ct)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var thread = await db.ConversationThreads
            .FirstOrDefaultAsync(
                t => t.Platform == platform && t.ExternalThreadId == externalThreadId && t.IsActive,
                ct);

        if (thread is not null)
            return thread;

        thread = new ConversationThread
        {
            Platform = platform,
            ExternalThreadId = externalThreadId,
            ExternalChannelId = channelId,
            ServiceUrl = serviceUrl,
            SessionId = Guid.NewGuid().ToString("N"),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            LastActivityUtc = DateTime.UtcNow,
        };

        db.ConversationThreads.Add(thread);
        await db.SaveChangesAsync(ct);

        logger.LogDebug(
            "Created conversation thread {Id} for {Platform} thread {ExternalId}",
            thread.Id, platform, externalThreadId);

        return thread;
    }

    public async Task TouchAsync(int threadId, CancellationToken ct)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var thread = await db.ConversationThreads.FindAsync([threadId], ct);
        if (thread is null) return;

        thread.LastActivityUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ConversationThread>> GetActiveThreadsAsync(CancellationToken ct)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        return await db.ConversationThreads
            .Where(t => t.IsActive)
            .AsNoTracking()
            .OrderByDescending(t => t.LastActivityUtc)
            .ToListAsync(ct);
    }

    public async Task RevokeAllAsync(ChatPlatform? platform, CancellationToken ct)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var query = db.ConversationThreads.Where(t => t.IsActive);

        if (platform.HasValue)
            query = query.Where(t => t.Platform == platform.Value);

        var threads = await query.ToListAsync(ct);
        foreach (var t in threads)
            t.IsActive = false;

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Revoked {Count} conversation threads (platform filter: {Platform})",
            threads.Count, platform?.ToString() ?? "all");
    }

    public async Task RevokeAsync(int threadId, CancellationToken ct)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var thread = await db.ConversationThreads.FindAsync([threadId], ct);
        if (thread is null) return;

        thread.IsActive = false;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Revoked conversation thread {Id}", threadId);
    }
}
