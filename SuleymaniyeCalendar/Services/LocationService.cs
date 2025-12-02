using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using SuleymaniyeCalendar.Resources.Strings;
using System.Diagnostics;

namespace SuleymaniyeCalendar.Services;

public class LocationService
{
    private readonly PerformanceService _perf;

    public LocationService(PerformanceService perf)
    {
        _perf = perf;
    }

    /// <summary>
    /// Gets the current device location, with caching and permission handling.
    /// </summary>
    /// <param name="refreshLocation">If true, requests fresh GPS fix; otherwise uses cached location.</param>
    /// <returns>Current location or default fallback coordinates.</returns>
    public async Task<Location?> GetCurrentLocationAsync(bool refreshLocation)
    {
        var location = new Location(0.0, 0.0);
        
        // WINDOWS SAFEGUARD: Bypass runtime geolocation on WinUI to prevent crashes
        if (DeviceInfo.Platform == DevicePlatform.WinUI)
        {
            var lat = Preferences.Get("LastLatitude", 0.0);
            var lng = Preferences.Get("LastLongitude", 0.0);
            var alt = Preferences.Get("LastAltitude", 0.0);
            if (lat != 0.0 || lng != 0.0)
            {
                return new Location(lat, lng, alt);
            }
            // Fallback default (Istanbul)
            return new Location(41.0, 29.0, 114.0);
        }

        // If we have a saved location and no explicit refresh is requested, use it
        if (!refreshLocation && Preferences.Get("LocationSaved", false))
        {
            var lat = Preferences.Get("LastLatitude", 0.0);
            var lng = Preferences.Get("LastLongitude", 0.0);
            var alt = Preferences.Get("LastAltitude", 0.0);
            if (lat != 0.0 || lng != 0.0)
            {
                return new Location(lat, lng, alt);
            }
        }

        // Ensure permission
        var status = PermissionStatus.Unknown;
        if (DeviceInfo.Platform == DevicePlatform.WinUI)
        {
            status = PermissionStatus.Granted;
        }
        else
        {
            status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (refreshLocation || status != PermissionStatus.Granted)
            {
                status = await CheckAndRequestLocationPermission().ConfigureAwait(false);
            }
        }

        if (status != PermissionStatus.Granted)
        {
            return location;
        }

        try
        {
            if (!refreshLocation)
            {
                using (_perf.StartTimer("Location.LastKnown"))
                {
                    location = await Geolocation.Default.GetLastKnownLocationAsync().ConfigureAwait(false);
                }
            }

            if (location == null || refreshLocation)
            {
                using (_perf.StartTimer("Location.Request"))
                {
                    var waitTime = refreshLocation ? 10 : 5;
                    location = await RequestLocationAsync(waitDelay: waitTime, forceActiveGps: refreshLocation).ConfigureAwait(false);
                }

                // Fallbacks
                if (location is null && !refreshLocation)
                {
                    using (_perf.StartTimer("Location.Fallback.LastKnown"))
                    {
                        var lastKnown = await Geolocation.Default.GetLastKnownLocationAsync().ConfigureAwait(false);
                        if (lastKnown is not null)
                            location = lastKnown;
                    }

                    if (location is null)
                    {
                        var lat = Preferences.Get("LastLatitude", 0.0);
                        var lng = Preferences.Get("LastLongitude", 0.0);
                        var alt = Preferences.Get("LastAltitude", 0.0);
                        if (lat != 0.0 || lng != 0.0)
                        {
                            location = new Location(lat, lng, alt);
                        }
                    }

                    location ??= new Location(42.142, 29.218, 10);
                }
            }

            if (location != null && location.Latitude != 0 && location.Longitude != 0)
            {
                Debug.WriteLine($"Latitude: {location.Latitude}, Longitude: {location.Longitude}, Altitude: {location.Altitude}");
                Preferences.Set("LastLatitude", location.Latitude);
                Preferences.Set("LastLongitude", location.Longitude);
                Preferences.Set("LastAltitude", location.Altitude ?? 0);
                Preferences.Set("LocationSaved", true);
            }
        }
        catch (FeatureNotSupportedException fnsEx)
        {
            Debug.WriteLine($"Location not supported: {fnsEx.Message}");
        }
        catch (FeatureNotEnabledException fneEx)
        {
            Debug.WriteLine($"Location not enabled: {fneEx.Message}");
            if (Preferences.Get("LastLatitude", 0.0) != 0.0 && Preferences.Get("LastLongitude", 0.0) != 0.0)
            {
                location ??= new Location();
                location.Latitude = Preferences.Get("LastLatitude", 0.0);
                location.Longitude = Preferences.Get("LastLongitude", 0.0);
                location.Altitude = Preferences.Get("LastAltitude", 0.0);
            }
        }
        catch (PermissionException pEx)
        {
            Debug.WriteLine($"Location permission denied: {pEx.Message}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Location error: {ex.Message}");
        }

        return location;
    }

