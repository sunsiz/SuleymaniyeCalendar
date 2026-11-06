using Android.App;
using Android.App.Job;
using Android.Content;
using Android.Util;
using SuleymaniyeCalendar.Services;

namespace SuleymaniyeCalendar;

[Service(Exported = true, Permission = "android.permission.BIND_JOB_SERVICE")]
public class AlarmRescheduleJob : JobService
{
    private const int JobId = 199301;
    private const int PeriodicJobId = 199302;

    internal static void Schedule(Context context)
    {
        var scheduler = context.GetSystemService(Context.JobSchedulerService) as JobScheduler;
        if (scheduler is null)
        {
            Log.Error(nameof(AlarmRescheduleJob), "JobScheduler is unavailable.");
            return;
        }

        var component = new ComponentName(context, Java.Lang.Class.FromType(typeof(AlarmRescheduleJob)));
        var job = new JobInfo.Builder(JobId, component)
            .SetMinimumLatency(0)
            .Build();
        if (scheduler.Schedule(job) != JobScheduler.ResultSuccess)
            Log.Error(nameof(AlarmRescheduleJob), "Could not schedule alarm recovery.");
    }

    internal static void SchedulePeriodic(Context context)
    {
        var scheduler = context.GetSystemService(Context.JobSchedulerService) as JobScheduler;
        if (scheduler is null)
        {
            Log.Error(nameof(AlarmRescheduleJob), "JobScheduler is unavailable.");
            return;
        }
        if (scheduler.GetPendingJob(PeriodicJobId) is not null)
            return;

        var component = new ComponentName(context, Java.Lang.Class.FromType(typeof(AlarmRescheduleJob)));
        var job = new JobInfo.Builder(PeriodicJobId, component)
            .SetPeriodic((long)TimeSpan.FromDays(1).TotalMilliseconds)
            .SetPersisted(true)
            .Build();
        if (scheduler.Schedule(job) != JobScheduler.ResultSuccess)
            Log.Error(nameof(AlarmRescheduleJob), "Could not schedule periodic alarm recovery.");
    }

    internal static void CancelPeriodic(Context context)
    {
        var scheduler = context.GetSystemService(Context.JobSchedulerService) as JobScheduler;
        scheduler?.Cancel(PeriodicJobId);
    }

    public override bool OnStartJob(JobParameters? parameters)
    {
        if (parameters is null)
            return false;

        _ = Task.Run(async () =>
        {
            var retry = false;
            try
            {
                var data = IPlatformApplication.Current?.Services.GetService<DataService>()
                    ?? throw new InvalidOperationException("Application services are unavailable.");
                if (data.CheckRemindersEnabledAny())
                    await data.SetMonthlyAlarmsAsync(forceReschedule: parameters.JobId == JobId).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(nameof(AlarmRescheduleJob), $"Alarm recovery failed: {ex}");
                retry = true;
            }
            finally
            {
                JobFinished(parameters, retry);
            }
        });
        return true;
    }

    public override bool OnStopJob(JobParameters? parameters) => true;
}
