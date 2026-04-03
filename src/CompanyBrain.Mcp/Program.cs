using CompanyBrain.DependencyInjection;
using CompanyBrain.Resources;
using CompanyBrain.Mcp.Tools;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5003, listenOptions =>
    {
        listenOptions.UseHttps();
    });
});

builder.Services.AddCompanyBrainCore(AppContext.BaseDirectory);
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithTools<CompanyBrainTools>()
    .WithListResourcesHandler(KnowledgeResourceHandlers.ListResourcesAsync)
    .WithReadResourceHandler(KnowledgeResourceHandlers.ReadResourceAsync);

var app = builder.Build();

app.MapMcp();

await app.RunAsync();