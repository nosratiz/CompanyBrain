using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using Blazored.LocalStorage;
using CompanyBrain.AdminPanel.Configuration;
using CompanyBrain.AdminPanel;
using CompanyBrain.AdminPanel.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

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

builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = backendApiBaseUri
});

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ILicenseService, LicenseService>();
builder.Services.AddScoped<IApiKeyService, ApiKeyService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<AuthStateProvider>();

await builder.Build().RunAsync();
