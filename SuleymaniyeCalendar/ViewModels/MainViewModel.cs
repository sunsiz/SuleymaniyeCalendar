#nullable enable

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using SuleymaniyeCalendar.Helpers;
using SuleymaniyeCalendar.Models;
using SuleymaniyeCalendar.Resources.Strings;
using SuleymaniyeCalendar.Services;
using SuleymaniyeCalendar.Views;
using Calendar = SuleymaniyeCalendar.Models.Calendar;

namespace SuleymaniyeCalendar.ViewModels;

/// <summary>
/// Main ViewModel for the prayer times home page.
/// Displays current day's prayer times with countdown timer and location info.
/// </summary>
/// <remarks>
/// Key responsibilities:
/// - Display 8 prayer times with temporal states (Past/Current/Future)
/// - Update countdown timer every second with animated progress
/// - Handle location refresh and geocoding for city display
/// - Schedule monthly alarms when reminders are enabled
/// - Detect midnight rollover for automatic day transitions
/// </remarks>
public partial class MainViewModel : BaseViewModel
{
    #region Private Fields

    /// <summary>
    /// Current day's prayer calendar data.
    /// Access is thread-safe via volatile read/write.
    /// </summary>
    private volatile Calendar? _calendar;

    /// <summary>
    /// Cached parsed prayer times to avoid repeated TimeSpan.Parse calls.
    /// Updated only when _calendar changes, not every second.
    /// </summary>
    private CachedPrayerTimes? _cachedTimes;

    /// <summary>
    /// Data service for prayer times, location, and alarm scheduling.
    /// </summary>
    private DataService _data = null!;

    /// <summary>
    /// Background task for initial data loading on startup.
    /// </summary>
    private Task? _startupRefreshTask;

    /// <summary>
    /// Background task for weekly alarm scheduling.
    /// </summary>
    private Task? _weeklyAlarmsTask;

    /// <summary>
    /// Timestamp of last UI refresh to throttle duplicate updates.
    /// </summary>
    private DateTimeOffset _lastUiRefresh = DateTimeOffset.MinValue;

    /// <summary>
    /// Minimum interval between UI refreshes to prevent churn.
    /// </summary>
    private static readonly TimeSpan UiRefreshThrottle = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Track current date for midnight rollover detection (PHASE 18).
    /// </summary>
    private DateTime _lastKnownDate = DateTime.Today;

    /// <summary>
    /// Track last minute to detect prayer window changes and optimize state updates.
    /// </summary>
    private int _lastMinute = DateTime.Now.Minute;

    /// <summary>
    /// Single-flight guard for UI refresh. 0 = idle, 1 = running.
    /// </summary>
    private int _refreshing;

    /// <summary>
    /// Single-flight guard for manual location refresh (pull-to-refresh). 0 = idle, 1 = running.
    /// </summary>
    private int _locationRefreshing;

    /// <summary>
    /// Timer for countdown tick updates every second.
    /// </summary>
    private IDispatcherTimer? _ticker;

    /// <summary>
    /// Handler for ticker events, stored for proper unsubscription.
    /// </summary>
    private EventHandler? _tickHandler;

    /// <summary>
    /// Prevents double navigation on fast taps.
    /// </summary>
    private bool _isNavigating;

    /// <summary>
    /// Performance monitoring service for timing operations.
    /// </summary>
    private readonly PerformanceService _perf;

    #endregion

    #region Observable Properties

    /// <summary>
    /// Gets or sets the collection of prayer times displayed in the UI.
    /// </summary>
    private ObservableCollection<Prayer>? prayers;
    public ObservableCollection<Prayer>? Prayers { get => prayers; set => SetProperty(ref prayers, value); }

    /// <summary>
    /// Gets or sets the formatted remaining time until next prayer.
    /// </summary>
    private string remainingTime = string.Empty;
    public string RemainingTime { get => remainingTime; set => SetProperty(ref remainingTime, value); }

    /// <summary>
    /// Gets or sets the progress percentage (0.0 to 1.0) for animated gradient (PHASE 17).
    /// </summary>
    private double timeProgress;
    public double TimeProgress { get => timeProgress; set => SetProperty(ref timeProgress, value); }

    /// <summary>
    /// Gets or sets the current city name from geocoding.
    /// </summary>
    private string city = string.Empty;
    public string City { get => city; set => SetProperty(ref city, value); }

    /// <summary>
    /// Gets or sets whether pull-to-refresh is active. Separate from IsBusy to avoid coupling.
    /// </summary>
    private bool isRefreshing;
    public bool IsRefreshing { get => isRefreshing; set => SetProperty(ref isRefreshing, value); }

