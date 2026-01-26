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

    public async Task SetMonthlyAlarmsAsync(Location location, bool forceReschedule = false)
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

        Preferences.Set(LastAutoReschedulePreferenceKey, DateTime.UtcNow.ToString("O"));

        // Check if any reminders are enabled
        bool remindersEnabled = CheckRemindersEnabledAny();

        if (!remindersEnabled)
        {
            _alarmService.CancelAllAlarms();
            ClearAlarmCoverage();
            return;
        }

        // Cancel existing alarms to ensure a clean slate (handles disabled prayers or changed times)
        // On Android this might be expensive, but ensures consistency.
        // On iOS it's fast.
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
            
            var days = await _repository.EnsureDaysRangeAsync(location, startDate, daysToSchedule);
            
            if (days.Count == 0)
            {
                Debug.WriteLine("❌ No days available for scheduling alarms");
                return;
            }

            int dayCounter = 0;
            int notificationCount = 0;
            DateTime? coverageThrough = null;

            foreach (var day in days)
            {
                try
                {
                    if (!TryParseCalendarDate(day.Date, out var baseDate)) continue;
                    
                    // Skip past days
                    if (baseDate < DateTime.Today) continue;

                    var now = DateTime.Now;
                    var falseFajrTime = ParseTime(day.FalseFajr);
                    var fajrTime = ParseTime(day.Fajr);
                    var sunriseTime = ParseTime(day.Sunrise);
                    var dhuhrTime = ParseTime(day.Dhuhr);
                    var asrTime = ParseTime(day.Asr);
                    var maghribTime = ParseTime(day.Maghrib);
                    var ishaTime = ParseTime(day.Isha);
                    var endOfIshaTime = ParseTime(day.EndOfIsha);

                    var isToday = baseDate.Date == DateTime.Today;

                    notificationCount += SchedulePrayerAlarmIfEnabled(baseDate, falseFajrTime, now, isToday, "falsefajr", AppResources.FecriKazip);
                    notificationCount += SchedulePrayerAlarmIfEnabled(baseDate, fajrTime, now, isToday, "fajr", AppResources.FecriSadik);
                    notificationCount += SchedulePrayerAlarmIfEnabled(baseDate, sunriseTime, now, isToday, "sunrise", AppResources.SabahSonu);
                    notificationCount += SchedulePrayerAlarmIfEnabled(baseDate, dhuhrTime, now, isToday, "dhuhr", AppResources.Ogle);
                    notificationCount += SchedulePrayerAlarmIfEnabled(baseDate, asrTime, now, isToday, "asr", AppResources.Ikindi);
                    notificationCount += SchedulePrayerAlarmIfEnabled(baseDate, maghribTime, now, isToday, "maghrib", AppResources.Aksam);
                    notificationCount += SchedulePrayerAlarmIfEnabled(baseDate, ishaTime, now, isToday, "isha", AppResources.Yatsi);
                    notificationCount += SchedulePrayerAlarmIfEnabled(baseDate, endOfIshaTime, now, isToday, "endofisha", AppResources.YatsiSonu);

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
                }
            }

            if (dayCounter > 0 && coverageThrough.HasValue)
            {
                PersistAlarmCoverage(coverageThrough.Value);
                Debug.WriteLine($"✅ Alarm scheduling complete: {notificationCount} notifications scheduled through {coverageThrough.Value:dd/MM/yyyy}");
            }
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"❌ SetMonthlyAlarmsAsync failed: {exception.Message}");
        }

        Debug.WriteLine("TimeStamp-SetMonthlyAlarms-Finish", DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss.fff tt"));
    }

    private int SchedulePrayerAlarmIfEnabled(DateTime baseDate, TimeSpan prayerTime, DateTime now, bool isToday, string prayerId, string prayerName)
    {
        if (Preferences.Get(prayerId + "Enabled", false))
        {
            var notifyTime = Preferences.Get(prayerId + "NotificationTime", 0);
            var alarmTime = baseDate.Add(prayerTime).AddMinutes(-notifyTime);

            // If it's today and the time has passed, don't schedule
            if (isToday && alarmTime <= now) return 0;

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

            _alarmService.SetAlarm(alarmTime, requestCode, settings);
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

    private static TimeSpan ParseTime(string? timeStr) =>
        AppConstants.ParseTimeSpan(timeStr);
}
