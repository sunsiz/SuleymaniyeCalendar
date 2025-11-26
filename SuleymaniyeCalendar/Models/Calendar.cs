namespace SuleymaniyeCalendar.Models;

/// <summary>
/// Represents a single day's prayer times and location data.
/// This is the core data model used throughout the application.
/// </summary>
/// <remarks>
/// Prayer times are stored as string representations (HH:mm format) for display.
/// Coordinates use double precision for accurate calculations.
/// </remarks>
public sealed class Calendar
{
    /// <summary>Geographic latitude in decimal degrees.</summary>
    public double Latitude { get; set; }

    /// <summary>Geographic longitude in decimal degrees.</summary>
    public double Longitude { get; set; }

    /// <summary>Altitude above sea level in meters.</summary>
    public double Altitude { get; set; }

    /// <summary>UTC offset in hours (e.g., 3 for UTC+3).</summary>
    public double TimeZone { get; set; }

    /// <summary>Daylight saving time indicator (1 = active, 0 = inactive).</summary>
    public double DayLightSaving { get; set; }

    /// <summary>False dawn time (Fecr-i Kazib) - HH:mm format.</summary>
    public string FalseFajr { get; set; }

    /// <summary>True dawn time (Fecr-i Sadık / Imsak) - HH:mm format.</summary>
    public string Fajr { get; set; }

    /// <summary>Sunrise time (Güneş) - HH:mm format.</summary>
    public string Sunrise { get; set; }

    /// <summary>Noon prayer time (Öğle) - HH:mm format.</summary>
    public string Dhuhr { get; set; }

    /// <summary>Afternoon prayer time (İkindi) - HH:mm format.</summary>
    public string Asr { get; set; }

    /// <summary>Sunset prayer time (Akşam) - HH:mm format.</summary>
    public string Maghrib { get; set; }

    /// <summary>Night prayer time (Yatsı) - HH:mm format.</summary>
    public string Isha { get; set; }

    /// <summary>End of night prayer window (Yatsının Sonu) - HH:mm format.</summary>
    public string EndOfIsha { get; set; }

    /// <summary>Date in dd/MM/yyyy format.</summary>
    public string Date { get; set; }
}
