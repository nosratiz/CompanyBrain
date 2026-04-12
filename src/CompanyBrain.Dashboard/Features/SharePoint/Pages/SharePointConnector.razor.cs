using CompanyBrain.Dashboard.Features.SharePoint.Data;
using CompanyBrain.Dashboard.Features.SharePoint.Models;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using System.Diagnostics;

namespace CompanyBrain.Dashboard.Features.SharePoint.Pages;

public partial class SharePointConnector : IDisposable
{
    // Authentication state
    private bool _isAuthenticated;
    private bool _isAuthenticating;
    private string? _userPrincipalName;
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

    private readonly CancellationTokenSource _cts = new();

    protected override async Task OnInitializedAsync()
    {
        await CheckAuthenticationAsync();
        await LoadSyncedFoldersAsync();
        await LoadConflictsAsync();
    }

    private async Task CheckAuthenticationAsync()
    {
        var token = await OAuthService.GetStoredTokenAsync(Options.Value.TenantId, _cts.Token);
        if (token is not null)
        {
            _isAuthenticated = true;
            _userPrincipalName = token.UserPrincipalName;
            _tenantId = token.TenantId;
        }
    }

    private async Task SignInAsync()
    {
        _isAuthenticating = true;
        StateHasChanged();

        try
        {
            var result = await OAuthService.AcquireTokenInteractiveAsync(_cts.Token);
            _isAuthenticated = true;
            _userPrincipalName = result.Account?.Username;
            _tenantId = result.TenantId;
            Snackbar.Add($"Signed in as {_userPrincipalName}", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Sign-in failed: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isAuthenticating = false;
        }
    }

    private async Task SignOutAsync()
    {
        // For now, just clear state - full sign-out would revoke tokens
        _isAuthenticated = false;
        _userPrincipalName = null;
        _tenantId = null;
        _sites = [];
        _selectedSite = null;
        _drives = [];
        _currentDriveItems.Clear();
        
        Snackbar.Add("Signed out", Severity.Info);
        await Task.CompletedTask;
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
            await LoadSyncedFoldersAsync();
            await LoadConflictsAsync();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Sync failed: {ex.Message}", Severity.Error);
        }
        finally
        {
            _syncingFolderId = null;
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
            if (OperatingSystem.IsWindows())
            {
                Process.Start("explorer.exe", path);
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start("open", path);
            }
            else
            {
                Process.Start("xdg-open", path);
            }
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
        _cts.Cancel();
        _cts.Dispose();
    }
}
