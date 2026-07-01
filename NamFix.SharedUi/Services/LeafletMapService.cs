using Microsoft.JSInterop;
using NamFix.Shared.Contracts;

namespace NamFix.SharedUi.Services;

/// <summary>
/// <see cref="IMapService"/> implemented over Leaflet.js via JS interop. The JS module ships inside
/// this RCL's wwwroot, so both the web host and a future MAUI BlazorWebView reuse it unchanged.
/// </summary>
public sealed class LeafletMapService : IMapService, IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;
    private readonly Dictionary<string, DotNetObjectReference<PinCallback>> _pinCallbacks = new();

    public LeafletMapService(IJSRuntime js) => _js = js;

    private async Task<IJSObjectReference> ModuleAsync() =>
        _module ??= await _js.InvokeAsync<IJSObjectReference>(
            "import", "./_content/NamFix.SharedUi/js/leafletInterop.js");

    public async Task RenderAsync(string elementId, GeoPoint center, int zoom, IEnumerable<MapMarker> markers)
    {
        var module = await ModuleAsync();
        var payload = markers.Select(m => new
        {
            lat = m.Location.Latitude,
            lng = m.Location.Longitude,
            title = m.Title,
            popup = m.Popup,
            url = m.Url
        });
        await module.InvokeVoidAsync("render", elementId, center.Latitude, center.Longitude, zoom, payload);
    }

    public async Task EnablePinModeAsync(string elementId, Func<GeoPoint, Task> onPinned)
    {
        var module = await ModuleAsync();
        var callback = DotNetObjectReference.Create(new PinCallback(onPinned));
        _pinCallbacks[elementId] = callback;
        await module.InvokeVoidAsync("enablePinMode", elementId, callback);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var cb in _pinCallbacks.Values) cb.Dispose();
        if (_module is not null) await _module.DisposeAsync();
    }

    /// <summary>Bridges Leaflet click events back to the .NET callback supplied by the component.</summary>
    public sealed class PinCallback
    {
        private readonly Func<GeoPoint, Task> _onPinned;
        public PinCallback(Func<GeoPoint, Task> onPinned) => _onPinned = onPinned;

        [JSInvokable]
        public Task OnPinned(double lat, double lng) => _onPinned(new GeoPoint(lat, lng));
    }
}

/// <summary>Browser geolocation behind <see cref="IGeolocationService"/> for "near me".</summary>
public sealed class BrowserGeolocationService : IGeolocationService
{
    private readonly IJSRuntime _js;
    private readonly ApiErrorNotifier _notifier;
    public BrowserGeolocationService(IJSRuntime js, ApiErrorNotifier notifier)
    {
        _js = js;
        _notifier = notifier;
    }

    public async Task<GeoPoint?> GetCurrentLocationAsync()
    {
        try
        {
            var module = await _js.InvokeAsync<IJSObjectReference>(
                "import", "./_content/NamFix.SharedUi/js/leafletInterop.js");
            var coords = await module.InvokeAsync<double[]?>("getCurrentLocation");
            return coords is { Length: 2 } ? new GeoPoint(coords[0], coords[1]) : null;
        }
        catch (JSException ex)
        {
            // JS rejects with a user-safe reason (permission blocked, timeout, unsupported).
            _notifier.Report(ex.Message, $"Geolocation failed: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            _notifier.Report("Couldn't get your location. Please try again.", $"Geolocation failed: {ex}");
            return null;
        }
    }
}
