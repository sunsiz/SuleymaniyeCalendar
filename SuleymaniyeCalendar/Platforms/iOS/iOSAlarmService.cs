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
        // Calculate time until alarm
        var now = DateTime.Now;
        var notifyTime = alarmTime;
        
        // If alarm time is in the past for today, it might be for tomorrow, 
        // but the logic in DataService usually handles dates.
        // We'll trust the alarmTime passed in.
        
        // Schedule the notification
        // We use the time string "HH:mm" as expected by SchedulePrayerNotificationAsync
        // But wait, SchedulePrayerNotificationAsync takes a string time and calculates offset.
        // Let's look at NotificationService.SchedulePrayerNotificationAsync signature again.
        // public static async Task SchedulePrayerNotificationAsync(string prayerName, string prayerTime, int notificationMinutesBefore = 0, DateTime? date = null)
        
        // The IAlarmService.SetAlarm receives the exact alarm time (already adjusted for offsets if any).
        // So we should pass 0 for notificationMinutesBefore.
        
        var prayerName = settings.PrayerName;
        var timeStr = alarmTime.ToString("HH:mm");
        
        // We need to run this async task
        Task.Run(async () => 
        {
            await NotificationService.SchedulePrayerNotificationAsync(
                prayerName, 
                timeStr, 
                0, 
                alarmTime.Date
            );
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
}
