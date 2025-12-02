#nullable enable

using System.Globalization;

namespace SuleymaniyeCalendar.Helpers;

/// <summary>
/// Centralized constants and utility methods used across the application.
/// Eliminates code duplication and provides a single source of truth.
/// </summary>
public static class AppConstants
{
    #region Date Formats

    /// <summary>
    /// Supported date formats for parsing Calendar dates from various API sources.
    /// Order matters: most specific formats first for better performance.
    /// </summary>
    public static readonly string[] SupportedDateFormats =
    [
        "dd.MM.yyyy",
        "dd/MM/yyyy",
        "dd-MM-yyyy",
        "yyyy-MM-dd"
    ];

    #endregion

    #region RTL Languages

    /// <summary>
    /// ISO 639-1 codes for right-to-left languages supported by this app.
    /// Used for UI layout direction and text rendering.
    /// </summary>
    public static readonly HashSet<string> RtlLanguageCodes =
    [
        "ar", // Arabic
        "fa", // Farsi (Persian)
        "ug"  // Uyghur
    ];

    /// <summary>
    /// Determines if a language code represents an RTL language.
    /// </summary>
    /// <param name="languageCode">Culture code (e.g., "ar", "ar-SA").</param>
    /// <returns>True if the language uses right-to-left text direction.</returns>
    public static bool IsRtlLanguage(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode)) return false;
        
        // Handle culture codes like "ar-SA" by extracting base language
        var baseCode = languageCode.Split('-')[0].ToLowerInvariant();
        return RtlLanguageCodes.Contains(baseCode);
    }

    #endregion

    #region Date Parsing

    /// <summary>
    /// Thread-safe date parsing from Calendar date strings.
    /// Uses InvariantCulture to avoid locale-specific parsing issues.
    /// </summary>
    /// <param name="dateString">Date string in supported format.</param>
    /// <returns>Parsed DateTime or DateTime.MinValue if parsing fails.</returns>
    public static DateTime ParseCalendarDate(string? dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString))
            return DateTime.MinValue;

        return DateTime.TryParseExact(
            dateString,
            SupportedDateFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var date) ? date : DateTime.MinValue;
    }

    /// <summary>
    /// Safe TimeSpan parsing with fallback to TimeSpan.Zero.
    /// </summary>
    /// <param name="timeString">Time string in HH:mm or HH:mm:ss format.</param>
    /// <returns>Parsed TimeSpan or TimeSpan.Zero if parsing fails.</returns>
    public static TimeSpan ParseTimeSpan(string? timeString)
    {
        if (string.IsNullOrWhiteSpace(timeString))
            return TimeSpan.Zero;

        return TimeSpan.TryParse(timeString, CultureInfo.InvariantCulture, out var time)
            ? time
            : TimeSpan.Zero;
    }

    #endregion

    #region Numeric Parsing

    /// <summary>
    /// Culture-safe double parsing using InvariantCulture.
    /// Prevents locale-specific decimal separator issues with coordinates.
    /// </summary>
    /// <param name="value">String representation of double value.</param>
    /// <returns>Parsed double or 0.0 if parsing fails.</returns>
    public static double ParseDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0.0;

        return double.TryParse(
            value,
            NumberStyles.Any,
            CultureInfo.InvariantCulture,
            out var result) ? result : 0.0;
    }

    #endregion

    #region Preference Keys

    /// <summary>
    /// Centralized preference key constants to avoid magic strings.
    /// </summary>
    public static class PreferenceKeys
    {
        // Location
        public const string LastLatitude = nameof(LastLatitude);
        public const string LastLongitude = nameof(LastLongitude);
        public const string LastAltitude = nameof(LastAltitude);
        public const string LocationSaved = nameof(LocationSaved);
        public const string City = "sehir";

        // Settings
        public const string FontSize = nameof(FontSize);
        public const string SelectedLanguage = nameof(SelectedLanguage);
        public const string ForegroundServiceEnabled = nameof(ForegroundServiceEnabled);
        public const string AlwaysRenewLocationEnabled = nameof(AlwaysRenewLocationEnabled);

        // Alarm scheduling
        public const string LastAlarmDate = nameof(LastAlarmDate);
        public const string LastAutoRescheduleUtc = nameof(LastAutoRescheduleUtc);

        // Prayer-specific suffix patterns
        public const string EnabledSuffix = "Enabled";
        public const string NotificationTimeSuffix = "NotificationTime";
        public const string AlarmSoundSuffix = "AlarmSound";
    }

    #endregion
}
