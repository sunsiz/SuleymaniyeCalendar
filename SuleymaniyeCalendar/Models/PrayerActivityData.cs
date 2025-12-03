#if __IOS__
using Foundation;

namespace SuleymaniyeCalendar.Models;

/// <summary>
/// Live Activity data for iOS 16.2+ displaying current prayer and remaining time.
/// Shown on lock screen and Dynamic Island.
/// </summary>
[Register("PrayerActivityData")]
public class PrayerActivityData : NSObject
{
    [Export("prayerName")]
    public string PrayerName { get; set; } = string.Empty;

    [Export("remainingTime")]
    public string RemainingTime { get; set; } = string.Empty;

    [Export("prayerTime")]
    public string PrayerTime { get; set; } = string.Empty;

    [Export("nextPrayerName")]
    public string NextPrayerName { get; set; } = string.Empty;

    [Export("timestamp")]
    public NSDate Timestamp { get; set; }

    public PrayerActivityData()
    {
        Timestamp = NSDate.Now;
    }
}
#endif