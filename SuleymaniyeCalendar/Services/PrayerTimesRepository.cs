using System.Collections.ObjectModel;
using System.Diagnostics;
using SuleymaniyeCalendar.Helpers;
using SuleymaniyeCalendar.Models;
using Calendar = SuleymaniyeCalendar.Models.Calendar;

namespace SuleymaniyeCalendar.Services;

public class PrayerTimesRepository
{
    private readonly JsonApiService _jsonApiService;
    private readonly XmlApiService _xmlApiService;
    private readonly PrayerCacheService _cacheService;
    private readonly PerformanceService _perf;

    public PrayerTimesRepository(
        JsonApiService jsonApiService,
        XmlApiService xmlApiService,
        PrayerCacheService cacheService,
        PerformanceService perf)
    {
        _jsonApiService = jsonApiService;
        _xmlApiService = xmlApiService;
        _cacheService = cacheService;
        _perf = perf;
    }

    public Task<ObservableCollection<Calendar>> GetMonthlyFromCacheOrEmptyAsync(Location location)
    {
        return _cacheService.GetMonthlyFromCacheOrEmptyAsync(location);
    }

    public Task<ObservableCollection<Calendar>?> GetMonthFromCacheAsync(Location location, int year, int month)
    {
        return _cacheService.TryGetMonthlyFromCacheAsync(location, year, month);
    }

