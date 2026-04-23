using CompanyBrain.Dashboard.Features.SharePoint.Data;
using CompanyBrain.Dashboard.Features.SharePoint.Models;
using CompanyBrain.Dashboard.Features.SharePoint.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using System.Diagnostics;

namespace CompanyBrain.Dashboard.Features.SharePoint.Pages;

public partial class SharePointConnector : IDisposable
{
    // SharePoint connection state (separate from app login)
    private bool _isConnected;
    private string? _connectedUser;
    private string? _tenantId;

    // Site search
    private string _siteSearchQuery = string.Empty;
    private bool _searchingSites;
    private List<SharePointSite> _sites = [];

    // Drive selection
    private SharePointSite? _selectedSite;
    private bool _loadingDrives;
    private List<SharePointDrive> _drives = [];
    private readonly Dictionary<string, List<SharePointDriveItem>> _currentDriveItems = new();

    // Synced folders
    private List<SyncedSharePointFolder> _syncedFolders = [];
    private bool _loadingSyncedFolders;
    private int? _syncingFolderId;

    // Conflicts
    private List<SharePointSyncConflict> _conflicts = [];

    // Sync all
    private bool _syncingAll;
    private DateTime? _lastSyncAllUtc;
    private string? _lastSyncAllMessage;
    private Severity _lastSyncAllSeverity = Severity.Normal;

    private readonly CancellationTokenSource _cts = new();
    private Timer? _refreshTimer;

