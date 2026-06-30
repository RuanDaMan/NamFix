namespace NamFix.Shared.Contracts;

/// <summary>
/// Geographic point. Shared by maps, geolocation, and distance sorting.
/// </summary>
public readonly record struct GeoPoint(double Latitude, double Longitude);

/// <summary>
/// Map provider abstraction. The web host implements this over Leaflet.js via JS interop;
/// a future MAUI host could swap in a native map without touching UI components.
/// </summary>
public interface IMapService
{
    /// <summary>Render a map into the element with the given id and drop the supplied markers.</summary>
    Task RenderAsync(string elementId, GeoPoint center, int zoom, IEnumerable<MapMarker> markers);

    /// <summary>Enable click-to-pin and report the chosen location via the callback.</summary>
    Task EnablePinModeAsync(string elementId, Func<GeoPoint, Task> onPinned);
}

public record MapMarker(GeoPoint Location, string Title, string? Popup = null, string? Url = null);

/// <summary>
/// Device geolocation. Web uses the browser Geolocation API; MAUI uses Essentials.
/// Behind an interface so "near me" works the same in both hosts.
/// </summary>
public interface IGeolocationService
{
    Task<GeoPoint?> GetCurrentLocationAsync();
}

/// <summary>
/// Token persistence. Web uses localStorage; MAUI uses SecureStorage. Keeping this behind an
/// interface is why JWT storage is not hard-coded into the UI.
/// </summary>
public interface ITokenStore
{
    Task<string?> GetAccessTokenAsync();
    Task<string?> GetRefreshTokenAsync();
    Task SetTokensAsync(string accessToken, string refreshToken);
    Task ClearAsync();
}

/// <summary>
/// Generic secure key/value storage abstraction for any host-specific secret persistence.
/// </summary>
public interface ISecureStorage
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string value);
    Task RemoveAsync(string key);
}