    /// <summary>
    /// Gets or sets the currently selected prayer for CollectionView binding.
    /// </summary>
    private Prayer? selectedPrayer;
    public Prayer? SelectedPrayer { get => selectedPrayer; set => SetProperty(ref selectedPrayer, value); }

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="MainViewModel"/> class.
    /// </summary>
    /// <param name="dataService">The data service for prayer times and location.</param>
    /// <param name="perf">Optional performance monitoring service.</param>
    public MainViewModel(DataService dataService, PerformanceService? perf = null)
        {
            IsBusy = true; // Show loading indicator initially
            _perf = perf ?? new PerformanceService();
            Debug.WriteLine("TimeStamp-MainViewModel-Start", DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss.fff tt"));
            if (DeviceInfo.Platform == DevicePlatform.Android && DeviceInfo.Version.Major >= 10)
            {
                //Prevent undesired behaviors caused by old settings.
                Preferences.Set("falsefajrAlarm", false);
                Preferences.Set("fajrAlarm", false);
                Preferences.Set("sunriseAlarm", false);
                Preferences.Set("dhuhrAlarm", false);
                Preferences.Set("asrAlarm", false);
                Preferences.Set("maghribAlarm", false);
                Preferences.Set("ishaAlarm", false);
                Preferences.Set("endofishaAlarm", false);
            }
            CultureInfo cultureInfo = new CultureInfo(Preferences.Get("SelectedLanguage", "tr"));
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = cultureInfo;
            Title = AppResources.PageTitle;
            Prayers = new ObservableCollection<Prayer>();
            _data = dataService;
            _calendar = _data.calendar;
            try
            {
                // Lightweight initial population; deeper refresh is coalesced in OnAppearing
                LoadPrayers();

                // Fire and forget non-UI work
                _ = Task.Run(() => GetCity());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MainViewModel initialization error: {ex.Message}");
            }
#if WINDOWS
            // On Windows we run in MOCK mode: no permission prompts, no geolocation queries.
            // Ensure a mock/cached city is visible immediately and skip first-run auto location logic.
            var mockCity = Preferences.Get("sehir", "Istanbul");
            City = mockCity;
#endif

            if (DeviceInfo.Platform != DevicePlatform.WinUI)
            {
                if (!Preferences.Get("LocationSaved", false))
                {
                    _ = CheckLocationInfoAsync(3000);
                }
                else
                {
                    _ = InitializeDataFromCacheAsync();
                }
            }

            if (DeviceInfo.Platform != DevicePlatform.WinUI && Preferences.Get("AlwaysRenewLocationEnabled", false))
                _startupRefreshTask = RefreshLocationCommand.ExecuteAsync(null);
            if (_data.CheckRemindersEnabledAny())
            {
                var lastAlarmDateStr = Preferences.Get("LastAlarmDate", "Empty");
                if (lastAlarmDateStr != "Empty")
                {
                    DateTime lastAlarm;
                    if (DateTime.TryParse(lastAlarmDateStr, out lastAlarm) || DateTime.TryParseExact(lastAlarmDateStr, new[] { "dd/MM/yyyy", "dd-MM-yyyy", "yyyy-MM-dd" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out lastAlarm))
                    {
                        if ((lastAlarm - DateTime.Today).Days > 4)
                            _weeklyAlarmsTask = _data.SetMonthlyAlarmsAsync();
                    }
                }
            }
            IsBusy = false;
            Debug.WriteLine("TimeStamp-ItemsViewModel-Finish", DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss.fff tt"));

            // Icon/UI refresh is handled by OnAppearing -> RefreshUiAsync()
        }

    #endregion

    #region Commands

        /// <summary>
        /// Opens the device's map application centered on the current location.
        /// </summary>
        [RelayCommand]
        private async Task GoToMap()
        {
            try
            {
#if WINDOWS
                // If no Windows MapServiceToken provided, skip map/placemark usage to avoid crash.
                var token = Preferences.Get("MapServiceToken", string.Empty);
                if (string.IsNullOrWhiteSpace(token))
                {
                    ShowToast(AppResources.HaritaHatasi);
                    return;
                }
#endif
                if (_calendar == null)
                {
                    ShowToast(AppResources.HaritaHatasi);
                    return;
                }
                // Store local copy of volatile field for thread safety
                var cal = _calendar;
                var location = new Location(Convert.ToDouble(cal.Latitude, CultureInfo.InvariantCulture.NumberFormat), Convert.ToDouble(cal.Longitude, CultureInfo.InvariantCulture.NumberFormat));
                var placeMark = await Geocoding.Default.GetPlacemarksAsync(Convert.ToDouble(cal.Latitude, CultureInfo.InvariantCulture.NumberFormat), Convert.ToDouble(cal.Longitude, CultureInfo.InvariantCulture.NumberFormat)).ConfigureAwait(true);
                // Handle case where all placemarks might be null
                var options = new MapLaunchOptions { Name = placeMark.FirstOrDefault()?.Thoroughfare ?? placeMark.FirstOrDefault()?.CountryName ?? "Location" };
                await Map.OpenAsync(location, options).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ShowToast(AppResources.HaritaHatasi + ex.Message);
            }
        }

        /// <summary>
        /// Refreshes the current location and updates prayer times.
        /// Used by pull-to-refresh and refresh button. Shows overlay during operation.
        /// </summary>
        [RelayCommand]
        public async Task RefreshLocation()
        {
            // Prevent concurrent refreshes (pull-to-refresh or button)
            if (System.Threading.Interlocked.Exchange(ref _locationRefreshing, 1) == 1)
                return;

            // Ensure permission up-front so the system prompt is shown predictably on user action
            var permission = await _data.CheckAndRequestLocationPermission().ConfigureAwait(false);
            if (permission != PermissionStatus.Granted)
            {
                await MainThread.InvokeOnMainThreadAsync(() => ShowToast(AppResources.KonumIzniIcerik));
                System.Threading.Volatile.Write(ref _locationRefreshing, 0);
                return;
            }

            // Immediately show overlay (single consistent indicator) and suppress RefreshView spinner
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                // If user pulled to refresh, MAUI may have set IsRefreshing=true automatically; cancel it.
                if (IsRefreshing) IsRefreshing = false;
                OverlayMessage = AppResources.Yenileniyor + "...";
                ShowOverlay = true;
            });

            // Kick off the heavy pipeline in the background
            var pipelineTask = Task.Run(async () => await RunLocationRefreshPipelineAsync());

            // No auto-dismiss; overlay closes when pipeline completes.

            // Return quickly; the background pipeline will update UI and reset internal state when done
            // Still yield once to keep async command behavior consistent
            await Task.Yield();
        }

        private async Task RunLocationRefreshPipelineAsync()
        {
            try
            {
                // Force location refresh when user pulls to refresh or taps refresh
                var location = await _data.GetCurrentLocationAsync(refreshLocation: true).ConfigureAwait(false);
                if (location != null && location.Latitude != 0 && location.Longitude != 0)
                {
                    // Ensure overlay visible if a background trigger (like first permission grant) invoked this without UI state
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        if (!ShowOverlay)
                        {
                            OverlayMessage = AppResources.Yenileniyor + "...";
                            ShowOverlay = true;
                        }
                    });
                    // Get fresh prayer times with the updated location
                    _calendar = await _data.GetTodayPrayerTimesAsync(refreshLocation: true).ConfigureAwait(false);
                    if (_calendar != null)
                    {
                        _cachedTimes = null; // Invalidate cached times
                        _data.calendar = _calendar; // sync service state
                    }

                    // Refresh monthly data (alarms depend on this); network may take time
                    var monthlyData = await _data.GetMonthlyPrayerTimesHybridAsync(location, forceRefresh: true).ConfigureAwait(false);
                    if (_data.CheckRemindersEnabledAny())
                    {
                        // Schedule notifications (can be slow on Android); keep off UI thread
                        await _data.SetMonthlyAlarmsAsync(forceReschedule: true).ConfigureAwait(false);
                    }                    

                    // Coalesced UI update after background work
                    await RefreshUiAsync(force: true).ConfigureAwait(false);
                    await MainThread.InvokeOnMainThreadAsync(() => ShowToast(AppResources.KonumYenilendi));

                    // Fire and forget non-UI work
                    _ = Task.Run(() => GetCity());
                }
                else
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        // Permission likely not granted or GPS disabled
                        ShowToast(AppResources.KonumIzniIcerik);
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RefreshLocation pipeline error: {ex}");
                // Do not show generic internet toast on any exception; be silent here to avoid false positives
            }
            finally
            {
                // Safety: ensure flags are reset even if auto-dismiss already ran
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    IsRefreshing = false; // safety
                    ShowOverlay = false;
                    OverlayMessage = null;
                });
                System.Threading.Volatile.Write(ref _locationRefreshing, 0);
            }
        }

        /// <summary>
        /// Navigates to the prayer detail page when a prayer is selected.
        /// Uses SelectedPrayer bound from XAML CollectionView.
        /// </summary>
        [RelayCommand]
        private async Task PrayerSelected()
        {
            var prayer = SelectedPrayer;
            if (prayer is null || _isNavigating) return;

            try
            {
                _isNavigating = true;

                await Shell.Current.GoToAsync(
                    nameof(PrayerDetailPage),
                    true,
                    new Dictionary<string, object> { { nameof(PrayerDetailViewModel.PrayerId), prayer.Id } }
                ).ConfigureAwait(false);
            }
            finally
            {
                // Clear selection so same item can be tapped again; do it on UI thread
                MainThread.BeginInvokeOnMainThread(() => SelectedPrayer = null);
                _isNavigating = false;
            }
        }

        /// <summary>
        /// Navigates to the monthly prayer times calendar page.
        /// </summary>
        [RelayCommand]
        private async Task GoToMonth()
        {
            if (_isNavigating) return;
            _isNavigating = true;
            try
            {
                await Shell.Current.GoToAsync(nameof(MonthPage), animate: false).ConfigureAwait(false);
            }
            finally
            {
                _isNavigating = false;
            }
        }

        /// <summary>
        /// Navigates to the application settings page.
        /// </summary>
        [RelayCommand]
        private async Task Settings()
        {
            IsBusy = true;
            await Shell.Current.GoToAsync($"{nameof(SettingsPage)}").ConfigureAwait(false);
            IsBusy = false;
        }

        /// <summary>
        /// Toggles the enabled state of a prayer's notification.
        /// Reschedules alarms in the background after the toggle.
        /// </summary>
        /// <param name="prayer">The prayer to toggle.</param>
        [RelayCommand]
        private void TogglePrayerEnabled(Prayer? prayer)
        {
            if (prayer == null) return;

            try
            {
                prayer.Enabled = !prayer.Enabled;
                Preferences.Set(prayer.Id + "Enabled", prayer.Enabled);

                Task.Run(async () =>
                {
                    try
                    {
                        await _data.SetMonthlyAlarmsAsync(forceReschedule: true).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error updating alarms: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error toggling prayer: {ex.Message}");
            }
        }

    #endregion

    #region Private Methods - Location

        /// <summary>
        /// Performs reverse geocoding to get the city name from current coordinates.
        /// Uses a 5-second timeout to prevent hanging. Falls back to cached city on failure.
        /// </summary>
        private async Task GetCityAsync()
        {
            try
            {
                // Add timeout to prevent hanging
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

#if WINDOWS
                // Provide mock city immediately when no token; avoids any Geocoding API call (which crashes without token).
                var mapToken = Preferences.Get("MapServiceToken", string.Empty);
                if (string.IsNullOrWhiteSpace(mapToken))
                {
                    var mockCity = Preferences.Get("sehir", "Istanbul");
                    await MainThread.InvokeOnMainThreadAsync(() => City = mockCity);
                    Debug.WriteLine("[MainViewModel] Using mock city on Windows (no MapServiceToken). Skipping Geocoding.");
                    return;
                }
#endif
                // Non-Windows OR Windows with valid token proceeds to real reverse geocoding.
                if (_calendar == null)
                {
                    Debug.WriteLine("GetCityAsync: _calendar is null, using cached city");
                    return;
                }
                var placemark = await Geocoding.Default
                    .GetPlacemarksAsync(
                        Convert.ToDouble(_calendar.Latitude, CultureInfo.InvariantCulture.NumberFormat),
                        Convert.ToDouble(_calendar.Longitude, CultureInfo.InvariantCulture.NumberFormat))
                    .ConfigureAwait(false);

                var city = placemark?.FirstOrDefault()?.Locality ??
                           placemark?.FirstOrDefault()?.AdminArea ??
                           placemark?.FirstOrDefault()?.SubAdminArea ??
                           placemark?.FirstOrDefault()?.CountryName;

                if (!string.IsNullOrWhiteSpace(city))
                {
                    await MainThread.InvokeOnMainThreadAsync(() => City = city);
                    Preferences.Set("sehir", city);
                }
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("GetCityAsync: Operation timed out after 5 seconds");
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"GetCityAsync error: {exception}");
            }

            // Fallback to cached city if geocoding failed
            if (string.IsNullOrEmpty(City))
            {
                var cachedCity = Preferences.Get("sehir", AppResources.Sehir);
                await MainThread.InvokeOnMainThreadAsync(() => City = cachedCity);
            }
        }

        /// <summary>
        /// Legacy synchronous wrapper for <see cref="GetCityAsync"/>.
        /// Kept for backward compatibility with fire-and-forget calls.
        /// </summary>
        private void GetCity()
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await GetCityAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"GetCity error: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Tracks whether initial location check has been performed.
        /// </summary>
        private bool _initialLocationChecked;

        /// <summary>
        /// Checks location permission and fetches prayer times on first app launch.
        /// Shows overlay during operation and schedules alarms if reminders enabled.
        /// </summary>
        /// <param name="timeDelay">Initial delay in milliseconds (clamped to max 500ms).</param>
        private async Task CheckLocationInfoAsync(int timeDelay)
        {
            if (_initialLocationChecked)
                return;
            // Shorten initial delay for faster perceived startup on first run
            timeDelay = Math.Min(timeDelay, 500);

            // Show lightweight overlay to communicate background work
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                OverlayMessage = AppResources.Yenileniyor + "...";
                ShowOverlay = true;
            });

            var status = await _data.CheckAndRequestLocationPermission().ConfigureAwait(false);
            if (status != PermissionStatus.Granted)
            {
                // Fast dismiss overlay if user denied; no need to linger
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    ShowOverlay = false;
                    OverlayMessage = null;
                });
                return;
            }

            if (!_data.HaveInternet())
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    ShowOverlay = false;
                    OverlayMessage = null;
                });
                return;
            }
            Debug.WriteLine($"**** {this.GetType().Name}.{nameof(CheckLocationInfoAsync)}: Starting at {DateTime.Now}");
            await Task.Delay(timeDelay).ConfigureAwait(false);
            var calendar = _data.calendar;
            calendar = await _data.GetTodayPrayerTimesAsync().ConfigureAwait(false);
            
            // Ensure we capture the calendar if we got one, even if we don't enter the default coordinates block
            if (calendar != null)
            {
                _calendar = calendar;
                _cachedTimes = null; // Invalidate cached times
                _data.calendar = calendar;
            }

            if (calendar != null && ((calendar.Altitude == 114.0 && calendar.Latitude == 41.0 && calendar.Longitude == 29.0) || (calendar.Altitude == 0 && calendar.Latitude == 0 && calendar.Longitude == 0)))
            {
                // Default coordinates detected, need fresh location
                _calendar = await _data.GetTodayPrayerTimesAsync(refreshLocation: true).ConfigureAwait(false);
                if (_calendar != null)
                {
                    _cachedTimes = null; // Invalidate cached times
                    _data.calendar = _calendar; // keep service field synchronized
                }
                var location = await _data.GetCurrentLocationAsync(false).ConfigureAwait(false);
                if (location != null && location.Latitude != 0 && location.Longitude != 0)
                {
                    var monthly = await _data.FetchSpecificMonthAsync(location, DateTime.Now.Month, DateTime.Now.Year).ConfigureAwait(false);
                    // If monthly data contains today, update calendars before UI refresh
                    var today = monthly?.FirstOrDefault(d => d.Date == DateTime.Today.ToString("dd.MM.yyyy") || d.Date == DateTime.Today.ToString("dd/MM/yyyy"));
                    if (today != null)
                    {
                        _calendar = today;
                        _cachedTimes = null; // Invalidate cached times
                        _data.calendar = today;
                    }
                }
            }
            if (_data.CheckRemindersEnabledAny())
            {
                await _data.SetMonthlyAlarmsAsync().ConfigureAwait(false);
            }            

            // Coalesced UI update
            await RefreshUiAsync(force: true).ConfigureAwait(false);

            // Success toast (location + times refreshed)
            await MainThread.InvokeOnMainThreadAsync(() => ShowToast(AppResources.KonumYenilendi));

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                ShowOverlay = false;
                OverlayMessage = null;
            });

            _initialLocationChecked = true;
        }

        private async Task InitializeDataFromCacheAsync()
        {
            try 
            {
                var calendar = await _data.GetTodayPrayerTimesAsync().ConfigureAwait(false);
                if (calendar != null)
                {
                    _calendar = calendar;
                    _cachedTimes = null; // Invalidate cached times
                    _data.calendar = calendar;
                    await RefreshUiAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"InitializeDataFromCacheAsync error: {ex.Message}");
            }
        }

    #endregion

    #region Private Methods - Prayer Loading

        /// <summary>
        /// Loads prayer times from the calendar model, computes temporal states, and updates the UI.
        /// Uses differential updates to minimize collection churn.
        /// </summary>
        /// <returns>The updated prayer collection.</returns>
        private ObservableCollection<Prayer>? LoadPrayers()
        {
            Debug.WriteLine("TimeStamp-MainViewModel-ExecuteLoadItemsCommand-Start", DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss.fff tt"));

            // Only adopt DataService.calendar if we don't already have a fresher _calendar.
            if (_calendar == null && _data.calendar != null)
            {
                _calendar = _data.calendar;
            }
            if (_calendar == null)
            {
                Debug.WriteLine("[MainViewModel] No calendar available yet; showing placeholder prayers.");
                // Show placeholder prayers immediately for better perceived performance
                if (Prayers == null || Prayers.Count == 0)
                {
                    Prayers = CreatePlaceholderPrayers();
                    RemainingTime = AppResources.Yenileniyor + "...";
                }
                return Prayers;
            }

            try
            {
                // Update cache if needed
                if (_cachedTimes == null)
                {
                    _cachedTimes = CachedPrayerTimes.FromCalendar(_calendar);
                }

                // Build unified prayers from the canonical Calendar model
                var list = _data.BuildPrayersFromCalendar(_calendar);

                // Compute temporal states using cached times (FAST)
                string[] seq = ["falsefajr", "fajr", "sunrise", "dhuhr", "asr", "maghrib", "isha", "endofisha"];
                
                // Use cached TimeSpans to construct DateTimes for today
                var today = DateTime.Today;
                var times = new Dictionary<string, DateTime>
                {
                    ["falsefajr"] = today.Add(_cachedTimes.FalseFajr),
                    ["fajr"] = today.Add(_cachedTimes.Fajr),
                    ["sunrise"] = today.Add(_cachedTimes.Sunrise),
                    ["dhuhr"] = today.Add(_cachedTimes.Dhuhr),
                    ["asr"] = today.Add(_cachedTimes.Asr),
                    ["maghrib"] = today.Add(_cachedTimes.Maghrib),
                    ["isha"] = today.Add(_cachedTimes.Isha),
                    ["endofisha"] = today.Add(_cachedTimes.EndOfIsha)
                };

                foreach (var id in seq)
                {
                    var p = list.FirstOrDefault(x => x.Id == id);
                    if (p is null) continue;
                    
                    DateTime current = times[id];
                    DateTime next;
                    
                    if (id == "endofisha")
                    {
                        // After EndOfIsha, next prayer is tomorrow's FalseFajr
                        next = times["falsefajr"].AddDays(1);
                    }
                    else
                    {
                        next = times[seq[Array.IndexOf(seq, id) + 1]];
                    }

                    p.State = CheckState(current, next);
                    p.StateDescription = GetStateDescription(p.State);
                    p.UpdateVisualState();

                    // Only create command if we are initializing the collection
                    // ApplyPrayerDiff preserves existing commands, so we only need these for the initial population
                    if (Prayers == null || Prayers.Count == 0)
                    {
                        // Wire a compiled-binding friendly navigation command on each item
                        p.NavigateCommand = new AsyncRelayCommand(async () =>
                        {
                            if (_isNavigating) return;
                            _isNavigating = true;
                            try
                            {
                                await Shell.Current.GoToAsync(
                                    nameof(PrayerDetailPage),
                                    true,
                                    new Dictionary<string, object> { { nameof(PrayerDetailViewModel.PrayerId), p.Id } }
                                ).ConfigureAwait(false);
                            }
                            finally
                            {
                                _isNavigating = false;
                            }
                        });
                    }
                }

                // Persist raw times consistently
                _data.SavePrayerTimesToPreferences(_calendar);

                // Apply diff (only 8 items) to minimize churn
                MainThread.BeginInvokeOnMainThread(() => ApplyPrayerDiff(list));
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

            return Prayers;
        }

        /// <summary>
        /// Applies differential updates to the Prayers collection to minimize UI churn.
        /// Only updates changed properties rather than replacing the entire collection.
        /// </summary>
        /// <param name="newList">The new prayer list to merge into the current collection.</param>
        private void ApplyPrayerDiff(List<Prayer> newList)
        {
            if (Prayers == null || Prayers.Count != newList.Count)
            {
                Prayers = new ObservableCollection<Prayer>(newList);
            }
            else
            {
                for (int i = 0; i < newList.Count; i++)
                {
                    var existing = Prayers[i];
                    var incoming = newList[i];
                    // Update only changed fields
                    // Also update localized Name when language changes – previously omitted so prayer names stayed stale after culture switch.
                    if (existing.Name != incoming.Name) existing.Name = incoming.Name;
                    if (existing.Time != incoming.Time) existing.Time = incoming.Time;
                    if (existing.IconPath != incoming.IconPath) existing.IconPath = incoming.IconPath;
                    if (existing.State != incoming.State)
                    {
                        existing.State = incoming.State;
                        existing.StateDescription = incoming.StateDescription;
                        existing.UpdateVisualState();
                    }
                    if (existing.Enabled != incoming.Enabled) existing.Enabled = incoming.Enabled;
                    // Keep NavigateCommand already assigned; no need to replace entire object
                }
            }
            Debug.WriteLine("[MainViewModel] ApplyPrayerDiff completed");
        }

        /// <summary>
        /// Creates placeholder prayer items with loading indicators for immediate UI feedback.
        /// Displayed while actual prayer times are being fetched.
        /// </summary>
        /// <returns>Collection of placeholder prayers with "--:--" times.</returns>
        private ObservableCollection<Prayer> CreatePlaceholderPrayers()
        {
            return new ObservableCollection<Prayer>
            {
                new Prayer { Id = "falsefajr", Name = AppResources.FecriKazip, Time = "--:--", IconPath = PrayerIconService.GetPrayerIconById("falsefajr"), State = "Waiting" },
                new Prayer { Id = "fajr", Name = AppResources.FecriSadik, Time = "--:--", IconPath = PrayerIconService.GetPrayerIconById("fajr"), State = "Waiting" },
                new Prayer { Id = "sunrise", Name = AppResources.SabahSonu, Time = "--:--", IconPath = PrayerIconService.GetPrayerIconById("sunrise"), State = "Waiting" },
                new Prayer { Id = "dhuhr", Name = AppResources.Ogle, Time = "--:--", IconPath = PrayerIconService.GetPrayerIconById("dhuhr"), State = "Waiting" },
                new Prayer { Id = "asr", Name = AppResources.Ikindi, Time = "--:--", IconPath = PrayerIconService.GetPrayerIconById("asr"), State = "Waiting" },
                new Prayer { Id = "maghrib", Name = AppResources.Aksam, Time = "--:--", IconPath = PrayerIconService.GetPrayerIconById("maghrib"), State = "Waiting" },
                new Prayer { Id = "isha", Name = AppResources.Yatsi, Time = "--:--", IconPath = PrayerIconService.GetPrayerIconById("isha"), State = "Waiting" },
                new Prayer { Id = "endofisha", Name = AppResources.YatsiSonu, Time = "--:--", IconPath = PrayerIconService.GetPrayerIconById("endofisha"), State = "Waiting" }
            };
        }

        /// <summary>
        /// Determines the temporal state of a prayer based on current and next prayer times.
        /// </summary>
        /// <param name="current">Start time of this prayer window.</param>
        /// <param name="next">Start time of the next prayer window.</param>
        /// <returns>"Passed", "Happening", or "Waiting" based on current time.</returns>
        private static string CheckState(DateTime current, DateTime next)
        {
            var now = DateTime.Now;
            // Use >= for boundary conditions to ensure no edge case returns empty string
            if (now >= next) return "Passed";
            if (now >= current) return "Happening"; // Implicitly now < next
            return "Waiting";
        }

        /// <summary>
        /// Converts state code to human-readable description for accessibility.
        /// </summary>
        /// <param name="state">The state code (Happening/Passed/Waiting).</param>
        /// <returns>User-friendly state description.</returns>
        private string GetStateDescription(string? state)
        {
            return state?.ToLower() switch
            {
                "happening" => "Current",
                "passed" => "Passed",
                "waiting" => "Upcoming",
                _ => ""
            };
        }

    #endregion

    #region Lifecycle Methods

        /// <summary>
        /// Called when the page appears. Starts the countdown timer and refreshes UI.
        /// </summary>
        public void OnAppearing()
        {
            // Ensure any stuck pull-to-refresh indicator is cleared when navigating back to this page
            if (IsRefreshing)
            {
                Application.Current?.Dispatcher.Dispatch(() => IsRefreshing = false);
            }

            // Check for date change immediately on appear (handles app resume after midnight)
            var today = DateTime.Today;
            if (today != _lastKnownDate)
            {
                Debug.WriteLine($"[MainViewModel.OnAppearing] Date changed from {_lastKnownDate:yyyy-MM-dd} to {today:yyyy-MM-dd} - invalidating stale data");
                _lastKnownDate = today;
                _lastMinute = DateTime.Now.Minute;
                _calendar = null;
                _cachedTimes = null;
            }

            // Schedule refresh on the next loop so the first frame can render immediately
            Application.Current?.Dispatcher.Dispatch(() =>
            {
                _ = RefreshUiAsync();
            });

            if (_ticker == null)
            {
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher == null) return;
                _ticker = dispatcher.CreateTimer();
                _ticker.Interval = TimeSpan.FromSeconds(1);
                // 🎨 PHASE 17: Update both RemainingTime and TimeProgress for animated gradient
                // 🔄 PHASE 18: Detect date changes + recalculate states when minute changes
                _tickHandler = (s, e) =>
                {
                    // Always update remaining time and progress (text updates every second)
                    RemainingTime = GetRemainingTime();

                    var now = DateTime.Now;
                    var today = now.Date;
                    var currentMinute = now.Minute;

                    // Check if date has changed (midnight crossed or system date changed)
                    if (today != _lastKnownDate)
                    {
                        Debug.WriteLine($"[MainViewModel] Date changed from {_lastKnownDate:yyyy-MM-dd} to {today:yyyy-MM-dd} - fetching new day's data");
                        _lastKnownDate = today;
                        _lastMinute = currentMinute;
                        
                        // Date changed - need to fetch new day's prayer times
                        // 1. Invalidate current calendar to force reload
                        _calendar = null;
                        _cachedTimes = null;
                        
                        // 2. Trigger data fetch and UI refresh (with error handling for fire-and-forget)
                        _ = Task.Run(async () => 
                        {
                            try
                            {
                                // Fetch new day's data from service
                                var newCalendar = await _data.GetTodayPrayerTimesAsync().ConfigureAwait(false);
                                if (newCalendar != null)
                                {
                                    _calendar = newCalendar;
                                    _data.calendar = newCalendar;
                                    await RefreshUiAsync(force: true).ConfigureAwait(false);
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[MainViewModel] Date change refresh failed: {ex.Message}");
                            }
                        });
                    }
                    // 🔄 PHASE 18: Recalculate prayer states when minute changes
                    // This ensures "current prayer" updates correctly (e.g., when crossing prayer boundaries)
                    // Only recalculate once per minute to optimize performance
                    else if (currentMinute != _lastMinute)
                    {
                        _lastMinute = currentMinute;
                        Application.Current?.Dispatcher.Dispatch(() => LoadPrayers());
                    }
                };
                _ticker.Tick += _tickHandler;
                _ticker.Start();
            }

            // Emit an aggregated performance snapshot shortly after appearing
            Application.Current?.Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(2), () =>
            {
                _perf.LogSummary("MainView");
            });

            // For returning users with auto-renew off, backfill today's cache entry if missing and update UI
            if (!Preferences.Get("AlwaysRenewLocationEnabled", false))
            {
                _ = Task.Run(async () =>
                {
                    var updated = await _data.EnsureTodayInCacheAsync().ConfigureAwait(false);
                    if (updated)
                    {
                        await RefreshUiAsync(force: true).ConfigureAwait(false);
                    }
                });
            }
        }

        /// <summary>
        /// Called when the page disappears. Stops the countdown timer to conserve resources.
        /// </summary>
        public void OnDisappearing()
        {
            if (_ticker != null)
            {
                if (_tickHandler != null) _ticker.Tick -= _tickHandler;
                _ticker.Stop();
                _ticker = null;
                _tickHandler = null;
            }
        }

        /// <summary>
        /// Coalesced and throttled UI refresh. Prevents duplicate updates during navigation churn.
        /// </summary>
        /// <param name="force">When true, bypasses throttle and forces immediate refresh.</param>
        private async Task RefreshUiAsync(bool force = false)
        {
            var now = DateTimeOffset.UtcNow;
            if (!force && now - _lastUiRefresh < UiRefreshThrottle)
                return;

            if (System.Threading.Interlocked.Exchange(ref _refreshing, 1) == 1)
                return;

            try
            {
                _lastUiRefresh = now;

                // Don’t block first frame – yield once if we’re called during navigation
                await Task.Yield();

                // Ensure any UI-bound collection updates happen on the main thread
                // Timer inside dispatch to measure actual work, not queue wait time
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    using (_perf.StartTimer("UI.LoadPrayers"))
                    {
                        LoadPrayers();
                    }
                });

                // Start city lookup in background without blocking UI
                _ = Task.Run(async () => await GetCityAsync());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RefreshUiAsync error: {ex}");
            }
            finally
            {
                System.Threading.Volatile.Write(ref _refreshing, 0);
            }
        }

    #endregion

    #region Time Calculation Methods

        /// <summary>
        /// Calculates the progress percentage (0.0 to 1.0) for animated gradient display (PHASE 17).
        /// </summary>
        /// <param name="startTime">Start of the current prayer window.</param>
        /// <param name="endTime">End of the current prayer window.</param>
        /// <param name="currentTime">Current time of day.</param>
        private void CalculateTimeProgress(TimeSpan startTime, TimeSpan endTime, TimeSpan currentTime)
        {
            var totalDuration = endTime - startTime;
            var elapsed = currentTime - startTime;

            if (totalDuration.TotalSeconds > 0)
            {
                TimeProgress = Math.Clamp(elapsed.TotalSeconds / totalDuration.TotalSeconds, 0.0, 1.0);
            }
            else
            {
                TimeProgress = 0.0;
            }
        }

        /// <summary>
        /// Calculates and formats the remaining time until the next prayer.
        /// Also updates <see cref="TimeProgress"/> for gradient animation.
        /// Uses cached parsed times for performance (avoids TimeSpan.Parse every second).
        /// </summary>
        /// <returns>Formatted remaining time string with localized prayer name.</returns>
        private string GetRemainingTime()
        {
            var currentTime = DateTime.Now.TimeOfDay;
            var cal = _calendar;
            if (cal is null)
            {
                TimeProgress = 0.0;
                return "";
            }

            // Lazy-initialize cached times when calendar is available
            _cachedTimes ??= CachedPrayerTimes.FromCalendar(cal);
            var t = _cachedTimes;

            try
            {
                if (currentTime < t.FalseFajr)
                {
                    CalculateTimeProgress(TimeSpan.Zero, t.FalseFajr, currentTime);
                    return AppResources.FecriKazibingirmesinekalanvakit +
                              (t.FalseFajr - currentTime).ToString(@"hh\:mm\:ss");
                }
                if (currentTime >= t.FalseFajr && currentTime <= t.Fajr)
                {
                    CalculateTimeProgress(t.FalseFajr, t.Fajr, currentTime);
                    return AppResources.FecriSadikakalanvakit +
                           (t.Fajr - currentTime).ToString(@"hh\:mm\:ss");
                }
                if (currentTime >= t.Fajr && currentTime <= t.Sunrise)
                {
                    CalculateTimeProgress(t.Fajr, t.Sunrise, currentTime);
                    return AppResources.SabahSonunakalanvakit +
                           (t.Sunrise - currentTime).ToString(@"hh\:mm\:ss");
                }
                if (currentTime >= t.Sunrise && currentTime <= t.Dhuhr)
                {
                    CalculateTimeProgress(t.Sunrise, t.Dhuhr, currentTime);
                    return AppResources.Ogleningirmesinekalanvakit +
                           (t.Dhuhr - currentTime).ToString(@"hh\:mm\:ss");
                }
                if (currentTime >= t.Dhuhr && currentTime <= t.Asr)
                {
                    CalculateTimeProgress(t.Dhuhr, t.Asr, currentTime);
                    return AppResources.Oglenincikmasinakalanvakit +
                           (t.Asr - currentTime).ToString(@"hh\:mm\:ss");
                }
                if (currentTime >= t.Asr && currentTime <= t.Maghrib)
                {
                    CalculateTimeProgress(t.Asr, t.Maghrib, currentTime);
                    return AppResources.Ikindinincikmasinakalanvakit +
                           (t.Maghrib - currentTime).ToString(@"hh\:mm\:ss");
                }
                if (currentTime >= t.Maghrib && currentTime <= t.Isha)
                {
                    CalculateTimeProgress(t.Maghrib, t.Isha, currentTime);
                    return AppResources.Aksamincikmasnakalanvakit +
                           (t.Isha - currentTime).ToString(@"hh\:mm\:ss");
                }
                if (currentTime >= t.Isha && currentTime <= t.EndOfIsha)
                {
                    CalculateTimeProgress(t.Isha, t.EndOfIsha, currentTime);
                    return AppResources.Yatsinincikmasinakalanvakit +
                           (t.EndOfIsha - currentTime).ToString(@"hh\:mm\:ss");
                }
                if (currentTime >= t.EndOfIsha)
                {
                    // After EndOfIsha, show full progress (100%)
                    TimeProgress = 1.0;
                    return AppResources.Yatsininciktigindangecenvakit +
                           (currentTime - t.EndOfIsha).ToString(@"hh\:mm\:ss");
                }
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"GetFormattedRemainingTime exception: {exception.Message}. Location: {cal?.Latitude}, {cal?.Longitude}");
            }

            return "";
        }

        /// <summary>
        /// Updates the prayer time display.
        /// Called when prayer states need to be refreshed.
        /// </summary>
        /// <remarks>
        /// Note: iOS Live Activities are NOT supported in .NET MAUI.
        /// Apple requires Live Activities to be implemented as a native Swift WidgetKit extension,
        /// which cannot be done purely in C#. This would require:
        /// - A separate Swift Widget Extension project in Xcode
        /// - SwiftUI views for Lock Screen/Dynamic Island
        /// - App Groups for data sharing
        /// See: https://developer.apple.com/documentation/activitykit
        /// </remarks>
        public Task UpdatePrayerTimeDisplayAsync()
        {
            // Update UI
            Application.Current?.Dispatcher.Dispatch(() =>
            {
                LoadPrayers(); // Recalculate states immediately
            });

            return Task.CompletedTask;
        }

        /// <summary>
        /// Fetches fresh prayer times with location refresh and saves to preferences.
        /// Shows toast on failure.
        /// </summary>
        private async Task GetPrayersAsync()
        {
            // GetPrayersAsync is called when user needs fresh prayer times, so refresh location
            _calendar = await _data.GetTodayPrayerTimesAsync(refreshLocation: true).ConfigureAwait(false);
            if (_calendar != null && _calendar.Latitude != 0)
            {
                // Invalidate cached times when calendar changes
                _cachedTimes = null;
                
                Preferences.Set("latitude", _calendar.Latitude);
                Preferences.Set("longitude", _calendar.Longitude);
                Preferences.Set("altitude", _calendar.Altitude);
                Preferences.Set("timezone", _calendar.TimeZone);
                Preferences.Set("daylightsaving", _calendar.DayLightSaving);
                Preferences.Set("falsefajr", _calendar.FalseFajr);
                Preferences.Set("fajr", _calendar.Fajr);
                Preferences.Set("sunrise", _calendar.Sunrise);
                Preferences.Set("dhuhr", _calendar.Dhuhr);
                Preferences.Set("asr", _calendar.Asr);
                Preferences.Set("maghrib", _calendar.Maghrib);
                Preferences.Set("isha", _calendar.Isha);
                Preferences.Set("endofisha", _calendar.EndOfIsha);
                Preferences.Set("date", _calendar.Date);

                await RefreshUiAsync(force: true).ConfigureAwait(false);
            }
            else
            {
                if ((DeviceInfo.Platform == DevicePlatform.Android && DeviceInfo.Version.Major >= 12))
                    ShowToast(AppResources.KonumIzniIcerik);
                else
                    ShowToast(AppResources.KonumKapali);
            }
        }

    #endregion

    #region Nested Types

    /// <summary>
    /// Cached parsed prayer times to avoid repeated TimeSpan.Parse calls.
    /// Parsing is done once when calendar changes, not every second.
    /// </summary>
    private sealed class CachedPrayerTimes
    {
        public TimeSpan FalseFajr { get; init; }
        public TimeSpan Fajr { get; init; }
        public TimeSpan Sunrise { get; init; }
        public TimeSpan Dhuhr { get; init; }
        public TimeSpan Asr { get; init; }
        public TimeSpan Maghrib { get; init; }
        public TimeSpan Isha { get; init; }
        public TimeSpan EndOfIsha { get; init; }

        public static CachedPrayerTimes FromCalendar(Calendar cal)
        {
            return new CachedPrayerTimes
            {
                FalseFajr = AppConstants.ParseTimeSpan(cal.FalseFajr),
                Fajr = AppConstants.ParseTimeSpan(cal.Fajr),
                Sunrise = AppConstants.ParseTimeSpan(cal.Sunrise),
                Dhuhr = AppConstants.ParseTimeSpan(cal.Dhuhr),
                Asr = AppConstants.ParseTimeSpan(cal.Asr),
                Maghrib = AppConstants.ParseTimeSpan(cal.Maghrib),
                Isha = AppConstants.ParseTimeSpan(cal.Isha),
                EndOfIsha = AppConstants.ParseTimeSpan(cal.EndOfIsha)
            };
        }
    }

    #endregion
    }
