namespace CompanyBrain.Admin.Server.Api.Contracts.Admin;

public sealed record UpdateUserRequest(string? FullName, string? Email);

public sealed record SetUserStatusRequest(bool IsActive);

public sealed record AssignLicenseRequest(Guid UserId, string Tier);

public sealed record UserDetailResponse
{
    public Guid Id { get; init; }
    public required string Email { get; init; }
    public required string FullName { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? LastLoginAt { get; init; }
    public bool IsActive { get; init; }
    public IReadOnlyList<LicenseSummary> Licenses { get; init; } = [];
}

public sealed record LicenseSummary
{
    public Guid Id { get; init; }
    public required string PlanName { get; init; }
    public required string Tier { get; init; }
    public bool IsActive { get; init; }
    public DateTime? ExpiresAt { get; init; }
}

public sealed record LicenseDetailResponse
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string? UserEmail { get; init; }
    public string? UserFullName { get; init; }
    public required string PlanName { get; init; }
    public required string Tier { get; init; }
    public DateTime PurchasedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public int MaxApiKeys { get; init; }
    public int MaxDocuments { get; init; }
    public long MaxStorageBytes { get; init; }
    public bool IsActive { get; init; }
}

public sealed record PaginatedResponse<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
