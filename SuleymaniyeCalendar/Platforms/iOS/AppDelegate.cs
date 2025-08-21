using Foundation;
using UIKit;
using UserNotifications;

namespace SuleymaniyeCalendar;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override bool FinishedLaunching(UIApplication app, NSDictionary options)
    {
        if (UIDevice.CurrentDevice.CheckSystemVersion(10, 0))
        {
            var center = UNUserNotificationCenter.Current;
            center.RequestAuthorization(UNAuthorizationOptions.Alert | UNAuthorizationOptions.Badge | UNAuthorizationOptions.Sound,
                (approved, error) => System.Diagnostics.Debug.WriteLine($"approved: {approved}, error: {error}"));
            center.Delegate = new UserNotificationCenterDelegate();
        }
        return base.FinishedLaunching(app, options);
    }
}
