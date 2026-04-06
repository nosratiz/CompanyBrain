namespace CompanyBrain.Landing.Services;

public sealed class TenantMiddleware(ITenantService tenantService) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var tenant = tenantService.ResolveTenant(context);
        context.Items["TenantConfig"] = tenant;
        await next(context);
    }
}
