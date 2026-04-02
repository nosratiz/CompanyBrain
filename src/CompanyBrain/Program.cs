using CompanyBrain.Api;
using CompanyBrain.Api.Serialization;
using CompanyBrain.DependencyInjection;

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
        Description = "HTTP API for ingesting internal knowledge, browsing stored Markdown resources, and searching the company knowledge base.",
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/", () => Results.Redirect("/swagger"));
app.MapCompanyBrainApi();

app.Run();