using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using CompanyBrain.DependencyInjection;
using CompanyBrain.Resources;
using CompanyBrain.Mcp.Tools;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddDebug();

builder.Services.AddCompanyBrainCore(AppContext.BaseDirectory);
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<CompanyBrainTools>()
    .WithListResourcesHandler(KnowledgeResourceHandlers.ListResourcesAsync)
    .WithReadResourceHandler(KnowledgeResourceHandlers.ReadResourceAsync);

await builder.Build().RunAsync();