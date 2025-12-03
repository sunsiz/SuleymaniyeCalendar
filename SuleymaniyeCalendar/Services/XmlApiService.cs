#nullable enable

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Xml.Linq;
using SuleymaniyeCalendar.Models;
using PrayerCalendar = SuleymaniyeCalendar.Models.Calendar;

namespace SuleymaniyeCalendar.Services;

/// <summary>
/// Handles legacy XML API operations for the Suleymaniye prayer times service.
/// Provides XML-based API calls and parsing as fallback when JSON API is unavailable.
/// </summary>
/// <remarks>
/// API Endpoint: http://servis.suleymaniyetakvimi.com/servis.asmx
/// 
/// Available methods:
/// - VakitHesabi: Single day prayer times
/// - Aylik: Monthly prayer times
/// </remarks>
public class XmlApiService
{
    #region Constants

    private const string BaseUrl = "http://servis.suleymaniyetakvimi.com/servis.asmx";

    #endregion

    #region Private Fields

    private readonly HttpClient _httpClient;
    private readonly PerformanceService _perf;

    /// <summary>
    /// Default shared HTTP client for XML API requests.
    /// </summary>
    private static readonly HttpClient DefaultHttpClient = new() { Timeout = TimeSpan.FromSeconds(15) };

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of XmlApiService with default HTTP client.
    /// </summary>
    /// <param name="perf">Performance monitoring service (optional).</param>
    public XmlApiService(PerformanceService? perf = null)
        : this(DefaultHttpClient, perf)
    {
    }

    /// <summary>
    /// Initializes a new instance of XmlApiService.
    /// </summary>
    /// <param name="httpClient">HTTP client for API requests.</param>
    /// <param name="perf">Performance monitoring service.</param>
    public XmlApiService(HttpClient httpClient, PerformanceService? perf = null)
    {
        _httpClient = httpClient;
        _perf = perf ?? new PerformanceService();
    }

    #endregion

    #region Public API Methods

