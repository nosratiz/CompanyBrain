using System.Diagnostics;
using System.Text.RegularExpressions;

namespace CompanyBrain.Dashboard.Features.ChatRelay.Services;

/// <summary>
/// Manages a secure inbound tunnel so that Slack / Teams cloud servers can POST
/// webhook events to this local DeepRoot node without opening any firewall ports.
///
/// <para>
/// <b>Strategy (tried in order):</b>
/// <list type="number">
///   <item><description>
///     <b>devtunnel</b> — Microsoft's .NET Dev Tunnels CLI.
///     Invoked as <c>devtunnel host --allow-anonymous --port {port}</c>.
///     Requires a prior <c>devtunnel login</c> (one-time, interactive).
///   </description></item>
///   <item><description>
///     <b>cloudflared</b> — Cloudflare's Argo Tunnel quick-tunnel mode.
///     Invoked as <c>cloudflared tunnel --url http://localhost:{port}</c>.
///     Needs no sign-in for ephemeral *.trycloudflare.com tunnels.
///   </description></item>
/// </list>
/// </para>
///
/// <para>
/// The public URL is exposed via <see cref="TunnelUrl"/> and persisted to
/// <see cref="ChatBotSettings.TunnelUrl"/> (for display in the Bot Management tab).
/// </para>
///
/// <para><b>Security note:</b> the tunnel allows anonymous inbound HTTP.  Actual
/// request authentication is enforced by the webhook endpoints (HMAC-SHA256 for Slack,
/// Bot Framework JWT check for Teams).</para>
/// </summary>
public sealed partial class DevTunnelService(
    ChatRelaySettingsService settingsService,
    ILogger<DevTunnelService> logger) : BackgroundService
{
    private volatile string? _tunnelUrl;
    private volatile bool _regenerateRequested;
    private CancellationTokenSource? _innerCts;
    private string? _activeTunnelId; // the devtunnel ID that is currently hosted

    /// <summary>The currently active public tunnel URL, or <c>null</c> when the tunnel is not running.</summary>
    public string? TunnelUrl => _tunnelUrl;

    /// <summary>Fires when the tunnel URL is first established or changes.</summary>
    public event Action<string>? TunnelUrlChanged;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var settings = await settingsService.GetSettingsAsync(stoppingToken);
            if (!settings.TunnelEnabled)
            {
                logger.LogInformation("DevTunnelService: tunnel is disabled — worker idle");
                return;
            }

            _regenerateRequested = false;
            using var innerCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            _innerCts = innerCts;

            var port = DetectListeningPort();
            logger.LogInformation("DevTunnelService: starting tunnel on port {Port}", port);

            bool started;
            try
            {
                // Try devtunnel first, then fall back to cloudflared.
                started = await TryStartDevTunnelAsync(port, innerCts.Token)
                       || await TryStartCloudflaredAsync(port, innerCts.Token);
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                // innerCts was cancelled by RegenerateTunnelAsync — not an app-level shutdown.
                // Treat as if the tunnel process exited cleanly so the loop can restart it.
                started = true;
            }

            if (!started)
            {
                logger.LogWarning(
                    "DevTunnelService: neither 'devtunnel' nor 'cloudflared' is available or succeeded. " +
                    "Install one of them and restart DeepRoot.");
                break;
            }

            if (!_regenerateRequested)
                break; // tunnel exited normally — no restart needed

            logger.LogInformation("DevTunnelService: regenerating tunnel as requested…");
        }

        _innerCts = null;
    }

    /// <summary>
    /// Tears down the current tunnel, deletes it from the devtunnels.ms server (freeing quota),
    /// then restarts with a brand-new tunnel ID and URL.
    /// </summary>
    public async Task RegenerateTunnelAsync(CancellationToken ct = default)
    {
        _regenerateRequested = true;

        // Snapshot and clear the stored tunnel ID before cancelling the process.
        var oldTunnelId = _activeTunnelId;
        await settingsService.ClearDevTunnelIdAsync(ct);

        // Cancel the running tunnel process — ExecuteAsync will loop and start fresh.
        _innerCts?.Cancel();

        // Delete the old tunnel from the devtunnels.ms server so quota is freed
        // and `devtunnel create` can succeed cleanly in the next loop iteration.
        if (!string.IsNullOrWhiteSpace(oldTunnelId))
            _ = DeleteDevTunnelAsync(oldTunnelId, CancellationToken.None);
    }

    // ── devtunnel ─────────────────────────────────────────────────────────────

    private async Task<bool> TryStartDevTunnelAsync(int port, CancellationToken ct)
    {
        if (!IsCommandAvailable("devtunnel"))
        {
            logger.LogInformation("'devtunnel' CLI not found — attempting automatic installation via dotnet tool");
            if (!await TryInstallDevTunnelAsync(ct))
            {
                logger.LogWarning("Automatic 'devtunnel' installation failed");
                return false;
            }
        }

        var settings = await settingsService.GetSettingsAsync(ct);
        var tunnelId = settings.DevTunnelId;

        if (!string.IsNullOrWhiteSpace(tunnelId))
        {
            // Try to host the stored tunnel.  If it fails (process exits without URL)
            // fall through to creating a fresh one.
            logger.LogInformation("Trying to host existing devtunnel: {TunnelId}", tunnelId);
            _activeTunnelId = tunnelId;
            if (await RunTunnelProcessAsync("devtunnel", $"host {tunnelId} -p {port}", DevTunnelUrlRegex(), ct))
                return true;

            logger.LogWarning("devtunnel {TunnelId} failed to produce a URL — creating a new one", tunnelId);
            _ = DeleteDevTunnelAsync(tunnelId, CancellationToken.None);
            await settingsService.ClearDevTunnelIdAsync(CancellationToken.None);
        }

        return await CreateAndHostNewTunnelAsync(port, ct);
    }

    /// <summary>
    /// Creates a brand-new persistent devtunnel, registers the local port, and hosts it.
    /// Falls back to an ephemeral host if creation fails.
    /// </summary>
    private async Task<bool> CreateAndHostNewTunnelAsync(int port, CancellationToken ct)
    {
        var tunnelId = await CreatePersistentTunnelAsync(port, ct);
        if (tunnelId is null)
        {
            logger.LogWarning("Could not create a persistent devtunnel — falling back to ephemeral mode");
            _activeTunnelId = null;
            return await RunTunnelProcessAsync("devtunnel", $"host --allow-anonymous -p {port}", DevTunnelUrlRegex(), ct);
        }

        await settingsService.UpdateDevTunnelIdAsync(tunnelId, ct);
        logger.LogInformation("New persistent devtunnel created and saved: {TunnelId}", tunnelId);
        _activeTunnelId = tunnelId;
        return await RunTunnelProcessAsync("devtunnel", $"host {tunnelId} -p {port}", DevTunnelUrlRegex(), ct);
    }

    /// <summary>
    /// Runs <c>devtunnel create --allow-anonymous</c> followed by
    /// <c>devtunnel port create &lt;id&gt; -p &lt;port&gt;</c> and returns the new tunnel ID,
    /// or <c>null</c> if either command fails.
    /// </summary>
    private async Task<string?> CreatePersistentTunnelAsync(int port, CancellationToken ct)
    {
        // Step 1 — create the tunnel
        string? tunnelId = null;
        try
        {
            var createPsi = new ProcessStartInfo("devtunnel", "create --allow-anonymous")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var createProc = Process.Start(createPsi);
            if (createProc is null) return null;

            var stdout = await createProc.StandardOutput.ReadToEndAsync(ct);
            var stderr = await createProc.StandardError.ReadToEndAsync(ct);
            await createProc.WaitForExitAsync(ct);

            if (createProc.ExitCode != 0)
            {
                logger.LogWarning("'devtunnel create' failed (exit {Code}): {Err}", createProc.ExitCode, stderr.Trim());
                return null;
            }

            // Output: "Tunnel ID: abc123" or "Created tunnel: abc123"
            var idMatch = DevTunnelIdRegex().Match(stdout + stderr);
            if (!idMatch.Success)
            {
                logger.LogWarning("'devtunnel create' succeeded but tunnel ID not found in output: {Out}", stdout.Trim());
                return null;
            }

            tunnelId = idMatch.Groups[1].Value;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "'devtunnel create' threw unexpectedly");
            return null;
        }

        // Step 2 — register the port on the tunnel
        try
        {
            var portPsi = new ProcessStartInfo("devtunnel", $"port create {tunnelId} -p {port}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var portProc = Process.Start(portPsi);
            if (portProc is not null)
            {
                await portProc.WaitForExitAsync(ct);
                if (portProc.ExitCode != 0)
                    logger.LogWarning("'devtunnel port create' exited {Code} — tunnel may still work", portProc.ExitCode);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "'devtunnel port create' threw — continuing anyway");
        }

        return tunnelId;
    }

    /// <summary>
    /// Returns <c>true</c> when the tunnel ID exists and is visible to the current user
    /// (<c>devtunnel show {id}</c> exits 0).  Returns <c>false</c> on any failure so the
    /// caller falls through to creating a new tunnel.
    /// </summary>
    private async Task<bool> DevTunnelExistsAsync(string tunnelId, CancellationToken ct)
    {
        // NOTE: kept for potential future use but no longer called on the hot path.
        try
        {
            var psi = new ProcessStartInfo("devtunnel", $"show {tunnelId}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var proc = Process.Start(psi);
            if (proc is null) return false;

            await proc.WaitForExitAsync(ct);
            return proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Deletes a persistent devtunnel from the devtunnels.ms server, freeing quota so that
    /// <c>devtunnel create</c> can succeed when regenerating.
    /// </summary>
    private async Task DeleteDevTunnelAsync(string tunnelId, CancellationToken ct)
    {
        try
        {
            logger.LogInformation("Deleting old devtunnel: {TunnelId}", tunnelId);

            var psi = new ProcessStartInfo("devtunnel", $"delete {tunnelId} -f")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var proc = Process.Start(psi);
            if (proc is null) return;

            await proc.WaitForExitAsync(ct);

            if (proc.ExitCode == 0)
                logger.LogInformation("Deleted devtunnel {TunnelId}", tunnelId);
            else
            {
                var err = await proc.StandardError.ReadToEndAsync(ct);
                logger.LogWarning("'devtunnel delete {TunnelId}' exited {Code}: {Err}", tunnelId, proc.ExitCode, err.Trim());
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete old devtunnel {TunnelId} — quota may be reached on next create", tunnelId);
        }
    }

    /// <summary>
    /// Installs the devtunnel CLI as a .NET global tool — works on Windows, macOS, and Linux
    /// as long as the .NET SDK is present (which it always is for a self-hosted DeepRoot node).
    /// </summary>
    private async Task<bool> TryInstallDevTunnelAsync(CancellationToken ct)
    {
        try
        {
            logger.LogInformation("Running: dotnet tool install -g Microsoft.devtunnel");

            var psi = new ProcessStartInfo("dotnet", "tool install -g Microsoft.devtunnel")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process is null) return false;

            var stdout = await process.StandardOutput.ReadToEndAsync(ct);
            var stderr = await process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            if (process.ExitCode == 0)
            {
                logger.LogInformation("'devtunnel' installed successfully: {Output}", stdout.Trim());

                // dotnet tools land in ~/.dotnet/tools — add to PATH for this process so
                // IsCommandAvailable (which spawns a child process) can find it immediately.
                var toolPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".dotnet", "tools");

                var current = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                if (!current.Contains(toolPath, StringComparison.OrdinalIgnoreCase))
                    Environment.SetEnvironmentVariable("PATH", $"{toolPath}{Path.PathSeparator}{current}");

                return IsCommandAvailable("devtunnel");
            }

            // Exit code 1 with "already installed" message is still a success.
            if (stdout.Contains("already installed", StringComparison.OrdinalIgnoreCase)
             || stderr.Contains("already installed", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation("'devtunnel' was already installed");
                return IsCommandAvailable("devtunnel");
            }

            logger.LogWarning("dotnet tool install exited {Code}: {Error}", process.ExitCode, stderr.Trim());
            return false;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to auto-install 'devtunnel'");
            return false;
        }
    }

    // ── cloudflared ───────────────────────────────────────────────────────────

    private async Task<bool> TryStartCloudflaredAsync(int port, CancellationToken ct)
    {
        if (!IsCommandAvailable("cloudflared"))
        {
            logger.LogDebug("'cloudflared' CLI not found in PATH");
            return false;
        }

        logger.LogInformation("Starting tunnel via 'cloudflared'");

        var args = $"tunnel --url http://localhost:{port}";
        return await RunTunnelProcessAsync("cloudflared", args, CloudflaredUrlRegex(), ct);
    }

    // ── Shared process runner ─────────────────────────────────────────────────

    /// <summary>
    /// Starts <paramref name="executable"/> and monitors its output for a public tunnel URL.
    /// Returns <c>true</c> when the tunnel was established (URL detected); <c>false</c> when
    /// the process exited without ever producing a URL (genuine failure — caller should retry
    /// with a fresh tunnel).  Throws <see cref="OperationCanceledException"/> when
    /// <paramref name="ct"/> is cancelled so the caller can decide whether to loop or stop.
    /// </summary>
    private async Task<bool> RunTunnelProcessAsync(
        string executable,
        string arguments,
        Regex urlPattern,
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo(executable, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        Process? process = null;
        var urlDetected = false;
        try
        {
            process = Process.Start(psi);
            if (process is null)
            {
                logger.LogWarning("Failed to start '{Exe}'", executable);
                return false;
            }

            var urlFound = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            process.OutputDataReceived += (_, e) => TryExtractUrl(e.Data, urlPattern, urlFound);
            process.ErrorDataReceived += (_, e) => TryExtractUrl(e.Data, urlPattern, urlFound);

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Phase 1 — wait up to 90 s for the URL to appear in the output.
            // devtunnel can take 45-90 s to establish a connection for a freshly created tunnel.
            using var urlTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            using var urlWait = CancellationTokenSource.CreateLinkedTokenSource(ct, urlTimeout.Token);

            try
            {
                var tunnelUrl = await urlFound.Task.WaitAsync(urlWait.Token);
                urlDetected = true;
                await OnTunnelUrlDiscoveredAsync(tunnelUrl, ct);
            }
            catch (OperationCanceledException) when (urlTimeout.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                // The 90 s window closed but the process is still running — the URL may still
                // come through later (e.g. slow auth round-trip).  Schedule late processing so
                // the URL is reported as soon as it appears rather than being silently dropped.
                logger.LogWarning(
                    "'{Exe}' started but tunnel URL not detected in 90 s — monitoring in background",
                    executable);

                _ = urlFound.Task.ContinueWith(
                    t =>
                    {
                        if (t.IsCompletedSuccessfully && !ct.IsCancellationRequested)
                        {
                            urlDetected = true;
                            _ = OnTunnelUrlDiscoveredAsync(t.Result, CancellationToken.None);
                        }
                    },
                    TaskScheduler.Default);
            }
            // OperationCanceledException from ct (regeneration / shutdown) propagates out.

            // Phase 2 — keep process alive.  Throws OCE when ct is cancelled.
            await process.WaitForExitAsync(ct);

            // Process exited without being cancelled.  If it never produced a URL it failed.
            if (!urlDetected)
                logger.LogWarning("'{Exe}' exited before a tunnel URL was established", executable);

            return urlDetected;
        }
        catch (OperationCanceledException)
        {
            // Rethrow so ExecuteAsync can decide whether to loop (regenerate) or stop.
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error running '{Exe}'", executable);
            return false;
        }
        finally
        {
            try { process?.Kill(); } catch { /* best-effort */ }
            process?.Dispose();
            _tunnelUrl = null;
        }
    }

    private void TryExtractUrl(string? line, Regex pattern, TaskCompletionSource<string> urlFound)
    {
        if (string.IsNullOrEmpty(line)) return;
        logger.LogTrace("[tunnel] {Line}", line);

        var match = pattern.Match(line);
        if (match.Success)
            urlFound.TrySetResult(match.Value);
    }

    private async Task OnTunnelUrlDiscoveredAsync(string url, CancellationToken ct)
    {
        _tunnelUrl = url;
        TunnelUrlChanged?.Invoke(url);

        logger.LogInformation("Tunnel active: {Url}", url);

        // Persist the URL so the Settings UI can display it without polling.
        try { await settingsService.UpdateTunnelUrlAsync(url, ct); }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to persist tunnel URL to settings"); }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static int DetectListeningPort()
    {
        // Try common ASP.NET env vars first, then fall back to 8080.
        var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS")
                ?? Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS");

        if (!string.IsNullOrEmpty(urls))
        {
            foreach (var segment in urls.Split(';'))
            {
                if (Uri.TryCreate(segment.Trim(), UriKind.Absolute, out var uri))
                    return uri.Port;
            }
        }

        return 5202;
    }

    private static bool IsCommandAvailable(string command)
    {
        try
        {
            var psi = new ProcessStartInfo(command, "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(2000);
            return p?.ExitCode == 0;
        }
        catch { return false; }
    }

    // ── URL extraction regexes ────────────────────────────────────────────────

    /// <summary>Matches devtunnel's output line: "https://xxxxx-5202.euw.devtunnels.ms" (any region subdomain).</summary>
    [GeneratedRegex(@"https://[a-z0-9\-]+(?:\.[a-z0-9]+)*\.devtunnels\.ms(?:/[^\s]*)?", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 500)]
    private static partial Regex DevTunnelUrlRegex();

    /// <summary>Matches cloudflared's output line: "https://random-words.trycloudflare.com".</summary>
    [GeneratedRegex(@"https://[a-z0-9\-]+\.trycloudflare\.com(?:/[^\s]*)?", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 500)]
    private static partial Regex CloudflaredUrlRegex();

    /// <summary>
    /// Extracts the tunnel ID from 'devtunnel create' output.
    /// Matches patterns like "Tunnel ID: abc123def" or "Created tunnel: abc123def"
    /// where the ID is alphanumeric with optional hyphens.
    /// </summary>
    [GeneratedRegex(@"(?:Tunnel ID:|Created tunnel:|tunnel\s+)([a-z0-9][a-z0-9\-]{3,})", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 500)]
    private static partial Regex DevTunnelIdRegex();
}
