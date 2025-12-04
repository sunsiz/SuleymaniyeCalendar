using Foundation;
using UIKit;
using UserNotifications;
using SuleymaniyeCalendar.Platforms.iOS;

namespace SuleymaniyeCalendar;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override bool FinishedLaunching(UIApplication app, NSDictionary options)
    {
        // Initialize notifications
        if (UIDevice.CurrentDevice.CheckSystemVersion(10, 0))
        {
            var center = UNUserNotificationCenter.Current;
            center.Delegate = new UserNotificationCenterDelegate();
            
            // Request permission and setup categories
            Task.Run(async () => await NotificationService.InitializeNotificationsAsync());
        }

        return base.FinishedLaunching(app, options);
    }
}
