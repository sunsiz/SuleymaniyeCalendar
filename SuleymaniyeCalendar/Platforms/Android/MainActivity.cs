using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace SuleymaniyeCalendar;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
	protected override async void OnCreate(Bundle savedInstanceState)
	{
        try
        {
            base.OnCreate(savedInstanceState);
            System.Diagnostics.Debug.WriteLine("MainActivity.OnCreate: Starting initialization...");
            // Yield once to keep async signature purposeful (avoids analyzer warning after deferring tasks)
            await Task.Yield();

            // Defer notification permission so location permission (requested by first page ViewModel) can surface first
            _ = Task.Run(async () =>
            {
                await Task.Delay(2500); // allow initial UI + location permission
                await MainThread.InvokeOnMainThreadAsync(async () => await EnsureNotificationPermissionAsync());
            });

            EnsureExactAlarmCapability();

            if (Preferences.Get("ForegroundServiceEnabled", true))
            {
                var startServiceIntent = new Intent(this, typeof(AlarmForegroundService));
                startServiceIntent.SetAction("SuleymaniyeTakvimi.action.START_SERVICE");

                if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                    StartForegroundService(startServiceIntent);
                else
                    StartService(startServiceIntent);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MainActivity.OnCreate: Exception - {ex.Message}");
        }
    }
	

    async Task EnsureNotificationPermissionAsync()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
            if (status != PermissionStatus.Granted)
            {
                var alreadyAsked = Preferences.Get("NotificationPermissionAsked", false);
                status = await Permissions.RequestAsync<Permissions.PostNotifications>();
                Preferences.Set("NotificationPermissionAsked", true);
                if (status == PermissionStatus.Granted)
                {
                    if (Preferences.Get("NotificationPrayerTimesEnabled", false) && Preferences.Get("ForegroundServiceEnabled", true))
                        RefreshForegroundServiceNotification();
                }
                // If denied the first time, do nothing intrusive; user can enable later from Settings screen.
            }
        }
    }

    void RefreshForegroundServiceNotification()
    {
        var refreshIntent = new Intent(this, typeof(AlarmForegroundService));
        refreshIntent.SetAction("SuleymaniyeTakvimi.action.REFRESH_NOTIFICATION");

        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            StartForegroundService(refreshIntent);
        else
            StartService(refreshIntent);
    }

    void OpenServiceChannelSettings()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var intent = new Intent(Android.Provider.Settings.ActionChannelNotificationSettings);
            intent.PutExtra(Android.Provider.Settings.ExtraAppPackage, PackageName);
            intent.PutExtra(Android.Provider.Settings.ExtraChannelId, "SuleymaniyeTakvimichannelId");
            intent.AddFlags(ActivityFlags.NewTask);
            StartActivity(intent);
        }
    }

    void EnsureExactAlarmCapability()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            var am = (AlarmManager)GetSystemService(AlarmService);
            if (am != null && !am.CanScheduleExactAlarms())
            {
                var intent = new Intent(Android.Provider.Settings.ActionRequestScheduleExactAlarm);
                intent.SetData(Android.Net.Uri.Parse($"package:{PackageName}"));
                StartActivity(intent);
            }
        }
    }
}
