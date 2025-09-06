using Android.App;
using Android.Content;
using Android.OS;

namespace SuleymaniyeCalendar;

[BroadcastReceiver(Enabled = true, Exported = true, Name = "org.suleymaniyevakfi.BootReceiver")]
[IntentFilter(new[] {
    Intent.ActionBootCompleted,
    Intent.ActionTimeChanged,
    Intent.ActionTimezoneChanged,
    Intent.ActionMyPackageReplaced })]
public class BootReceiver : BroadcastReceiver
{
    public override void OnReceive(Context context, Intent intent)
    {
        var action = intent?.Action;
        if (action is null) return;

        // Start foreground service to rehydrate notifications/alarms after system events
        bool shouldStart = action == Intent.ActionBootCompleted
                           || action == Intent.ActionTimeChanged
                           || action == Intent.ActionTimezoneChanged
                           || action == Intent.ActionMyPackageReplaced;

        if (!shouldStart) return;
        if (!Preferences.Get("ForegroundServiceEnabled", true)) return;

        var startIntent = new Intent(context, typeof(AlarmForegroundService))
            .SetAction("SuleymaniyeTakvimi.action.START_SERVICE");
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            context.StartForegroundService(startIntent);
        else
            context.StartService(startIntent);
    }
}