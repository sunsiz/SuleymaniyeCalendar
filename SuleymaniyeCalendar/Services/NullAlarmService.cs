namespace SuleymaniyeCalendar.Services;

/// <summary>
/// No-op implementation of IAlarmService for platforms without alarm support (iOS, Windows).
/// All methods are intentionally empty.
/// </summary>
public sealed class NullAlarmService : IAlarmService
{
    public void SetAlarm(DateTime alarmTime, int requestCode, NotificationSettings settings) { }
    public void CancelAllAlarms() { }
    public void StartAlarmForegroundService() { }
    public void StopAlarmForegroundService() { }
}
