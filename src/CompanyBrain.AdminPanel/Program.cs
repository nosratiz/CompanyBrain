using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using Blazored.LocalStorage;
using CompanyBrain.AdminPanel.Configuration;
using CompanyBrain.AdminPanel;
using CompanyBrain.AdminPanel.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Logging.SetMinimumLevel(LogLevel.Information);
builder.Logging.AddFilter("Microsoft", LogLevel.Warning);

builder.Services.AddMudServices();
builder.Services.AddBlazoredLocalStorage();

var backendApiOptions = builder.Configuration
    .GetSection(BackendApiOptions.SectionName)
    .Get<BackendApiOptions>()
    ?? new BackendApiOptions();

if (!Uri.TryCreate(backendApiOptions.BaseUrl, UriKind.Absolute, out var backendApiBaseUri))
{
    throw new InvalidOperationException("BackendApi:BaseUrl must be a valid absolute URL.");
}

builder.Services.AddScoped<AuthStateProvider>();
builder.Services.AddScoped<ApiLoggingHandler>();
builder.Services.AddScoped<UnauthorizedRedirectHandler>();
builder.Services.AddHttpClient("Default", client =>
{
    client.BaseAddress = backendApiBaseUri;
})
.AddHttpMessageHandler<ApiLoggingHandler>()
.AddHttpMessageHandler<UnauthorizedRedirectHandler>();

builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("Default"));

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAdminService, AdminService>();

await builder.Build().RunAsync();
