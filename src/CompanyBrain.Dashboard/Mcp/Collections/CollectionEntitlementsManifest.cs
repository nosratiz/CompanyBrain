using CompanyBrain.Dashboard.Features.License;

namespace CompanyBrain.Dashboard.Mcp.Collections;

public sealed record CollectionEntitlementsManifest(
    LicenseTier Tier,
    string? Team,
    IReadOnlyList<string> AllowedCollections,
    IReadOnlyDictionary<string, IReadOnlyList<string>> DepartmentCollectionAccess,
    DateTimeOffset RetrievedAtUtc)
{
    public bool CanAccessCollection(string collectionId, string? team)
    {
        if (string.Equals(collectionId, "General", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (Tier == LicenseTier.Free)
        {
            return false;
        }

        if (Tier == LicenseTier.Professional)
        {
            var normalized = AllowedCollections
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return normalized.Contains(collectionId);
        }

        if (Tier == LicenseTier.Enterprise)
        {
            if (string.IsNullOrWhiteSpace(team) || DepartmentCollectionAccess.Count == 0)
            {
                return true;
            }

            return DepartmentCollectionAccess.TryGetValue(team, out var departmentCollections)
                && departmentCollections.Contains(collectionId, StringComparer.OrdinalIgnoreCase);
        }

        return false;
    }
}