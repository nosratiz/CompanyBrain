using CompanyBrain.Dashboard.Features.Confluence.Data;
using CompanyBrain.Dashboard.Features.Confluence.Models;
using CompanyBrain.Dashboard.Features.Confluence.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using System.Diagnostics;

namespace CompanyBrain.Dashboard.Features.Confluence.Pages;

public partial class ConfluenceConnector : IDisposable
{
    // Connection state
    private bool _isConnected;
    private string _connectedDomain = string.Empty;
    private string _connectedEmail = string.Empty;

    // Credentials form
    private string _domain = string.Empty;
    private string _email = string.Empty;
    private string _apiToken = string.Empty;
    private bool _showToken;
    private bool _testing;

    // Spaces browser
    private bool _loadingSpaces;
    private List<ConfluenceSpace> _spaces = [];
    private string? _addingSpaceId;

    // Synced spaces
    private bool _loadingSyncedSpaces;
    private List<ConfluenceSyncedSpace> _syncedSpaces = [];
    private HashSet<string> _syncedSpaceIds = [];
    private int? _syncingSpaceId;

    // Sync all
    private bool _syncingAll;
    private string? _lastSyncMessage;
    private Severity _lastSyncSeverity = Severity.Normal;

    private readonly CancellationTokenSource _cts = new();

    protected override async Task OnInitializedAsync()
    {
        await CheckConnectionAsync();
        await LoadSyncedSpacesAsync();
    }

    private async Task CheckConnectionAsync()
    {
        var opts = await SettingsProvider.GetEffectiveOptionsAsync(_cts.Token);
        if (!string.IsNullOrEmpty(opts.Domain) && !string.IsNullOrEmpty(opts.ApiToken))
        {
            _isConnected = true;
            _connectedDomain = opts.Domain;
            _connectedEmail = opts.Email;
            _domain = opts.Domain;
            _email = opts.Email;
        }
    }