    protected override async Task OnInitializedAsync()
    {
        HandleAdminConsentCallback();
        await CheckSharePointConnectionAsync();
        await LoadSyncedFoldersAsync();
        await LoadConflictsAsync();
        _refreshTimer = new Timer(_ => InvokeAsync(async () =>
        {
            if (!_syncingAll && _syncingFolderId is null)
                await LoadSyncedFoldersAsync();
        }), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    private void HandleAdminConsentCallback()
    {
        var uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var adminConsent = query["admin_consent"];

        if (adminConsent == "granted")
        {
            Snackbar.Add("Admin consent granted! Click 'Connect to SharePoint' to sign in.", Severity.Success);
        }
        else if (adminConsent == "declined")
        {
            Snackbar.Add("Admin consent was declined. SharePoint sync requires admin-consented permissions.", Severity.Warning);
        }
    }

    private async Task CheckSharePointConnectionAsync()
    {
        var options = await SettingsProvider.GetEffectiveOptionsAsync(_cts.Token);
        _tenantId = options.TenantId;

        if (string.IsNullOrEmpty(_tenantId)) return;

        var token = await OAuthService.GetActiveConnectionAsync(_tenantId, _cts.Token);
        if (token is not null)
        {
            _isConnected = true;
            _connectedUser = token.UserPrincipalName;
        }
    }

    private void ConnectSharePoint()
    {
        NavigationManager.NavigateTo("/api/sharepoint/connect", forceLoad: true);
    }

    private void RequestAdminConsent()
    {
        NavigationManager.NavigateTo("/api/sharepoint/admin-consent", forceLoad: true);
    }

    private async Task DisconnectSharePointAsync()
    {
        if (_tenantId is null) return;

        await OAuthService.DisconnectAsync(_tenantId, _cts.Token);
        _isConnected = false;
        _connectedUser = null;
        Snackbar.Add("SharePoint disconnected", Severity.Info);
    }

    private async Task SearchSitesAsync()
    {
        if (string.IsNullOrWhiteSpace(_siteSearchQuery) || _tenantId is null)
            return;

        _searchingSites = true;
        StateHasChanged();

        try
        {
            _sites = (await SyncService.SearchSitesAsync(_tenantId, _siteSearchQuery, _cts.Token)).ToList();

            if (_sites.Count == 0)
            {
                Snackbar.Add("No sites found", Severity.Info);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Search failed: {ex.Message}", Severity.Error);
        }
        finally
        {
            _searchingSites = false;
        }
    }

    private async Task SelectSiteAsync(SharePointSite site)
    {
        _selectedSite = site;
        _drives = [];
        _currentDriveItems.Clear();
        _loadingDrives = true;
        StateHasChanged();

        try
        {
            _drives = (await SyncService.GetSiteDrivesAsync(_tenantId!, site.Id, _cts.Token)).ToList();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Failed to load drives: {ex.Message}", Severity.Error);
        }
        finally
        {
            _loadingDrives = false;
        }
    }

    private async Task LoadDriveItemsAsync(SharePointDrive drive)
    {
        try
        {
            var items = await SyncService.GetDriveItemsAsync(_tenantId!, drive.Id, null, _cts.Token);
            _currentDriveItems[drive.Id] = items.ToList();
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Failed to load items: {ex.Message}", Severity.Error);
        }
    }

    private async Task ConfigureSyncAsync(SharePointDrive drive, string folderPath)
    {
        if (_selectedSite is null || _tenantId is null)
            return;

        try
        {
            var syncedFolder = await SyncService.ConfigureSyncFolderAsync(
                _tenantId,
                _selectedSite.DisplayName,
                _selectedSite.Id,
                drive.Id,
                drive.Name,
                folderPath,
                _cts.Token);

            Snackbar.Add($"Configured sync: {syncedFolder.LocalPath}", Severity.Success);
            await LoadSyncedFoldersAsync();

            // Trigger initial sync
            await TriggerSyncAsync(syncedFolder.Id);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Failed to configure sync: {ex.Message}", Severity.Error);
        }
    }

    private async Task LoadSyncedFoldersAsync()
    {
        _loadingSyncedFolders = true;
        StateHasChanged();

        try
        {
            await using var db = await DbContextFactory.CreateDbContextAsync(_cts.Token);
            _syncedFolders = await db.SyncedFolders
                .OrderByDescending(f => f.Id)
                .ToListAsync(_cts.Token);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Failed to load synced folders: {ex.Message}", Severity.Error);
        }
        finally
        {
            _loadingSyncedFolders = false;
            StateHasChanged();
        }
    }

    private async Task LoadConflictsAsync()
    {
        try
        {
            await using var db = await DbContextFactory.CreateDbContextAsync(_cts.Token);
            _conflicts = await db.SyncConflicts
                .OrderByDescending(c => c.Id)
                .ToListAsync(_cts.Token);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Failed to load conflicts: {ex.Message}", Severity.Error);
        }
    }

    private async Task TriggerSyncAsync(int folderId)
    {
        _syncingFolderId = folderId;
        StateHasChanged();

        try
        {
            await SyncService.SyncFolderAsync(folderId, _cts.Token);
            Snackbar.Add("Sync completed", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Sync failed: {ex.Message}", Severity.Error);
        }
        finally
        {
            _syncingFolderId = null;
            await LoadSyncedFoldersAsync();
            await LoadConflictsAsync();
        }
    }

    private async Task SyncAllAsync()
    {
        if (_syncingAll) return;

        _syncingAll = true;
        _lastSyncAllMessage = null;
        StateHasChanged();

        try
        {
            var enabledCount = _syncedFolders.Count(f => f.IsEnabled);
            if (enabledCount == 0)
            {
                _lastSyncAllMessage = "No enabled folders to sync. Add and enable folders first.";
                _lastSyncAllSeverity = Severity.Info;
                Snackbar.Add(_lastSyncAllMessage, Severity.Info);
                return;
            }

            var (success, failed) = await SyncWorker.TriggerSyncAllAsync(_cts.Token);
            _lastSyncAllUtc = DateTime.UtcNow;

            if (failed == 0 && success > 0)
            {
                _lastSyncAllMessage = $"Successfully synced {success} folder(s)";
                _lastSyncAllSeverity = Severity.Success;
            }
            else if (failed > 0 && success > 0)
            {
                _lastSyncAllMessage = $"Synced {success} folder(s), {failed} failed";
                _lastSyncAllSeverity = Severity.Warning;
            }
            else if (failed > 0)
            {
                _lastSyncAllMessage = $"All {failed} folder(s) failed to sync";
                _lastSyncAllSeverity = Severity.Error;
            }
            else
            {
                _lastSyncAllMessage = "No folders were synced";
                _lastSyncAllSeverity = Severity.Info;
            }

            Snackbar.Add(_lastSyncAllMessage, _lastSyncAllSeverity);

            await LoadSyncedFoldersAsync();
            await LoadConflictsAsync();
        }
        catch (Exception ex)
        {
            _lastSyncAllMessage = $"Sync failed: {ex.Message}";
            _lastSyncAllSeverity = Severity.Error;
            Snackbar.Add(_lastSyncAllMessage, Severity.Error);
        }
        finally
        {
            _syncingAll = false;
        }
    }

    private async Task ToggleSyncEnabledAsync(SyncedSharePointFolder folder)
    {
        try
        {
            await using var db = await DbContextFactory.CreateDbContextAsync(_cts.Token);
            var dbFolder = await db.SyncedFolders.FindAsync([folder.Id], _cts.Token);
            if (dbFolder is not null)
            {
                dbFolder.IsEnabled = !dbFolder.IsEnabled;
                await db.SaveChangesAsync(_cts.Token);
                folder.IsEnabled = dbFolder.IsEnabled;
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Failed to toggle sync: {ex.Message}", Severity.Error);
        }
    }

    private async Task RemoveSyncFolderAsync(SyncedSharePointFolder folder)
    {
        var confirmed = await DialogService.ShowMessageBox(
            "Remove Sync Folder",
            $"Remove sync configuration for '{folder.SiteName}/{folder.DriveName}'? Local files will not be deleted.",
            yesText: "Remove",
            cancelText: "Cancel");

        if (confirmed != true)
            return;

        try
        {
            await using var db = await DbContextFactory.CreateDbContextAsync(_cts.Token);
            var dbFolder = await db.SyncedFolders.FindAsync([folder.Id], _cts.Token);
            if (dbFolder is not null)
            {
                db.SyncedFolders.Remove(dbFolder);
                await db.SaveChangesAsync(_cts.Token);
                _syncedFolders.Remove(folder);
                StateHasChanged();
                Snackbar.Add("Sync folder removed", Severity.Success);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Failed to remove: {ex.Message}", Severity.Error);
        }
    }

    private async Task OpenFolderAsync(string path)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                Snackbar.Add($"Folder not found: {path}", Severity.Warning);
                return;
            }

            var psi = new ProcessStartInfo
            {
                UseShellExecute = true
            };

            if (OperatingSystem.IsWindows())
            {
                psi.FileName = "explorer.exe";
                psi.Arguments = $"\"{path}\"";
            }
            else if (OperatingSystem.IsMacOS())
            {
                psi.FileName = "open";
                psi.Arguments = $"\"{path}\"";
            }
            else
            {
                psi.FileName = "xdg-open";
                psi.Arguments = $"\"{path}\"";
            }

            Process.Start(psi);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Failed to open folder: {ex.Message}", Severity.Error);
        }

        await Task.CompletedTask;
    }

    private async Task ResolveConflictAsync(int conflictId, string resolution)
    {
        try
        {
            await using var db = await DbContextFactory.CreateDbContextAsync(_cts.Token);
            var conflict = await db.SyncConflicts.FindAsync([conflictId], _cts.Token);
            if (conflict is not null)
            {
                conflict.Status = resolution;
                conflict.ResolvedAtUtc = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(_cts.Token);

                // If keeping remote, trigger a sync
                if (resolution == "KeepRemote")
                {
                    var file = await db.SyncedFiles.FindAsync([conflict.SyncedFileId], _cts.Token);
                    if (file is not null)
                    {
                        await TriggerSyncAsync(file.SyncedFolderId);
                    }
                }

                await LoadConflictsAsync();
                Snackbar.Add($"Conflict resolved: {resolution}", Severity.Success);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Failed to resolve conflict: {ex.Message}", Severity.Error);
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        var order = 0;
        double size = bytes;

        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }

    public void Dispose()
    {
        _refreshTimer?.Dispose();
        _cts.Cancel();
        _cts.Dispose();
    }
}
