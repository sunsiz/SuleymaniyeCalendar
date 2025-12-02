namespace SuleymaniyeCalendar.Services;

public class NotificationSettings
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Sound { get; set; } = "kus";
    public string PrayerId { get; set; } = string.Empty;
    public string PrayerName { get; set; } = string.Empty;
}

/// <summary>
/// Platform-specific service for scheduling prayer time alarms/notifications.
/// Implemented by AlarmForegroundService on Android, NullAlarmService on other platforms.
/// </summary>
public interface IAlarmService
{
    /// <summary>
    /// Schedules an alarm for a specific prayer time.
    /// </summary>
    /// <param name="alarmTime">The exact time to trigger the alarm.</param>
    /// <param name="requestCode">Unique ID for the alarm.</param>
    /// <param name="settings">Notification details (title, body, sound).</param>
    void SetAlarm(DateTime alarmTime, int requestCode, NotificationSettings settings);

    /// <summary>
    /// Cancels all scheduled prayer alarms.
    /// </summary>
    void CancelAllAlarms();

    /// <summary>
    /// Starts the foreground service for alarm scheduling (Android only).
    /// </summary>
    void StartAlarmForegroundService();

    /// <summary>
    /// Stops the foreground service (Android only).
    /// </summary>
    void StopAlarmForegroundService();
}
