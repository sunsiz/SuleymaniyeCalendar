using UserNotifications;
using Foundation;
using System.Diagnostics;

namespace SuleymaniyeCalendar.Platforms.iOS;

/// <summary>
/// Shows persistent notification on iOS lock screen for current prayer.
/// Updates every minute to reflect remaining time.
/// </summary>
public class PersistentNotificationService
{
    /// <summary>
    /// Shows a persistent notification with current prayer and remaining time.
    /// </summary>
    public static async Task ShowPersistentNotificationAsync(
        string prayerName,
        string remainingTime,
        string nextPrayer)
    {
        try
        {
            var content = new UNMutableNotificationContent
            {
                Title = "Prayer Time",
                Body = $"{prayerName}: {remainingTime} remaining",
                Sound = null, // No sound for persistent notification
                Badge = NSNumber.FromInt32(1),
                ThreadIdentifier = "PrayerTimes"
            };

            // Add category for custom actions
            content.CategoryIdentifier = "PRAYER_TIMES";
            
            // Add custom data
            var userInfo = new NSMutableDictionary
            {
                { new NSString("prayerName"), new NSString(prayerName) },
                { new NSString("remainingTime"), new NSString(remainingTime) },
                { new NSString("nextPrayer"), new NSString(nextPrayer) }
            };
            content.UserInfo = userInfo;

            // Request notification after 2 seconds
            var trigger = UNTimeIntervalNotificationTrigger.CreateTrigger(2, false);
            var request = UNNotificationRequest.FromIdentifier("PrayerNotification", content, trigger);

            // Schedule the notification
            await UNUserNotificationCenter.Current.AddNotificationRequestAsync(request);
            
            Debug.WriteLine($"? Persistent notification: {prayerName} ({remainingTime})");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? Persistent notification failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Removes the persistent prayer notification.
    /// </summary>
    public static void HidePersistentNotification()
    {
        UNUserNotificationCenter.Current.RemoveDeliveredNotifications(new[] { "PrayerNotification" });
        Debug.WriteLine("? Persistent notification removed");
    }
}