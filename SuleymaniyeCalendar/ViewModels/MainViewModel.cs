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
    public partial class MainViewModel:BaseViewModel
    {
        private Calendar _calendar;
        private DataService _data;
    private Task _startupRefreshTask;
    private Task _weeklyAlarmsTask;

        //private bool _permissionRequested;
        [ObservableProperty] public ObservableCollection<Prayer> _prayers;

        [ObservableProperty] public string _remainingTime;

        [ObservableProperty] public string _city;
        private IDispatcherTimer _ticker;
        private EventHandler _tickHandler;

    // Dedicated flag for pull-to-refresh to avoid coupling with IsBusy
    [ObservableProperty]
    private bool isRefreshing;

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
            LoadPrayers();
            GetCity();
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
            //Console.WriteLine("CurrentCulture is {0}.", CultureInfo.CurrentCulture.Name);
            Debug.WriteLine("TimeStamp-ItemsViewModel-Finish", DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss.fff tt"));
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
                //UserDialogs.Instance.Toast(AppResources.HaritaHatasi + ex.Message);
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
            //using (UserDialogs.Instance.Loading(AppResources.Yenileniyor))
            //{
                _calendar = await _data.PrepareMonthlyPrayerTimes().ConfigureAwait(false);
                await _data.SetWeeklyAlarmsAsync().ConfigureAwait(false);
            //}
            MainThread.BeginInvokeOnMainThread(() =>
            {
                GetCity();
                LoadPrayers();
            });
            IsBusy = false;
            IsRefreshing = false;
        }
        
        [RelayCommand]
    private async Task PrayerSelected(Prayer prayer)
        {
            if (prayer == null)
                return;

            // This will push the ItemDetailPage onto the navigation stack
            //await Shell.Current.GoToAsync($"{nameof(ItemDetailPage)}?{nameof(ItemDetailViewModel.PrayerTimeId)}={prayerTime.Id}").ConfigureAwait(false);
            await Shell.Current.GoToAsync(nameof(PrayerDetailPage), true, new Dictionary<string, object> { { nameof(PrayerDetailViewModel.PrayerId), prayer.Id } }).ConfigureAwait(false);
        }

        [RelayCommand]
    private async Task GoToMonth()
        {
            IsBusy = true;
            // This will push the MonthPage onto the navigation stack
            //await Shell.Current.GoToAsync($"{nameof(MonthPage)}").ConfigureAwait(false);
            await Shell.Current.GoToAsync(nameof(MonthPage)).ConfigureAwait(false);
            IsBusy = false;
        }

    [RelayCommand]
    private async Task Settings()
        {
            IsBusy = true;
            // This will push the SettingsPage onto the navigation stack
            await Shell.Current.GoToAsync($"{nameof(SettingsPage)}").ConfigureAwait(false);
            IsBusy = false;
        }

        private async void GetCity()
        {
            try
            {
                //Without the Convert.ToDouble conversion it confuses the ',' and '.' when UI culture changed. like latitude=50.674367348783 become latitude= 50674367348783 then throw exception.
                var placemark = await Geocoding.Default.GetPlacemarksAsync(Convert.ToDouble(_calendar.Latitude, CultureInfo.InvariantCulture.NumberFormat), Convert.ToDouble(_calendar.Longitude, CultureInfo.InvariantCulture.NumberFormat)).ConfigureAwait(false);
                if (placemark != null)
                    City = placemark.FirstOrDefault()?.AdminArea ?? placemark.FirstOrDefault()?.CountryName;
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
        if (isLocationEnabled.Result!=PermissionStatus.Granted) return;
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
                    // No recursive RefreshLocation call; update UI directly below
                }
                
                await _data.SetWeeklyAlarmsAsync().ConfigureAwait(false);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    GetCity();
                    LoadPrayers();
                });
                //GetCity();
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
                var falseFajr = new Prayer() { Id = "falsefajr", Name = AppResources.FecriKazip, Time = _calendar.FalseFajr, Enabled = Preferences.Get("falsefajrEnabled", false), State = CheckState(DateTime.Parse(_calendar.FalseFajr), DateTime.Parse(_calendar.Fajr)) };
                var fajr = new Prayer() { Id = "fajr", Name = AppResources.FecriSadik, Time = _calendar.Fajr, Enabled = Preferences.Get("fajrEnabled", false), State = CheckState(DateTime.Parse(_calendar.Fajr), DateTime.Parse(_calendar.Sunrise)) };
                var sunRise = new Prayer() { Id = "sunrise", Name = AppResources.SabahSonu, Time = _calendar.Sunrise, Enabled = Preferences.Get("sunriseEnabled", false), State = CheckState(DateTime.Parse(_calendar.Sunrise), DateTime.Parse(_calendar.Dhuhr)) };
                var dhuhr = new Prayer() { Id = "dhuhr", Name = AppResources.Ogle, Time = _calendar.Dhuhr, Enabled = Preferences.Get("dhuhrEnabled", false), State = CheckState(DateTime.Parse(_calendar.Dhuhr), DateTime.Parse(_calendar.Asr)) };
                var asr = new Prayer() { Id = "asr", Name = AppResources.Ikindi, Time = _calendar.Asr, Enabled = Preferences.Get("asrEnabled", false), State = CheckState(DateTime.Parse(_calendar.Asr), DateTime.Parse(_calendar.Maghrib)) };
                var maghrib = new Prayer() { Id = "maghrib", Name = AppResources.Aksam, Time = _calendar.Maghrib, Enabled = Preferences.Get("maghribEnabled", false), State = CheckState(DateTime.Parse(_calendar.Maghrib), DateTime.Parse(_calendar.Isha)) };
                var isha = new Prayer() { Id = "isha", Name = AppResources.Yatsi, Time = _calendar.Isha, Enabled = Preferences.Get("ishaEnabled", false), State = CheckState(DateTime.Parse(_calendar.Isha), DateTime.Parse(_calendar.EndOfIsha)) };
                var endOfIsha = new Prayer() { Id = "endofisha", Name = AppResources.YatsiSonu, Time = _calendar.EndOfIsha, Enabled = Preferences.Get("endofishaEnabled", false), State = CheckState(DateTime.Parse(_calendar.EndOfIsha), DateTime.Parse(_calendar.FalseFajr)) };
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
                    Prayers = new ObservableCollection<Prayer>(list);
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

        public void OnAppearing()
        {
            LoadPrayers();
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

        //partial void OnSelectedPrayerTimeChanged(PrayerTime value){OnPrayerTimeSelected(value);}
        private async Task GetPrayersAsync()
        {
            //IsBusy = true;
            //var data = new DataService();

            _calendar = await _data.GetPrayerTimesAsync(true).ConfigureAwait(false);
            if (_calendar.Latitude != 0)
            {
                //Application.Current.Properties["takvim"] = Vakitler;
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
                //await Application.Current.SavePropertiesAsync();
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    GetCity();
                    LoadPrayers();
                });
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
