using Foundation;
using SuleymaniyeCalendar.Services;
using UserNotifications;

namespace SuleymaniyeCalendar.Platforms.iOS;

/// <summary>
/// iOS implementation of IAlarmService using local notifications.
/// </summary>
public class iOSAlarmService : IAlarmService
{
    public void SetAlarm(DateTime alarmTime, int requestCode, NotificationSettings settings)
    {
        // alarmTime is already adjusted for the notification offset by NotificationSchedulerService
        // (e.g., if prayer is at 12:57 and offset is 5 min, alarmTime is 12:52)
        
        var prayerName = settings.PrayerName;
        
        // Use the exact alarm time that was already calculated with offset
        var alarmTimeString = alarmTime.ToString("HH:mm");
        
        // Get the actual prayer time from settings for display in the notification
        var actualPrayerTime = settings.PrayerTime;
        
        // Get the custom sound from settings
        var soundName = settings.Sound;
        
        // Fire-and-forget with proper error handling
        _ = Task.Run(async () => 
        {
            try
            {
                // Pass the offset-adjusted time as trigger time, actual prayer time for display, and custom sound
                await NotificationService.SchedulePrayerNotificationAsync(
                    prayerName, 
                    alarmTimeString,  // Notification trigger time (offset-adjusted)
                    0, 
                    alarmTime.Date,
                    actualPrayerTime,  // Actual prayer time for notification body display
                    soundName          // Custom alarm sound
                ).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"iOS notification scheduling failed for {prayerName}: {ex.Message}");
            }
        });
    }

    public void CancelAllAlarms()
    {
        UNUserNotificationCenter.Current.RemoveAllPendingNotificationRequests();
        UNUserNotificationCenter.Current.RemoveAllDeliveredNotifications();
    }

    public void StartAlarmForegroundService()
    {
        // Not applicable on iOS
    }

    public void StopAlarmForegroundService()
    {
        // Not applicable on iOS
    }

    public void RefreshNotification()
    {
        // Not applicable on iOS (notifications are scheduled, not persistent)
    }

    public bool SupportsForegroundService => false;
}
