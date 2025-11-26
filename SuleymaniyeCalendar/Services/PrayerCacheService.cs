#nullable enable

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using SuleymaniyeCalendar.Models;
using PrayerCalendar = SuleymaniyeCalendar.Models.Calendar;

namespace SuleymaniyeCalendar.Services;

/// <summary>
/// Manages prayer times caching with a unified year-based cache system.
/// Provides file I/O, cache invalidation, and cache merging functionality.
/// </summary>
/// <remarks>
/// Cache Strategy:
/// - Uses JSON files stored in LocalApplicationData folder
/// - File naming: prayercache_{lat}_{lon}_{year}.json
/// - Location-aware: automatically invalidates cache when user moves ~2km
/// - Year-based: one cache file per year per location for efficient storage
/// </remarks>
public class PrayerCacheService
{
    #region Constants

    /// <summary>
    /// Current cache format version. Increment when cache structure changes
    /// to trigger automatic cache refresh on app update.
    /// </summary>
    public const int CacheVersion = 2;

    /// <summary>
    /// Location change threshold in degrees (~2km) for cache invalidation.
    /// </summary>
    private const double LocationChangeThreshold = 0.02;

    /// <summary>
    /// Supported date formats for parsing PrayerCalendar dates.
    /// </summary>
    private static readonly string[] DateFormats = { "dd.MM.yyyy", "dd/MM/yyyy", "dd-MM-yyyy", "yyyy-MM-dd" };

    #endregion

    #region Private Fields

    private readonly PerformanceService _perf;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of PrayerCacheService.
    /// </summary>
    /// <param name="perf">Performance monitoring service.</param>
    public PrayerCacheService(PerformanceService? perf = null)
    {
        _perf = perf ?? new PerformanceService();
    }

    #endregion

    #region Public Methods - Cache Reading

