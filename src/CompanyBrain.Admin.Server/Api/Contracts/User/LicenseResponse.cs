namespace CompanyBrain.Admin.Server.Api.Contracts.User;

public sealed record LicenseResponse
{
    public Guid Id { get; init; }
    public required string PlanName { get; init; }
    public required string Tier { get; init; }
    public DateTime PurchasedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public int MaxApiKeys { get; init; }
    public int MaxDocuments { get; init; }
    public long MaxStorageBytes { get; init; }
    public bool IsActive { get; init; }
}