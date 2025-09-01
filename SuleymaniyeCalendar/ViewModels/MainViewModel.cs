using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
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

        [ObservableProperty] public ObservableCollection<Prayer> _prayers;

        [ObservableProperty] public string _remainingTime;

        [ObservableProperty] public string _city;
        private IDispatcherTimer _ticker;
        private EventHandler _tickHandler;

        // Dedicated flag for pull-to-refresh to avoid coupling with IsBusy
        [ObservableProperty]
        private bool isRefreshing;

        // Bind CollectionView.SelectedItem to this
        [ObservableProperty]
        private Prayer _selectedPrayer;

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
            CultureInfo cultureInfo = new CultureInfo(Preferences.Get("SelectedLanguage", "en"));
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
                if ((DateTime.Parse(lastAlarmDateStr) - DateTime.Today).Days > 4)
                    _weeklyAlarmsTask = _data.SetWeeklyAlarmsAsync();
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

            IsRefreshing = true;
            IsBusy = true;

            _calendar = await _data.PrepareMonthlyPrayerTimes().ConfigureAwait(false);
            await _data.SetWeeklyAlarmsAsync().ConfigureAwait(false);

            // Coalesced UI update after background work
            await RefreshUiAsync(force: true).ConfigureAwait(false);

            IsBusy = false;
            IsRefreshing = false;
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
                        await _data.SetWeeklyAlarmsAsync().ConfigureAwait(false);
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

        private async void GetCity()
        {
            try
            {
                var placemark = await Geocoding.Default
                    .GetPlacemarksAsync(
                        Convert.ToDouble(_calendar.Latitude, CultureInfo.InvariantCulture.NumberFormat),
                        Convert.ToDouble(_calendar.Longitude, CultureInfo.InvariantCulture.NumberFormat))
                    .ConfigureAwait(false);

                var city = placemark?.FirstOrDefault()?.AdminArea ?? placemark?.FirstOrDefault()?.CountryName;

                if (!string.IsNullOrWhiteSpace(city))
                {
                    await MainThread.InvokeOnMainThreadAsync(() => City = city);
                }
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception);
            }

            if (!string.IsNullOrEmpty(City)) Preferences.Set("sehir", City);
            City ??= Preferences.Get("sehir", AppResources.Sehir);
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
                    _calendar = await _data.GetPrayerTimesFastAsync().ConfigureAwait(false);
                    var location = await _data.GetCurrentLocationAsync(false).ConfigureAwait(false);
                    if (location != null && location.Latitude != 0 && location.Longitude != 0)
                        _data.GetMonthlyPrayerTimes(location, false);
                }

                await _data.SetWeeklyAlarmsAsync().ConfigureAwait(false);

                // Coalesced UI update
                await RefreshUiAsync(force: true).ConfigureAwait(false);

                _initialLocationChecked = true;
            });
        }

        private ObservableCollection<Prayer> LoadPrayers()
        {
            Debug.WriteLine("TimeStamp-MainViewModel-ExecuteLoadItemsCommand-Start", DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss.fff tt"));

            _calendar = _data.calendar;
            try
            {
                var list = new List<Prayer>();

                var falseFajr = new Prayer();
                falseFajr.Id = "falsefajr";
                falseFajr.Name = AppResources.FecriKazip;
                falseFajr.Time = _calendar.FalseFajr;
                falseFajr.Enabled = Preferences.Get("falsefajrEnabled", false);
                falseFajr.State = CheckState(DateTime.Parse(_calendar.FalseFajr), DateTime.Parse(_calendar.Fajr));
                falseFajr.StateDescription = GetStateDescription(falseFajr.State);
                falseFajr.UpdateVisualState();

                var fajr = new Prayer();
                fajr.Id = "fajr";
                fajr.Name = AppResources.FecriSadik;
                fajr.Time = _calendar.Fajr;
                fajr.Enabled = Preferences.Get("fajrEnabled", false);
                fajr.State = CheckState(DateTime.Parse(_calendar.Fajr), DateTime.Parse(_calendar.Sunrise));
                fajr.StateDescription = GetStateDescription(fajr.State);
                fajr.UpdateVisualState();

                var sunRise = new Prayer();
                sunRise.Id = "sunrise";
                sunRise.Name = AppResources.SabahSonu;
                sunRise.Time = _calendar.Sunrise;
                sunRise.Enabled = Preferences.Get("sunriseEnabled", false);
                sunRise.State = CheckState(DateTime.Parse(_calendar.Sunrise), DateTime.Parse(_calendar.Dhuhr));
                sunRise.StateDescription = GetStateDescription(sunRise.State);
                sunRise.UpdateVisualState();

                var dhuhr = new Prayer();
                dhuhr.Id = "dhuhr";
                dhuhr.Name = AppResources.Ogle;
                dhuhr.Time = _calendar.Dhuhr;
                dhuhr.Enabled = Preferences.Get("dhuhrEnabled", false);
                dhuhr.State = CheckState(DateTime.Parse(_calendar.Dhuhr), DateTime.Parse(_calendar.Asr));
                dhuhr.StateDescription = GetStateDescription(dhuhr.State);
                dhuhr.UpdateVisualState();

                var asr = new Prayer();
                asr.Id = "asr";
                asr.Name = AppResources.Ikindi;
                asr.Time = _calendar.Asr;
                asr.Enabled = Preferences.Get("asrEnabled", false);
                asr.State = CheckState(DateTime.Parse(_calendar.Asr), DateTime.Parse(_calendar.Maghrib));
                asr.StateDescription = GetStateDescription(asr.State);
                asr.UpdateVisualState();

                var maghrib = new Prayer();
                maghrib.Id = "maghrib";
                maghrib.Name = AppResources.Aksam;
                maghrib.Time = _calendar.Maghrib;
                maghrib.Enabled = Preferences.Get("maghribEnabled", false);
                maghrib.State = CheckState(DateTime.Parse(_calendar.Maghrib), DateTime.Parse(_calendar.Isha));
                maghrib.StateDescription = GetStateDescription(maghrib.State);
                maghrib.UpdateVisualState();

                var isha = new Prayer();
                isha.Id = "isha";
                isha.Name = AppResources.Yatsi;
                isha.Time = _calendar.Isha;
                isha.Enabled = Preferences.Get("ishaEnabled", false);
                isha.State = CheckState(DateTime.Parse(_calendar.Isha), DateTime.Parse(_calendar.EndOfIsha));
                isha.StateDescription = GetStateDescription(isha.State);
                isha.UpdateVisualState();

                var endOfIsha = new Prayer();
                endOfIsha.Id = "endofisha";
                endOfIsha.Name = AppResources.YatsiSonu;
                endOfIsha.Time = _calendar.EndOfIsha;
                endOfIsha.Enabled = Preferences.Get("endofishaEnabled", false);
                endOfIsha.State = CheckState(DateTime.Parse(_calendar.EndOfIsha), DateTime.Parse(_calendar.FalseFajr));
                endOfIsha.StateDescription = GetStateDescription(endOfIsha.State);
                endOfIsha.UpdateVisualState();

                list.Add(falseFajr);
                list.Add(fajr);
                list.Add(sunRise);
                list.Add(dhuhr);
                list.Add(asr);
                list.Add(maghrib);
                list.Add(isha);
                list.Add(endOfIsha);

                // Persist prayer times
                Preferences.Set(falseFajr.Id, _calendar.FalseFajr);
                Preferences.Set(fajr.Id, _calendar.Fajr);
                Preferences.Set(sunRise.Id, _calendar.Sunrise);
                Preferences.Set(dhuhr.Id, _calendar.Dhuhr);
                Preferences.Set(asr.Id, _calendar.Asr);
                Preferences.Set(maghrib.Id, _calendar.Maghrib);
                Preferences.Set(isha.Id, _calendar.Isha);
                Preferences.Set(endOfIsha.Id, _calendar.EndOfIsha);

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
                    Debug.WriteLine($"[MainViewModel] Refreshed prayers. Enabled flags: FF:{falseFajr.Enabled} F:{fajr.Enabled} SR:{sunRise.Enabled} D:{dhuhr.Enabled} A:{asr.Enabled} M:{maghrib.Enabled} I:{isha.Enabled} EI:{endOfIsha.Enabled}");
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

                LoadPrayers();
                await MainThread.InvokeOnMainThreadAsync(() => GetCity());
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
            _calendar = await _data.GetPrayerTimesAsync(true).ConfigureAwait(false);
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