    /// <summary>
    /// Loads cached prayer times for a specific year and location.
    /// </summary>
    /// <param name="location">User's location.</param>
    /// <param name="year">PrayerCalendar year to load.</param>
    /// <returns>List of cached PrayerCalendar days, or null if not cached.</returns>
    public async Task<List<PrayerCalendar>?> LoadYearCacheAsync(Location location, int year)
    {
        try
        {
            var path = GetYearCachePath(location, year);
            if (!File.Exists(path)) return null;

            string json;
            using (_perf.StartTimer($"Cache.Read.{year}"))
            {
                json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
            }

            var wrapper = JsonSerializer.Deserialize<YearCacheWrapper>(json);
            if (wrapper != null && wrapper.Version == CacheVersion && wrapper.Year == year)
            {
                return wrapper.Days ?? new List<PrayerCalendar>();
            }

            // Cache version mismatch - return null to trigger refresh
            Debug.WriteLine($"Cache version mismatch for year {year}. Expected {CacheVersion}, got {wrapper?.Version}");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"LoadYearCacheAsync failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Tries to retrieve a specific month's prayer data from the unified year cache.
    /// </summary>
    /// <param name="location">User's location.</param>
    /// <param name="year">Target year.</param>
    /// <param name="month">Target month (1-12).</param>
    /// <returns>Observable collection of PrayerCalendar days if cache has complete month data, null otherwise.</returns>
    public async Task<ObservableCollection<PrayerCalendar>?> TryGetMonthlyFromCacheAsync(
        Location location, 
        int year, 
        int month)
    {
        try
        {
            var list = await LoadYearCacheAsync(location, year).ConfigureAwait(false);
            if (list == null || list.Count == 0) return null;

            var monthDays = list
                .Where(d =>
                {
                    var dd = ParseCalendarDateOrMin(d.Date);
                    if (dd == DateTime.MinValue) return false;
                    return dd.Year == year && dd.Month == month;
                })
                .OrderBy(d => ParseCalendarDateOrMin(d.Date))
                .ToList();

            // Only return if we have a reasonably complete month (28+ days)
            if (monthDays.Count >= 28)
            {
                return new ObservableCollection<PrayerCalendar>(monthDays);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TryGetMonthlyFromCacheAsync failed: {ex.Message}");
        }
        return null;
    }

    /// <summary>
    /// Returns the current month's data from unified cache if present (even if incomplete).
    /// Returns an empty collection when no cache file or month fragment exists.
    /// Never hits network.
    /// </summary>
    /// <param name="location">User's location for cache lookup.</param>
    /// <returns>Observable collection of PrayerCalendar days (may be empty but never null).</returns>
    public async Task<ObservableCollection<PrayerCalendar>> GetMonthlyFromCacheOrEmptyAsync(Location location)
    {
        var year = DateTime.Now.Year;
        var month = DateTime.Now.Month;

        try
        {
            var list = await LoadYearCacheAsync(location, year).ConfigureAwait(false) ?? new List<PrayerCalendar>();
            
            var monthDays = list
                .Where(d =>
                {
                    var dd = ParseCalendarDateOrMin(d.Date);
                    return dd != DateTime.MinValue && dd.Year == year && dd.Month == month;
                })
                .OrderBy(d => ParseCalendarDateOrMin(d.Date))
                .ToList();

            return new ObservableCollection<PrayerCalendar>(monthDays);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GetMonthlyFromCacheOrEmptyAsync failed: {ex.Message}");
            return new ObservableCollection<PrayerCalendar>();
        }
    }

    /// <summary>
    /// Gets today's PrayerCalendar data from cache.
    /// </summary>
    /// <param name="location">User's location.</param>
    /// <returns>Today's PrayerCalendar data or null if not cached.</returns>
    public async Task<PrayerCalendar?> GetTodayFromCacheAsync(Location location)
    {
        try
        {
            if (location.Latitude == 0 && location.Longitude == 0)
                return null;

            var currentYear = DateTime.Today.Year;
            var cachedYearData = await LoadYearCacheAsync(location, currentYear).ConfigureAwait(false);

            if (cachedYearData?.Count > 0)
            {
                var todayData = cachedYearData.FirstOrDefault(d =>
                    ParseCalendarDateOrMin(d.Date) == DateTime.Today);

                if (todayData != null)
                {
                    // Ensure location data is present
                    todayData.Latitude = location.Latitude;
                    todayData.Longitude = location.Longitude;
                    todayData.Altitude = location.Altitude ?? 0;
                    return todayData;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GetTodayFromCacheAsync failed: {ex.Message}");
        }
        return null;
    }

    #endregion

    #region Public Methods - Cache Writing

    /// <summary>
    /// Saves prayer times for a specific year to the cache file.
    /// </summary>
    /// <param name="location">User's location.</param>
    /// <param name="year">PrayerCalendar year.</param>
    /// <param name="days">List of PrayerCalendar days to save.</param>
    public async Task SaveYearCacheAsync(Location location, int year, List<PrayerCalendar> days)
    {
        try
        {
            var wrapper = new YearCacheWrapper
            {
                Version = CacheVersion,
                Latitude = location.Latitude,
                Longitude = location.Longitude,
                Altitude = location.Altitude ?? 0,
                Year = year,
                Days = days
            };

            var json = JsonSerializer.Serialize(wrapper, new JsonSerializerOptions { WriteIndented = false });
            var path = GetYearCachePath(location, year);

            using (_perf.StartTimer($"Cache.Write.{year}"))
            {
                await File.WriteAllTextAsync(path, json).ConfigureAwait(false);
            }

            Debug.WriteLine($"Saved {days.Count} days to cache for year {year}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SaveYearCacheAsync failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Saves a list of PrayerCalendar days to the unified year cache.
    /// Groups days by year and merges with existing cache data.
    /// </summary>
    /// <param name="location">User's location for cache file naming.</param>
    /// <param name="days">List of PrayerCalendar days to save.</param>
    public async Task SaveToUnifiedCacheAsync(Location location, List<PrayerCalendar> days)
    {
        if (days == null || days.Count == 0) return;

        try
        {
            var groups = days
                .Where(d => ParseCalendarDateOrMin(d.Date) != DateTime.MinValue)
                .GroupBy(d => ParseCalendarDateOrMin(d.Date).Year);

            foreach (var g in groups)
            {
                var year = g.Key;
                var existing = await LoadYearCacheAsync(location, year).ConfigureAwait(false) ?? new List<PrayerCalendar>();
                var merged = MergeCalendars(existing, g.ToList());
                await SaveYearCacheAsync(location, year, merged).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SaveToUnifiedCacheAsync failed: {ex.Message}");
        }
    }

    #endregion

    #region Public Methods - Cache Maintenance

    /// <summary>
    /// Clears all year caches if user location has changed significantly (~2km).
    /// Prevents stale prayer times when user relocates.
    /// </summary>
    /// <param name="location">Current user location.</param>
    public void ClearCachesIfLocationChanged(Location location)
    {
        try
        {
            var lastLat = Preferences.Get("CacheLastLat", double.NaN);
            var lastLon = Preferences.Get("CacheLastLon", double.NaN);

            if (double.IsNaN(lastLat) || double.IsNaN(lastLon) ||
                Math.Abs(lastLat - location.Latitude) > LocationChangeThreshold ||
                Math.Abs(lastLon - location.Longitude) > LocationChangeThreshold)
            {
                var dir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var files = Directory.GetFiles(dir, "prayercache_*.json");

                foreach (var f in files)
                {
                    try { File.Delete(f); }
                    catch { /* ignore individual file deletion errors */ }
                }

                Debug.WriteLine($"Cleared {files.Length} cache files due to location change");

                Preferences.Set("CacheLastLat", location.Latitude);
                Preferences.Set("CacheLastLon", location.Longitude);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ClearCachesIfLocationChanged failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks if today's data exists in cache for given location.
    /// </summary>
    /// <param name="location">User's location.</param>
    /// <returns>True if today's data is cached.</returns>
    public async Task<bool> HasTodayInCacheAsync(Location location)
    {
        var today = await GetTodayFromCacheAsync(location).ConfigureAwait(false);
        return today != null;
    }

    #endregion

    #region Private Methods - Path and Parsing

    /// <summary>
    /// Gets the file path for a year's cache based on location and year.
    /// </summary>
    private static string GetYearCachePath(Location location, int year)
    {
        var lat = Math.Round(location.Latitude, 4).ToString(CultureInfo.InvariantCulture);
        var lon = Math.Round(location.Longitude, 4).ToString(CultureInfo.InvariantCulture);
        var file = $"prayercache_{lat}_{lon}_{year}.json";
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), file);
    }

    /// <summary>
    /// Parses PrayerCalendar date string to DateTime, returning DateTime.MinValue on failure.
    /// Uses invariant culture and multiple format patterns for robustness.
    /// </summary>
    public static DateTime ParseCalendarDateOrMin(string? dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString))
            return DateTime.MinValue;

        if (DateTime.TryParseExact(dateString, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return dt.Date;

        if (DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
            return dt.Date;

        return DateTime.MinValue;
    }

    #endregion

    #region Private Methods - Merge

    /// <summary>
    /// Merges new PrayerCalendar entries into existing list, preferring newer data for duplicates.
    /// </summary>
    /// <param name="existing">Existing cached PrayerCalendar entries.</param>
    /// <param name="incoming">New PrayerCalendar entries to merge.</param>
    /// <returns>Merged and sorted list of PrayerCalendar entries.</returns>
    private static List<PrayerCalendar> MergeCalendars(List<PrayerCalendar> existing, List<PrayerCalendar> incoming)
    {
        var map = new Dictionary<DateTime, PrayerCalendar>();

        // Add existing entries
        foreach (var cal in existing)
        {
            var key = ParseCalendarDateOrMin(cal.Date);
            if (key != DateTime.MinValue)
                map[key] = cal;
        }

        // Overwrite with incoming entries (prefer newer data)
        foreach (var cal in incoming)
        {
            var key = ParseCalendarDateOrMin(cal.Date);
            if (key != DateTime.MinValue)
                map[key] = cal;
        }

        return map.OrderBy(kv => kv.Key).Select(kv => kv.Value).ToList();
    }

    #endregion

    #region Nested Types

    /// <summary>
    /// Wrapper class for serializing year-based prayer cache to JSON.
    /// Contains location coordinates for cache validation.
    /// </summary>
    private sealed class YearCacheWrapper
    {
        /// <summary>Cache format version for backward compatibility.</summary>
        public int Version { get; set; }

        /// <summary>Latitude used when generating this cache.</summary>
        public double Latitude { get; set; }

        /// <summary>Longitude used when generating this cache.</summary>
        public double Longitude { get; set; }

        /// <summary>Altitude used when generating this cache.</summary>
        public double Altitude { get; set; }

        /// <summary>Year this cache covers.</summary>
        public int Year { get; set; }

        /// <summary>Collection of PrayerCalendar days for the year.</summary>
        public List<PrayerCalendar> Days { get; set; } = new();
    }

    #endregion
}
