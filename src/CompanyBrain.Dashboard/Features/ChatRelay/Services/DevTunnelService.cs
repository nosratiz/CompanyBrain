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

    /// <summary>The currently active public tunnel URL, or <c>null</c> when the tunnel is not running.</summary>
    public string? TunnelUrl => _tunnelUrl;

    /// <summary>Fires when the tunnel URL is first established or changes.</summary>
    public event Action<string>? TunnelUrlChanged;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = await settingsService.GetSettingsAsync(stoppingToken);
        if (!settings.TunnelEnabled)
        {
            logger.LogInformation("DevTunnelService: tunnel is disabled — worker idle");
            return;
        }

        var port = DetectListeningPort();
        logger.LogInformation("DevTunnelService: starting tunnel on port {Port}", port);

        // Try devtunnel first, then fall back to cloudflared.
        var started = await TryStartDevTunnelAsync(port, stoppingToken)
                   || await TryStartCloudflaredAsync(port, stoppingToken);

        if (!started)
        {
            logger.LogWarning(
                "DevTunnelService: neither 'devtunnel' nor 'cloudflared' is available or succeeded. " +
                "Install one of them and restart DeepRoot.");
        }
    }

    // ── devtunnel ─────────────────────────────────────────────────────────────

    private async Task<bool> TryStartDevTunnelAsync(int port, CancellationToken ct)
    {
        if (!IsCommandAvailable("devtunnel"))
        {
            logger.LogDebug("'devtunnel' CLI not found in PATH");
            return false;
        }

        logger.LogInformation("Starting tunnel via 'devtunnel'");

        var args = $"host --allow-anonymous -p {port}";
        return await RunTunnelProcessAsync("devtunnel", args, DevTunnelUrlRegex(), ct);
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
        try
        {
            process = Process.Start(psi);
            if (process is null)
            {
                logger.LogWarning("Failed to start '{Exe}'", executable);
                return false;
            }

            // Read both stdout and stderr to look for the public URL.
            var urlFound = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            process.OutputDataReceived += (_, e) => TryExtractUrl(e.Data, urlPattern, urlFound);
            process.ErrorDataReceived += (_, e) => TryExtractUrl(e.Data, urlPattern, urlFound);

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Wait up to 30 seconds for the URL to appear in the output.
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var combined = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);

            try
            {
                var tunnelUrl = await urlFound.Task.WaitAsync(combined.Token);
                await OnTunnelUrlDiscoveredAsync(tunnelUrl, ct);
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                logger.LogWarning("'{Exe}' started but the tunnel URL was not detected within 30 s", executable);
            }

            // Keep the process alive until the app shuts down.
            try { await process.WaitForExitAsync(ct); }
            catch (OperationCanceledException) { /* shutdown */ }

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
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
}
