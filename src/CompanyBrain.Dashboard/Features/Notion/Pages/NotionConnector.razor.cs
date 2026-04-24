using CompanyBrain.Dashboard.Data;
using CompanyBrain.Dashboard.Features.AutoSync.Models;
using CompanyBrain.Dashboard.Features.Notion.Api;
using CompanyBrain.Dashboard.Features.Notion.Services;
using CompanyBrain.Dashboard.Services;
using CompanyBrain.Dashboard.Services.Dtos;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using MudBlazor;

namespace CompanyBrain.Dashboard.Features.Notion.Pages;

public partial class NotionConnector : IDisposable
{
    // ── Connection state ──────────────────────────────────────────────────────

    private bool _hasToken;
    private string _apiToken = string.Empty;
    private string _workspaceFilter = string.Empty;
    private bool _showToken;
    private bool _saving;
    private bool _testing;

    // ── Browse pages ──────────────────────────────────────────────────────────

    private bool _loadingPages;
    private List<NotionPageObject> _availablePages = [];
    private HashSet<string> _filteredPageIds = [];
    private int _browsePageIndex;
    private const int BrowsePageSize = 12;
    private IEnumerable<NotionPageObject> PagedNotionPages =>
        _availablePages.Skip(_browsePageIndex * BrowsePageSize).Take(BrowsePageSize);

    // ── Synced pages ──────────────────────────────────────────────────────────

    private bool _loadingSynced;
    private List<KnowledgeResourceDescriptor> _syncedPages = [];
    private SyncSchedule? _schedule;

    // ── Sync ──────────────────────────────────────────────────────────────────

    private bool _syncing;
    private string? _syncMessage;
    private Severity _syncSeverity = Severity.Normal;

    private readonly CancellationTokenSource _cts = new();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var (filter, hasToken) = await SettingsProvider.GetConfigAsync(_cts.Token);
            _workspaceFilter = filter;
            _hasToken = hasToken;
            _filteredPageIds = ParseFilteredIds(_workspaceFilter);

