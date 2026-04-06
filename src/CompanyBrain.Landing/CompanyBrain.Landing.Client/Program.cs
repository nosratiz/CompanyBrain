using CompanyBrain.Landing.Client.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddMudServices();
builder.Services.AddAuthorizationCore();

// Auth state
builder.Services.AddSingleton<TokenAuthStateProvider>();
builder.Services.AddSingleton<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<TokenAuthStateProvider>());

// HttpClient → points at the multi-tenant API
builder.Services.AddScoped(sp =>
    new HttpClient { BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5130") });

// API client
builder.Services.AddScoped<ApiClient>();

await builder.Build().RunAsync();