    public async Task<PermissionStatus> CheckAndRequestLocationPermission()
    {
        if (DeviceInfo.Platform == DevicePlatform.WinUI)
        {
            return PermissionStatus.Granted;
        }

        var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (status == PermissionStatus.Granted)
            return status;

        var firstAskKey = "LocationPermissionAsked";
        var alreadyAsked = Preferences.Get(firstAskKey, false);

        status = await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            return await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        });
        Preferences.Set(firstAskKey, true);

        if (status == PermissionStatus.Denied)
        {
            var shouldShow = Permissions.ShouldShowRationale<Permissions.LocationWhenInUse>();
            if (shouldShow)
            {
                await ShowToastAsync(AppResources.KonumIzniIcerik);
            }
            else if (alreadyAsked)
            {
                await ShowAlertAsync(AppResources.KonumIzniBaslik, AppResources.KonumIzniIcerik);
            }
        }
        return status;
    }

    public async Task<Location?> RequestLocationAsync(int waitDelay = 5, bool forceActiveGps = false)
    {
        if (DeviceInfo.Platform == DevicePlatform.WinUI)
        {
            return null;
        }
        Location? location = null;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(waitDelay + 10));
            
            var accuracy = forceActiveGps ? GeolocationAccuracy.Best : GeolocationAccuracy.Medium;
            var requestFast = new GeolocationRequest(accuracy, TimeSpan.FromSeconds(waitDelay));

            using (_perf.StartTimer("Location.Request.Fast"))
            {
                location = await Geolocation.Default.GetLocationAsync(requestFast, cts.Token).ConfigureAwait(false);
            }

            if (location is null && !cts.IsCancellationRequested)
            {
                var fallbackTimeout = TimeSpan.FromSeconds(Math.Max(waitDelay + 3, 8));
                var requestFallback = new GeolocationRequest(GeolocationAccuracy.Low, fallbackTimeout);
                using (_perf.StartTimer("Location.Request.Fallback"))
                {
                    location = await Geolocation.Default.GetLocationAsync(requestFallback, cts.Token).ConfigureAwait(false);
                }
            }
        }
        catch (FeatureNotSupportedException fnsEx)
        {
            await ShowAlertAsync(AppResources.CihazGPSDesteklemiyor, fnsEx.Message);
        }
        catch (FeatureNotEnabledException fneEx)
        {
            await ShowAlertAsync(AppResources.CihazGPSDesteklemiyor, fneEx.Message);
        }
        catch (PermissionException pEx)
        {
            await ShowAlertAsync(AppResources.KonumIzniBaslik, pEx.Message);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"RequestLocationAsync failed: {ex.Message}");
        }

        return location;
    }

    private async Task ShowToastAsync(string message)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            using var cts = new CancellationTokenSource();
            var toast = Toast.Make(message, ToastDuration.Long, 14);
            await toast.Show(cts.Token);
        });
    }

    private async Task ShowAlertAsync(string title, string message)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await Shell.Current.DisplayAlert(title, message, AppResources.Tamam);
        });
    }
}
