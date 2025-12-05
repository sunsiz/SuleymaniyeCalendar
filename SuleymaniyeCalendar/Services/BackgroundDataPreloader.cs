using System;
using System.Threading.Tasks;
using SuleymaniyeCalendar.ViewModels;

namespace SuleymaniyeCalendar.Services
{
    /// <summary>
    /// Service to preload commonly accessed data in the background for improved performance
    /// </summary>
    public class BackgroundDataPreloader
    {
        private readonly DataService _dataService;
        private volatile bool _hasPreloadedToday;

        public BackgroundDataPreloader(DataService dataService)
        {
            _dataService = dataService;
        }

        /// <summary>
        /// Start background preloading of data after app launch
        /// </summary>
        public async Task StartBackgroundPreloadAsync()
        {
            if (_hasPreloadedToday) return;

            try
            {
                // Start background preload after a short delay to not interfere with initial UI
                await Task.Delay(2000);

                // Preload monthly data in background
                // We don't check ViewModel state here to avoid captive dependency on Transient MonthViewModel
                // DataService handles caching, so this is safe to call repeatedly
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var location = await _dataService.GetCurrentLocationAsync(false);
                        if (location != null && location.Latitude != 0 && location.Longitude != 0)
                        {
                            // This will cache the data for when user navigates to MonthPage
                            await _dataService.GetMonthlyPrayerTimesHybridAsync(location, false);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Background preload failed: {ex.Message}");
                    }
                });

                _hasPreloadedToday = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Background preloader error: {ex.Message}");
            }
        }

        /// <summary>
        /// Force refresh background data
        /// </summary>
        public async Task RefreshBackgroundDataAsync()
        {
            try
            {
                var location = await _dataService.GetCurrentLocationAsync(false);
                if (location != null && location.Latitude != 0 && location.Longitude != 0)
                {
                    // Refresh data in background
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _dataService.GetMonthlyPrayerTimesHybridAsync(location, true);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Background refresh failed: {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Background refresh error: {ex.Message}");
            }
        }
    }
}
