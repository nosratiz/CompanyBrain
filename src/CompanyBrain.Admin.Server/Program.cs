using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using CompanyBrain.Admin.Server.Api.Contracts.Auth;
using CompanyBrain.Admin.Server.Api.Contracts.User;
using CompanyBrain.Admin.Server.Data;
using CompanyBrain.Admin.Server.Logging;
using CompanyBrain.Admin.Server.Services;
using CompanyBrain.Admin.Server.Services.Interfaces;
using CompanyBrain.Admin.Server.Api;
using CompanyBrain.Admin.Server.Api.Validation;
using FluentValidation;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting CompanyBrain Admin Server");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, loggerConfiguration) =>
    {
        loggerConfiguration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithEnvironmentName()
            .Enrich.WithMachineName()
            .Enrich.WithProcessId()
            .Enrich.WithThreadId()
            .Enrich.WithProperty("Application", "CompanyBrain.Admin.Server");
    });

    // Configure services
    builder.Services.AddDbContext<UserDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("AdminDb")
            ?? "Host=localhost;Port=5432;Database=companybrain_admin;Username=postgres;Password=postgres"));

    // JWT Authentication
    var jwtKey = builder.Configuration["Jwt:Key"] ?? "CompanyBrain_Admin_Secret_Key_Min32Chars!";
    var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "CompanyBrain.Admin";

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtIssuer,
                ValidAudience = jwtIssuer,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
            };
        });

    builder.Services.AddAuthorization();

    // Services
    builder.Services.AddScoped<IUserService, UserService>();
    builder.Services.AddScoped<IUserLicenseService, UserLicenseService>();
    builder.Services.AddScoped<IUserApiKeyService, UserApiKeyService>();
    builder.Services.AddScoped<IJwtService, JwtService>();
    builder.Services.AddScoped<IValidator<RegisterRequest>, RegisterRequestValidator>();
    builder.Services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();
    builder.Services.AddScoped<IValidator<PurchaseLicenseRequest>, PurchaseLicenseRequestValidator>();
    builder.Services.AddScoped<IValidator<CreateApiKeyRequest>, CreateApiKeyRequestValidator>();

    // OpenAPI
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Company Brain Admin API",
            Version = "v1"
        });
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
        });
        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                },
                Array.Empty<string>()
            }
        });
    });

    // CORS
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    });

    var app = builder.Build();

    app.Lifetime.ApplicationStarted.Register(() =>
    {
        var addresses = app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()?
            .Addresses;

        Log.Information(
            "CompanyBrain Admin Server started in {Environment} on {Addresses}",
            app.Environment.EnvironmentName,
            addresses is { Count: > 0 } ? string.Join(", ", addresses) : "unknown");
    });

    app.Lifetime.ApplicationStopping.Register(() =>
    {
        Log.Information("CompanyBrain Admin Server is stopping");
    });

    app.Lifetime.ApplicationStopped.Register(() =>
    {
        Log.Information("CompanyBrain Admin Server stopped");
    });

    await AdminDataSeeder.SeedAsync(app.Services, app.Configuration);

    // Configure pipeline
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "Handled {RequestMethod} {RequestPath} => {StatusCode} in {Elapsed:0.0000} ms";
        options.EnrichDiagnosticContext = AdminRequestLogging.EnrichDiagnosticContext;
    });

    app.UseHttpsRedirection();
    app.UseCors();

    app.UseAuthentication();
    app.UseAuthorization();

    // Map API endpoints
    app.MapAuthApi();
    app.MapUserApi();
    app.MapAdminApi();

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "CompanyBrain Admin Server terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
