using SuleymaniyeCalendar.Models;

namespace SuleymaniyeCalendar.Services;

/// <summary>
/// Service for mapping prayer times to their corresponding animated weather icons.
/// Based on astronomical definitions of Islamic prayer times.
/// </summary>
public class PrayerIconService
{
    /// <summary>
    /// Gets the appropriate animated SVG icon path for a prayer based on its ID.
    /// </summary>
    /// <param name="prayerId">ID of the prayer (fajr, dhuhr, asr, maghrib, isha, etc.)</param>
    /// <returns>Path to the animated SVG icon in the Resources/Images folder</returns>
    public static string GetPrayerIconById(string prayerId)
    {
        return prayerId?.ToLower() switch
        {
            "falsefajr" => "overcastnight.svg",
            "fajr" => "overcast.svg",           // Dawn: thin red line on horizon
            "dhuhr" => "clearday.svg",                        // Midday: sun at highest point
            "asr" => "partlycloudyday.svg",                 // Afternoon: sun going west, long shadows
            "maghrib" => "sunset.svg",                       // Evening: sun setting
            "isha" => "overcastnight.svg",                     // Night: complete darkness with stars
            "sunrise" => "sunrise.svg",                                 // Sunrise time
            "endofisha" => "starrynight.svg",                          // End of night prayer
            _ => "clearday.svg"                                         // Default fallback
        };
    }

    /// <summary>
    /// Updates a Prayer object with the appropriate animated icon based on its ID.
    /// </summary>
    /// <param name="prayer">Prayer object to update</param>
    public static void AssignIconById(Prayer prayer)
    {
        if (prayer is null) return;
        
        prayer.IconPath = GetPrayerIconById(prayer.Id);
        
        // Don't set description as it's not translated
        prayer.Description = string.Empty;
    }

    /// <summary>
    /// Gets the appropriate animated SVG icon path for a prayer based on its astronomical characteristics.
    /// </summary>
    /// <param name="prayerName">Name of the prayer (Fajr, Dhuhr, Asr, Maghrib, Isha)</param>
    /// <returns>Path to the animated SVG icon in the Resources/Images folder</returns>
    public static string GetPrayerIcon(string prayerName)
    {
        return prayerName?.ToLower() switch
        {
            "fajr" or "imsak" => "sunrise.svg",           // Dawn: thin red line on horizon, transition from dark to light
            "dhuhr" or "zuhr" => "clearday.svg",          // Midday: sun at highest point, clear bright day
            "asr" or "ikindi" => "partlycloudyday.svg",   // Afternoon: sun going west, long shadows, partly cloudy atmosphere
            "maghrib" or "aksam" => "sunset.svg",         // Evening: sun setting, end of day light
            "isha" or "yatsi" => "starrynight.svg",       // Night: complete darkness with visible stars
            _ => "clearday.svg"                           // Default fallback
        };
    }

    /// <summary>
    /// Updates a Prayer object with the appropriate animated icon based on its name.
    /// </summary>
    /// <param name="prayer">Prayer object to update</param>
    public static void AssignIcon(Prayer prayer)
    {
        if (prayer is null) return;
        
        prayer.IconPath = GetPrayerIcon(prayer.Name);
        
        // Update the description with astronomical context
        prayer.Description = GetPrayerDescription(prayer.Name);
    }

    /// <summary>
    /// Gets the astronomical description for a prayer time.
    /// </summary>
    /// <param name="prayerName">Name of the prayer</param>
    /// <returns>Astronomical description of the prayer time</returns>
    private static string GetPrayerDescription(string prayerName)
    {
        return prayerName?.ToLower() switch
        {
            "fajr" or "imsak" => "Dawn prayer when a thin red line appears on the horizon",
            "dhuhr" or "zuhr" => "Midday prayer when the sun reaches its highest point",
            "asr" or "ikindi" => "Afternoon prayer when shadows equal object height plus midday shadow",
            "maghrib" or "aksam" => "Evening prayer when the sun completely sets",
            "isha" or "yatsi" => "Night prayer when complete darkness falls and stars are visible",
            _ => "Prayer time"
        };
    }

    /// <summary>
    /// Gets all available prayer icons.
    /// </summary>
    /// <returns>Dictionary mapping prayer names to icon paths</returns>
    public static Dictionary<string, string> GetAllPrayerIcons()
    {
        return new Dictionary<string, string>
        {
            { "fajr", "sunrise.svg" },
            { "dhuhr", "clearday.svg" },
            { "asr", "partlycloudyday.svg" },
            { "maghrib", "sunset.svg" },
            { "isha", "starrynight.svg" }
        };
    }
}
