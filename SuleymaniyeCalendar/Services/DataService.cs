#nullable enable

using System.Collections.ObjectModel;
using SuleymaniyeCalendar.Models;
using SuleymaniyeCalendar.Resources.Strings;
using Calendar = SuleymaniyeCalendar.Models.Calendar;

namespace SuleymaniyeCalendar.Services;

/// <summary>
/// Facade service for managing prayer times, location data, and alarm scheduling.
/// Delegates to specialized services: LocationService, PrayerTimesRepository, NotificationSchedulerService.
/// </summary>
public class DataService
{
    private readonly LocationService _locationService;
    private readonly PrayerTimesRepository _repository;
    private readonly NotificationSchedulerService _scheduler;
    private readonly PerformanceService _perf;

    /// <summary>
    /// Current day's prayer calendar data. Shared with ViewModels for UI binding.
    /// </summary>
    public Calendar? calendar { get; set; }

    public DataService(
        LocationService locationService,
        PrayerTimesRepository repository,
        NotificationSchedulerService scheduler,
        PerformanceService perf)
    {
        _locationService = locationService;
        _repository = repository;
        _scheduler = scheduler;
        _perf = perf;
    }

    public Task<Location?> GetCurrentLocationAsync(bool refreshLocation) 
        => _locationService.GetCurrentLocationAsync(refreshLocation);

    public Task<PermissionStatus> CheckAndRequestLocationPermission()
        => _locationService.CheckAndRequestLocationPermission();

    public Task<ObservableCollection<Calendar>?> GetMonthlyPrayerTimesHybridAsync(Location location, bool forceRefresh = false)
        => _repository.GetMonthlyPrayerTimesHybridAsync(location, null, null, forceRefresh);

    public Task<Calendar?> GetDailyPrayerTimesHybridAsync(Location location, DateTime? date = null)
        => _repository.GetDailyPrayerTimesHybridAsync(location, date);

    public Task SetMonthlyAlarmsAsync(Location location, bool forceReschedule = false)
        => _scheduler.SetMonthlyAlarmsAsync(location, forceReschedule);

    public bool HaveInternet() => _repository.HaveInternet();

    public async Task<Calendar?> PrepareMonthlyPrayerTimes()
    {
        var location = await GetCurrentLocationAsync(false);
        if (location == null) return null;
        return await _repository.GetTodayPrayerTimesAsync(location);
    }

    public async Task<Calendar?> GetPrayerTimesHybridAsync(bool refreshLocation = false)
    {
        var location = await GetCurrentLocationAsync(refreshLocation);
        if (location == null) return null;
        return await GetDailyPrayerTimesHybridAsync(location, DateTime.Today);
    }

    public Task<ObservableCollection<Calendar>> GetMonthlyFromCacheOrEmptyAsync(Location location)
        => _repository.GetMonthlyFromCacheOrEmptyAsync(location);

    public Task<ObservableCollection<Calendar>?> GetMonthFromCacheAsync(Location location, int year, int month)
        => _repository.GetMonthFromCacheAsync(location, year, month);

    public Task<ObservableCollection<Calendar>?> FetchSpecificMonthAsync(Location location, int month, int year)
        => _repository.GetMonthlyPrayerTimesHybridAsync(location, month, year);

    public Task<bool> EnsureTodayInCacheAsync(Location location)
        => _repository.EnsureTodayInCacheAsync(location);

    public async Task<bool> EnsureTodayInCacheAsync()
    {
        var location = await GetCurrentLocationAsync(false);
        if (location == null) return false;
        return await _repository.EnsureTodayInCacheAsync(location);
    }

    public async Task SetMonthlyAlarmsAsync(bool forceReschedule = false)
    {
        var location = await GetCurrentLocationAsync(false);
        if (location != null)
        {
            await _scheduler.SetMonthlyAlarmsAsync(location, forceReschedule);
        }
    }

    public List<Prayer> BuildPrayersFromCalendar(Calendar calendar)
    {
        return new List<Prayer>
        {
            new Prayer { Id = "falsefajr", Name = AppResources.FecriKazip, Time = calendar.FalseFajr, IconPath = PrayerIconService.GetPrayerIconById("falsefajr"), Enabled = Preferences.Get("falsefajrEnabled", false) },
            new Prayer { Id = "fajr", Name = AppResources.FecriSadik, Time = calendar.Fajr, IconPath = PrayerIconService.GetPrayerIconById("fajr"), Enabled = Preferences.Get("fajrEnabled", false) },
            new Prayer { Id = "sunrise", Name = AppResources.SabahSonu, Time = calendar.Sunrise, IconPath = PrayerIconService.GetPrayerIconById("sunrise"), Enabled = Preferences.Get("sunriseEnabled", false) },
            new Prayer { Id = "dhuhr", Name = AppResources.Ogle, Time = calendar.Dhuhr, IconPath = PrayerIconService.GetPrayerIconById("dhuhr"), Enabled = Preferences.Get("dhuhrEnabled", false) },
            new Prayer { Id = "asr", Name = AppResources.Ikindi, Time = calendar.Asr, IconPath = PrayerIconService.GetPrayerIconById("asr"), Enabled = Preferences.Get("asrEnabled", false) },
            new Prayer { Id = "maghrib", Name = AppResources.Aksam, Time = calendar.Maghrib, IconPath = PrayerIconService.GetPrayerIconById("maghrib"), Enabled = Preferences.Get("maghribEnabled", false) },
            new Prayer { Id = "isha", Name = AppResources.Yatsi, Time = calendar.Isha, IconPath = PrayerIconService.GetPrayerIconById("isha"), Enabled = Preferences.Get("ishaEnabled", false) },
            new Prayer { Id = "endofisha", Name = AppResources.YatsiSonu, Time = calendar.EndOfIsha, IconPath = PrayerIconService.GetPrayerIconById("endofisha"), Enabled = Preferences.Get("endofishaEnabled", false) }
        };
    }

    public bool CheckRemindersEnabledAny() => _scheduler.CheckRemindersEnabledAny();

    public void SavePrayerTimesToPreferences(Calendar calendar)
    {
        if (calendar == null) return;
        
        // Legacy keys (kept for compatibility)
        Preferences.Set("FajrTime", calendar.Fajr);
        Preferences.Set("SunTime", calendar.Sunrise);
        Preferences.Set("ZuhrTime", calendar.Dhuhr);
        Preferences.Set("AsrTime", calendar.Asr);
        Preferences.Set("MaghribTime", calendar.Maghrib);
        Preferences.Set("IshaTime", calendar.Isha);
        
        // Keys used by PrayerDetailViewModel (matching PrayerId)
        Preferences.Set("falsefajr", calendar.FalseFajr);
        Preferences.Set("fajr", calendar.Fajr);
        Preferences.Set("sunrise", calendar.Sunrise);
        Preferences.Set("dhuhr", calendar.Dhuhr);
        Preferences.Set("asr", calendar.Asr);
        Preferences.Set("maghrib", calendar.Maghrib);
        Preferences.Set("isha", calendar.Isha);
        Preferences.Set("endofisha", calendar.EndOfIsha);

        Preferences.Set("LastUpdateDate", calendar.Date);
    }
}
