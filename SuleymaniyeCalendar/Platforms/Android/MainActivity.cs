using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;

namespace SuleymaniyeCalendar;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override async void OnCreate(Bundle? savedInstanceState)
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

            if (AnyRemindersEnabled())
            {
                EnsureExactAlarmCapability();
            }

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

    private bool AnyRemindersEnabled()
    {
        return Preferences.Get("falsefajrEnabled", false) ||
           Preferences.Get("fajrEnabled", false) ||
           Preferences.Get("sunriseEnabled", false) ||
           Preferences.Get("dhuhrEnabled", false) ||
           Preferences.Get("asrEnabled", false) ||
           Preferences.Get("maghribEnabled", false) ||
           Preferences.Get("ishaEnabled", false) ||
           Preferences.Get("endofishaEnabled", false);
    }

    async Task EnsureNotificationPermissionAsync()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.PostNotifications>();
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

    void EnsureExactAlarmCapability()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            var am = GetSystemService(AlarmService) as AlarmManager;
            if (am != null && !am.CanScheduleExactAlarms())
            {
                var intent = new Intent(Android.Provider.Settings.ActionRequestScheduleExactAlarm);
                intent.SetData(Android.Net.Uri.Parse($"package:{PackageName}"));
                StartActivity(intent);
            }
        }
    }

    /// <summary>
    /// Called after MAUI has fully initialized. Sets the Android system status bar color
    /// to match the app's Shell header. On Android 15+ (API 35) where SetStatusBarColor
    /// is deprecated, disables the contrast scrim so the transparent status bar doesn't
    /// show a gray overlay.
    /// </summary>
    protected override void OnPostCreate(Bundle? savedInstanceState)
    {
        base.OnPostCreate(savedInstanceState);
        ApplyStatusBarColor();
    }

    /// <summary>
    /// Re-applies status bar color when activity resumes (handles theme changes).
    /// </summary>
    protected override void OnResume()
    {
        base.OnResume();
        ApplyStatusBarColor();
    }

    void ApplyStatusBarColor()
    {
        if (Window is null) return;

        var isDark = (Resources?.Configuration?.UiMode & Android.Content.Res.UiMode.NightMask)
                     == Android.Content.Res.UiMode.NightYes;

        var color = isDark
            ? Android.Graphics.Color.ParseColor("#201F24")   // TabBarBackgroundColorDark
            : Android.Graphics.Color.ParseColor("#8A4E1E");  // TabBarBgColor

        // Set status bar color (works on API < 35)
        Window.SetStatusBarColor(color);

        // Disable the contrast enforcement scrim that Android 15+ adds over the
        // transparent status bar — our backgrounds are dark enough for white icons
        if (OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            Window.StatusBarContrastEnforced = false;
        }

        // Ensure light (white) status bar icons on dark brown/dark backgrounds
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            Window.InsetsController?.SetSystemBarsAppearance(0,
                (int)WindowInsetsControllerAppearance.LightStatusBars);
        }
    }
}
