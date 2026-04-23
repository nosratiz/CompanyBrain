using CompanyBrain.Dashboard.Features.AutoSetup.Services;
using MudBlazor;

namespace CompanyBrain.Dashboard.Features.AutoSetup.Pages;

public partial class AutoSetupPage : IDisposable
{
    // ── State: Claude Desktop ───────────────────────────────────────────
    private bool _claudeConfigured;
    private string? _claudeConfigPath;
    private bool _configuringClaude;

    // ── State: M365 Device Code ─────────────────────────────────────────
    private bool _m365Connected;
    private string? _m365User;
    private bool _m365Authenticating;
    private IReadOnlyList<M365DeviceCodeAuthService.DiscoveredSite>? _discoveredSites;

    // ── State: Auto-Provisioning ────────────────────────────────────────
    private bool _autoProvisioning;
    private int _provisionedCount;
    private int _provisionTotal;
    private string? _provisioningStatus;
    private List<AutoProvisionResult> _provisionResults = [];

    private sealed record AutoProvisionResult(
        string SiteName,
        string WebUrl,
        bool IsRootSite,
        int DrivesConfigured,
        string? Error);

    // ── State: Device Code UI ───────────────────────────────────────────
    private bool _deviceCodeVisible;
    private string? _deviceCodeUserCode;
    private string? _deviceCodeVerificationUrl;
    private string? _deviceCodeMessage;

    // ── State: Copilot Manifest ─────────────────────────────────────────
    private bool _copilotManifestExists;
    private string? _copilotManifestPath;
    private bool _generatingManifest;

    // ── State: One-Click ────────────────────────────────────────────────
    private bool _runningOneClick;
    private string? _oneClickMessage;
    private Severity _oneClickSeverity;

    private readonly CancellationTokenSource _cts = new();
    private CancellationTokenSource? _deviceCodeCts;

    // ─────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────────

    protected override async Task OnInitializedAsync()
    {
        await RefreshStatusAsync();
    }

