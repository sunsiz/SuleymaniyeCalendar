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

        // Throttle refreshes to avoid duplicate work during navigation churn
        private DateTimeOffset _lastUiRefresh = DateTimeOffset.MinValue;
        private static readonly TimeSpan UiRefreshThrottle = TimeSpan.FromSeconds(2);

        // Single-flight guard for UI refresh
        private int _refreshing; // 0 = idle, 1 = running

    private ObservableCollection<Prayer> prayers;
    public ObservableCollection<Prayer> Prayers { get => prayers; set => SetProperty(ref prayers, value); }

    private string remainingTime;
    public string RemainingTime { get => remainingTime; set => SetProperty(ref remainingTime, value); }

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

        public MainViewModel(DataService dataService)
        {
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

            // Lightweight initial population; deeper refresh is coalesced in OnAppearing
            LoadPrayers();

            // Fire and forget non-UI work
            _ = Task.Run(() => GetCity());

            if (!Preferences.Get("LocationSaved", false))
                CheckLocationInfo(3000);

            if (Preferences.Get("AlwaysRenewLocationEnabled", false))
                _startupRefreshTask = RefreshLocationCommand.ExecuteAsync(null);

            var lastAlarmDateStr = Preferences.Get("LastAlarmDate", "Empty");
            if (lastAlarmDateStr != "Empty")
            {
                DateTime lastAlarm;
                if (DateTime.TryParse(lastAlarmDateStr, out lastAlarm) || DateTime.TryParseExact(lastAlarmDateStr, new[] {"dd/MM/yyyy","dd-MM-yyyy","yyyy-MM-dd"}, CultureInfo.InvariantCulture, DateTimeStyles.None, out lastAlarm))
                {
                    if ((lastAlarm - DateTime.Today).Days > 4)
                        _weeklyAlarmsTask = _data.SetMonthlyAlarmsAsync();
                }
            }
            Debug.WriteLine("TimeStamp-ItemsViewModel-Finish", DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss.fff tt"));

            // Icon/UI refresh is handled by OnAppearing -> RefreshUiAsync()
        }

        [RelayCommand]
        public async Task GoToMap()
        {
            try
            {
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
            if (IsRefreshing || IsBusy)
                return;

            // Set UI flags on main thread
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                IsRefreshing = true;
                IsBusy = true;
            });

            try
            {
                // Force location refresh when user manually taps refresh button
                var location = await _data.GetCurrentLocationAsync(refreshLocation: true).ConfigureAwait(false);
                if (location != null && location.Latitude != 0 && location.Longitude != 0)
                {
                    // Get fresh prayer times with the updated location
                    _calendar = await _data.GetPrayerTimesHybridAsync(refreshLocation: true).ConfigureAwait(false);
                    // Also refresh monthly data for alarms
                    var monthlyData = await _data.GetMonthlyPrayerTimesHybridAsync(location, forceRefresh: true).ConfigureAwait(false);
                    
                    await _data.SetMonthlyAlarmsAsync().ConfigureAwait(false);

                    // Coalesced UI update after background work
                    await RefreshUiAsync(force: true).ConfigureAwait(false);

                    // Fire and forget non-UI work
                    _ = Task.Run(() => GetCity());
                }
                else
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        ShowToast(AppResources.KonumIzniIcerik);
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RefreshLocation error: {ex}");
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    ShowToast(AppResources.TakvimIcinInternet);
                });
            }
            finally
            {
                // Always reset UI flags on main thread to unblock RefreshView spinner
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    IsBusy = false;
                    IsRefreshing = false;
                });
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
        private void CheckLocationInfo(int timeDelay)
        {
            if (_initialLocationChecked)
                return;
            var isLocationEnabled = _data.CheckAndRequestLocationPermission();
            if (isLocationEnabled.Result != PermissionStatus.Granted) return;

            _ = Task.Run(async () =>
            {
                if (!_data.HaveInternet()) return;
                Debug.WriteLine($"**** {this.GetType().Name}.{nameof(CheckLocationInfo)}: Starting at {DateTime.Now}");
                await Task.Delay(timeDelay).ConfigureAwait(false);
                var calendar = _data.calendar;
                calendar = await _data.PrepareMonthlyPrayerTimes().ConfigureAwait(false);
                if ((calendar.Altitude == 114.0 && calendar.Latitude == 41.0 && calendar.Longitude == 29.0) || (calendar.Altitude == 0 && calendar.Latitude == 0 && calendar.Longitude == 0))
                {
                    // Default coordinates detected, need fresh location
                    _calendar = await _data.GetPrayerTimesHybridAsync(refreshLocation: true).ConfigureAwait(false);
                    var location = await _data.GetCurrentLocationAsync(false).ConfigureAwait(false);
                    if (location != null && location.Latitude != 0 && location.Longitude != 0)
                        _data.GetMonthlyPrayerTimes(location, false);
                }

                await _data.SetMonthlyAlarmsAsync().ConfigureAwait(false);

                // Coalesced UI update
                await RefreshUiAsync(force: true).ConfigureAwait(false);

                _initialLocationChecked = true;
            });
        }

        private ObservableCollection<Prayer> LoadPrayers()
        {
            Debug.WriteLine("TimeStamp-MainViewModel-ExecuteLoadItemsCommand-Start", DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss.fff tt"));

            _calendar = _data.calendar ?? _calendar;
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
                }

                // Persist raw times consistently
                _data.SavePrayerTimesToPreferences(_calendar);

                // Apply to UI atomically on main thread
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (Prayers == null)
                    {
                        Prayers = new ObservableCollection<Prayer>(list);
                    }
                    else
                    {
                        Prayers.Clear();
                        foreach (var p in list)
                            Prayers.Add(p);
                    }
                    Debug.WriteLine($"[MainViewModel] Refreshed prayers. Enabled flags: FF:{list.First(x=>x.Id=="falsefajr").Enabled} F:{list.First(x=>x.Id=="fajr").Enabled} SR:{list.First(x=>x.Id=="sunrise").Enabled} D:{list.First(x=>x.Id=="dhuhr").Enabled} A:{list.First(x=>x.Id=="asr").Enabled} M:{list.First(x=>x.Id=="maghrib").Enabled} I:{list.First(x=>x.Id=="isha").Enabled} EI:{list.First(x=>x.Id=="endofisha").Enabled}");
                    Debug.WriteLine("TimeStamp-MainViewModel-ExecuteLoadItemsCommand-Finish", DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss.fff tt"));
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

            return Prayers;
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
            // Schedule refresh on the next loop so the first frame can render immediately
            Application.Current?.Dispatcher.Dispatch(() =>
            {
                _ = RefreshUiAsync();
            });

            if (_ticker == null)
            {
                _ticker = Application.Current.Dispatcher.CreateTimer();
                _ticker.Interval = TimeSpan.FromSeconds(1);
                _tickHandler = (s, e) => RemainingTime = GetRemainingTime();
                _ticker.Tick += _tickHandler;
                _ticker.Start();
            }

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
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    LoadPrayers();
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

        private string GetRemainingTime()
        {
            var currentTime = DateTime.Now.TimeOfDay;
            try
            {
                if (currentTime < TimeSpan.Parse(_calendar.FalseFajr))
                    return AppResources.FecriKazibingirmesinekalanvakit +
                              (TimeSpan.Parse(_calendar.FalseFajr) - currentTime).ToString(@"hh\:mm\:ss");
                if (currentTime >= TimeSpan.Parse(_calendar.FalseFajr) && currentTime <= TimeSpan.Parse(_calendar.Fajr))
                    return AppResources.FecriSadikakalanvakit +
                           (TimeSpan.Parse(_calendar.Fajr) - currentTime).ToString(@"hh\:mm\:ss");
                if (currentTime >= TimeSpan.Parse(_calendar.Fajr) && currentTime <= TimeSpan.Parse(_calendar.Sunrise))
                    return AppResources.SabahSonunakalanvakit +
                           (TimeSpan.Parse(_calendar.Sunrise) - currentTime).ToString(@"hh\:mm\:ss");
                if (currentTime >= TimeSpan.Parse(_calendar.Sunrise) && currentTime <= TimeSpan.Parse(_calendar.Dhuhr))
                    return AppResources.Ogleningirmesinekalanvakit +
                           (TimeSpan.Parse(_calendar.Dhuhr) - currentTime).ToString(@"hh\:mm\:ss");
                if (currentTime >= TimeSpan.Parse(_calendar.Dhuhr) && currentTime <= TimeSpan.Parse(_calendar.Asr))
                    return AppResources.Oglenincikmasinakalanvakit +
                           (TimeSpan.Parse(_calendar.Asr) - currentTime).ToString(@"hh\:mm\:ss");
                if (currentTime >= TimeSpan.Parse(_calendar.Asr) && currentTime <= TimeSpan.Parse(_calendar.Maghrib))
                    return AppResources.Ikindinincikmasinakalanvakit +
                           (TimeSpan.Parse(_calendar.Maghrib) - currentTime).ToString(@"hh\:mm\:ss");
                if (currentTime >= TimeSpan.Parse(_calendar.Maghrib) && currentTime <= TimeSpan.Parse(_calendar.Isha))
                    return AppResources.Aksamincikmasnakalanvakit +
                           (TimeSpan.Parse(_calendar.Isha) - currentTime).ToString(@"hh\:mm\:ss");
                if (currentTime >= TimeSpan.Parse(_calendar.Isha) && currentTime <= TimeSpan.Parse(_calendar.EndOfIsha))
                    return AppResources.Yatsinincikmasinakalanvakit +
                           (TimeSpan.Parse(_calendar.EndOfIsha) - currentTime).ToString(@"hh\:mm\:ss");
                if (currentTime >= TimeSpan.Parse(_calendar.EndOfIsha))
                    return AppResources.Yatsininciktigindangecenvakit +
                           (currentTime - TimeSpan.Parse(_calendar.EndOfIsha)).ToString(@"hh\:mm\:ss");
            }
            catch (Exception exception)
            {
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
