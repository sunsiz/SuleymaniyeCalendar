using UserNotifications;
using Foundation;
using System.Diagnostics;
using System.Globalization;
using SuleymaniyeCalendar.Models;
using SuleymaniyeCalendar.Resources.Strings;

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
    /// <param name="notificationTime">Time to fire the notification (e.g., "05:25" for 5 min before prayer)</param>
    /// <param name="notificationMinutesBefore">Minutes before prayer (for calculating actual prayer time display)</param>
    /// <param name="date">Date to schedule notification</param>
    /// <param name="actualPrayerTime">Optional: actual prayer time for display (e.g., "05:30")</param>
    /// <param name="soundName">Optional: custom sound file name without extension (e.g., "kus", "ezan")</param>
    public static async Task SchedulePrayerNotificationAsync(
        string prayerName,
        string notificationTime,
        int notificationMinutesBefore = 0,
        DateTime? date = null,
        string? actualPrayerTime = null,
        string? soundName = null)
    {
        try
        {
            // NOTE: Culture is now set ONCE in the calling service (NotificationSchedulerService)
            // No need to set it per-notification to avoid 30x overhead

            // Cache authorization status check (don't check 30 times per batch)
            // Caller should check once before batch scheduling
            var center = UNUserNotificationCenter.Current;

            var targetDate = date ?? DateTime.Today;

            // Parse notification trigger time using InvariantCulture for consistent HH:mm parsing
            if (!TimeSpan.TryParse(notificationTime, System.Globalization.CultureInfo.InvariantCulture, out var time))
            {
                Debug.WriteLine($"❌ Invalid notification time format: {notificationTime}");
                return;
            }

            // The time passed is already the notification trigger time (offset-adjusted)
            var triggerTime = time;
            
            // Check if the scheduled time is in the past for today
            // NotificationSchedulerService already filters past times, but double-check here
            var scheduledDateTime = targetDate.Add(triggerTime);
            if (scheduledDateTime <= DateTime.Now)
            {
                Debug.WriteLine($"⚠️ Notification time in past: {prayerName} at {scheduledDateTime:yyyy-MM-dd HH:mm} (now: {DateTime.Now:HH:mm})");
                return;
            }
            
            // Use actual prayer time for display if provided, otherwise calculate from notification time
            var displayTime = actualPrayerTime ?? notificationTime;

            // Create notification content with custom sound
            // iOS requires sound files to be in the app bundle (Resources/Raw folder)
            // Sound file names should include extension (e.g., "kus.mp3")
            UNNotificationSound notificationSound;
            if (!string.IsNullOrEmpty(soundName))
            {
                var soundFileName = soundName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)
                    ? soundName
                    : $"{soundName}.mp3";

                // Cache sound path lookup - this is expensive when called 30 times
                // Note: If file is missing in bundle, iOS may silently drop sound; fallback to default.
                notificationSound = UNNotificationSound.GetSound(soundFileName);
                Debug.WriteLine($"🔊 Using custom sound: {soundFileName}");
            }
            else
            {
                notificationSound = UNNotificationSound.Default;
            }

            var content = new UNMutableNotificationContent
            {
                Title = AppResources.SuleymaniyeVakfiTakvimi,
                Body = $"{prayerName} {AppResources.Vakti}{displayTime}",
                Sound = notificationSound,
                // Set badge to 1 to show there's a prayer notification
                // Badge will be cleared when user opens the app
                Badge = 1,
                CategoryIdentifier = PrayerNotificationCategoryId,
                ThreadIdentifier = "PrayerTimes"
            };

            // Add custom data (only property-list compatible values)
            var userInfo = new NSMutableDictionary
            {
                { new NSString("prayerName"), new NSString(prayerName) },
                { new NSString("prayerTime"), new NSString(displayTime) },
                { new NSString("date"), new NSString(targetDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)) }
            };
            content.UserInfo = userInfo;

            // Create trigger for specific time using the parsed triggerTime
            // Use user's local calendar and timezone for correct scheduling
            var components = new NSDateComponents
            {
                Calendar = NSCalendar.CurrentCalendar,
                TimeZone = NSTimeZone.LocalTimeZone,
                Year = targetDate.Year,
                Month = targetDate.Month,
                Day = targetDate.Day,
                Hour = triggerTime.Hours,
                Minute = triggerTime.Minutes,
                Second = 0
            };

            var trigger = UNCalendarNotificationTrigger.CreateTrigger(components, false);

            // Create notification request with unique ID per prayer and date
            var requestId = $"prayer_{prayerName}_{targetDate:yyyyMMdd}_{triggerTime.Hours:D2}{triggerTime.Minutes:D2}";
            var request = UNNotificationRequest.FromIdentifier(requestId, content, trigger);

            // Schedule notification with comprehensive error handling
            try
            {
                await center.AddNotificationRequestAsync(request);
                Debug.WriteLine($"✅ Scheduled: {prayerName} at {triggerTime:hh\\:mm} ({targetDate:yyyy-MM-dd}) [ID: {requestId}]");
            }
            catch (Exception addEx)
            {
                // Log detailed error to help diagnose iOS notification issues
                Debug.WriteLine($"❌ AddNotificationRequest failed for {prayerName}: {addEx.GetType().Name}: {addEx.Message}");

                // Check if this is a permission issue (only on first error to avoid spam)
                var permissionSettings = await UNUserNotificationCenter.Current.GetNotificationSettingsAsync();
                Debug.WriteLine($"   Permission status: {permissionSettings.AuthorizationStatus}");

                if (permissionSettings.AuthorizationStatus != UNAuthorizationStatus.Authorized)
                {
                    Debug.WriteLine("   ⚠️ CRITICAL: Notification permission not authorized. User must grant permission in Settings.");
                }

                throw; // Re-throw to trigger outer catch
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Notification scheduling failed: {ex.Message}");
            Debug.WriteLine($"   Stack trace: {ex.StackTrace}");
        }
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
    /// Schedules a test notification 30 seconds from now for testing purposes.
    /// </summary>
    public static async Task ScheduleTestNotificationAsync()
    {
        try
        {
            var center = UNUserNotificationCenter.Current;
            
            // Set culture to user's selected language
            try
            {
                var savedLanguage = Preferences.Get("SelectedLanguage", "tr");
                var culture = new CultureInfo(savedLanguage);
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;
                AppResources.Culture = culture;
            }
            catch { }

            // Check permission
            var settings = await center.GetNotificationSettingsAsync();
            if (settings.AuthorizationStatus != UNAuthorizationStatus.Authorized)
            {
                Debug.WriteLine("⚠️ Cannot schedule test notification: permission not granted");
                return;
            }

            // Create test notification content
            var content = new UNMutableNotificationContent
            {
                Title = "Test Notification",
                Body = "This is a test notification from Süleymaniye Calendar. If you see this, notifications are working!",
                Sound = UNNotificationSound.Default,
                Badge = 1
            };

            // Schedule for 30 seconds from now
            var trigger = UNTimeIntervalNotificationTrigger.CreateTrigger(30, false);
            var requestId = $"test_notification_{DateTime.Now:yyyyMMddHHmmss}";
            var request = UNNotificationRequest.FromIdentifier(requestId, content, trigger);

            await center.AddNotificationRequestAsync(request);
            Debug.WriteLine($"✅ Test notification scheduled for 30 seconds from now [ID: {requestId}]");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Failed to schedule test notification: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Checks if notification permission has been granted.
    /// </summary>
    /// <returns>True if notification permission is granted, false otherwise.</returns>
    public static async Task<bool> CheckNotificationPermissionAsync()
    {
        try
        {
            var center = UNUserNotificationCenter.Current;
            var settings = await center.GetNotificationSettingsAsync();

            var isAuthorized = settings.AuthorizationStatus is UNAuthorizationStatus.Authorized
                or UNAuthorizationStatus.Provisional
                or UNAuthorizationStatus.Ephemeral;

            Debug.WriteLine($"📋 Notification permission status: {settings.AuthorizationStatus} (Authorized: {isAuthorized})");

            return isAuthorized;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Failed to check notification permission: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Requests notification permission from the user.
    /// This can be called even if permission was previously denied.
    /// </summary>
    /// <returns>True if permission is granted, false otherwise.</returns>
    public static async Task<bool> RequestNotificationPermissionAsync()
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
                SetupNotificationCategories();
                return true;
            }
            else if (error != null)
            {
                Debug.WriteLine($"⚠️ Notification permission denied: {error.LocalizedDescription}");
            }
            else
            {
                Debug.WriteLine("⚠️ Notification permission denied by user");
            }
            
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Notification permission request failed: {ex.Message}");
            return false;
        }
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