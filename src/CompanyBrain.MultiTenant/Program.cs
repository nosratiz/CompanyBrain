using System.Text;
using CompanyBrain.MultiTenant.Api;
using CompanyBrain.MultiTenant.Api.Validation;
using CompanyBrain.MultiTenant.DependencyInjection;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] 
    ?? throw new InvalidOperationException("JWT key not configured");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "CompanyBrain.MultiTenant";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() 
    { 
        Title = "CompanyBrain MultiTenant API", 
        Version = "v1",
        Description = "Multi-tenant management API with authentication"
    });

    options.AddSecurityDefinition("Bearer", new()
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new()
    {
        {
            new()
            {
                Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// Multi-tenant services
var connectionString = builder.Configuration.GetConnectionString("TenantDb") 
    ?? "Host=localhost;Port=5432;Database=companybrain_admin;Username=postgres;Password=123qweQWE";
var storagePath = builder.Configuration["Storage:BasePath"] ?? "data/tenants";

builder.Services.AddCompanyBrainMultiTenant(connectionString, storagePath);
builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>(includeInternalTypes: true);

// Health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Ensure database is created
await app.Services.EnsureTenantDatabaseAsync();

// Swagger (all environments for API discoverability)
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "CompanyBrain MultiTenant API v1");
    options.RoutePrefix = "swagger";
});

app.UseAuthentication();
app.UseAuthorization();

// Map endpoints
app.MapHealthChecks("/health");

var mcpServerUrl = builder.Configuration["Mcp:ServerUrl"] ?? "http://localhost:8080/mcp";
app.MapTenantApi(mcpServerUrl);
app.MapAuthApi();
app.MapProfileApi();

// Redirect root to Swagger
app.MapGet("/", () => Results.Redirect("/swagger"));

app.Run();