            await Task.WhenAll(LoadScheduleAsync(), LoadSyncedPagesAsync());
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to initialise Notion connector page");
            Snackbar.Add("Failed to load Notion settings", Severity.Error);
        }
    }

    // ── Connection ────────────────────────────────────────────────────────────

    private async Task SaveAsync()
    {
        _saving = true;
        StateHasChanged();
        try
        {
            if (string.IsNullOrEmpty(_apiToken) && _hasToken)
            {
                // Keep existing token — only update the filter
                var existing = await SettingsProvider.GetDecryptedTokenAsync(_cts.Token) ?? string.Empty;
                await SettingsProvider.SaveAsync(existing, _workspaceFilter, _cts.Token);
            }
            else
            {
                await SettingsProvider.SaveAsync(_apiToken, _workspaceFilter, _cts.Token);
            }

            var (filter, hasToken) = await SettingsProvider.GetConfigAsync(_cts.Token);
            _workspaceFilter = filter;
            _hasToken = hasToken;
            _apiToken = string.Empty;
            _filteredPageIds = ParseFilteredIds(_workspaceFilter);

            await EnsureScheduleExistsAsync();
            Snackbar.Add("Notion settings saved", Severity.Success);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to save Notion settings");
            Snackbar.Add($"Save failed: {ex.Message}", Severity.Error);
        }
        finally
        {
            _saving = false;
        }
    }

    private async Task TestConnectionAsync()
    {
        _testing = true;
        StateHasChanged();
        try
        {
            var token = !string.IsNullOrEmpty(_apiToken)
                ? _apiToken
                : await SettingsProvider.GetDecryptedTokenAsync(_cts.Token);

            if (string.IsNullOrEmpty(token))
            {
                Snackbar.Add("No API token available — save one first.", Severity.Warning);
                return;
            }

            ApiClient.SetToken(token);
            var result = await ApiClient.GetBotUserAsync(_cts.Token);

            if (result.IsSuccess)
                Snackbar.Add($"Connected as: {result.Value.Name}", Severity.Success);
            else
                Snackbar.Add($"Connection failed: {result.Errors[0].Message}", Severity.Error);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Test error: {ex.Message}", Severity.Error);
        }
        finally
        {
            _testing = false;
        }
    }

    private async Task DisconnectAsync()
    {
        try
        {
            await SettingsProvider.SaveAsync(string.Empty, string.Empty, _cts.Token);
            _hasToken = false;
            _apiToken = string.Empty;
            _workspaceFilter = string.Empty;
            _filteredPageIds.Clear();
            _availablePages.Clear();
            Snackbar.Add("Notion disconnected", Severity.Info);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Disconnect failed: {ex.Message}", Severity.Error);
        }
    }

    // ── Browse pages ──────────────────────────────────────────────────────────

    private async Task LoadPagesAsync()
    {
        _loadingPages = true;
        StateHasChanged();
        try
        {
            var token = await SettingsProvider.GetDecryptedTokenAsync(_cts.Token);
            if (string.IsNullOrEmpty(token))
            {
                Snackbar.Add("No API token saved.", Severity.Warning);
                return;
            }

            ApiClient.SetToken(token);

            var pages = new List<NotionPageObject>();
            string? cursor = null;

            do
            {
                var result = await ApiClient.SearchPagesAsync(null, cursor, _cts.Token);
                if (result.IsFailed)
                {
                    Snackbar.Add($"Failed to load pages: {result.Errors[0].Message}", Severity.Error);
                    return;
                }

                pages.AddRange(result.Value.Results);
                cursor = result.Value.HasMore ? result.Value.NextCursor : null;

            } while (cursor is not null);

            _availablePages = pages;
            _browsePageIndex = 0;

            if (_availablePages.Count == 0)
                Snackbar.Add("No pages found. Make sure your integration has access to pages.", Severity.Info);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error loading pages: {ex.Message}", Severity.Error);
        }
        finally
        {
            _loadingPages = false;
        }
    }

    private async Task AddPageToFilterAsync(NotionPageObject page)
    {
        var id = NormalizeId(page.Id);
        _filteredPageIds.Add(id);
        _workspaceFilter = string.Join(", ", _filteredPageIds);
        StateHasChanged();

        // Persist immediately
        try
        {
            var existing = await SettingsProvider.GetDecryptedTokenAsync(_cts.Token) ?? string.Empty;
            await SettingsProvider.SaveAsync(existing, _workspaceFilter, _cts.Token);
            await EnsureScheduleExistsAsync();
            Snackbar.Add($"'{page.GetTitle()}' added to sync filter", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Failed to save filter: {ex.Message}", Severity.Error);
        }
    }

    // ── Synced pages ──────────────────────────────────────────────────────────

    private async Task LoadSyncedPagesAsync()
    {
        _loadingSynced = true;
        StateHasChanged();
        try
        {
            var all = await KnowledgeApi.ListResourcesAsync();
            var collection = _schedule?.CollectionName ?? "notion";
            _syncedPages = (all ?? [])
                .Where(r => r.Name.StartsWith($"resources/{collection}/", StringComparison.OrdinalIgnoreCase)
                         || r.Name.StartsWith($"{collection}/", StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => r.Title ?? r.Name)
                .ToList();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load synced Notion pages");
        }
        finally
        {
            _loadingSynced = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task LoadScheduleAsync()
    {
        try
        {
            await using var db = await DbFactory.CreateDbContextAsync(_cts.Token);
            _schedule = await db.SyncSchedules
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SourceType == SourceType.Notion, _cts.Token);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load Notion SyncSchedule");
        }
    }

    private async Task EnsureScheduleExistsAsync()
    {
        await using var db = await DbFactory.CreateDbContextAsync(_cts.Token);

        var existing = await db.SyncSchedules
            .FirstOrDefaultAsync(s => s.SourceType == SourceType.Notion, _cts.Token);

        if (existing is null)
        {
            db.SyncSchedules.Add(new SyncSchedule
            {
                SourceType = SourceType.Notion,
                SourceUrl = _workspaceFilter,
                CollectionName = "notion",
                CronExpression = "0 */6 * * *",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            });
        }
        else
        {
            existing.SourceUrl = _workspaceFilter;
        }

        await db.SaveChangesAsync(_cts.Token);
        await LoadScheduleAsync();
    }

    // ── Sync ──────────────────────────────────────────────────────────────────

    private async Task SyncNowAsync()
    {
        if (_syncing) return;
        _syncing = true;
        _syncMessage = null;
        StateHasChanged();
        try
        {
            await EnsureScheduleExistsAsync();
            await SyncWorker.ForceRunBySourceTypeAsync(SourceType.Notion, _cts.Token);
            await Task.WhenAll(LoadScheduleAsync(), LoadSyncedPagesAsync());
            await AuditService.LogAsync(CompanyBrain.Dashboard.Data.Audit.AuditEventType.SyncScheduleRun,
                new CompanyBrain.Dashboard.Services.Audit.AuditEntry(
                    ActorEmail: AuthStore.Email,
                    ResourceType: "Notion.AllPages",
                    ResourceName: "All Notion pages",
                    Metadata: new { Trigger = "Manual", Source = "NotionConnector", PageCount = _syncedPages.Count }));
            _syncMessage = $"Sync complete — {_syncedPages.Count} page(s) in knowledge base";
            _syncSeverity = Severity.Success;
            Snackbar.Add(_syncMessage, Severity.Success);
        }
        catch (Exception ex)
        {
            await AuditService.LogAsync(CompanyBrain.Dashboard.Data.Audit.AuditEventType.SyncScheduleRun,
                new CompanyBrain.Dashboard.Services.Audit.AuditEntry(
                    ActorEmail: AuthStore.Email,
                    ResourceType: "Notion.AllPages",
                    ResourceName: "All Notion pages",
                    Success: false,
                    ErrorMessage: ex.Message,
                    Metadata: new { Trigger = "Manual", Source = "NotionConnector" }));
            _syncMessage = $"Sync failed: {ex.Message}";
            _syncSeverity = Severity.Error;
            Snackbar.Add(_syncMessage, Severity.Error);
        }
        finally
        {
            _syncing = false;
            StateHasChanged();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string NormalizeId(string id) =>
        id.Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant();

    private static HashSet<string> ParseFilteredIds(string filter) =>
        (filter ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeId)
            .ToHashSet();

    private static string FormatBytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F1} GB"
    };

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
