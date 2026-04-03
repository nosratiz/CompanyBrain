namespace CompanyBrain.MultiTenant.Abstractions;

/// <summary>
/// Provides access to the current tenant context.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// The current tenant ID, or null if not in a tenant context.
    /// </summary>
    Guid? TenantId { get; }

    /// <summary>
    /// The current tenant slug, or null if not in a tenant context.
    /// </summary>
    string? TenantSlug { get; }

    /// <summary>
    /// Whether we're currently in a valid tenant context.
    /// </summary>
    bool HasTenant => TenantId.HasValue;
}

/// <summary>
/// Allows setting the tenant context (for middleware use).
/// </summary>
public interface ITenantContextAccessor : ITenantContext
{
    void SetTenant(Guid tenantId, string tenantSlug);
    void Clear();
}