    /// <summary>
    /// Gets daily prayer times from the XML API.
    /// </summary>
    /// <param name="latitude">Location latitude.</param>
    /// <param name="longitude">Location longitude.</param>
    /// <param name="altitude">Location altitude in meters.</param>
    /// <param name="date">Target date (defaults to today).</param>
    /// <returns>Calendar with prayer times or null if failed.</returns>
    public async Task<PrayerCalendar?> GetDailyPrayerTimesAsync(
        double latitude,
        double longitude,
        double altitude,
        DateTime? date = null)
    {
        try
        {
            var targetDate = date ?? DateTime.Today;
            var uri = BuildDailyUrl(latitude, longitude, altitude, targetDate);

            string xmlResult;
            using (_perf.StartTimer("XML.Daily.Fetch"))
            {
                var response = await _httpClient.GetAsync(uri).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                xmlResult = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }

            if (string.IsNullOrEmpty(xmlResult) || !xmlResult.StartsWith("<?xml"))
            {
                Debug.WriteLine("XmlApiService: Invalid XML response");
                return null;
            }

            using (_perf.StartTimer("XML.Daily.Parse"))
            {
                return ParseDailyXml(xmlResult);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"XmlApiService.GetDailyPrayerTimesAsync failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gets monthly prayer times from the XML API.
    /// </summary>
    /// <param name="latitude">Location latitude.</param>
    /// <param name="longitude">Location longitude.</param>
    /// <param name="altitude">Location altitude in meters.</param>
    /// <param name="month">Target month (1-12).</param>
    /// <param name="year">Target year.</param>
    /// <returns>Collection of calendar days or null if failed.</returns>
    public async Task<ObservableCollection<PrayerCalendar>?> GetMonthlyPrayerTimesAsync(
        double latitude,
        double longitude,
        double altitude,
        int month,
        int year)
    {
        try
        {
            var uri = BuildMonthlyUrl(latitude, longitude, altitude, month, year);

            string xmlResult;
            using (_perf.StartTimer("XML.Monthly.Fetch"))
            {
                var response = await _httpClient.GetAsync(uri).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                xmlResult = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }

            if (string.IsNullOrEmpty(xmlResult) || !xmlResult.StartsWith("<?xml"))
            {
                Debug.WriteLine("XmlApiService: Invalid XML response");
                return null;
            }

            using (_perf.StartTimer("XML.Monthly.Parse"))
            {
                var doc = XDocument.Parse(xmlResult);
                return ParseMonthlyXml(doc, latitude, longitude, altitude);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"XmlApiService.GetMonthlyPrayerTimesAsync failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Tests connectivity to the XML API.
    /// </summary>
    /// <returns>True if API is reachable.</returns>
    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync(BaseUrl).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region URL Builders

    /// <summary>
    /// Builds the URL for daily prayer times API call.
    /// </summary>
    private static Uri BuildDailyUrl(double latitude, double longitude, double altitude, DateTime date)
    {
        var timeZone = TimeZoneInfo.Local.BaseUtcOffset.Hours;
        var daylightSaving = TimeZoneInfo.Local.IsDaylightSavingTime(date) ? 1 : 0;
        var dateStr = date.ToString("dd/MM/yyyy");

        // Use InvariantCulture to ensure decimal points (not commas) in coordinates
        var url = $"{BaseUrl}/VakitHesabi?" +
            $"Enlem={latitude.ToString(CultureInfo.InvariantCulture)}" +
            $"&Boylam={longitude.ToString(CultureInfo.InvariantCulture)}" +
            $"&Yukseklik={altitude.ToString(CultureInfo.InvariantCulture)}" +
            $"&SaatBolgesi={timeZone}" +
            $"&yazSaati={daylightSaving}" +
            $"&Tarih={dateStr}";

        return new Uri(url);
    }

    /// <summary>
    /// Builds the URL for monthly prayer times API call.
    /// </summary>
    private static Uri BuildMonthlyUrl(double latitude, double longitude, double altitude, int month, int year)
    {
        var timeZone = TimeZoneInfo.Local.BaseUtcOffset.Hours;
        // Use first day of month for daylight saving calculation
        var firstOfMonth = new DateTime(year, month, 1);
        var daylightSaving = TimeZoneInfo.Local.IsDaylightSavingTime(firstOfMonth) ? 1 : 0;

        var url = $"{BaseUrl}/Aylik?" +
            $"Enlem={latitude.ToString(CultureInfo.InvariantCulture)}" +
            $"&Boylam={longitude.ToString(CultureInfo.InvariantCulture)}" +
            $"&Yukseklik={altitude.ToString(CultureInfo.InvariantCulture)}" +
            $"&SaatBolgesi={timeZone}" +
            $"&yazSaati={daylightSaving}" +
            $"&Ay={month}" +
            $"&Yil={year}";

        return new Uri(url);
    }

    #endregion

    #region XML Parsing

    /// <summary>
    /// Parses daily prayer times from XML response.
    /// </summary>
    /// <param name="xmlResult">Raw XML string from API.</param>
    /// <returns>Parsed calendar or null if parsing fails.</returns>
    private static PrayerCalendar? ParseDailyXml(string xmlResult)
    {
        try
        {
            var calendar = new PrayerCalendar();
            var doc = XDocument.Parse(xmlResult);
            
            if (doc.Root == null) return null;

            foreach (var item in doc.Root.Descendants())
            {
                switch (item.Name.LocalName)
                {
                    case "Enlem":
                        calendar.Latitude = ParseDouble(item.Value);
                        break;
                    case "Boylam":
                        calendar.Longitude = ParseDouble(item.Value);
                        break;
                    case "Yukseklik":
                        calendar.Altitude = ParseDouble(item.Value);
                        break;
                    case "SaatBolgesi":
                        calendar.TimeZone = ParseDouble(item.Value);
                        break;
                    case "YazKis":
                        calendar.DayLightSaving = ParseDouble(item.Value);
                        break;
                    case "FecriKazip":
                        calendar.FalseFajr = item.Value;
                        break;
                    case "FecriSadik":
                        calendar.Fajr = item.Value;
                        break;
                    case "SabahSonu":
                        calendar.Sunrise = item.Value;
                        break;
                    case "Ogle":
                        calendar.Dhuhr = item.Value;
                        break;
                    case "Ikindi":
                        calendar.Asr = item.Value;
                        break;
                    case "Aksam":
                        calendar.Maghrib = item.Value;
                        break;
                    case "Yatsi":
                        calendar.Isha = item.Value;
                        break;
                    case "YatsiSonu":
                        calendar.EndOfIsha = item.Value;
                        break;
                }
            }

            return calendar;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ParseDailyXml failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Parses monthly prayer times from XML document.
    /// </summary>
    /// <param name="doc">Parsed XML document.</param>
    /// <param name="latitude">Fallback latitude if not in XML.</param>
    /// <param name="longitude">Fallback longitude if not in XML.</param>
    /// <param name="altitude">Fallback altitude if not in XML.</param>
    /// <returns>Collection of calendar days.</returns>
    private static ObservableCollection<PrayerCalendar> ParseMonthlyXml(
        XDocument doc, 
        double latitude = 0.0, 
        double longitude = 0.0, 
        double altitude = 0.0)
    {
        var monthlyCalendar = new ObservableCollection<PrayerCalendar>();
        
        if (doc.Root == null) return monthlyCalendar;

        foreach (var item in doc.Root.Descendants())
        {
            if (item.Name.LocalName == "TakvimListesi")
            {
                var calendarItem = new PrayerCalendar();
                
                foreach (var subitem in item.Descendants())
                {
                    switch (subitem.Name.LocalName)
                    {
                        case "Tarih":
                            calendarItem.Date = subitem.Value;
                            break;
                        case "Enlem":
                            calendarItem.Latitude = ParseDouble(subitem.Value);
                            break;
                        case "Boylam":
                            calendarItem.Longitude = ParseDouble(subitem.Value);
                            break;
                        case "Yukseklik":
                            calendarItem.Altitude = ParseDouble(subitem.Value);
                            break;
                        case "SaatBolgesi":
                            calendarItem.TimeZone = ParseDouble(subitem.Value);
                            break;
                        case "YazKis":
                            calendarItem.DayLightSaving = ParseDouble(subitem.Value);
                            break;
                        case "FecriKazip":
                            calendarItem.FalseFajr = subitem.Value;
                            break;
                        case "FecriSadik":
                            calendarItem.Fajr = subitem.Value;
                            break;
                        case "SabahSonu":
                            calendarItem.Sunrise = subitem.Value;
                            break;
                        case "Ogle":
                            calendarItem.Dhuhr = subitem.Value;
                            break;
                        case "Ikindi":
                            calendarItem.Asr = subitem.Value;
                            break;
                        case "Aksam":
                            calendarItem.Maghrib = subitem.Value;
                            break;
                        case "Yatsi":
                            calendarItem.Isha = subitem.Value;
                            break;
                        case "YatsiSonu":
                            calendarItem.EndOfIsha = subitem.Value;
                            break;
                    }
                }

                // Apply fallback coordinates if not present in XML
                if (calendarItem.Latitude == 0) calendarItem.Latitude = latitude;
                if (calendarItem.Longitude == 0) calendarItem.Longitude = longitude;
                if (calendarItem.Altitude == 0) calendarItem.Altitude = altitude;
                
                monthlyCalendar.Add(calendarItem);
            }
        }

        return monthlyCalendar;
    }

    /// <summary>
    /// Safely parses a double value using InvariantCulture.
    /// Prevents locale-specific decimal separator issues.
    /// </summary>
    private static double ParseDouble(string value)
    {
        return Convert.ToDouble(value, CultureInfo.InvariantCulture.NumberFormat);
    }

    #endregion
}
