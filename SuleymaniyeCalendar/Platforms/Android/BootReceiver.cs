using Android.App;
using Android.Content;
using Android.OS;

namespace SuleymaniyeCalendar;

[BroadcastReceiver(Enabled = true, Exported = false)]
public class BootReceiver : BroadcastReceiver
{
    public override void OnReceive(Context context, Intent intent)
    {
        if (intent?.Action == Intent.ActionBootCompleted &&
            Preferences.Get("ForegroundServiceEnabled", true))
        {
            var startIntent = new Intent(context, typeof(AlarmForegroundService))
                .SetAction("SuleymaniyeTakvimi.action.START_SERVICE");
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                context.StartForegroundService(startIntent);
            else
                context.StartService(startIntent);
        }
    }
}