using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SuleymaniyeCalendar.Models;
using SuleymaniyeCalendar.Resources.Strings;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using SuleymaniyeCalendar.Services;
using SuleymaniyeCalendar.Views;
using Calendar = SuleymaniyeCalendar.Models.Calendar;
using Microsoft.Maui.ApplicationModel;

namespace SuleymaniyeCalendar.ViewModels
{
    public partial class MainViewModel : BaseViewModel
    {
        private Calendar _calendar;
        private DataService _data;
        private Task _startupRefreshTask;
        private Task _weeklyAlarmsTask;
        // (Removed legacy _skipWindowsGeocoding flag; Windows geocoding guarded by token check in GetCityAsync.)

        // Throttle refreshes to avoid duplicate work during navigation churn
        private DateTimeOffset _lastUiRefresh = DateTimeOffset.MinValue;
        private static readonly TimeSpan UiRefreshThrottle = TimeSpan.FromSeconds(2);

        // 🔄 PHASE 18: Track date for midnight rollover detection
        private DateTime _lastKnownDate = DateTime.Today;
        // Track last minute to detect prayer window changes (optimize performance)
        private int _lastMinute = DateTime.Now.Minute;

        // Single-flight guard for UI refresh
        private int _refreshing; // 0 = idle, 1 = running
                                 // Single-flight guard for manual location refresh pipeline (pull-to-refresh and refresh button)
        private int _locationRefreshing; // 0 = idle, 1 = running

        private ObservableCollection<Prayer> prayers;
        public ObservableCollection<Prayer> Prayers { get => prayers; set => SetProperty(ref prayers, value); }

        private string remainingTime;
        public string RemainingTime { get => remainingTime; set => SetProperty(ref remainingTime, value); }

        // 🎨 PHASE 17: Progress percentage for animated gradient (0.0 to 1.0)
        private double timeProgress;
        public double TimeProgress { get => timeProgress; set => SetProperty(ref timeProgress, value); }

        private string city;
        public string City { get => city; set => SetProperty(ref city, value); }
        private IDispatcherTimer _ticker;
        private EventHandler _tickHandler;

        // Dedicated flag for pull-to-refresh to avoid coupling with IsBusy
        private bool isRefreshing;
        public bool IsRefreshing { get => isRefreshing; set => SetProperty(ref isRefreshing, value); }

        // Bind CollectionView.SelectedItem to this
        private Prayer selectedPrayer;
        public Prayer SelectedPrayer { get => selectedPrayer; set => SetProperty(ref selectedPrayer, value); }

        // Prevent double navigation/reentrancy on fast taps/selection churn
        private bool _isNavigating;

        private readonly PerformanceService _perf;

        public MainViewModel(DataService dataService, PerformanceService perf = null)
        {
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
                    _ = CheckLocationInfoAsync(3000);
            }

