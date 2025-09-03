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
		base.OnCreate(savedInstanceState);

		await EnsureNotificationPermissionAsync();
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
	

    async Task EnsureNotificationPermissionAsync()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.PostNotifications>();
                if (status != PermissionStatus.Granted)
                {
                    // Optional: guide user to settings
                    AppInfo.ShowSettingsUI();
                }
                else
                {
                    // Permission granted - refresh notification display if prayer notifications are enabled
                    if (Preferences.Get("NotificationPrayerTimesEnabled", false) && Preferences.Get("ForegroundServiceEnabled", true))
                    {
                        RefreshForegroundServiceNotification();
                    }
                }
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
