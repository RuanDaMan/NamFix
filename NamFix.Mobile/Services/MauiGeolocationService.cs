using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
using NamFix.Shared.Contracts;
using NamFix.SharedUi.Services;

namespace NamFix.Mobile.Services;

/// <summary>
/// <see cref="IGeolocationService"/> for the MAUI host using the native MAUI Geolocation API instead
/// of the WebView's <c>navigator.geolocation</c>. This matters on Android: the native API raises the
/// OS runtime-permission prompt <em>inside the app</em> the first time (and when the permission is
/// merely unset), rather than silently failing and forcing the user out to system Settings — which is
/// what a bare WebView geolocation call does.
/// </summary>
public sealed class MauiGeolocationService : IGeolocationService
{
    private readonly ApiErrorNotifier _notifier;
    public MauiGeolocationService(ApiErrorNotifier notifier) => _notifier = notifier;

    public async Task<GeoPoint?> GetCurrentLocationAsync()
    {
        try
        {
            // Permission requests + location must run on the UI thread for the system dialog to show.
            return await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                    status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

                if (status != PermissionStatus.Granted)
                {
                    _notifier.Report(
                        "Location permission is needed to use your current location. Please allow it when prompted.",
                        "Geolocation: location permission not granted.");
                    return (GeoPoint?)null;
                }

                var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(15));
                var location = await Geolocation.Default.GetLocationAsync(request)
                               ?? await Geolocation.Default.GetLastKnownLocationAsync();

                return location is null ? null : new GeoPoint(location.Latitude, location.Longitude);
            });
        }
        catch (FeatureNotSupportedException)
        {
            _notifier.Report("Location isn't supported on this device.", "Geolocation: FeatureNotSupportedException.");
            return null;
        }
        catch (PermissionException ex)
        {
            _notifier.Report(
                "Location permission is needed to use your current location. Please allow it when prompted.",
                $"Geolocation: PermissionException: {ex}");
            return null;
        }
        catch (Exception ex)
        {
            _notifier.Report("Couldn't get your location. Please try again.", $"Geolocation failed: {ex}");
            return null;
        }
    }
}
