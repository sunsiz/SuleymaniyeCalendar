using Android.App;
using Android.Content;
using Android.OS;
// NOTE: We add an explicit Name so the generated Java class matches the manifest entry
// (org.suleymaniyevakfi.BootReceiver). Without this, Android tried to load
// org.suleymaniyevakfi.BootReceiver but the actual marshalled type name differed,
// resulting in ClassNotFoundException at boot/time change broadcasts.

namespace SuleymaniyeCalendar;

[BroadcastReceiver(Enabled = true, Exported = true, Name = "org.suleymaniyevakfi.BootReceiver")]
// We rely on intent-filters declared in AndroidManifest.xml; alternatively could add
// [IntentFilter] attributes here.
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