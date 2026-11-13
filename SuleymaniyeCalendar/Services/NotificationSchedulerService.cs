using System.Diagnostics;
using System.Globalization;
using SuleymaniyeCalendar.Helpers;
using SuleymaniyeCalendar.Models;
using SuleymaniyeCalendar.Resources.Strings;

namespace SuleymaniyeCalendar.Services;

public class NotificationSchedulerService
{
    private readonly IAlarmService _alarmService;
    private readonly PrayerTimesRepository _repository;
    private readonly PerformanceService _perf;

    private const string LastAlarmDatePreferenceKey = "LastAlarmDate";
    private const string LastAutoReschedulePreferenceKey = "LastAutoRescheduleUtc";
    private static readonly TimeSpan AutoRescheduleWindow = TimeSpan.FromDays(3);
    private static readonly TimeSpan AutoRescheduleCooldown = TimeSpan.FromHours(6);

    public NotificationSchedulerService(
        IAlarmService alarmService,
        PrayerTimesRepository repository,
        PerformanceService perf)
    {
        _alarmService = alarmService;
        _repository = repository;
        _perf = perf;
    }

    // Serializes scheduling runs. The service start, app start, preloader and location refresh can all trigger
    // scheduling at once; interleaved cancel-all/schedule passes used to wipe alarms scheduled by another run.
    private static readonly SemaphoreSlim _scheduleGate = new(1, 1);
    public async Task CancelAllAlarmsAsync()
    {
        await _scheduleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (CheckRemindersEnabledAny())
                return;
            CancelAllAlarmsCore();
        }
        finally
        {
            _scheduleGate.Release();
        }
    }

    /// <summary>Saves one prayer's settings and replaces its alarms as a serialized operation.</summary>
    public async Task SavePrayerSettingsAsync(
        string prayerId, bool enabled, int notificationTime, string? sound, Func<Task<Location?>> getLocationAsync)
    {
        await _scheduleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var enabledKey = prayerId + "Enabled";
            var timeKey = prayerId + "NotificationTime";
            var soundKey = prayerId + "AlarmSound";
            var oldEnabled = Preferences.Get(enabledKey, false);
            var oldTime = Preferences.Get(timeKey, 0);
            var oldSound = Preferences.Get(soundKey, "kus");
            var replacementStarted = false;
            Location? location = null;
            try
            {
                Preferences.Set(enabledKey, enabled);
                Preferences.Set(timeKey, notificationTime);
                if (sound is not null)
                    Preferences.Set(soundKey, sound);

                if (CheckRemindersEnabledAny())
                {
                    location = ValidateLocation(await getLocationAsync().ConfigureAwait(false));
                    await SetMonthlyAlarmsCoreAsync(location, true, () => replacementStarted = true).ConfigureAwait(false);
                }
                else
                {
                    replacementStarted = true;
                    CancelAllAlarmsCore();
                }
            }
            catch (Exception saveException)
            {
                Preferences.Set(enabledKey, oldEnabled);
                Preferences.Set(timeKey, oldTime);
                Preferences.Set(soundKey, oldSound);
                if (replacementStarted)
                {
                    try
                    {
                        if (CheckRemindersEnabledAny())
                        {
                            location = ValidateLocation(location ?? await getLocationAsync().ConfigureAwait(false));
                            await SetMonthlyAlarmsCoreAsync(location, true).ConfigureAwait(false);
                        }
                        else
                        {
                            CancelAllAlarmsCore();
                        }
                    }
                    catch (Exception restoreException)
                    {
                        Debug.WriteLine($"Failed to restore previous alarms: {restoreException}");
                        throw new AlarmSettingsSaveException(true, new AggregateException(saveException, restoreException));
                    }
                }
                throw new AlarmSettingsSaveException(false, saveException);
            }
        }
        finally
        {
            _scheduleGate.Release();
        }
    }

    public async Task SetMonthlyAlarmsAsync(Location location, bool forceReschedule = false)
    {
        ValidateLocation(location);

        await _scheduleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await SetMonthlyAlarmsCoreAsync(location, forceReschedule).ConfigureAwait(false);
        }
        finally
        {
            _scheduleGate.Release();
        }
    }

    private static Location ValidateLocation(Location? location)
    {
        if (location is null || (location.Latitude == 0 && location.Longitude == 0))
            throw new InvalidOperationException(AppResources.KonumHatasi);
        return location;
    }

    private void CancelAllAlarmsCore()
    {
        ClearAlarmCoverage();
        Preferences.Remove(LastAutoReschedulePreferenceKey);
        _alarmService.CancelAllAlarms();
#if ANDROID
        AlarmRescheduleJob.CancelPeriodic(Android.App.Application.Context);
#endif
    }

    private async Task SetMonthlyAlarmsCoreAsync(Location location, bool forceReschedule, Action? onReplacementStarted = null)
    {
        Debug.WriteLine("TimeStamp-SetMonthlyAlarms-Start", DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss.fff tt"));
        
        // Set culture to user's selected language for localized notification text
        try
        {
            var savedLanguage = Preferences.Get("SelectedLanguage", "tr");
            var culture = new CultureInfo(savedLanguage);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            AppResources.Culture = culture;
            Debug.WriteLine($"🔔 NotificationSchedulerService: Culture set to {culture.Name}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Failed to set culture in NotificationSchedulerService: {ex.Message}");
        }
        
        // Cancel stale alarms even when old coverage would otherwise skip rescheduling.
        if (!CheckRemindersEnabledAny())
        {
            onReplacementStarted?.Invoke();
            CancelAllAlarmsCore();
            return;
        }

        // Check if we need to reschedule
        if (!forceReschedule)
        {
            var lastCoverageStr = Preferences.Get(LastAlarmDatePreferenceKey, string.Empty);
            if (DateTime.TryParse(lastCoverageStr, out var lastCoverage))
            {
                var daysRemaining = (lastCoverage - DateTime.Today).TotalDays;
                if (daysRemaining > 3)
                {
                    Debug.WriteLine($"Skipping alarm reschedule. Covered until {lastCoverage:dd/MM/yyyy} ({daysRemaining:F1} days left)");
                    return;
                }
            }
        }

        // Check cooldown
        var lastRunStr = Preferences.Get(LastAutoReschedulePreferenceKey, string.Empty);
        if (!forceReschedule && DateTime.TryParse(lastRunStr, out var lastRun))
        {
            if (DateTime.UtcNow - lastRun < AutoRescheduleCooldown)
            {
                Debug.WriteLine("Skipping alarm reschedule due to cooldown.");
                return;
            }
        }

        // Preferences.Set(LastAutoReschedulePreferenceKey, DateTime.UtcNow.ToString("O"));

        // // Cancel existing alarms to ensure a clean slate (handles disabled prayers or changed times)
        // // On Android this might be expensive, but ensures consistency.
        // // On iOS it's fast.
        // _alarmService.CancelAllAlarms(); // Optional: Enable if overwriting isn't sufficient

        try
        {
            // Calculate how many days to schedule based on platform and prayer count
            // iOS has a hard limit of 64 pending local notifications
            var startDate = DateTime.Today;
            int daysToSchedule = 30; // Default for Android
            
#if __IOS__
            // iOS: Calculate optimal days to stay under 64 notification limit
            int enabledPrayerCount = GetEnabledPrayerCount();
            daysToSchedule = CalculateOptimalDaysForIOS(enabledPrayerCount);
            Debug.WriteLine($"📱 iOS: Scheduling {daysToSchedule} days for {enabledPrayerCount} enabled prayer(s) (max {daysToSchedule * enabledPrayerCount}/64 notifications)");
#endif
            
            var days = await _repository.EnsureDaysRangeAsync(location, startDate, daysToSchedule).ConfigureAwait(false);
            
            if (days.Count != daysToSchedule)
                throw new InvalidOperationException(AppResources.OtuzGunHatirlaticiHatasi);

            for (var i = 0; i < days.Count; i++)
            {
                var day = days[i];
                if (!TryParseCalendarDate(day.Date, out var date) || date.Date != startDate.AddDays(i))
                    throw new InvalidOperationException(AppResources.OtuzGunHatirlaticiHatasi);

                ValidateEnabledTime(day.FalseFajr, "falsefajr");
                ValidateEnabledTime(day.Fajr, "fajr");
                ValidateEnabledTime(day.Sunrise, "sunrise");
                ValidateEnabledTime(day.Dhuhr, "dhuhr");
                ValidateEnabledTime(day.Asr, "asr");
                ValidateEnabledTime(day.Maghrib, "maghrib");
                ValidateEnabledTime(day.Isha, "isha");
                ValidateEnabledTime(day.EndOfIsha, "endofisha");
            }

            // Only cancel once new data is in hand: cancelling first left the user with no alarms at all
            // whenever the network/API failed mid-run (e.g. while travelling).
            _alarmService.ValidateSchedulingAvailability();
            onReplacementStarted?.Invoke();
            ClearAlarmCoverage();
            Preferences.Remove(LastAutoReschedulePreferenceKey);
            _alarmService.CancelAllAlarms();

            int dayCounter = 0;
            int notificationCount = 0;
            bool schedulingFailed = false;
            DateTime? coverageThrough = null;

            async Task<int> TryScheduleAsync(DateTime date, string? time, DateTime now, string id, string name)
            {
                try
                {
                    return await SchedulePrayerAlarmIfEnabledAsync(date, time, now, id, name).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    schedulingFailed = true;
                    Debug.WriteLine($"Alarm scheduling failed for {id} on {date:yyyy-MM-dd}: {ex.Message}");
                    return 0;
                }
            }

            foreach (var day in days)
            {
                try
                {
                    if (!TryParseCalendarDate(day.Date, out var baseDate)) continue;
                    
                    // Skip past days
                    if (baseDate < DateTime.Today) continue;

                    var now = DateTime.Now;
                    notificationCount += await TryScheduleAsync(baseDate, day.FalseFajr, now, "falsefajr", AppResources.FecriKazip);
                    notificationCount += await TryScheduleAsync(baseDate, day.Fajr, now, "fajr", AppResources.FecriSadik);
                    notificationCount += await TryScheduleAsync(baseDate, day.Sunrise, now, "sunrise", AppResources.SabahSonu);
                    notificationCount += await TryScheduleAsync(baseDate, day.Dhuhr, now, "dhuhr", AppResources.Ogle);
                    notificationCount += await TryScheduleAsync(baseDate, day.Asr, now, "asr", AppResources.Ikindi);
                    notificationCount += await TryScheduleAsync(baseDate, day.Maghrib, now, "maghrib", AppResources.Aksam);
                    notificationCount += await TryScheduleAsync(baseDate, day.Isha, now, "isha", AppResources.Yatsi);
                    notificationCount += await TryScheduleAsync(baseDate, day.EndOfIsha, now, "endofisha", AppResources.YatsiSonu);

                    dayCounter++;
                    coverageThrough = baseDate;
                    
                    
#if IOS
                    // Add small delay every 10 days to avoid iOS rate limiting during bulk scheduling
                    // This helps prevent iOS from flagging the app as "misbehaving"
                    if (dayCounter % 10 == 0)
                    {
                        await Task.Delay(50).ConfigureAwait(false);
                    }
#endif
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"❌ Error processing day {day.Date}: {ex.Message}");
                    throw;
                }
            }

            if (schedulingFailed || dayCounter == 0 || !coverageThrough.HasValue)
                throw new InvalidOperationException(AppResources.Alarmkurarkenhataolustu);

            if (dayCounter > 0 && coverageThrough.HasValue)
            {
                PersistAlarmCoverage(coverageThrough.Value);
                Preferences.Set(LastAutoReschedulePreferenceKey, DateTime.UtcNow.ToString("O"));
#if ANDROID
                AlarmRescheduleJob.SchedulePeriodic(Android.App.Application.Context);
#endif
                Debug.WriteLine($"✅ Alarm scheduling complete: {notificationCount} notifications scheduled through {coverageThrough.Value:dd/MM/yyyy}");
            }
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"❌ SetMonthlyAlarmsAsync failed: {exception.Message}");
            throw;
        }

        Debug.WriteLine("TimeStamp-SetMonthlyAlarms-Finish", DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss.fff tt"));
    }

    private async Task<int> SchedulePrayerAlarmIfEnabledAsync(DateTime baseDate, string? prayerTimeString, DateTime now, string prayerId, string prayerName)
    {
        if (Preferences.Get(prayerId + "Enabled", false))
        {
            if (!AppConstants.TryParsePrayerTime(prayerTimeString, out var prayerTime))
                throw new InvalidOperationException(AppResources.OtuzGunHatirlaticiHatasi);
            var notifyTime = Preferences.Get(prayerId + "NotificationTime", 0);
            var alarmTime = baseDate.Add(prayerTime).AddMinutes(-notifyTime);

            // If it's today and the time has passed, don't schedule
            if (alarmTime <= now) return 0;

            // Unique ID generation:
            // Include year to prevent collision across year boundaries (Dec 25 - Jan 24 spanning New Year)
            // Format: (Year % 10) * 100000 + DayOfYear * 100 + PrayerIndex
            // Max value: 9 * 100000 + 366 * 100 + 7 = 936607 (fits in int32)
            int prayerIndex = prayerId switch
            {
                "falsefajr" => 0, "fajr" => 1, "sunrise" => 2, "dhuhr" => 3,
                "asr" => 4, "maghrib" => 5, "isha" => 6, "endofisha" => 7, _ => 8
            };
            int requestCode = ((baseDate.Year % 10) * 100000) + (baseDate.DayOfYear * 100) + prayerIndex;

            // Calculate actual prayer time (alarm time + offset minutes)
            var actualPrayerTime = alarmTime.AddMinutes(notifyTime);
            
            // Use localized strings for notification content
            var notifyMinutesText = notifyTime > 0 
                ? $"{notifyTime}{AppResources.DakikaOnceden}" 
                : string.Empty;
            
            var sound = Preferences.Get(prayerId + "AlarmSound", "kus");

#if IOS
            // iOS requires Athan sound under 30 seconds; swap to iOS-specific file while keeping preference unchanged
            if (string.Equals(sound, "ezan", StringComparison.OrdinalIgnoreCase))
            {
                sound = "ezanios";
            }
#endif

            var settings = new NotificationSettings
            {
                Title = AppResources.SuleymaniyeVakfiTakvimi,
                Body = $"{prayerName} {AppResources.Vakti}{actualPrayerTime:HH:mm} {notifyMinutesText}".Trim(),
                Sound = sound,
                PrayerId = prayerId,
                PrayerName = prayerName,
                PrayerTime = actualPrayerTime.ToString("HH:mm")
            };

            await _alarmService.SetAlarmAsync(alarmTime, requestCode, settings).ConfigureAwait(false);
            return 1;
        }
        return 0;
    }

    public bool CheckRemindersEnabledAny()
    {
        return Preferences.Get("falsefajrEnabled", false) || Preferences.Get("fajrEnabled", false) ||
               Preferences.Get("sunriseEnabled", false) || Preferences.Get("dhuhrEnabled", false) ||
               Preferences.Get("asrEnabled", false) || Preferences.Get("maghribEnabled", false) ||
               Preferences.Get("ishaEnabled", false) || Preferences.Get("endofishaEnabled", false);
    }

    /// <summary>
    /// Counts how many prayers are currently enabled for notifications.
    /// Used by iOS to calculate optimal day range under the 64-notification limit.
    /// </summary>
    private int GetEnabledPrayerCount()
    {
        int count = 0;
        if (Preferences.Get("falsefajrEnabled", false)) count++;
        if (Preferences.Get("fajrEnabled", false)) count++;
        if (Preferences.Get("sunriseEnabled", false)) count++;
        if (Preferences.Get("dhuhrEnabled", false)) count++;
        if (Preferences.Get("asrEnabled", false)) count++;
        if (Preferences.Get("maghribEnabled", false)) count++;
        if (Preferences.Get("ishaEnabled", false)) count++;
        if (Preferences.Get("endofishaEnabled", false)) count++;
        return count;
    }

