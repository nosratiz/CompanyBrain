// -----------------------------------------------------------------------------
//  DeepRoot.Photino — desktop shell entry point.
//
//  Boots the full CompanyBrain.Dashboard WebApplication on a private
//  localhost port, then opens a Photino native window pointed at it.
// -----------------------------------------------------------------------------

using System.Net;
using System.Net.Sockets;
using CompanyBrain.Dashboard.DependencyInjection;
using DeepRoot.Photino;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Photino.NET;

// 1. Reserve a free localhost port up-front so we can hand it to Photino.
int port = GetFreeTcpPort();
string baseUrl = $"http://127.0.0.1:{port}";

// 2. Build the Dashboard WebApplication exactly the way its own Program.cs does.
var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls(baseUrl);

// Static Web Assets from referenced projects (Dashboard wwwroot, MudBlazor,
// Blazor framework JS) are only auto-loaded in the Development environment.
// We need them in every environment for the desktop shell, so opt in here.
builder.WebHost.UseStaticWebAssets();

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(o =>
{
    o.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
    o.IncludeScopes   = true;
    o.SingleLine      = true;
});

builder.Services.AddDashboardServices(builder.Configuration, builder.Environment);

var app = builder.Build();

await app.InitializeDashboardDatabasesAsync();

app.UseDashboardMiddleware();
app.MapDashboardEndpoints();

// 3. Start Kestrel in the background, then resolve the actually bound URL.
await app.StartAsync();

string startUrl = app.Services.GetRequiredService<IServer>()
                              .Features.Get<IServerAddressesFeature>()
                              ?.Addresses.FirstOrDefault()
              ?? baseUrl;

app.Logger.LogInformation("DeepRoot desktop shell listening at {Url}", startUrl);

// 4. Open the native Photino window pointing at the Dashboard.
var window = new PhotinoWindow()
    .SetTitle("DeepRoot")
    .SetUseOsDefaultSize(false)
    .SetSize(width: 1400, height: 900)
    .SetUseOsDefaultLocation(true)
    .SetResizable(true)
    .SetContextMenuEnabled(false)
    .SetDevToolsEnabled(builder.Environment.IsDevelopment());

string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", PlatformIcon());
if (File.Exists(iconPath))
    window.SetIconFile(iconPath); // window titlebar proxy icon

window.RegisterWindowCreatedHandler((_, _) =>
{
    // The Cocoa run loop is now active and Photino's NSApplication is initialised.
    // setApplicationIconImage: must be called here (not before WaitForClose) so the
    // Dock distributed notification fires correctly.
    if (OperatingSystem.IsMacOS() && File.Exists(iconPath))
        MacOsIcon.SetDockIcon(iconPath);
});

window.RegisterWindowClosingHandler((_, _) =>
{
    try
    {
        app.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        ((IAsyncDisposable)app).DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Error during desktop shell shutdown");
    }
    return false; // allow close to proceed
});

window.Load(new Uri(startUrl)).WaitForClose();

// ---------------------------------------------------------------------------
static int GetFreeTcpPort()
{
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    int p = ((IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
    return p;
}

static string PlatformIcon() =>
    OperatingSystem.IsWindows() ? "deeproot.ico" :
    OperatingSystem.IsMacOS()   ? "deeproot.icns" :
                                  "deeproot.png";
