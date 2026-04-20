using CompanyBrain.Dashboard.Features.AutoSetup.Services;

namespace CompanyBrain.Dashboard.Features.AutoSetup.Api;

/// <summary>
/// One-Click Setup API that orchestrates Claude Desktop MCP injection,
/// M365 device-code authentication, and Copilot manifest generation.
/// </summary>
public static class AutoSetupApi
{
    public static WebApplication MapAutoSetupApi(this WebApplication app)
    {
        var group = app.MapGroup("/api/setup")
            .WithTags("Auto-Setup");

        // ── Status ──────────────────────────────────────────────────────────
        group.MapGet("/status", GetSetupStatusAsync)
            .WithName("GetSetupStatus")
            .WithDescription("Returns the current configuration status of all integrations.");

        // ── One-Click ───────────────────────────────────────────────────────
        group.MapPost("/one-click", RunOneClickSetupAsync)
            .WithName("RunOneClickSetup")
            .WithDescription("Runs Claude Desktop injection and Copilot manifest generation in parallel.");

        // ── Claude Desktop ──────────────────────────────────────────────────
        group.MapPost("/claude", ConfigureClaudeAsync)
            .WithName("ConfigureClaude")
            .WithDescription("Injects CompanyBrain MCP server into Claude Desktop config.");

        group.MapDelete("/claude", RemoveClaudeAsync)
            .WithName("RemoveClaude")
            .WithDescription("Removes CompanyBrain from Claude Desktop config.");

        // ── M365 Device Code ────────────────────────────────────────────────
        group.MapPost("/m365/device-code", StartDeviceCodeFlowAsync)
            .WithName("StartM365DeviceCode")
            .WithDescription("Starts the Microsoft 365 device code authentication flow.");

        // ── Copilot Manifest ────────────────────────────────────────────────
        group.MapPost("/copilot-manifest", GenerateCopilotManifestAsync)
            .WithName("GenerateCopilotManifest")
            .WithDescription("Generates a Microsoft Copilot Agent manifest.");

        return app;
    }

    /// <summary>
    /// GET /api/setup/status — integration health dashboard.
    /// </summary>
    private static async Task<IResult> GetSetupStatusAsync(
        ClaudeHandshakeService claude,
        CopilotManifestService copilot,
        CancellationToken cancellationToken)
    {
        var claudeConfigured = await claude.IsConfiguredAsync(cancellationToken);
        var copilotManifestExists = copilot.ManifestExists();

        return Results.Ok(new
        {
            ClaudeDesktop = new
            {
                Configured = claudeConfigured,
                ConfigPath = ClaudeHandshakeService.GetClaudeConfigPath()
            },
            CopilotAgent = new
            {
                ManifestGenerated = copilotManifestExists,
                ManifestPath = copilot.GetManifestPath()
            }
        });
    }

    /// <summary>
    /// POST /api/setup/one-click — parallel setup of Claude + Copilot manifest.
    /// M365 device code is separate because it requires user interaction.
    /// </summary>
    private static async Task<IResult> RunOneClickSetupAsync(
        ClaudeHandshakeService claude,
        CopilotManifestService copilot,
        CancellationToken cancellationToken)
    {
        // Run Claude injection and Copilot manifest generation in parallel
        var claudeTask = claude.ConfigureAsync(cancellationToken);
        var copilotTask = copilot.GenerateManifestAsync(cancellationToken: cancellationToken);

        await Task.WhenAll(claudeTask, copilotTask);

        var claudeResult = await claudeTask;
        var copilotResult = await copilotTask;

        return Results.Ok(new
        {
            Claude = new
            {
                claudeResult.Success,
                claudeResult.Message,
                claudeResult.ConfigPath,
                claudeResult.BackupPath
            },
            CopilotManifest = new
            {
                copilotResult.Success,
                copilotResult.Message,
                copilotResult.ManifestPath
            },
            OverallSuccess = claudeResult.Success && copilotResult.Success,
            NextStep = "Use POST /api/setup/m365/device-code to connect Microsoft 365 (requires user interaction)."
        });
    }

    /// <summary>
    /// POST /api/setup/claude — inject MCP server into Claude Desktop.
    /// </summary>
    private static async Task<IResult> ConfigureClaudeAsync(
        ClaudeHandshakeService claude,
        CancellationToken cancellationToken)
    {
        var result = await claude.ConfigureAsync(cancellationToken);
        return result.Success ? Results.Ok(result) : Results.UnprocessableEntity(result);
    }

    /// <summary>
    /// DELETE /api/setup/claude — remove MCP server from Claude Desktop.
    /// </summary>
    private static async Task<IResult> RemoveClaudeAsync(
        ClaudeHandshakeService claude,
        CancellationToken cancellationToken)
    {
        var result = await claude.RemoveAsync(cancellationToken);
        return result.Success ? Results.Ok(result) : Results.UnprocessableEntity(result);
    }

    /// <summary>
    /// POST /api/setup/m365/device-code — start device code flow.
    /// Returns the user code and verification URL. The caller must display these
    /// to the user and poll for completion (the endpoint blocks until auth completes or times out).
    /// </summary>
    private static async Task<IResult> StartDeviceCodeFlowAsync(
        M365DeviceCodeAuthService m365,
        CancellationToken cancellationToken)
    {
        string? userCode = null;
        string? verificationUrl = null;
        string? message = null;

        var result = await m365.AuthenticateWithDeviceCodeAsync(
            (code, url, msg) =>
            {
                userCode = code;
                verificationUrl = url;
                message = msg;
            },
            cancellationToken);

        if (result.Success)
        {
            return Results.Ok(new
            {
                result.Success,
                result.Message,
                result.UserPrincipalName,
                DiscoveredSites = result.DiscoveredSites?.Select(s => new
                {
                    s.SiteId,
                    s.DisplayName,
                    s.WebUrl,
                    s.IsRootSite
                })
            });
        }

        // If auth is pending, return the device code info for the user to act on
        if (userCode is not null)
        {
            return Results.Ok(new
            {
                Success = false,
                Pending = true,
                UserCode = userCode,
                VerificationUrl = verificationUrl,
                Message = message ?? "Go to the URL and enter the code to sign in."
            });
        }

        return Results.UnprocessableEntity(new
        {
            result.Success,
            result.Message
        });
    }

    /// <summary>
    /// POST /api/setup/copilot-manifest — generate Copilot Agent manifest.
    /// </summary>
    private static async Task<IResult> GenerateCopilotManifestAsync(
        CopilotManifestService copilot,
        CancellationToken cancellationToken)
    {
        var result = await copilot.GenerateManifestAsync(cancellationToken: cancellationToken);
        return result.Success ? Results.Ok(result) : Results.UnprocessableEntity(result);
    }
}
