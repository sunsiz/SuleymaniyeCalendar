using Android.App;
using Android.Content;
using SuleymaniyeCalendar.Services;

namespace SuleymaniyeCalendar.Platforms.Android
{
    [BroadcastReceiver(Enabled = true, Exported = true, Name = "org.suleymaniyevakfi.RadioNotificationReceiver")]
    [IntentFilter(new[] { "RADIO_PAUSE_ACTION" })]
    public class RadioNotificationReceiver : BroadcastReceiver
    {
        public override void OnReceive(Context context, Intent intent)
        {
            if (intent?.Action == "RADIO_PAUSE_ACTION")
            {
                // Get the radio service from DI container using the same pattern as other services
                var radioService = (Microsoft.Maui.Controls.Application.Current?.Handler?.MauiContext?.Services?.GetService(typeof(IRadioService)) as IRadioService);
                
                if (radioService != null)
                {
                    // We need to call this async method in a background task
                    Task.Run(async () => await radioService.PauseAsync());
                }
            }
        }
    }
}
