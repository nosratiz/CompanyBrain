using Microsoft.JSInterop;

namespace CompanyBrain.Dashboard.Services;

/// <summary>
/// Service to monitor network connectivity status using browser APIs.
/// </summary>
public sealed class NetworkStatusService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _module;
    private DotNetObjectReference<NetworkStatusService>? _dotNetRef;
    private bool _isOnline = true;
    
    public event Action<bool>? OnStatusChanged;
    
    public bool IsOnline => _isOnline;

    public NetworkStatusService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task InitializeAsync()
    {
        try
        {
            _module = await _jsRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./js/networkStatus.js");
            
            _dotNetRef = DotNetObjectReference.Create(this);
            _isOnline = await _module.InvokeAsync<bool>("initialize", _dotNetRef);
        }
        catch
        {
            // If JS interop fails, assume online
            _isOnline = true;
        }
    }

    [JSInvokable]
    public void UpdateNetworkStatus(bool isOnline)
    {
        if (_isOnline != isOnline)
        {
            _isOnline = isOnline;
            OnStatusChanged?.Invoke(isOnline);
        }
    }

    public async Task<bool> CheckConnectionAsync()
    {
        if (_module is null) return true;
        
        try
        {
            return await _module.InvokeAsync<bool>("checkConnection");
        }
        catch
        {
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("dispose");
                await _module.DisposeAsync();
            }
            catch
            {
                // Ignore disposal errors
            }
        }
        
        _dotNetRef?.Dispose();
    }
}
