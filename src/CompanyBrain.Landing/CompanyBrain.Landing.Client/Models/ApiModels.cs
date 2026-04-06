namespace CompanyBrain.Landing.Client.Models;

// ── Auth ──
public sealed class LoginRequest
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

public sealed class RegisterRequest
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public Guid? TenantId { get; set; }
}

public sealed class AuthResponse
{
    public string Token { get; set; } = "";
    public UserResponse User { get; set; } = new();
}

public sealed class UserResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public Guid TenantId { get; set; }
    public string Role { get; set; } = "User";
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

// ── Profile ──
public sealed class ProfileResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public Guid TenantId { get; set; }
    public string? TenantName { get; set; }
    public string Role { get; set; } = "User";
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; }
}

public sealed class UpdateProfileRequest
{
    public string DisplayName { get; set; } = "";
}

public sealed class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = "";
    public string NewPassword { get; set; } = "";
}

// ── Tenant ──
public sealed class CreateTenantRequest
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Plan { get; set; } = "Free";
}

public sealed class TenantResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string? Description { get; set; }
    public string Status { get; set; } = "Active";
    public string Plan { get; set; } = "Free";
    public int MaxDocuments { get; set; }
    public int MaxApiKeys { get; set; }
    public long MaxStorageBytes { get; set; }
    public DateTime CreatedAt { get; set; }
    public int ActiveApiKeys { get; set; }
    public int ActiveUsers { get; set; }
}

public sealed class TenantSummaryResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Status { get; set; } = "Active";
    public string Plan { get; set; } = "Free";
    public DateTime CreatedAt { get; set; }
}

public sealed class TenantListResponse
{
    public List<TenantSummaryResponse> Tenants { get; set; } = [];
}

public sealed class UpdateTenantPlanRequest
{
    public string Plan { get; set; } = "";
}

// ── API Keys ──
public sealed class CreateApiKeyRequest
{
    public string Name { get; set; } = "";
    public string Scope { get; set; } = "ReadOnly";
    public DateTime? ExpiresAt { get; set; }
}

public sealed class ApiKeyCreatedResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string KeyPrefix { get; set; } = "";
    public string PlainKey { get; set; } = "";
    public string Scope { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? Warning { get; set; }
}

public sealed class ApiKeyResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string KeyPrefix { get; set; } = "";
    public string Scope { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public bool IsRevoked { get; set; }
    public int RequestsPerMinute { get; set; }
    public int RequestsPerDay { get; set; }
}

public sealed class ApiKeyListResponse
{
    public List<ApiKeyResponse> ApiKeys { get; set; } = [];
}

public sealed class RevokeApiKeyRequest
{
    public string? Reason { get; set; }
}

// ── Storage ──
public sealed class TenantStorageStatsResponse
{
    public int DocumentCount { get; set; }
    public long TotalBytes { get; set; }
    public string FormattedSize { get; set; } = "";
    public long MaxBytes { get; set; }
    public double UsagePercentage { get; set; }
}

// ── MCP Connection ──
public sealed class McpConnectionInfoResponse
{
    public string ServerUrl { get; set; } = "";
    public string Protocol { get; set; } = "";
    public List<string> SupportedClients { get; set; } = [];
    public string Instructions { get; set; } = "";
}
