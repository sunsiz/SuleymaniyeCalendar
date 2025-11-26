namespace SuleymaniyeCalendar.Services;

/// <summary>
/// Platform-specific service for scheduling prayer time alarms/notifications.
/// Implemented by AlarmForegroundService on Android, NullAlarmService on other platforms.
/// </summary>
public interface IAlarmService
{
    /// <summary>
    /// Schedules an alarm for a specific prayer time.
    /// </summary>
    /// <param name="date">The date of the alarm.</param>
    /// <param name="triggerTimeSpan">Time of day for the alarm.</param>
    /// <param name="timeOffset">Minutes before/after prayer time to trigger (can be negative).</param>
    /// <param name="name">Prayer name for the notification.</param>
    void SetAlarm(DateTime date, TimeSpan triggerTimeSpan, int timeOffset, string name);

    /// <summary>
    /// Cancels all scheduled prayer alarms.
    /// </summary>
    void CancelAlarm();

    /// <summary>
    /// Starts the foreground service for alarm scheduling (Android only).
    /// </summary>
    void StartAlarmForegroundService();

    /// <summary>
    /// Stops the foreground service (Android only).
    /// </summary>
    void StopAlarmForegroundService();
}
