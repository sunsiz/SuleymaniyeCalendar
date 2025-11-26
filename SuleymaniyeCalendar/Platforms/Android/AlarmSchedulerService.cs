using System;
using SuleymaniyeCalendar.Services;

namespace SuleymaniyeCalendar;

internal sealed class AlarmSchedulerService : IAlarmService
{
    // Delegates to AlarmScheduler to place an alarm on the system clock.
    public void SetAlarm(DateTime date, TimeSpan triggerTimeSpan, int timeOffset, string name)
    {
        AlarmScheduler.SchedulePrayer(date, triggerTimeSpan, timeOffset, name);
    }

    // Removes every pending alarm created by the scheduler.
    public void CancelAlarm()
    {
        AlarmScheduler.CancelAll();
    }

    // Headless scheduler never elevates to a foreground service.
    public void StartAlarmForegroundService()
    {
        // Headless scheduler does not maintain a persistent notification.
    }

    // Headless scheduler never holds a foreground notification.
    public void StopAlarmForegroundService()
    {
        // Headless scheduler does not maintain a persistent notification.
    }
}
