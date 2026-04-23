using CompanyBrain.Dashboard.Data;
using CompanyBrain.Dashboard.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CompanyBrain.Dashboard.Services;

public sealed class CollectionPolicyService(
    IDbContextFactory<DocumentAssignmentDbContext> contextFactory)
{
    public async Task<IReadOnlyList<CollectionPolicy>> GetByCollectionAsync(
        string collectionId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.CollectionPolicies
            .AsNoTracking()
            .Where(policy => policy.CollectionId == collectionId)
            .OrderBy(policy => policy.Department)
            .ToListAsync(cancellationToken);
    }

    public async Task<CollectionPolicy> UpsertAsync(
        string collectionId,
        string department,
        int privacyAggressionPercent,
        bool isSyncing,
        CancellationToken cancellationToken = default)
    {
        var clampedPrivacy = Math.Clamp(privacyAggressionPercent, 0, 100);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.CollectionPolicies
            .FirstOrDefaultAsync(
                policy => policy.CollectionId == collectionId && policy.Department == department,
                cancellationToken);

        if (existing is null)
        {
            existing = new CollectionPolicy
            {
                CollectionId = collectionId,
                Department = department,
            };
            db.CollectionPolicies.Add(existing);
        }

        existing.PrivacyAggressionPercent = clampedPrivacy;
        existing.IsSyncing = isSyncing;
        existing.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return existing;
    }
}