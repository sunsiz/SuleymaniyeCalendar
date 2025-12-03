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

    public async Task<Calendar?> GetTodayPrayerTimesAsync(Location location)
    {
        var today = DateTime.Now;
        // Try cache first
        var cached = await _cacheService.TryGetDailyFromUnifiedCacheAsync(location, today).ConfigureAwait(false);
        if (cached != null) return cached;

        // Fallback to fetch
        return await GetDailyPrayerTimesHybridAsync(location, today).ConfigureAwait(false);
    }

    public async Task<Calendar?> GetDailyPrayerTimesHybridAsync(Location location, DateTime? date = null)
    {
        if (!HaveInternet()) return null;

        var targetDate = date ?? DateTime.Today;
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
            
            if (jsonResult != null)
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
                    // Simple string match for now, assuming format consistency
                    // Ideally use parsed dates
                    return d.Date.StartsWith(targetDate.ToString("dd.MM.yyyy")) || 
                           d.Date.StartsWith(targetDate.ToString("dd/MM/yyyy"));
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
        
        // Invalidate yearly caches if location changed meaningfully
        // Note: This logic was in DataService, but PrayerCacheService handles cache keys based on location anyway.
        // We might need to expose ClearYearCachesIfLocationChanged in PrayerCacheService if it's not there.
        // For now, we assume PrayerCacheService handles it or we just load what we have.
        
        var result = new List<Calendar>();
        var endDate = startDate.AddDays(daysNeeded - 1);

        // Load caches for years involved
        var years = Enumerable.Range(startDate.Year, endDate.Year - startDate.Year + 1).ToArray();
        var yearCaches = new Dictionary<int, List<Calendar>>();
        foreach (var y in years)
        {
            List<Calendar>? cached;
            using (_perf.StartTimer($"Cache.LoadYear.{y}"))
            {
                cached = await _cacheService.LoadYearCacheAsync(location, y).ConfigureAwait(false);
            }
            yearCaches[y] = cached ?? new List<Calendar>();
        }

        // Helper to try get a whole month; fetch if missing.
        async Task<List<Calendar>> GetMonthAsync(int year, int month)
        {
            // Try from cache
            if (yearCaches.TryGetValue(year, out var cachedYear) && cachedYear.Count > 0)
            {
                var monthDays = cachedYear.Where(d => {
                    var dd = ParseCalendarDateOrMin(d.Date);
                    return dd != DateTime.MinValue && dd.Year == year && dd.Month == month;
                }).ToList();
                
                if (monthDays.Count > 27) return monthDays; // assume month is sufficiently complete
            }

            // Fetch via JSON monthly endpoint (now supports year parameter)
            ObservableCollection<Calendar>? fetched = await _jsonApiService.GetMonthlyPrayerTimesAsync(
                location.Latitude, location.Longitude, month, location.Altitude ?? 0, year).ConfigureAwait(false);

            // If monthly not available or failed, fetch missing days via daily endpoint for each date
            if (fetched == null || fetched.Count == 0)
            {
                var daysInMonth = DateTime.DaysInMonth(year, month);
                var list = new List<Calendar>(daysInMonth);
                for (int d = 1; d <= daysInMonth; d++)
                {
                    var date = new DateTime(year, month, d);
                    var day = await _jsonApiService.GetDailyPrayerTimesAsync(location.Latitude, location.Longitude, date, location.Altitude ?? 0).ConfigureAwait(false);
                    if (day != null) list.Add(day);
                }
                fetched = new ObservableCollection<Calendar>(list);
            }

            // Merge into cache and persist
            if (fetched != null && fetched.Count > 0)
            {
                var toAdd = fetched.ToList();
                if (!yearCaches.ContainsKey(year)) yearCaches[year] = new List<Calendar>();
                
                // We need a MergeCalendars helper. 
                // Since we don't have access to DataService's private methods, we'll implement a simple one here.
                var existing = yearCaches[year];
                var merged = new List<Calendar>(existing);
                foreach (var item in toAdd)
                {
                    if (!merged.Any(x => x.Date == item.Date))
                    {
                        merged.Add(item);
                    }
                }
                yearCaches[year] = merged;

                using (_perf.StartTimer($"Cache.SaveYear.{year}"))
                {
                    await _cacheService.SaveYearCacheAsync(location, year, yearCaches[year]).ConfigureAwait(false);
                }
                return toAdd;
            }

            return new List<Calendar>();
        }

        // Collect days covering the requested span
        var cursor = new DateTime(startDate.Year, startDate.Month, 1);
        while (cursor <= endDate)
        {
            List<Calendar> monthDays;
            using (_perf.StartTimer($"EnsureDays.GetMonth.{cursor.Year}-{cursor.Month}"))
            {
                monthDays = await GetMonthAsync(cursor.Year, cursor.Month).ConfigureAwait(false);
            }
            result.AddRange(monthDays);
            cursor = cursor.AddMonths(1);
        }

        // Filter to range and ensure order/distinct
        var inRange = result
            .Where(d =>
            {
                var dd = ParseCalendarDateOrMin(d.Date);
                return dd != DateTime.MinValue && dd >= startDate && dd <= endDate;
            })
            .GroupBy(d => d.Date) // Distinct by date string
            .Select(g => g.First())
            .OrderBy(d => ParseCalendarDateOrMin(d.Date))
            .ToList();

        return inRange;
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
