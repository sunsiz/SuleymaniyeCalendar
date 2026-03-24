using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using AndroidX.Core.App;
using System.Globalization;
using Uri = Android.Net.Uri;

namespace SuleymaniyeCalendar;

[BroadcastReceiver(Enabled = true, Exported = false)]
public class AlarmNotificationReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null) return;

        // Set culture to user's selected language for localized notification text
        string savedLanguage = "tr";
        try
        {
            savedLanguage = Preferences.Get("SelectedLanguage", "tr");
            System.Diagnostics.Debug.WriteLine($"🔔 AlarmNotificationReceiver: savedLanguage = {savedLanguage}");

            var culture = new CultureInfo(savedLanguage);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            Resources.Strings.AppResources.Culture = culture;

            System.Diagnostics.Debug.WriteLine($"🔔 Culture set to: {culture.Name}, AppResources test: {Resources.Strings.AppResources.Alarmi}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Failed to set notification culture: {ex.Message}");
        }

        var name = intent?.GetStringExtra("name") ?? string.Empty;
        var timeStr = intent?.GetStringExtra("time") ?? string.Empty;
        var prayerId = intent?.GetStringExtra("prayerId") ?? string.Empty;

        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(timeStr))
            return;

        // Ensure channels exist (no-op if already created)
        NotificationChannelManager.CreateAlarmNotificationChannels();

        var nm = context.GetSystemService(Context.NotificationService) as NotificationManager;
        if (nm == null) return;

        var pkg = context.PackageName;

        // If prayerId not provided in intent, try to match by localized name (backward compatibility)
        if (string.IsNullOrEmpty(prayerId))
        {
            prayerId = name switch
            {
                "Fecri Kazip" => "falsefajr",
                "Fecri Sadık" => "fajr",
                "Sabah Sonu"  => "sunrise",
                "Öğle"        => "dhuhr",
                "İkindi"      => "asr",
                "Akşam"       => "maghrib",
                "Yatsı"       => "isha",
                "Yatsı Sonu"  => "endofisha",
                _              => "asr" // fallback
            };
        }

        // Prefer sound from Intent (set at schedule time), fall back to Preferences
        var soundPref = intent?.GetStringExtra("sound");
        if (string.IsNullOrEmpty(soundPref))
        {
            soundPref = Preferences.Get(prayerId + "AlarmSound", "kus");
        }
        System.Diagnostics.Debug.WriteLine($"🔔 AlarmNotificationReceiver: prayerId={prayerId}, sound={soundPref}");

        var channelId = soundPref switch
        {
            "kus"   => "SuleymaniyeTakvimialarmbirdchannelId",
            "horoz" => "SuleymaniyeTakvimialarmroosterchannelId",
            "ezan"  => "SuleymaniyeTakvimialarmadhanchannelId",
            "alarm" => "SuleymaniyeTakvimialarmalarmchannelId",
            "alarm2"=> "SuleymaniyeTakvimialarmalarm1channelId",
            "beep1" => "SuleymaniyeTakvimialarmalarm2channelId",
            "beep2" => "SuleymaniyeTakvimialarmalarm3channelId",
            "beep3" => "SuleymaniyeTakvimialarmalarm4channelId",
            _       => "SuleymaniyeTakvimialarmalarmchannelId"
        };

        var openAppIntent = new Intent(context, typeof(MainActivity))
            .SetAction("Alarm.action.MAIN_ACTIVITY")
            .SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTask);

        var piFlags = (Build.VERSION.SdkInt > BuildVersionCodes.R)
            ? PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable
            : PendingIntentFlags.UpdateCurrent;

        var contentPi = PendingIntent.GetActivity(context, 0, openAppIntent, piFlags);

        // Localized texts (same as old AlarmReceiver helpers)
        string title = name switch
        {
            "Fecri Kazip" => Resources.Strings.AppResources.FecriKazip + " " + Resources.Strings.AppResources.Alarmi,
            "Fecri Sadık" => Resources.Strings.AppResources.FecriSadik + " " + Resources.Strings.AppResources.Alarmi,
            "Sabah Sonu"  => Resources.Strings.AppResources.SabahSonu + " " + Resources.Strings.AppResources.Alarmi,
            "Öğle"        => Resources.Strings.AppResources.Ogle + " " + Resources.Strings.AppResources.Alarmi,
            "İkindi"      => Resources.Strings.AppResources.Ikindi + " " + Resources.Strings.AppResources.Alarmi,
            "Akşam"       => Resources.Strings.AppResources.Aksam + " " + Resources.Strings.AppResources.Alarmi,
            "Yatsı"       => Resources.Strings.AppResources.Yatsi + " " + Resources.Strings.AppResources.Alarmi,
            "Yatsı Sonu"  => Resources.Strings.AppResources.YatsiSonu + " " + Resources.Strings.AppResources.Alarmi,
            _             => Resources.Strings.AppResources.Alarmi
        };

        var content = $"{name} {Resources.Strings.AppResources.Vakti} {timeStr}";

        // Use unique notification ID per prayer so simultaneous alarms don't overwrite each other
        int notificationId = GenerateNotificationId(prayerId);

        // Create large icon bitmap safely - decode fresh copy to avoid recycled bitmap issues
        Bitmap? largeIcon = null;
        try
        {
            largeIcon = BitmapFactory.DecodeResource(context.Resources, Resource.Drawable.app_logo);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to decode large icon: {ex.Message}");
        }

        var builder = new NotificationCompat.Builder(context, channelId)
            .SetSmallIcon(Resource.Drawable.app_logo)
            .SetContentTitle(title)
            .SetContentText(content)
            .SetPriority((int)NotificationPriority.Max)
            .SetCategory(NotificationCompat.CategoryAlarm)
            .SetAutoCancel(true)
            .SetContentIntent(contentPi)
            .SetDefaults(0);

        // Only set large icon if bitmap was successfully created and not recycled
        if (largeIcon != null && !largeIcon.IsRecycled)
        {
            builder.SetLargeIcon(largeIcon);
        }

        var notification = builder.Build();
        if (notification != null && nm != null)
        {
            nm.Notify(notificationId, notification);
        }
    }

    /// <summary>
    /// Generates a stable notification ID per prayer to prevent notification overwrites
    /// while allowing updates for the same prayer.
    /// </summary>
    private static int GenerateNotificationId(string prayerId)
    {
        return prayerId switch
        {
            "falsefajr" => 2026_00,
            "fajr"      => 2026_01,
            "sunrise"   => 2026_02,
            "dhuhr"     => 2026_03,
            "asr"       => 2026_04,
            "maghrib"   => 2026_05,
            "isha"      => 2026_06,
            "endofisha" => 2026_07,
            _           => 2026_08
        };
    }
}