            if (DeviceInfo.Platform != DevicePlatform.WinUI && Preferences.Get("AlwaysRenewLocationEnabled", false))
                _startupRefreshTask = RefreshLocationCommand.ExecuteAsync(null);

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
            Debug.WriteLine("TimeStamp-ItemsViewModel-Finish", DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss.fff tt"));

            // Icon/UI refresh is handled by OnAppearing -> RefreshUiAsync()
        }

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
                var location = new Location(Convert.ToDouble(_calendar.Latitude, CultureInfo.InvariantCulture.NumberFormat), Convert.ToDouble(_calendar.Longitude, CultureInfo.InvariantCulture.NumberFormat));
                var placeMark = await Geocoding.Default.GetPlacemarksAsync(Convert.ToDouble(_calendar.Latitude, CultureInfo.InvariantCulture.NumberFormat), Convert.ToDouble(_calendar.Longitude, CultureInfo.InvariantCulture.NumberFormat)).ConfigureAwait(true);
                var options = new MapLaunchOptions { Name = placeMark.FirstOrDefault()?.Thoroughfare ?? placeMark.FirstOrDefault()?.CountryName };
                await Map.OpenAsync(location, options).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ShowToast(AppResources.HaritaHatasi + ex.Message);
            }
        }

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
                    _calendar = await _data.GetPrayerTimesHybridAsync(refreshLocation: true).ConfigureAwait(false);
                    if (_calendar != null)
                    {
                        _data.calendar = _calendar; // sync service state
                    }

                    // Refresh monthly data (alarms depend on this); network may take time
                    var monthlyData = await _data.GetMonthlyPrayerTimesHybridAsync(location, forceRefresh: true).ConfigureAwait(false);

                    // Schedule notifications (can be slow on Android); keep off UI thread
                    await _data.SetMonthlyAlarmsAsync().ConfigureAwait(false);

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

        // Changed: parameterless; uses SelectedPrayer (bound from XAML)
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

        [RelayCommand]
        private async Task Settings()
        {
            IsBusy = true;
            await Shell.Current.GoToAsync($"{nameof(SettingsPage)}").ConfigureAwait(false);
            IsBusy = false;
        }

        [RelayCommand]
        private void TogglePrayerEnabled(Prayer prayer)
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
                        await _data.SetMonthlyAlarmsAsync().ConfigureAwait(false);
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

        // Legacy method - keep for backward compatibility but make it non-blocking
        private async void GetCity()
        {
            await GetCityAsync();
        }

        private bool _initialLocationChecked;
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
            calendar = await _data.PrepareMonthlyPrayerTimes().ConfigureAwait(false);
            if ((calendar.Altitude == 114.0 && calendar.Latitude == 41.0 && calendar.Longitude == 29.0) || (calendar.Altitude == 0 && calendar.Latitude == 0 && calendar.Longitude == 0))
            {
                // Default coordinates detected, need fresh location
                _calendar = await _data.GetPrayerTimesHybridAsync(refreshLocation: true).ConfigureAwait(false);
                if (_calendar != null)
                {
                    _data.calendar = _calendar; // keep service field synchronized
                }
                var location = await _data.GetCurrentLocationAsync(false).ConfigureAwait(false);
                if (location != null && location.Latitude != 0 && location.Longitude != 0)
                {
                    var monthly = _data.GetMonthlyPrayerTimes(location, false);
                    // If monthly data contains today, update calendars before UI refresh
                    var today = monthly?.FirstOrDefault(d => d.Date == DateTime.Today.ToString("dd/MM/yyyy"));
                    if (today != null)
                    {
                        _calendar = today;
                        _data.calendar = today;
                    }
                }
            }

            await _data.SetMonthlyAlarmsAsync().ConfigureAwait(false);

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

        private ObservableCollection<Prayer> LoadPrayers()
        {
            Debug.WriteLine("TimeStamp-MainViewModel-ExecuteLoadItemsCommand-Start", DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss.fff tt"));

            // Only adopt DataService.calendar if we don't already have a fresher _calendar.
            if (_calendar == null && _data.calendar != null)
            {
                _calendar = _data.calendar;
            }
            if (_calendar == null)
            {
                Debug.WriteLine("[MainViewModel] No calendar available yet; skipping LoadPrayers.");
                return Prayers ?? new ObservableCollection<Prayer>();
            }

            try
            {
                // Build unified prayers from the canonical Calendar model
                var list = _data.BuildPrayersFromCalendar(_calendar);

                // Compute temporal states using the same rules as before
                string[] seq = ["falsefajr", "fajr", "sunrise", "dhuhr", "asr", "maghrib", "isha", "endofisha"];
                var times = new Dictionary<string, string>
                {
                    ["falsefajr"] = _calendar.FalseFajr,
                    ["fajr"] = _calendar.Fajr,
                    ["sunrise"] = _calendar.Sunrise,
                    ["dhuhr"] = _calendar.Dhuhr,
                    ["asr"] = _calendar.Asr,
                    ["maghrib"] = _calendar.Maghrib,
                    ["isha"] = _calendar.Isha,
                    ["endofisha"] = _calendar.EndOfIsha
                };

                DateTime ParseTimeOrMin(string s)
                {
                    try { return DateTime.Parse(s); } catch { return DateTime.MinValue; }
                }

                foreach (var id in seq)
                {
                    var p = list.FirstOrDefault(x => x.Id == id);
                    if (p is null) continue;
                    string current = times[id];
                    // For the last item, reuse FalseFajr as the next bound like the legacy logic
                    string next = id == "endofisha" ? _calendar.FalseFajr : times[seq[Array.IndexOf(seq, id) + 1]];

                    p.State = CheckState(ParseTimeOrMin(current), ParseTimeOrMin(next));
                    p.StateDescription = GetStateDescription(p.State);
                    p.UpdateVisualState();

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

        private string CheckState(DateTime current, DateTime next)
        {
            var state = "";
            if (DateTime.Now > next) state = "Passed";
            if (DateTime.Now > current && DateTime.Now < next) state = "Happening";
            if (DateTime.Now < current) state = "Waiting";
            return state;
        }

        private string GetStateDescription(string state)
        {
            return state?.ToLower() switch
            {
                "happening" => "Current",
                "passed" => "Passed",
                "waiting" => "Upcoming",
                _ => ""
            };
        }

        public void OnAppearing()
        {
            // Ensure any stuck pull-to-refresh indicator is cleared when navigating back to this page
            if (IsRefreshing)
            {
                Application.Current?.Dispatcher.Dispatch(() => IsRefreshing = false);
            }

            // 🔄 PHASE 18: Always recalculate prayer states from cached data
            // This ensures correct "current prayer" when system time changes
            Application.Current?.Dispatcher.Dispatch(() =>
            {
                LoadPrayers(); // Recalculate states immediately
            });

            // Schedule refresh on the next loop so the first frame can render immediately
            Application.Current?.Dispatcher.Dispatch(() =>
            {
                _ = RefreshUiAsync();
            });

            if (_ticker == null)
            {
                _ticker = Application.Current.Dispatcher.CreateTimer();
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
                        // Date changed - need to fetch new day's prayer times from server
                        _ = LoadPrayers();
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

        // Coalesced + throttled UI refresh
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
                using (_perf.StartTimer("UI.LoadPrayers"))
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    LoadPrayers();
                });
                }

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

        // 🎨 PHASE 17: Helper method to calculate progress percentage (0.0 to 1.0)
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

        private string GetRemainingTime()
        {
            var currentTime = DateTime.Now.TimeOfDay;
            try
            {
                if (currentTime < TimeSpan.Parse(_calendar.FalseFajr))
                {
                    CalculateTimeProgress(TimeSpan.Zero, TimeSpan.Parse(_calendar.FalseFajr), currentTime);
                    return AppResources.FecriKazibingirmesinekalanvakit +
                              (TimeSpan.Parse(_calendar.FalseFajr) - currentTime).ToString(@"hh\:mm\:ss");
                }
                if (currentTime >= TimeSpan.Parse(_calendar.FalseFajr) && currentTime <= TimeSpan.Parse(_calendar.Fajr))
                {
                    CalculateTimeProgress(TimeSpan.Parse(_calendar.FalseFajr), TimeSpan.Parse(_calendar.Fajr), currentTime);
                    return AppResources.FecriSadikakalanvakit +
                           (TimeSpan.Parse(_calendar.Fajr) - currentTime).ToString(@"hh\:mm\:ss");
                }
                if (currentTime >= TimeSpan.Parse(_calendar.Fajr) && currentTime <= TimeSpan.Parse(_calendar.Sunrise))
                {
                    CalculateTimeProgress(TimeSpan.Parse(_calendar.Fajr), TimeSpan.Parse(_calendar.Sunrise), currentTime);
                    return AppResources.SabahSonunakalanvakit +
                           (TimeSpan.Parse(_calendar.Sunrise) - currentTime).ToString(@"hh\:mm\:ss");
                }
                if (currentTime >= TimeSpan.Parse(_calendar.Sunrise) && currentTime <= TimeSpan.Parse(_calendar.Dhuhr))
                {
                    CalculateTimeProgress(TimeSpan.Parse(_calendar.Sunrise), TimeSpan.Parse(_calendar.Dhuhr), currentTime);
                    return AppResources.Ogleningirmesinekalanvakit +
                           (TimeSpan.Parse(_calendar.Dhuhr) - currentTime).ToString(@"hh\:mm\:ss");
                }
                if (currentTime >= TimeSpan.Parse(_calendar.Dhuhr) && currentTime <= TimeSpan.Parse(_calendar.Asr))
                {
                    CalculateTimeProgress(TimeSpan.Parse(_calendar.Dhuhr), TimeSpan.Parse(_calendar.Asr), currentTime);
                    return AppResources.Oglenincikmasinakalanvakit +
                           (TimeSpan.Parse(_calendar.Asr) - currentTime).ToString(@"hh\:mm\:ss");
                }
                if (currentTime >= TimeSpan.Parse(_calendar.Asr) && currentTime <= TimeSpan.Parse(_calendar.Maghrib))
                {
                    CalculateTimeProgress(TimeSpan.Parse(_calendar.Asr), TimeSpan.Parse(_calendar.Maghrib), currentTime);
                    return AppResources.Ikindinincikmasinakalanvakit +
                           (TimeSpan.Parse(_calendar.Maghrib) - currentTime).ToString(@"hh\:mm\:ss");
                }
                if (currentTime >= TimeSpan.Parse(_calendar.Maghrib) && currentTime <= TimeSpan.Parse(_calendar.Isha))
                {
                    CalculateTimeProgress(TimeSpan.Parse(_calendar.Maghrib), TimeSpan.Parse(_calendar.Isha), currentTime);
                    return AppResources.Aksamincikmasnakalanvakit +
                           (TimeSpan.Parse(_calendar.Isha) - currentTime).ToString(@"hh\:mm\:ss");
                }
                if (currentTime >= TimeSpan.Parse(_calendar.Isha) && currentTime <= TimeSpan.Parse(_calendar.EndOfIsha))
                {
                    CalculateTimeProgress(TimeSpan.Parse(_calendar.Isha), TimeSpan.Parse(_calendar.EndOfIsha), currentTime);
                    return AppResources.Yatsinincikmasinakalanvakit +
                           (TimeSpan.Parse(_calendar.EndOfIsha) - currentTime).ToString(@"hh\:mm\:ss");
                }
                if (currentTime >= TimeSpan.Parse(_calendar.EndOfIsha))
                {
                    // After EndOfIsha, show full progress (100%)
                    TimeProgress = 1.0;
                    return AppResources.Yatsininciktigindangecenvakit +
                           (currentTime - TimeSpan.Parse(_calendar.EndOfIsha)).ToString(@"hh\:mm\:ss");
                }
            }
            catch (Exception exception)
            {
                TimeProgress = 0.0;
                System.Diagnostics.Debug.WriteLine(
                    $"GetFormattedRemainingTime exception: {exception.Message}. Location: {_calendar.Latitude}, {_calendar.Longitude}");
            }

            return "";
        }

        private async Task GetPrayersAsync()
        {
            // GetPrayersAsync is called when user needs fresh prayer times, so refresh location
            _calendar = await _data.GetPrayerTimesHybridAsync(refreshLocation: true).ConfigureAwait(false);
            if (_calendar.Latitude != 0)
            {
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
    }
}
