using CompanyBrain.MultiTenant.Abstractions;

namespace CompanyBrain.MultiTenant.Services;

/// <summary>
/// AsyncLocal-based tenant context for per-request isolation.
/// </summary>
public sealed class TenantContextAccessor : ITenantContextAccessor
{
    private static readonly AsyncLocal<TenantHolder> CurrentHolder = new();

    public Guid? TenantId => CurrentHolder.Value?.TenantId;
    public string? TenantSlug => CurrentHolder.Value?.TenantSlug;

    public void SetTenant(Guid tenantId, string tenantSlug)
    {
        CurrentHolder.Value = new TenantHolder(tenantId, tenantSlug);
    }

    public void Clear()
    {
        CurrentHolder.Value = null;
    }

    private sealed record TenantHolder(Guid TenantId, string TenantSlug);
}
