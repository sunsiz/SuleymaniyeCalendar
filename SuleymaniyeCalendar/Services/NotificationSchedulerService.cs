using System.Diagnostics;
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
            // Ensure we have 30 days of data
            var startDate = DateTime.Today;
            var daysToSchedule = await _repository.EnsureDaysRangeAsync(location, startDate, 30);
            
            if (daysToSchedule.Count == 0)
            {
                Debug.WriteLine("❌ No days available for scheduling alarms");
                return;
            }

            int dayCounter = 0;
            DateTime? coverageThrough = null;

            foreach (var day in daysToSchedule)
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

                    SchedulePrayerAlarmIfEnabled(baseDate, falseFajrTime, now, isToday, "falsefajr", "Fecri Kazip");
                    SchedulePrayerAlarmIfEnabled(baseDate, fajrTime, now, isToday, "fajr", "Fecri Sadık");
                    SchedulePrayerAlarmIfEnabled(baseDate, sunriseTime, now, isToday, "sunrise", "Sabah Sonu");
                    SchedulePrayerAlarmIfEnabled(baseDate, dhuhrTime, now, isToday, "dhuhr", "Öğle");
                    SchedulePrayerAlarmIfEnabled(baseDate, asrTime, now, isToday, "asr", "İkindi");
                    SchedulePrayerAlarmIfEnabled(baseDate, maghribTime, now, isToday, "maghrib", "Akşam");
                    SchedulePrayerAlarmIfEnabled(baseDate, ishaTime, now, isToday, "isha", "Yatsı");
                    SchedulePrayerAlarmIfEnabled(baseDate, endOfIshaTime, now, isToday, "endofisha", "Yatsı Sonu");

                    dayCounter++;
                    coverageThrough = baseDate;
                    
                    if (dayCounter >= 30) break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"❌ Error processing day {day.Date}: {ex.Message}");
                }
            }

            if (dayCounter > 0 && coverageThrough.HasValue)
            {
                PersistAlarmCoverage(coverageThrough.Value);
                Debug.WriteLine($"✅ Alarm scheduling complete through {coverageThrough.Value:dd/MM/yyyy}");
            }
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"❌ SetMonthlyAlarmsAsync failed: {exception.Message}");
        }

        Debug.WriteLine("TimeStamp-SetMonthlyAlarms-Finish", DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss.fff tt"));
    }

    private void SchedulePrayerAlarmIfEnabled(DateTime baseDate, TimeSpan prayerTime, DateTime now, bool isToday, string prayerId, string prayerName)
    {
        if (Preferences.Get(prayerId + "Enabled", false))
        {
            var notifyTime = Preferences.Get(prayerId + "NotificationTime", 0);
            var alarmTime = baseDate.Add(prayerTime).AddMinutes(-notifyTime);

            // If it's today and the time has passed, don't schedule
            if (isToday && alarmTime <= now) return;

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
            
            var settings = new NotificationSettings
            {
                Title = "Süleymaniye Takvimi",
                Body = $"{prayerName} vaktine {notifyTime} dakika kaldı.",
                Sound = Preferences.Get(prayerId + "AlarmSound", "kus"),
                PrayerId = prayerId,
                PrayerName = prayerName,
                PrayerTime = actualPrayerTime.ToString("HH:mm")
            };

            _alarmService.SetAlarm(alarmTime, requestCode, settings);
        }
    }

    public bool CheckRemindersEnabledAny()
    {
        return Preferences.Get("falsefajrEnabled", false) || Preferences.Get("fajrEnabled", false) ||
               Preferences.Get("sunriseEnabled", false) || Preferences.Get("dhuhrEnabled", false) ||
               Preferences.Get("asrEnabled", false) || Preferences.Get("maghribEnabled", false) ||
               Preferences.Get("ishaEnabled", false) || Preferences.Get("endofishaEnabled", false);
    }

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
