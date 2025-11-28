using System;
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
            // Use List | Banner instead of obsolete Alert option (iOS 14+)
            completionHandler(UNNotificationPresentationOptions.List | 
                              UNNotificationPresentationOptions.Banner |
                              UNNotificationPresentationOptions.Sound | 
                              UNNotificationPresentationOptions.Badge);
        }

        // This method is called when the user interacts with a notification
        public override void DidReceiveNotificationResponse(
            UNUserNotificationCenter center,
            UNNotificationResponse response,
            Action completionHandler)
        {
            try
            {
                var userInfo = response.Notification.Request.Content.UserInfo;
                
                if (userInfo?["prayerName"] is NSString prayerName)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"📲 User tapped notification: {prayerName}");
                    
                    // Navigate to prayer detail or show information
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await Shell.Current.GoToAsync("mainpage");
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Notification response error: {ex.Message}");
            }
            finally
            {
                completionHandler();
            }
        }
    }
}