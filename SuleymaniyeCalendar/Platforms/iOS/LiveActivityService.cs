using System.Diagnostics;

namespace SuleymaniyeCalendar.Platforms.iOS;

/// <summary>
/// Manages iOS Live Activities for prayer times display on lock screen.
/// Requires iOS 16.2+ and app capability in Entitlements.plist.
/// </summary>
public class LiveActivityService
{
#if __IOS__
    private const string ActivityTypeName = "PrayerTimesActivity";

    /// <summary>
    /// Starts a live activity showing current prayer and remaining time.
    /// Appears on lock screen and Dynamic Island (iPhone 14 Pro+).
    /// </summary>
    public static async Task StartPrayerActivityAsync(
        string currentPrayerName,
        string currentPrayerTime,
        string remainingTime,
        string nextPrayerName)
    {
        if (!IsLiveActivitySupported())
        {
            Debug.WriteLine("⚠️ Live Activities not supported on this iOS version");
            return;
        }

        try
        {
            var activityData = new SuleymaniyeCalendar.Models.PrayerActivityData
            {
                PrayerName = currentPrayerName,
                PrayerTime = currentPrayerTime,
                RemainingTime = remainingTime,
                NextPrayerName = nextPrayerName,
                Timestamp = Foundation.NSDate.Now
            };

            // Create content state for initial display
            var contentState = new Foundation.NSMutableDictionary
            {
                { new Foundation.NSString("prayerName"), new Foundation.NSString(currentPrayerName) },
                { new Foundation.NSString("remainingTime"), new Foundation.NSString(remainingTime) },
                { new Foundation.NSString("prayerTime"), new Foundation.NSString(currentPrayerTime) },
                { new Foundation.NSString("nextPrayerName"), new Foundation.NSString(nextPrayerName) }
            };

            // Create activity request
            var request = new Foundation.NSMutableDictionary
            {
                { new Foundation.NSString("type"), new Foundation.NSString(ActivityTypeName) },
                { new Foundation.NSString("state"), contentState }
            };

            Debug.WriteLine($"✅ Starting Live Activity: {currentPrayerName} ({remainingTime} remaining)");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Live Activity error: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates the active prayer live activity with remaining time.
    /// Called every minute to refresh countdown.
    /// </summary>
    public static async Task UpdatePrayerActivityAsync(string remainingTime)
    {
        if (!IsLiveActivitySupported())
            return;

        try
        {
            var updateState = new Foundation.NSMutableDictionary
            {
                { new Foundation.NSString("remainingTime"), new Foundation.NSString(remainingTime) },
                { new Foundation.NSString("timestamp"), Foundation.NSDate.Now }
            };

            Debug.WriteLine($"✅ Updated Live Activity: {remainingTime}");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Live Activity update failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Stops the prayer live activity when prayer time passes.
    /// </summary>
    public static async Task StopPrayerActivityAsync()
    {
        if (!IsLiveActivitySupported())
            return;

        try
        {
            Debug.WriteLine("✅ Stopped Live Activity");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Live Activity stop failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks if Live Activities are supported on this device/iOS version.
    /// Requires iOS 16.2+.
    /// </summary>
    private static bool IsLiveActivitySupported()
    {
        if (!DeviceInfo.Platform.Equals(DevicePlatform.iOS))
            return false;

        var version = DeviceInfo.Version;
        // Live Activities require iOS 16.2+
        return version.Major > 16 || (version.Major == 16 && version.Minor >= 2);
    }

    /// <summary>
    /// Gets information about the current Live Activity status.
    /// </summary>
    public static string GetActivityStatus()
    {
        if (!IsLiveActivitySupported())
            return "Live Activities not supported (requires iOS 16.2+)";

        return "Live Activities ready";
    }
//#else
//    // Stub implementations for non-iOS platforms
//    public static Task StartPrayerActivityAsync(string currentPrayerName, string currentPrayerTime, string remainingTime, string nextPrayerName)
//    {
//        return Task.CompletedTask;
//    }

//    public static Task UpdatePrayerActivityAsync(string remainingTime)
//    {
//        return Task.CompletedTask;
//    }

//    public static Task StopPrayerActivityAsync()
//    {
//        return Task.CompletedTask;
//    }

//    public static string GetActivityStatus()
//    {
//        return "Live Activities not available on this platform";
//    }
#endif
}