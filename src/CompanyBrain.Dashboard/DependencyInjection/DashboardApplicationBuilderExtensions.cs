using CompanyBrain.Dashboard.Data;
using CompanyBrain.Dashboard.Middleware;

namespace CompanyBrain.Dashboard.DependencyInjection;

/// <summary>
/// Extension methods for configuring the Dashboard application pipeline.
/// </summary>
public static class DashboardApplicationBuilderExtensions
{
    /// <summary>
    /// Configures the Dashboard middleware pipeline.
    /// </summary>
    public static WebApplication UseDashboardMiddleware(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseGlobalExceptionHandler();
        app.UseRequestLogging();
        app.UseSecurityHeaders();

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseHsts();
        }

        app.UseCors();
        app.UseSwagger();
        app.UseSwaggerUI();

        app.UseStaticFiles();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();

        return app;
    }

    /// <summary>
    /// Ensures the SQLite database is created on startup.
    /// </summary>
    public static async Task<WebApplication> InitializeDatabaseAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocumentAssignmentDbContext>();
        await db.Database.EnsureCreatedAsync();

        return app;
    }
}
