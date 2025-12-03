using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.Content;
using Microsoft.Maui.Storage;

namespace SuleymaniyeCalendar;

[BroadcastReceiver(Enabled = true, Exported = true, Name = "org.suleymaniyevakfi.BootReceiver")]
[IntentFilter(new[] {
    Intent.ActionBootCompleted,
    Intent.ActionTimeChanged,
    Intent.ActionTimezoneChanged,
    Intent.ActionMyPackageReplaced,
    Intent.ActionUserUnlocked })]
public class BootReceiver : BroadcastReceiver
{
    private static readonly string[] RescheduleActions =
    {
        Intent.ActionBootCompleted,
        Intent.ActionTimeChanged,
        Intent.ActionTimezoneChanged,
        Intent.ActionMyPackageReplaced
    };

    public override void OnReceive(Context? context, Intent? intent)
    {
        var action = intent?.Action;
        if (action is null || context is null) return;

        if (IsRescheduleAction(action))
        {
            // AlarmRescheduleJobIntentService.Enqueue(context, action);
            TryStartForegroundService(context, action);
            return;
        }

        if (action == Intent.ActionUserUnlocked)
        {
            ResumePendingForegroundStart(context, action);
        }
    }

    private static bool IsRescheduleAction(string action)
    {
        foreach (var candidate in RescheduleActions)
        {
            if (candidate == action)
            {
                return true;
            }
        }

        return false;
    }

    private static void TryStartForegroundService(Context context, string sourceAction)
    {
        if (!Preferences.Get("ForegroundServiceEnabled", true))
        {
            Preferences.Set(AlarmForegroundService.PendingForegroundRestartPreferenceKey, false);
            return;
        }

        if (!IsUserUnlocked(context))
        {
            Preferences.Set(AlarmForegroundService.PendingForegroundRestartPreferenceKey, true);
            return;
        }

        Preferences.Set(AlarmForegroundService.PendingForegroundRestartPreferenceKey, false);
        StartForegroundService(context, sourceAction);
    }

    private static void ResumePendingForegroundStart(Context context, string sourceAction)
    {
        var pending = Preferences.Get(AlarmForegroundService.PendingForegroundRestartPreferenceKey, false);
        if (!pending)
        {
            return;
        }

        if (!Preferences.Get("ForegroundServiceEnabled", true))
        {
            Preferences.Set(AlarmForegroundService.PendingForegroundRestartPreferenceKey, false);
            return;
        }

        if (!IsUserUnlocked(context))
        {
            return;
        }

        Preferences.Set(AlarmForegroundService.PendingForegroundRestartPreferenceKey, false);
        StartForegroundService(context, sourceAction);
    }

    private static bool IsUserUnlocked(Context context)
    {
        var userManager = context.GetSystemService(Context.UserService) as UserManager;
        return userManager?.IsUserUnlocked ?? true;
    }

    private static void StartForegroundService(Context context, string actionSource)
    {
        var startIntent = new Intent(context, typeof(AlarmForegroundService))
            .SetAction(AlarmForegroundService.StartAction)
            .PutExtra(AlarmForegroundService.ExtraStartReason, actionSource ?? string.Empty);

        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            ContextCompat.StartForegroundService(context, startIntent);
        }
        else
        {
            context.StartService(startIntent);
        }
    }
}