using UserNotifications;
using Foundation;
using System.Diagnostics;
using SuleymaniyeCalendar.Models;

namespace SuleymaniyeCalendar.Platforms.iOS;

/// <summary>
/// iOS implementation of prayer time notifications.
/// Uses UNUserNotificationCenter for scheduled local notifications.
/// </summary>
public class NotificationService
{
    private const string PrayerNotificationCategoryId = "PRAYER_TIME_CATEGORY";

    /// <summary>
    /// Initializes notification categories and requests user permission.
    /// Must be called once at app startup.
    /// </summary>
    public static async Task InitializeNotificationsAsync()
    {
        try
        {
            var center = UNUserNotificationCenter.Current;
            
            // Request permission
            var (granted, error) = await center.RequestAuthorizationAsync(
                UNAuthorizationOptions.Alert | 
                UNAuthorizationOptions.Sound | 
                UNAuthorizationOptions.Badge
            );

            if (granted)
            {
                Debug.WriteLine("✅ Notification permission granted");
                
                // Set up notification categories for actions
                SetupNotificationCategories();
            }
            else if (error != null)
            {
                Debug.WriteLine($"⚠️ Notification permission denied: {error.LocalizedDescription}");
            }
            else
            {
                Debug.WriteLine("⚠️ Notification permission denied by user");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Notification initialization failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Schedules a prayer time notification.
    /// </summary>
    /// <param name="prayerName">Prayer name (e.g., "Fajr", "Dhuhr")</param>
    /// <param name="prayerTime">Prayer time (e.g., "05:30")</param>
    /// <param name="notificationMinutesBefore">Minutes before prayer to notify (0-60)</param>
    /// <param name="date">Date to schedule notification</param>
    public static async Task SchedulePrayerNotificationAsync(
        string prayerName,
        string prayerTime,
        int notificationMinutesBefore = 0,
        DateTime? date = null)
    {
        try
        {
            var targetDate = date ?? DateTime.Today;

            // Parse prayer time
            if (!TimeSpan.TryParse(prayerTime, out var time))
            {
                Debug.WriteLine($"❌ Invalid prayer time format: {prayerTime}");
                return;
            }

            // Calculate notification time (subtract minutes before)
            var notificationTime = time.Subtract(TimeSpan.FromMinutes(notificationMinutesBefore));
            if (notificationTime.TotalSeconds <= 0)
            {
                Debug.WriteLine($"⚠️ Notification time in past: {prayerName} at {prayerTime}");
                return;
            }

            // Create notification content
            var content = new UNMutableNotificationContent
            {
                Title = "Prayer Time",
                Body = $"{prayerName} - {prayerTime}",
                Sound = UNNotificationSound.Default,
                Badge = NSNumber.FromInt32(1),
                CategoryIdentifier = PrayerNotificationCategoryId,
                ThreadIdentifier = "PrayerTimes"
            };

            // Add custom data
            var userInfo = new NSMutableDictionary
            {
                { new NSString("prayerName"), new NSString(prayerName) },
                { new NSString("prayerTime"), new NSString(prayerTime) },
                { new NSString("date"), NSDate.FromTimeIntervalSinceNow(0) }
            };
            content.UserInfo = userInfo;

            // Create trigger for specific time
            var calendar = NSCalendar.CurrentCalendar;
            var components = calendar.Components(
                NSCalendarUnit.Year | NSCalendarUnit.Month | NSCalendarUnit.Day, 
                NSDate.FromTimeIntervalSinceNow(0)
            );
            
            components.Hour = (int)notificationTime.Hours;
            components.Minute = (int)notificationTime.Minutes;
            components.Second = 0;

            var triggerDate = calendar.DateFromComponents(components);
            var trigger = UNCalendarNotificationTrigger.CreateTrigger(components, false);

            // Create notification request
            var requestId = $"prayer_{prayerName}_{targetDate:yyyyMMdd}";
            var request = UNNotificationRequest.FromIdentifier(requestId, content, trigger);

            // Schedule notification
            await UNUserNotificationCenter.Current.AddNotificationRequestAsync(request);
            
            Debug.WriteLine($"✅ Scheduled: {prayerName} at {notificationTime:HH:mm} ({targetDate:yyyy-MM-dd})");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Notification scheduling failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Schedules 30 days of prayer notifications.
    /// Similar to Android's SetMonthlyAlarmsAsync.
    /// </summary>
    public static async Task ScheduleMonthlyNotificationsAsync(
        List<Calendar> daysData,
        Dictionary<string, int> notificationMinutes)
    {
        try
        {
            var count = 0;
            
            foreach (var day in daysData.OrderBy(d => d.Date))
            {
                if (!DateTime.TryParse(day.Date, out var date) || date < DateTime.Today)
                    continue;

                // Schedule each prayer if enabled
                await SchedulePrayerIfEnabledAsync(day, "falsefajr", day.FalseFajr, notificationMinutes, date);
                await SchedulePrayerIfEnabledAsync(day, "fajr", day.Fajr, notificationMinutes, date);
                await SchedulePrayerIfEnabledAsync(day, "sunrise", day.Sunrise, notificationMinutes, date);
                await SchedulePrayerIfEnabledAsync(day, "dhuhr", day.Dhuhr, notificationMinutes, date);
                await SchedulePrayerIfEnabledAsync(day, "asr", day.Asr, notificationMinutes, date);
                await SchedulePrayerIfEnabledAsync(day, "maghrib", day.Maghrib, notificationMinutes, date);
                await SchedulePrayerIfEnabledAsync(day, "isha", day.Isha, notificationMinutes, date);
                await SchedulePrayerIfEnabledAsync(day, "endofisha", day.EndOfIsha, notificationMinutes, date);

                count++;
                if (count >= 30) break;
            }

            Debug.WriteLine($"✅ Scheduled notifications for {count} days");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Monthly notification scheduling failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Helper to schedule individual prayer if enabled in preferences.
    /// </summary>
    private static async Task SchedulePrayerIfEnabledAsync(
        Calendar day,
        string prayerId,
        string prayerTime,
        Dictionary<string, int> notificationMinutes,
        DateTime date)
    {
        var isEnabled = Preferences.Get($"{prayerId}Enabled", false);
        if (!isEnabled) return;

        var minutesBefore = notificationMinutes.ContainsKey(prayerId) 
            ? notificationMinutes[prayerId] 
            : 0;

        var prayerNameKey = prayerId switch
        {
            "falsefajr" => "FecriKazip",
            "fajr" => "FecriSadik",
            "sunrise" => "SabahSonu",
            "dhuhr" => "Ogle",
            "asr" => "Ikindi",
            "maghrib" => "Aksam",
            "isha" => "Yatsi",
            "endofisha" => "YatsiSonu",
            _ => prayerId
        };

        await SchedulePrayerNotificationAsync(prayerNameKey, prayerTime, minutesBefore, date);
    }

    /// <summary>
    /// Cancels all scheduled prayer notifications.
    /// Useful when user disables reminders.
    /// </summary>
    public static void CancelAllNotifications()
    {
        UNUserNotificationCenter.Current.RemoveAllPendingNotificationRequests();
        Debug.WriteLine("✅ All notifications cancelled");
    }

    /// <summary>
    /// Gets count of pending notifications.
    /// </summary>
    public static async Task<int> GetPendingNotificationCountAsync()
    {
        var requests = await UNUserNotificationCenter.Current.GetPendingNotificationRequestsAsync();
        Debug.WriteLine($"📊 Pending notifications: {requests.Length}");
        return requests.Length;
    }

    /// <summary>
    /// Sets up notification categories for interactive actions.
    /// </summary>
    private static void SetupNotificationCategories()
    {
        // Notification categories setup - simplified to avoid type conversion issues
        // The basic notification functionality works without custom categories
        try
        {
            // Note: Full category setup can be implemented when needed
            // For now, we rely on default notification presentation
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠️ Notification category setup failed: {ex.Message}");
        }
    }
}