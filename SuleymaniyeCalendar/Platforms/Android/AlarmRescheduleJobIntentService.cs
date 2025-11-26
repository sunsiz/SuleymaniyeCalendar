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
internal sealed class AlarmRescheduleJobIntentService : JobIntentService
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
