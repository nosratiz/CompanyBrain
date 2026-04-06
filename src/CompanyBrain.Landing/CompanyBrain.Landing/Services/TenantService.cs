namespace CompanyBrain.Landing.Services;

public interface ITenantService
{
    TenantConfiguration ResolveTenant(HttpContext context);
}

public sealed class TenantService : ITenantService
{
    private readonly Dictionary<string, TenantConfiguration> _tenants;

    public TenantService(IConfiguration configuration)
    {
        _tenants = new Dictionary<string, TenantConfiguration>(StringComparer.OrdinalIgnoreCase);

        var tenantsSection = configuration.GetSection("Tenants");
        if (tenantsSection.Exists())
        {
            foreach (var child in tenantsSection.GetChildren())
            {
                var config = new TenantConfiguration();
                child.Bind(config);
                config.TenantSlug = child.Key.ToLowerInvariant();
                _tenants[config.TenantSlug] = config;
            }
        }
    }

    public TenantConfiguration ResolveTenant(HttpContext context)
    {
        // 1. Try X-Tenant-Slug header
        if (context.Request.Headers.TryGetValue("X-Tenant-Slug", out var headerSlug)
            && !string.IsNullOrWhiteSpace(headerSlug))
        {
            if (_tenants.TryGetValue(headerSlug!, out var headerTenant))
                return headerTenant;
        }

        // 2. Try subdomain: acme.companybrain.dev → "acme"
        var host = context.Request.Host.Host;
        var parts = host.Split('.');
        if (parts.Length >= 3)
        {
            var subdomain = parts[0];
            if (_tenants.TryGetValue(subdomain, out var subTenant))
                return subTenant;
        }

        // 3. Try query string: ?tenant=acme
        if (context.Request.Query.TryGetValue("tenant", out var querySlug)
            && !string.IsNullOrWhiteSpace(querySlug))
        {
            if (_tenants.TryGetValue(querySlug!, out var queryTenant))
                return queryTenant;
        }

        // 4. Default tenant
        return new TenantConfiguration();
    }
}