    public async Task<ObservableCollection<Calendar>?> GetMonthlyPrayerTimesHybridAsync(Location location, int? month = null, int? year = null, bool forceRefresh = false)
    {
        var targetMonth = month ?? DateTime.Now.Month;
        var targetYear = year ?? DateTime.Now.Year;

        Debug.WriteLine($"Starting Hybrid Monthly Prayer Times request for {targetMonth}/{targetYear}");

        // Try unified cache first if not forcing refresh
        if (!forceRefresh)
        {
            ObservableCollection<Calendar>? unified;
            using (_perf.StartTimer("Cache.TryGetMonthlyUnified"))
            {
                unified = await _cacheService.TryGetMonthlyFromCacheAsync(location, targetYear, targetMonth).ConfigureAwait(false);
            }
            if (unified != null)
            {
                Debug.WriteLine("Hybrid: Returning unified cache data");
                return unified;
            }
        }

        if (!HaveInternet()) return null;

        var altitude = location.Altitude ?? 0;

        // Strategy 1: Try new JSON API first
        Debug.WriteLine("Hybrid: Trying new JSON API");
        try
        {
            ObservableCollection<Calendar>? jsonResult;
            using (_perf.StartTimer("JSON.Monthly"))
            {
                jsonResult = await _jsonApiService.GetMonthlyPrayerTimesAsync(
                    location.Latitude, location.Longitude, targetMonth, altitude, targetYear);
            }
            
            if (jsonResult != null && jsonResult.Count > 0)
            {
                Debug.WriteLine($"Hybrid: JSON API success - {jsonResult.Count} days");
                await _cacheService.SaveToUnifiedCacheAsync(location, jsonResult.ToList()).ConfigureAwait(false);
                return jsonResult;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Hybrid: JSON API failed - {ex.Message}");
        }

        // Strategy 2: Fallback to old XML API (async)
        Debug.WriteLine("Hybrid: Falling back to XML API");
        try
        {
            ObservableCollection<Calendar>? xmlResult;
            using (_perf.StartTimer("XML.Monthly.Fallback"))
            {
                xmlResult = await GetMonthlyPrayerTimesXmlAsync(location, targetMonth, targetYear, forceRefresh).ConfigureAwait(false);
            }
            if (xmlResult != null && xmlResult.Count > 0)
            {
                Debug.WriteLine($"Hybrid: XML API success - {xmlResult.Count} days");
                return xmlResult;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Hybrid: XML API failed - {ex.Message}");
        }

        Debug.WriteLine("Hybrid: Both APIs failed");
        return null;
    }

    /// <summary>
    /// Gets today's prayer times. Delegates to GetDailyPrayerTimesHybridAsync which checks cache first.
    /// </summary>
    public Task<Calendar?> GetTodayPrayerTimesAsync(Location location)
        => GetDailyPrayerTimesHybridAsync(location, DateTime.Today);

    public async Task<Calendar?> GetDailyPrayerTimesHybridAsync(Location location, DateTime? date = null)
    {
        var targetDate = date ?? DateTime.Today;
        
        // Check cache first
        var cached = await _cacheService.TryGetDailyFromUnifiedCacheAsync(location, targetDate).ConfigureAwait(false);
        if (cached is not null) 
        {
            Debug.WriteLine("Hybrid Daily: Returning cached data");
            return cached;
        }

        if (!HaveInternet()) return null;

        var altitude = location.Altitude ?? 0;

        // Strategy 1: Try new JSON API first
        Debug.WriteLine("Hybrid Daily: Trying new JSON API");
        try
        {
            Calendar? jsonResult;
            using (_perf.StartTimer("JSON.Daily"))
            {
                jsonResult = await _jsonApiService.GetDailyPrayerTimesAsync(
                    location.Latitude, location.Longitude, targetDate, altitude);
            }
            
            if (jsonResult is not null)
            {
                Debug.WriteLine("Hybrid Daily: JSON API success");
                await _cacheService.SaveToUnifiedCacheAsync(location, new List<Calendar> { jsonResult }).ConfigureAwait(false);
                return jsonResult;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Hybrid Daily: JSON API failed - {ex.Message}");
        }

        // Strategy 2: Fallback to old XML API
        // Note: XML API only supports monthly fetch, so we fetch month and extract day
        Debug.WriteLine("Hybrid Daily: Falling back to XML API");
        try
        {
            var monthly = await GetMonthlyPrayerTimesXmlAsync(location, targetDate.Month, targetDate.Year, false).ConfigureAwait(false);
            if (monthly != null)
            {
                var day = monthly.FirstOrDefault(d => 
                {
                    // Use proper date parsing for reliable comparison
                    if (string.IsNullOrEmpty(d.Date)) return false;
                    var parsedDate = AppConstants.ParseCalendarDate(d.Date);
                    return parsedDate.Date == targetDate.Date;
                });
                
                if (day != null)
                {
                    Debug.WriteLine("Hybrid Daily: XML API success (extracted from monthly)");
                    return day;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Hybrid Daily: XML API failed - {ex.Message}");
        }

        return null;
    }

    private async Task<ObservableCollection<Calendar>?> GetMonthlyPrayerTimesXmlAsync(Location location, int month, int year, bool forceRefresh = false)
    {
        if (!forceRefresh)
        {
            var cached = await _cacheService.TryGetMonthlyFromCacheAsync(location, year, month).ConfigureAwait(false);
            if (cached != null) return cached;
        }

        if (!HaveInternet()) return null;

        try
        {
            var result = await _xmlApiService.GetMonthlyPrayerTimesAsync(
                location.Latitude,
                location.Longitude,
                location.Altitude ?? 0,
                month,
                year).ConfigureAwait(false);

            if (result != null && result.Count > 0)
            {
                await _cacheService.SaveToUnifiedCacheAsync(location, result.ToList()).ConfigureAwait(false);
            }
            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Async XML monthly failed: {ex.Message}");
            return null;
        }
    }

    public async Task<List<Calendar>> EnsureDaysRangeAsync(Location location, DateTime startDate, int daysNeeded)
    {
        Debug.WriteLine($"EnsureDaysRangeAsync: Ensuring {daysNeeded} days from {startDate:dd/MM/yyyy} for location {location.Latitude},{location.Longitude}");

        var result = new List<Calendar>(daysNeeded);
        var seen = new HashSet<DateTime>();

        // Load caches for years involved (and any additionally touched years as we iterate)
        var yearCaches = new Dictionary<int, List<Calendar>>();

        async Task<List<Calendar>> LoadYearAsync(int year)
        {
            if (yearCaches.TryGetValue(year, out var existing))
                return existing;

            List<Calendar>? cached;
            using (_perf.StartTimer($"Cache.LoadYear.{year}"))
            {
                cached = await _cacheService.LoadYearCacheAsync(location, year).ConfigureAwait(false);
            }

            var list = cached ?? new List<Calendar>();
            yearCaches[year] = list;
            return list;
        }

        static bool TryGetParsedDate(Calendar cal, out DateTime parsed)
        {
            parsed = AppConstants.ParseCalendarDate(cal.Date);
            return parsed != DateTime.MinValue;
        }

        static List<Calendar> DistinctByParsedDate(IEnumerable<Calendar> cals)
        {
            var map = new Dictionary<DateTime, Calendar>();
            foreach (var c in cals)
            {
                if (!TryGetParsedDate(c, out var d))
                    continue;

                // keep first occurrence
                if (!map.ContainsKey(d.Date))
                    map[d.Date] = c;
            }
            return map.OrderBy(k => k.Key).Select(k => k.Value).ToList();
        }

        async Task<List<Calendar>> GetMonthAsync(int year, int month)
        {
            var cachedYear = await LoadYearAsync(year).ConfigureAwait(false);

            // Try month from cache
            var monthFromCache = cachedYear
                .Where(d => TryGetParsedDate(d, out var dd) && dd.Year == year && dd.Month == month)
                .ToList();

            // If sufficiently complete, return
            if (monthFromCache.Count > 27)
                return DistinctByParsedDate(monthFromCache);

            // Fetch via JSON monthly endpoint
            ObservableCollection<Calendar>? fetched;
            using (_perf.StartTimer($"JSON.Monthly.{year}-{month}"))
            {
                fetched = await _jsonApiService.GetMonthlyPrayerTimesAsync(
                    location.Latitude, location.Longitude, month, location.Altitude ?? 0, year).ConfigureAwait(false);
            }

            // If monthly not available or failed, fetch missing days via daily endpoint for each date
            if (fetched == null || fetched.Count == 0)
            {
                var daysInMonth = DateTime.DaysInMonth(year, month);
                var list = new List<Calendar>(daysInMonth);
                for (var day = 1; day <= daysInMonth; day++)
                {
                    var date = new DateTime(year, month, day);
                    var daily = await _jsonApiService.GetDailyPrayerTimesAsync(
                        location.Latitude, location.Longitude, date, location.Altitude ?? 0).ConfigureAwait(false);
                    if (daily != null)
                        list.Add(daily);
                }

                fetched = new ObservableCollection<Calendar>(list);
            }

            // Merge into year cache + persist
            if (fetched != null && fetched.Count > 0)
            {
                var merged = new List<Calendar>(cachedYear);
                var existingDates = new HashSet<DateTime>(
                    cachedYear.Select(c => AppConstants.ParseCalendarDate(c.Date)).Where(d => d != DateTime.MinValue).Select(d => d.Date));

                foreach (var item in fetched)
                {
                    var d = AppConstants.ParseCalendarDate(item.Date);
                    if (d == DateTime.MinValue)
                        continue;

                    if (existingDates.Add(d.Date))
                        merged.Add(item);
                }

                yearCaches[year] = merged;

                using (_perf.StartTimer($"Cache.SaveYear.{year}"))
                {
                    await _cacheService.SaveYearCacheAsync(location, year, merged).ConfigureAwait(false);
                }

                var monthMerged = merged
                    .Where(d => TryGetParsedDate(d, out var dd) && dd.Year == year && dd.Month == month)
                    .ToList();

                return DistinctByParsedDate(monthMerged);
            }

            // Return whatever we have (even if incomplete)
            return DistinctByParsedDate(monthFromCache);
        }

        // Count-driven collection: keep fetching months until we have daysNeeded distinct days from startDate
        var cursor = new DateTime(startDate.Year, startDate.Month, 1);
        while (result.Count < daysNeeded)
        {
            var monthDays = await GetMonthAsync(cursor.Year, cursor.Month).ConfigureAwait(false);

            foreach (var cal in monthDays)
            {
                if (!TryGetParsedDate(cal, out var d))
                    continue;

                if (d.Date < startDate.Date)
                    continue;

                if (seen.Add(d.Date))
                {
                    result.Add(cal);
                    if (result.Count >= daysNeeded)
                        break;
                }
            }

            // Move to next month (handles year boundaries)
            cursor = cursor.AddMonths(1);

            // Safety: avoid infinite loop if API/caches are broken
            if (cursor > startDate.AddYears(2))
                break;
        }

        // Ensure stable ordering by parsed date
        result = result
            .Select(c => new { Cal = c, Date = AppConstants.ParseCalendarDate(c.Date) })
            .Where(x => x.Date != DateTime.MinValue)
            .OrderBy(x => x.Date)
            .Select(x => x.Cal)
            .Take(daysNeeded)
            .ToList();

        return result;
    }

    public async Task<bool> EnsureTodayInCacheAsync(Location location)
    {
        var today = DateTime.Now;
        var cached = await _cacheService.TryGetDailyFromUnifiedCacheAsync(location, today).ConfigureAwait(false);
        
        if (cached == null)
        {
            Debug.WriteLine("EnsureTodayInCacheAsync: Today not in cache, fetching...");
            await GetDailyPrayerTimesHybridAsync(location, today).ConfigureAwait(false);
            return true;
        }
        return false;
    }

    public bool HaveInternet()
    {
        return Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
    }

    private static DateTime ParseCalendarDateOrMin(string? dateStr) =>
        AppConstants.ParseCalendarDate(dateStr);
}
