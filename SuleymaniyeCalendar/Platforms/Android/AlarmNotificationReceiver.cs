using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using AndroidX.Core.App;
using Uri = Android.Net.Uri;

namespace SuleymaniyeCalendar;

[BroadcastReceiver(Enabled = true, Exported = false)]
public class AlarmNotificationReceiver : BroadcastReceiver
{
    private const int NotificationId = 2025;

    public override void OnReceive(Context context, Intent intent)
    {
        var name = intent?.GetStringExtra("name") ?? string.Empty;
        var timeStr = intent?.GetStringExtra("time") ?? string.Empty;
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(timeStr))
            return;

        // Ensure channels exist (no-op if already created)
        NotificationChannelManager.CreateAlarmNotificationChannels();

        var nm = (NotificationManager)context.GetSystemService(Context.NotificationService);

    var pkg = context.PackageName;

        // Pick channel by user selection (same ids as before)
        var channelId = name switch
        {
            "Fecri Kazip" => Preferences.Get("fecrikazipAlarmSesi", "alarm"),
            "Fecri Sadık" => Preferences.Get("fecrisadikAlarmSesi", "alarm"),
            "Sabah Sonu" => Preferences.Get("sabahsonuAlarmSesi", "alarm"),
            "Öğle"       => Preferences.Get("ogleAlarmSesi", "alarm"),
            "İkindi"     => Preferences.Get("ikindiAlarmSesi", "alarm"),
            "Akşam"      => Preferences.Get("aksamAlarmSesi", "alarm"),
            "Yatsı"      => Preferences.Get("yatsiAlarmSesi", "alarm"),
            "Yatsı Sonu" => Preferences.Get("yatsisonuAlarmSesi", "alarm"),
            _            => "alarm"
        } switch
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

        var builder = new NotificationCompat.Builder(context, channelId)
            .SetSmallIcon(Resource.Drawable.app_logo)
            .SetLargeIcon(BitmapFactory.DecodeResource(context.Resources, Resource.Drawable.app_logo))
            .SetContentTitle(title)
            .SetContentText(content)
            .SetPriority((int)NotificationPriority.Max)
            .SetCategory(NotificationCompat.CategoryAlarm)
            .SetAutoCancel(true)
            .SetContentIntent(contentPi)
            .SetDefaults(0);

        nm.Notify(NotificationId, builder.Build());
    }
}