    private async Task TestAndSaveAsync()
    {
        _testing = true;
        StateHasChanged();

        try
        {
            // Temporarily save to test, then either keep or discard
            await SettingsProvider.SaveCredentialsAsync(_domain, _email, _apiToken, _cts.Token);

            var (success, error) = await ApiService.TestConnectionAsync(_cts.Token);
            if (!success)
            {
                await SettingsProvider.ClearCredentialsAsync(_cts.Token);
                Snackbar.Add($"Connection failed: {error}", Severity.Error);
                return;
            }

            _isConnected = true;
            _connectedDomain = _domain;
            _connectedEmail = _email;
            Snackbar.Add("Connected to Confluence successfully", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error: {ex.Message}", Severity.Error);
        }
        finally
        {
            _testing = false;
        }
    }

    private async Task DisconnectAsync()
    {
        await SettingsProvider.ClearCredentialsAsync(_cts.Token);
        _isConnected = false;
        _connectedDomain = string.Empty;
        _connectedEmail = string.Empty;
        _apiToken = string.Empty;
        _spaces.Clear();
        Snackbar.Add("Disconnected from Confluence", Severity.Info);
    }

    private async Task LoadSpacesAsync()
    {
        _loadingSpaces = true;
        StateHasChanged();

        try
        {
            _spaces = await ApiService.GetSpacesAsync(_cts.Token);
            if (_spaces.Count == 0)
                Snackbar.Add("No spaces found in your Confluence instance", Severity.Info);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Failed to load spaces: {ex.Message}", Severity.Error);
        }
        finally
        {
            _loadingSpaces = false;
        }
    }

    private async Task AddSpaceAsync(ConfluenceSpace space)
    {
        _addingSpaceId = space.Id;
        StateHasChanged();

        try
        {
            var record = await SyncService.ConfigureSyncSpaceAsync(space, _cts.Token);
            Snackbar.Add($"Space '{space.Name}' added — starting initial sync…", Severity.Success);
            await LoadSyncedSpacesAsync();
            await TriggerSyncAsync(record.Id);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Failed to add space: {ex.Message}", Severity.Error);
        }
        finally
        {
            _addingSpaceId = null;
        }
    }

    private async Task LoadSyncedSpacesAsync()
    {
        _loadingSyncedSpaces = true;
        StateHasChanged();

        try
        {
            await using var db = await DbContextFactory.CreateDbContextAsync(_cts.Token);
            _syncedSpaces = await db.SyncedSpaces
                .OrderByDescending(s => s.Id)
                .ToListAsync(_cts.Token);
            _syncedSpaceIds = _syncedSpaces.Select(s => s.SpaceId).ToHashSet();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Failed to load synced spaces: {ex.Message}", Severity.Error);
        }
        finally
        {
            _loadingSyncedSpaces = false;
        }
    }

    private async Task TriggerSyncAsync(int spaceId)
    {
        _syncingSpaceId = spaceId;
        StateHasChanged();

        try
        {
            await SyncWorker.TriggerSyncAsync(spaceId, _cts.Token);
            Snackbar.Add("Sync completed", Severity.Success);
            await LoadSyncedSpacesAsync();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Sync failed: {ex.Message}", Severity.Error);
        }
        finally
        {
            _syncingSpaceId = null;
        }
    }

    private async Task SyncAllAsync()
    {
        if (_syncingAll) return;

        _syncingAll = true;
        _lastSyncMessage = null;
        StateHasChanged();

        try
        {
            var (success, failed) = await SyncWorker.TriggerSyncAllAsync(_cts.Token);

            if (failed == 0 && success > 0)
            {
                _lastSyncMessage = $"Successfully synced {success} space(s)";
                _lastSyncSeverity = Severity.Success;
            }
            else if (failed > 0 && success > 0)
            {
                _lastSyncMessage = $"Synced {success}, {failed} failed";
                _lastSyncSeverity = Severity.Warning;
            }
            else if (failed > 0)
            {
                _lastSyncMessage = $"All {failed} space(s) failed";
                _lastSyncSeverity = Severity.Error;
            }
            else
            {
                _lastSyncMessage = "No spaces were synced";
                _lastSyncSeverity = Severity.Info;
            }

            Snackbar.Add(_lastSyncMessage, _lastSyncSeverity);
            await LoadSyncedSpacesAsync();
        }
        catch (Exception ex)
        {
            _lastSyncMessage = $"Sync failed: {ex.Message}";
            _lastSyncSeverity = Severity.Error;
            Snackbar.Add(_lastSyncMessage, Severity.Error);
        }
        finally
        {
            _syncingAll = false;
        }
    }

    private async Task ToggleSpaceEnabledAsync(ConfluenceSyncedSpace space)
    {
        try
        {
            await using var db = await DbContextFactory.CreateDbContextAsync(_cts.Token);
            var record = await db.SyncedSpaces.FindAsync([space.Id], _cts.Token);
            if (record is not null)
            {
                record.IsEnabled = !record.IsEnabled;
                await db.SaveChangesAsync(_cts.Token);
                space.IsEnabled = record.IsEnabled;
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Failed to toggle space: {ex.Message}", Severity.Error);
        }
    }

    private async Task RemoveSpaceAsync(ConfluenceSyncedSpace space)
    {
        var confirmed = await DialogService.ShowMessageBox(
            "Remove Synced Space",
            $"Remove sync configuration for '{space.SpaceName}'? Local files will not be deleted.",
            yesText: "Remove",
            cancelText: "Cancel");

        if (confirmed != true) return;

        try
        {
            await using var db = await DbContextFactory.CreateDbContextAsync(_cts.Token);
            var record = await db.SyncedSpaces.FindAsync([space.Id], _cts.Token);
            if (record is not null)
            {
                db.SyncedSpaces.Remove(record);
                await db.SaveChangesAsync(_cts.Token);
                _syncedSpaces.Remove(space);
                _syncedSpaceIds.Remove(space.SpaceId);
                StateHasChanged();
                Snackbar.Add("Synced space removed", Severity.Success);
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

            var psi = new ProcessStartInfo { UseShellExecute = true };

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

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
