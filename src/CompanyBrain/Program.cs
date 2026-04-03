using CompanyBrain.Api;
using CompanyBrain.Api.Serialization;
using CompanyBrain.DependencyInjection;
using CompanyBrain.Mcp.Resources;
using CompanyBrain.Mcp.Tools;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
    options.IncludeScopes = true;
    options.SingleLine = true;
});

builder.Services.AddCompanyBrain(builder.Environment.ContentRootPath);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, CompanyBrainJsonSerializerContext.Default);
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Company Brain API",
        Version = "v1",
        Description = "HTTP API for ingesting internal knowledge, browsing stored Markdown resources, and searching the company knowledge base. Also serves as an MCP server.",
    });
});

// Configure CORS to allow all origins
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Configure MCP Server
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithTools<CompanyBrainTools>()
    .WithListResourcesHandler(KnowledgeResourceHandlers.ListResourcesAsync)
    .WithReadResourceHandler(KnowledgeResourceHandlers.ReadResourceAsync);

var app = builder.Build();

app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/", () => Results.Redirect("/swagger"));
app.MapCompanyBrainApi();
app.MapMcp();

app.Run();