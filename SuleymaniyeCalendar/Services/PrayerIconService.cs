using SuleymaniyeCalendar.Models;

namespace SuleymaniyeCalendar.Services;

/// <summary>
/// Maps prayer times to weather-themed icons based on astronomical conditions.
/// Icons represent the sky condition at each prayer time.
/// </summary>
public static class PrayerIconService
{
    /// <summary>
    /// Icon mappings for prayer IDs (internal identifiers).
    /// </summary>
    private static readonly Dictionary<string, string> IconsByPrayerId = new()
    {
        ["falsefajr"] = "overcastnight",     // Before dawn - dark sky
        ["fajr"] = "overcast",               // Dawn - thin light on horizon
        ["sunrise"] = "sunrise",             // Sun rising
        ["dhuhr"] = "clearday",              // Midday - sun at peak
        ["asr"] = "partlycloudyday",         // Afternoon - sun descending
        ["maghrib"] = "sunset",              // Sunset
        ["isha"] = "overcastnight",          // Night begins
        ["endofisha"] = "starrynight"        // Deep night - stars visible
    };

    /// <summary>
    /// Gets the appropriate animated icon name for a prayer ID.
    /// </summary>
    /// <param name="prayerId">Prayer identifier (e.g., "fajr", "dhuhr").</param>
    /// <returns>Icon name (e.g., "sunrise", "clearday").</returns>
    public static string GetPrayerIconById(string prayerId)
    {
        return IconsByPrayerId.GetValueOrDefault(prayerId?.ToLowerInvariant() ?? "", "clearday");
    }

    /// <summary>
    /// Assigns the icon path to a Prayer object based on its ID.
    /// </summary>
    public static void AssignIconById(Prayer prayer)
    {
        if (prayer is null) return;

        prayer.IconPath = GetPrayerIconById(prayer.Id);
        prayer.Description = string.Empty; // Description not used (untranslated)
    }

    /// <summary>
    /// Alias for <see cref="AssignIconById"/> - kept for backward compatibility.
    /// </summary>
    public static void AssignIcon(Prayer prayer) => AssignIconById(prayer);

    /// <summary>
    /// Gets all prayer icons as a dictionary (for debugging/testing).
    /// </summary>
    public static IReadOnlyDictionary<string, string> GetAllPrayerIcons() => IconsByPrayerId;
}
