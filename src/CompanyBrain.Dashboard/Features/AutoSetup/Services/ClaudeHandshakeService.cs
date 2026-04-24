using System.Text.Json;
using System.Text.Json.Nodes;

namespace CompanyBrain.Dashboard.Features.AutoSetup.Services;

/// <summary>
/// Automatically injects the CompanyBrain MCP server configuration into Claude Desktop's config file.
/// Detects the OS-specific config path, creates a timestamped backup, and merges the server entry.
/// </summary>
public sealed class ClaudeHandshakeService(
    ILogger<ClaudeHandshakeService> logger,
    IHostEnvironment environment)
{
    private const string McpServerName = "company-brain";

    /// <summary>
    /// Result of a Claude Desktop handshake attempt.
    /// </summary>
    public sealed record HandshakeResult(
        bool Success,
        string Message,
        string? ConfigPath = null,
        string? BackupPath = null);

    /// <summary>
    /// Injects the CompanyBrain MCP server entry into Claude Desktop's configuration.
    /// </summary>
    public async Task<HandshakeResult> ConfigureAsync(CancellationToken cancellationToken = default)
    {
        var configPath = GetClaudeConfigPath();
        if (configPath is null)
            return new HandshakeResult(false, "Could not determine Claude Desktop config path for this OS.");

        logger.LogInformation("Claude Desktop config path: {Path}", configPath);

        try
        {
            var configDir = Path.GetDirectoryName(configPath)!;

            if (!Directory.Exists(configDir))
                Directory.CreateDirectory(configDir);

            // Load or create config
            JsonObject root;
            if (File.Exists(configPath))
            {
                var existing = await File.ReadAllTextAsync(configPath, cancellationToken);

                // Backup before modification
                var backupPath = await CreateBackupAsync(configPath, existing, cancellationToken);
                logger.LogInformation("Created backup: {BackupPath}", backupPath);

                root = JsonNode.Parse(existing)?.AsObject() ?? new JsonObject();

                if (IsAlreadyConfigured(root))
                {
                    logger.LogInformation("CompanyBrain MCP server already configured in Claude Desktop");
                    return new HandshakeResult(true, "Already configured — no changes needed.", configPath);
                }

                return await MergeAndSave(root, configPath, backupPath, cancellationToken);
            }

            // No existing config — create from scratch
            root = new JsonObject();
            return await MergeAndSave(root, configPath, null, cancellationToken);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Claude Desktop config has invalid JSON");
            return new HandshakeResult(false, $"Claude config has invalid JSON: {ex.Message}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to configure Claude Desktop");
            return new HandshakeResult(false, $"Failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks whether CompanyBrain is currently registered in Claude Desktop config.
    /// </summary>
    public async Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default)
    {
        var configPath = GetClaudeConfigPath();
        if (configPath is null || !File.Exists(configPath))
            return false;

        try
        {
            var content = await File.ReadAllTextAsync(configPath, cancellationToken);
            var root = JsonNode.Parse(content)?.AsObject();
            var servers = root?["mcpServers"]?.AsObject();
            return servers?[McpServerName] is not null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Removes the CompanyBrain entry from Claude Desktop config.
    /// </summary>
    public async Task<HandshakeResult> RemoveAsync(CancellationToken cancellationToken = default)
    {
        var configPath = GetClaudeConfigPath();
        if (configPath is null || !File.Exists(configPath))
            return new HandshakeResult(true, "No Claude config found — nothing to remove.");

        try
        {
            var content = await File.ReadAllTextAsync(configPath, cancellationToken);
            var backupPath = await CreateBackupAsync(configPath, content, cancellationToken);
            var root = JsonNode.Parse(content)?.AsObject();

            if (root?["mcpServers"]?.AsObject() is { } servers && servers.ContainsKey(McpServerName))
            {
                servers.Remove(McpServerName);
                await SaveConfigAsync(root, configPath, cancellationToken);
                return new HandshakeResult(true, "Removed CompanyBrain from Claude Desktop config.", configPath, backupPath);
            }

            return new HandshakeResult(true, "CompanyBrain was not configured — nothing to remove.");
        }
        catch (Exception ex)
        {
            return new HandshakeResult(false, $"Failed to remove: {ex.Message}");
        }
    }

    private async Task<HandshakeResult> MergeAndSave(
        JsonObject root, string configPath, string? backupPath,
        CancellationToken cancellationToken)
    {
        var mcpServers = root["mcpServers"]?.AsObject();
        if (mcpServers is null)
        {
            mcpServers = new JsonObject();
            root["mcpServers"] = mcpServers;
        }

        var (command, args) = GetMcpStdioCommand();
        var argsArray = new JsonArray(args.Select(a => (JsonNode?)JsonValue.Create(a)).ToArray());
        var serverEntry = new JsonObject
        {
            ["command"] = command,
            ["args"] = argsArray
        };

        mcpServers[McpServerName] = serverEntry;

        // Validate before saving
        var serialized = root.ToJsonString(SerializerOptions);
        try
        {
            JsonDocument.Parse(serialized).Dispose();
        }
        catch (JsonException ex)
        {
            return new HandshakeResult(false, $"Generated config has invalid JSON — aborting: {ex.Message}");
        }

        await SaveConfigAsync(root, configPath, cancellationToken);
        logger.LogInformation("Successfully configured CompanyBrain MCP server in Claude Desktop");

        return new HandshakeResult(true, "CompanyBrain MCP server added to Claude Desktop.", configPath, backupPath);
    }

    private static bool IsAlreadyConfigured(JsonObject root)
    {
        var servers = root["mcpServers"]?.AsObject();
        if (servers?[McpServerName] is not JsonObject existing)
            return false;

        if (existing["args"] is not JsonArray args)
            return false;

        return args.Any(a => a?.GetValue<string>() == "--stdio");
    }

    private (string command, string[] args) GetMcpStdioCommand()
    {
        var processPath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "dotnet";
        var processName = Path.GetFileNameWithoutExtension(processPath);

        if (string.Equals(processName, "dotnet", StringComparison.OrdinalIgnoreCase))
            return (processPath, ["run", "--project", environment.ContentRootPath, "--no-launch-profile", "--", "--stdio"]);

        return (processPath, ["--stdio"]);
    }

    private static async Task<string> CreateBackupAsync(
        string configPath, string content, CancellationToken cancellationToken)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var backupPath = $"{configPath}.backup_{timestamp}";
        await File.WriteAllTextAsync(backupPath, content, cancellationToken);
        return backupPath;
    }

    private static async Task SaveConfigAsync(
        JsonObject root, string configPath, CancellationToken cancellationToken)
    {
        var json = root.ToJsonString(SerializerOptions);
        await File.WriteAllTextAsync(configPath, json, cancellationToken);
    }

    internal static string? GetClaudeConfigPath()
    {
        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "Claude", "claude_desktop_config.json");
        }

        if (OperatingSystem.IsMacOS())
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "Library", "Application Support", "Claude", "claude_desktop_config.json");
        }

        if (OperatingSystem.IsLinux())
        {
            var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
                             ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            return Path.Combine(configHome, "Claude", "claude_desktop_config.json");
        }

        return null;
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