    private async Task RefreshStatusAsync()
    {
        try
        {
            // Claude
            _claudeConfigured = await ClaudeService.IsConfiguredAsync(_cts.Token);
            _claudeConfigPath = ClaudeHandshakeService.GetClaudeConfigPath();

            // Copilot
            _copilotManifestExists = CopilotService.ManifestExists();
            _copilotManifestPath = CopilotService.GetManifestPath();
        }
        catch (Exception)
        {
            // Swallow — non-critical status check
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // One-Click Setup
    // ─────────────────────────────────────────────────────────────────────

    private async Task RunOneClickSetupAsync()
    {
        _runningOneClick = true;
        _oneClickMessage = null;
        StateHasChanged();

        try
        {
            var claudeTask = ClaudeService.ConfigureAsync(_cts.Token);
            var copilotTask = CopilotService.GenerateManifestAsync(cancellationToken: _cts.Token);

            await Task.WhenAll(claudeTask, copilotTask);

            var claudeResult = await claudeTask;
            var copilotResult = await copilotTask;

            if (claudeResult.Success && copilotResult.Success)
            {
                _oneClickSeverity = Severity.Success;
                _oneClickMessage = "All integrations configured successfully! " +
                                   "Use the M365 Sign In button to connect your Microsoft account.";
            }
            else
            {
                var failures = new List<string>();
                if (!claudeResult.Success) failures.Add($"Claude: {claudeResult.Message}");
                if (!copilotResult.Success) failures.Add($"Copilot: {copilotResult.Message}");

                _oneClickSeverity = Severity.Warning;
                _oneClickMessage = $"Partial setup — {string.Join("; ", failures)}";
            }

            await RefreshStatusAsync();
        }
        catch (Exception ex)
        {
            _oneClickSeverity = Severity.Error;
            _oneClickMessage = $"Setup failed: {ex.Message}";
        }
        finally
        {
            _runningOneClick = false;
            StateHasChanged();
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Claude Desktop
    // ─────────────────────────────────────────────────────────────────────

    private async Task ConfigureClaudeAsync()
    {
        _configuringClaude = true;
        StateHasChanged();

        try
        {
            var result = await ClaudeService.ConfigureAsync(_cts.Token);

            _oneClickSeverity = result.Success ? Severity.Success : Severity.Error;
            _oneClickMessage = result.Success
                ? $"Claude Desktop configured. Config: {result.ConfigPath}"
                : result.Message;

            await RefreshStatusAsync();
        }
        catch (Exception ex)
        {
            _oneClickSeverity = Severity.Error;
            _oneClickMessage = $"Claude injection failed: {ex.Message}";
        }
        finally
        {
            _configuringClaude = false;
            StateHasChanged();
        }
    }

    private async Task RemoveClaudeAsync()
    {
        _configuringClaude = true;
        StateHasChanged();

        try
        {
            var result = await ClaudeService.RemoveAsync(_cts.Token);

            _oneClickSeverity = result.Success ? Severity.Info : Severity.Error;
            _oneClickMessage = result.Message;

            await RefreshStatusAsync();
        }
        catch (Exception ex)
        {
            _oneClickSeverity = Severity.Error;
            _oneClickMessage = $"Claude removal failed: {ex.Message}";
        }
        finally
        {
            _configuringClaude = false;
            StateHasChanged();
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // M365 Device Code
    // ─────────────────────────────────────────────────────────────────────

    private async Task StartDeviceCodeFlowAsync()
    {
        _m365Authenticating = true;
        _deviceCodeVisible = false;
        _oneClickMessage = null;
        StateHasChanged();

        // Use a dedicated CTS so cancelling device code doesn't kill other operations
        _deviceCodeCts?.Cancel();
        _deviceCodeCts?.Dispose();
        _deviceCodeCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
        var ct = _deviceCodeCts.Token;

        try
        {
            // Run the MSAL polling on a background thread so it doesn't block the
            // Blazor circuit's sync context — this prevents the SignalR connection
            // from stalling, which would cancel the token and expire the code.
            var result = await Task.Run(async () =>
                await M365Service.AuthenticateWithDeviceCodeAsync(
                    (userCode, verificationUrl, message) =>
                    {
                        _deviceCodeUserCode = userCode;
                        _deviceCodeVerificationUrl = verificationUrl;
                        _deviceCodeMessage = message;
                        _deviceCodeVisible = true;
                        _ = InvokeAsync(StateHasChanged);
                    },
                    ct), ct);

            _deviceCodeVisible = false;

            if (result.Success)
            {
                _m365Connected = true;
                _m365User = result.UserPrincipalName;
                _discoveredSites = result.DiscoveredSites;
                _oneClickSeverity = Severity.Success;
                _oneClickMessage = $"Connected as {result.UserPrincipalName} — {result.DiscoveredSites?.Count ?? 0} site(s) discovered. Auto-provisioning sync folders…";
                await InvokeAsync(StateHasChanged);

                // Auto-provision all discovered sites as synced folders
                await AutoProvisionDiscoveredSitesAsync();
            }
            else
            {
                _oneClickSeverity = Severity.Error;
                _oneClickMessage = result.Message;
            }
        }
        catch (OperationCanceledException)
        {
            _deviceCodeVisible = false;
            _oneClickSeverity = Severity.Info;
            _oneClickMessage = "Device code flow was cancelled.";
        }
        catch (Exception ex)
        {
            _deviceCodeVisible = false;
            _oneClickSeverity = Severity.Error;
            _oneClickMessage = $"M365 authentication failed: {ex.Message}";
        }
        finally
        {
            _m365Authenticating = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private void CancelDeviceCodeFlow()
    {
        _deviceCodeCts?.Cancel();
        _deviceCodeVisible = false;
        _m365Authenticating = false;
        StateHasChanged();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Auto-Provision Discovered Sites
    // ─────────────────────────────────────────────────────────────────────

    private async Task AutoProvisionDiscoveredSitesAsync()
    {
        if (_discoveredSites is null || _discoveredSites.Count == 0)
            return;

        _autoProvisioning = true;
        _provisionedCount = 0;
        _provisionTotal = _discoveredSites.Count;
        _provisionResults = [];
        await InvokeAsync(StateHasChanged);

        try
        {
            var options = await SettingsProvider.GetEffectiveOptionsAsync(_cts.Token);
            var tenantId = options.TenantId;

            if (string.IsNullOrEmpty(tenantId))
            {
                _oneClickSeverity = Severity.Warning;
                _oneClickMessage = "TenantId not configured — cannot auto-provision sites.";
                return;
            }

            foreach (var site in _discoveredSites)
            {
                _provisioningStatus = $"Provisioning {site.DisplayName}…";
                await InvokeAsync(StateHasChanged);

                try
                {
                    var drives = await SyncService.GetSiteDrivesAsync(tenantId, site.SiteId, _cts.Token);
                    var drivesConfigured = 0;

                    foreach (var drive in drives)
                    {
                        try
                        {
                            var folder = await SyncService.ConfigureSyncFolderAsync(
                                tenantId,
                                site.DisplayName,
                                site.SiteId,
                                drive.Id,
                                drive.Name,
                                string.Empty,
                                _cts.Token);

                            // Trigger a sync regardless of whether the folder was new or already existed
                            try
                            {
                                await SyncService.SyncFolderAsync(folder.Id, _cts.Token);
                            }
                            catch
                            {
                                // Sync failure is non-fatal — folder is still configured
                            }

                            drivesConfigured++;
                        }
                        catch
                        {
                            // Individual drive failure — continue with others
                        }
                    }

                    _provisionResults.Add(new AutoProvisionResult(
                        site.DisplayName, site.WebUrl, site.IsRootSite, drivesConfigured, null));
                }
                catch (Exception ex)
                {
                    _provisionResults.Add(new AutoProvisionResult(
                        site.DisplayName, site.WebUrl, site.IsRootSite, 0, ex.Message));
                }

                _provisionedCount++;
                await InvokeAsync(StateHasChanged);
            }

            var totalDrives = _provisionResults.Sum(r => r.DrivesConfigured);
            var failedSites = _provisionResults.Count(r => r.Error is not null);

            _oneClickSeverity = failedSites == 0 ? Severity.Success : Severity.Warning;
            _oneClickMessage = $"Auto-provisioned {totalDrives} document library folder(s) across {_provisionResults.Count - failedSites} site(s)." +
                (failedSites > 0 ? $" {failedSites} site(s) failed." : "") +
                " Visit SharePoint › Synced Folders to manage sync.";
        }
        finally
        {
            _autoProvisioning = false;
            _provisioningStatus = null;
            await InvokeAsync(StateHasChanged);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Copilot Manifest
    // ─────────────────────────────────────────────────────────────────────

    private async Task GenerateCopilotManifestAsync()
    {
        _generatingManifest = true;
        StateHasChanged();

        try
        {
            var result = await CopilotService.GenerateManifestAsync(cancellationToken: _cts.Token);

            _oneClickSeverity = result.Success ? Severity.Success : Severity.Error;
            _oneClickMessage = result.Message;

            await RefreshStatusAsync();
        }
        catch (Exception ex)
        {
            _oneClickSeverity = Severity.Error;
            _oneClickMessage = $"Manifest generation failed: {ex.Message}";
        }
        finally
        {
            _generatingManifest = false;
            StateHasChanged();
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Cleanup
    // ─────────────────────────────────────────────────────────────────────

    public void Dispose()
    {
        _deviceCodeCts?.Cancel();
        _deviceCodeCts?.Dispose();
        _cts.Cancel();
        _cts.Dispose();
    }
}
