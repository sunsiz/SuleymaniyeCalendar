using Foundation;
using UserNotifications;

namespace SuleymaniyeCalendar
{
    internal class UserNotificationCenterDelegate : UNUserNotificationCenterDelegate
    {
        // This method is called when a notification is delivered to a foreground app
        public override void WillPresentNotification(UNUserNotificationCenter center, UNNotification notification, 
            Action<UNNotificationPresentationOptions> completionHandler)
        {
            // Show the notification even when the app is in the foreground
            completionHandler(UNNotificationPresentationOptions.Alert | 
                            UNNotificationPresentationOptions.Sound | 
                            UNNotificationPresentationOptions.Badge);
        }

        // This method is called when the user interacts with a notification
        public override void DidReceiveNotificationResponse(UNUserNotificationCenter center, UNNotificationResponse response, 
            Action completionHandler)
        {
            // Handle the notification tap/interaction here if needed
            completionHandler();
        }
    }
}