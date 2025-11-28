using System;
using Android.App;
using Android.Content;
using AndroidX.Core.App;
using SuleymaniyeCalendar.Services;

namespace SuleymaniyeCalendar;

[Service(
    Name = "org.suleymaniyevakfi.AlarmRescheduleJob",
    Permission = "android.permission.BIND_JOB_SERVICE",
    Exported = false
)]
// JobIntentService is marked obsolete by Google but still works reliably for background job execution.
// The recommended replacement (WorkManager) would require significant architectural changes.
// This service handles alarm rescheduling after device reboot/time changes.
#pragma warning disable CS0618 // JobIntentService is obsolete but still functional
internal sealed class AlarmRescheduleJobIntentService : JobIntentService
#pragma warning restore CS0618
{
    private const int JobId = 1994;
    internal const string ExtraReason = "extra_reschedule_reason";

    // Enqueues headless work to rebuild prayer alarms after critical broadcasts.
    internal static void Enqueue(Context context, string reason)
    {
        var work = new Intent(context, typeof(AlarmRescheduleJobIntentService));
        work.PutExtra(ExtraReason, reason ?? string.Empty);
        EnqueueWork(context, Java.Lang.Class.FromType(typeof(AlarmRescheduleJobIntentService)), JobId, work);
    }

    // Executes the reschedule operation on a JobScheduler backed worker thread.
    protected override void OnHandleWork(Intent intent)
    {
        try
        {
            var alarmService = new AlarmSchedulerService();
            var dataService = new DataService(alarmService);
            dataService.SetMonthlyAlarmsAsync().GetAwaiter().GetResult();
            System.Diagnostics.Debug.WriteLine("AlarmRescheduleJobIntentService: monthly alarms refreshed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AlarmRescheduleJobIntentService failure: {ex.Message}\n{ex.StackTrace}");
        }
    }
}