#if __IOS__
    /// <summary>
    /// Calculates optimal number of days to schedule on iOS to stay under 64 notification limit.
    /// iOS silently fails to schedule ANY notifications when limit is exceeded.
    /// </summary>
    /// <param name="enabledPrayerCount">Number of prayers with notifications enabled.</param>
    /// <returns>Number of days to schedule (1-30).</returns>
    private static int CalculateOptimalDaysForIOS(int enabledPrayerCount)
    {
        const int iOSNotificationLimit = 64;
        
        if (enabledPrayerCount <= 0) return 30;
        
        // Calculate max days while staying under limit
        int maxDays = iOSNotificationLimit / enabledPrayerCount;
        
        // Clamp to reasonable range (at least 7 days, max 30 days)
        return Math.Clamp(maxDays, 7, 30);
    }
#endif

    private void PersistAlarmCoverage(DateTime date)
    {
        Preferences.Set(LastAlarmDatePreferenceKey, date.ToString("O"));
    }

    private void ClearAlarmCoverage()
    {
        Preferences.Remove(LastAlarmDatePreferenceKey);
    }

    private static bool TryParseCalendarDate(string? dateStr, out DateTime date)
    {
        date = AppConstants.ParseCalendarDate(dateStr);
        return date != DateTime.MinValue;
    }

    private static void ValidateEnabledTime(string? time, string prayerId)
    {
        if (Preferences.Get(prayerId + "Enabled", false) &&
            !AppConstants.TryParsePrayerTime(time, out _))
            throw new InvalidOperationException(AppResources.OtuzGunHatirlaticiHatasi);
    }
}
