#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Text.Json;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using SuleymaniyeCalendar.Models;
using SuleymaniyeCalendar.Resources.Strings;
using Calendar = SuleymaniyeCalendar.Models.Calendar;

namespace SuleymaniyeCalendar.Services;

/// <summary>
/// Core data service for managing prayer times, location data, caching, and alarm scheduling.
/// Acts as the central hub for all data operations in the application.
/// </summary>
/// <remarks>
/// <para>Key responsibilities:</para>
/// <list type="bullet">
///   <item>Fetch prayer times from JSON API with XML fallback</item>
///   <item>Manage local caching with unified year-based cache system</item>
///   <item>Handle GPS location and permission requests</item>
///   <item>Schedule 30-day rolling alarm notifications</item>
///   <item>Coordinate between API services and UI layer</item>
/// </list>
/// </remarks>
public class DataService
{
    #region Constants

    /// <summary>
    /// Current version of the unified cache format for migration support.
    /// </summary>
    private const int UnifiedCacheVersion = 1;

    /// <summary>
    /// Preference key for storing the last alarm coverage date.
    /// </summary>
    private const string LastAlarmDatePreferenceKey = "LastAlarmDate";

    /// <summary>
    /// Preference key for storing the last auto-reschedule timestamp.
    /// </summary>
    private const string LastAutoReschedulePreferenceKey = "LastAutoRescheduleUtc";

    /// <summary>
    /// Number of days before alarm coverage expiry to trigger auto-reschedule.
    /// </summary>
    private static readonly TimeSpan AutoRescheduleWindow = TimeSpan.FromDays(3);

    /// <summary>
    /// Minimum time between automatic reschedule operations.
    /// </summary>
    private static readonly TimeSpan AutoRescheduleCooldown = TimeSpan.FromHours(6);

    /// <summary>
    /// Supported date formats for parsing calendar dates from various sources.
    /// </summary>
    private static readonly string[] KnownDateFormats =
    [
        "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy",
        "yyyy-MM-dd", "yyyy/MM/dd", "MM/dd/yyyy", "M/d/yyyy",
        "dd.MM.yyyy", "d.MM.yyyy"
    ];

    #endregion

    #region Private Fields

    /// <summary>
    /// Service for scheduling and managing prayer alarms.
    /// </summary>
    private readonly IAlarmService _alarmService;

    /// <summary>
    /// JSON API service for fetching prayer times from the new API.
    /// </summary>
    private readonly JsonApiService _jsonApiService;

    /// <summary>
    /// XML API service for fetching prayer times from the legacy API.
    /// </summary>
    private readonly XmlApiService _xmlApiService;

    /// <summary>
    /// Cache service for managing prayer times cache.
    /// </summary>
    private readonly PrayerCacheService _cacheService;

    /// <summary>
    /// Performance monitoring service for timing operations.
    /// </summary>
    private readonly PerformanceService _perf;

    /// <summary>
    /// Cached location data for the current user.
    /// </summary>
    private Calendar? _location;

    /// <summary>
    /// Cached monthly prayer times collection.
    /// </summary>
    private ObservableCollection<Calendar>? _monthlyCalendar;

    /// <summary>
    /// Shared HTTP client for XML API requests with 15-second timeout.
    /// </summary>
    private static readonly HttpClient _xmlHttpClient = new() { Timeout = TimeSpan.FromSeconds(15) };

    #endregion

    #region Public Fields

    /// <summary>
    /// Current day's prayer calendar data. Shared with ViewModels for UI binding.
    /// </summary>
    public Calendar calendar;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="DataService"/> class with full dependency injection.
    /// </summary>
    /// <param name="alarmService">Service for scheduling prayer alarms.</param>
    /// <param name="jsonApiService">JSON API service for fetching prayer times.</param>
    /// <param name="xmlApiService">XML API service for legacy fallback.</param>
    /// <param name="cacheService">Cache service for managing prayer data persistence.</param>
    /// <param name="perf">Optional performance monitoring service.</param>
    public DataService(
        IAlarmService alarmService, 
        JsonApiService jsonApiService,
        XmlApiService xmlApiService,
        PrayerCacheService cacheService,
        PerformanceService? perf = null)
    {
        _alarmService = alarmService;
        _jsonApiService = jsonApiService;
        _xmlApiService = xmlApiService;
        _cacheService = cacheService;
        _perf = perf ?? new PerformanceService();
        calendar = GetTakvimFromFile() ?? new Calendar
        {
            Latitude = Preferences.Get("latitude", 41.0),
            Longitude = Preferences.Get("longitude", 29.0),
            Altitude = Preferences.Get("altitude", 114.0),
            TimeZone = Preferences.Get("timezone", 3.0),
            DayLightSaving = Preferences.Get("daylightsaving", 0.0),
            FalseFajr = Preferences.Get("falsefajr", "06:28"),
            Fajr = Preferences.Get("fajr", "07:16"),
            Sunrise = Preferences.Get("sunrise", "08:00"),
            Dhuhr = Preferences.Get("dhuhr", "12:59"),
            Asr = Preferences.Get("asr", "15:27"),
            Maghrib = Preferences.Get("maghrib", "17:54"),
            Isha = Preferences.Get("isha", "18:41"),
            EndOfIsha = Preferences.Get("endofisha", "19:31"),
            Date = Preferences.Get("date", DateTime.Today.ToString("dd/MM/yyyy"))
        };
    }

    /// <summary>
    /// Initializes a new instance with only alarm service (backward compatibility for widgets).
    /// Creates default services internally.
    /// </summary>
    /// <param name="alarmService">Service for scheduling prayer alarms.</param>
    public DataService(IAlarmService alarmService) 
        : this(alarmService, new JsonApiService(), new XmlApiService(), new PrayerCacheService())
    {
    }

    #endregion

    #region Date Parsing Utilities

    /// <summary>
    /// Attempts to parse a calendar date string using multiple known formats.
    /// </summary>
    /// <param name="input">The date string to parse.</param>
    /// <param name="date">The parsed date if successful.</param>
    /// <returns>True if parsing succeeded; otherwise false.</returns>
    private static bool TryParseCalendarDate(string? input, out DateTime date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(input)) return false;
        var s = input.Trim();
        return DateTime.TryParseExact(s, KnownDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
            || DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    /// <summary>
    /// Parses a calendar date string, returning <see cref="DateTime.MinValue"/> on failure.
    /// </summary>
    /// <param name="input">The date string to parse.</param>
    /// <returns>Parsed date or MinValue if parsing fails.</returns>
    private static DateTime ParseCalendarDateOrMin(string? input)
    {
        return TryParseCalendarDate(input, out var dt) ? dt : DateTime.MinValue;
    }

    #endregion

    #region Alarm Coverage Management

    /// <summary>
    /// Persists the most recent coverage day along with the timestamp of the scheduling run.
    /// </summary>
    /// <param name="coverageThrough">The date through which alarms are scheduled.</param>
    private static void PersistAlarmCoverage(DateTime coverageThrough)
    {
        Preferences.Set(LastAlarmDatePreferenceKey, coverageThrough.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Preferences.Set(LastAutoReschedulePreferenceKey, DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Removes cached coverage metadata when alarms are disabled.
    /// </summary>
    private static void ClearAlarmCoverage()
    {
        Preferences.Remove(LastAlarmDatePreferenceKey);
        Preferences.Remove(LastAutoReschedulePreferenceKey);
    }

    /// <summary>
    /// Determines whether an automatic reschedule should be throttled based on remaining coverage or cooldowns.
    /// </summary>
    /// <param name="forceReschedule">If true, bypasses throttling.</param>
    /// <returns>True if rescheduling should be skipped; otherwise false.</returns>
    private bool ShouldThrottleAutoReschedule(bool forceReschedule)
    {
        if (forceReschedule)
        {
            return false;
        }

        var coverageRaw = Preferences.Get(LastAlarmDatePreferenceKey, string.Empty);
        if (TryParseCalendarDate(coverageRaw, out var coverageDate))
        {
            var daysRemaining = (coverageDate - DateTime.Today).TotalDays;
            if (daysRemaining > AutoRescheduleWindow.TotalDays)
				{
					Debug.WriteLine($"[DataService] Skipping auto reschedule; {daysRemaining:F1} days remain.");
					return true;
				}
			}

			var lastAutoRaw = Preferences.Get(LastAutoReschedulePreferenceKey, string.Empty);
			if (!string.IsNullOrWhiteSpace(lastAutoRaw)
				&& DateTime.TryParse(lastAutoRaw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var lastAutoUtc))
			{
				var sinceLast = DateTime.UtcNow - lastAutoUtc;
				if (sinceLast < AutoRescheduleCooldown)
				{
					Debug.WriteLine($"[DataService] Skipping auto reschedule; last run was {sinceLast.TotalHours:F1}h ago.");
					return true;
				}
        }

        return false;
    }

    #endregion

    #region Cache and File Methods

    /// <summary>
    /// Loads today's prayer times from the unified cache file.
    /// Used for initial synchronous loading during app startup.
    /// </summary>
    /// <returns>Today's calendar data or null if not cached.</returns>
    private Calendar? GetTakvimFromFile()
    {
        try
        {
            var location = new Location
            {
                Latitude = Preferences.Get("LastLatitude", 0.0),
                Longitude = Preferences.Get("LastLongitude", 0.0),
                Altitude = Preferences.Get("LastAltitude", 0.0)
            };

            if (location.Latitude != 0.0 && location.Longitude != 0.0)
            {
                // Delegate to PrayerCacheService for cache lookup
                var todayData = Task.Run(() => _cacheService.GetTodayFromCacheAsync(location)).Result;
                return todayData;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GetTakvimFromFile cache error: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Prepares monthly prayer times on app startup.
    /// Respects user's auto-renew location preference.
    /// </summary>
    /// <returns>Today's calendar data.</returns>
    public async Task<Calendar?> PrepareMonthlyPrayerTimes()
		{
			// On app startup, respect the user's auto-renew preference:
			// - When AlwaysRenewLocationEnabled is ON, force a fresh GPS fix to renew location
			// - Otherwise, use last known/saved location to avoid prompting at launch
			var forceLocationRefresh = Preferences.Get("AlwaysRenewLocationEnabled", false);
			var location = await GetCurrentLocationAsync(forceLocationRefresh).ConfigureAwait(false);
			ObservableCollection<Calendar> monthly;
			using (_perf.StartTimer("DataService.GetMonthly.HybridStartup"))
			{
				monthly = await GetMonthlyPrayerTimesHybridAsync(location, true).ConfigureAwait(false);
			}
			if (monthly != null && monthly.Count > 0)
			{
				// Pick today's entry; fallback to closest
				var today = monthly.FirstOrDefault(d => ParseCalendarDateOrMin(d.Date) == DateTime.Today)
						   ?? monthly.OrderBy(d => Math.Abs((ParseCalendarDateOrMin(d.Date) - DateTime.Today).Days)).First();
				calendar = today;
			}
			else
			{
				calendar = GetTakvimFromFile();
	        }
        return calendar;
    }

    #endregion

    #region Location Methods

    /// <summary>
    /// Gets the current device location, with caching and permission handling.
    /// </summary>
    /// <param name="refreshLocation">If true, requests fresh GPS fix; otherwise uses cached location.</param>
    /// <returns>Current location or default fallback coordinates.</returns>
    /// <remarks>
    /// On Windows (WinUI), bypasses runtime geolocation to prevent crashes.
    /// Falls back through: GPS → Last known → Preferences → Default (Istanbul).
    /// </remarks>
    public async Task<Location?> GetCurrentLocationAsync(bool refreshLocation)
		{
			var location = new Location(0.0, 0.0);
			// WINDOWS SAFEGUARD: Some Windows environments (missing capability or OS-level location disabled)
			// have been observed to terminate the process (exit code 0xC000027B) when invoking Geolocation APIs.
			// To prevent crash-at-start, we completely bypass runtime geolocation on WinUI and rely on the
			// last persisted coordinates (if any). User can still refresh on mobile platforms.
			if (DeviceInfo.Platform == DevicePlatform.WinUI)
			{
				var lat = Preferences.Get("LastLatitude", 0.0);
				var lng = Preferences.Get("LastLongitude", 0.0);
				var alt = Preferences.Get("LastAltitude", 0.0);
				if (lat != 0.0 || lng != 0.0)
				{
					return new Location(lat, lng, alt);
				}
				// Fallback default (Istanbul) – mirrors initial placeholder values used elsewhere.
				return new Location(41.0, 29.0, 114.0);
			}
			// If we have a saved location and no explicit refresh is requested, use it without requesting permissions
			if (!refreshLocation && Preferences.Get("LocationSaved", false))
			{
				var lat = Preferences.Get("LastLatitude", 0.0);
				var lng = Preferences.Get("LastLongitude", 0.0);
				var alt = Preferences.Get("LastAltitude", 0.0);
				if (lat != 0.0 || lng != 0.0)
				{
					return new Location(lat, lng, alt);
				}
			}
			// Always ensure permission when explicitly refreshing or when not granted
			{
				var status = PermissionStatus.Unknown;
				if (DeviceInfo.Platform == DevicePlatform.WinUI)
				{
					status = PermissionStatus.Granted;
				}
				else
				{
					status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
					if (refreshLocation || status != PermissionStatus.Granted)
					{
						status = await CheckAndRequestLocationPermission().ConfigureAwait(false);
					}
				}

				if (status != PermissionStatus.Granted)
				{
					// Permission not granted; return default (0,0) and let callers show a permission message
					return location;
				}
			}
			try
			{
				if (!refreshLocation)
				{
					using (_perf.StartTimer("Location.LastKnown"))
					{
						location = await Geolocation.Default.GetLastKnownLocationAsync().ConfigureAwait(false);
					}
				}

				if (location == null || refreshLocation)
				{
					using (_perf.StartTimer("Location.Request"))
					{
						// Shorten initial wait to improve perceived performance; fallback path inside will extend slightly if needed.
						location = await RequestLocationAsync(waitDelay: 5).ConfigureAwait(false);
					}

					// Fallbacks when a fresh fix isn't available quickly
					if (location is null)
					{
						using (_perf.StartTimer("Location.Fallback.LastKnown"))
						{
							var lastKnown = await Geolocation.Default.GetLastKnownLocationAsync().ConfigureAwait(false);
							if (lastKnown is not null)
								location = lastKnown;
						}

						// Try stored preferences if last-known is unavailable
						if (location is null)
						{
							var lat = Preferences.Get("LastLatitude", 0.0);
							var lng = Preferences.Get("LastLongitude", 0.0);
							var alt = Preferences.Get("LastAltitude", 0.0);
							if (lat != 0.0 || lng != 0.0)
							{
								location = new Location(lat, lng, alt);
							}
						}

						// Absolute last resort: keep existing default
						location ??= new Location(42.142, 29.218, 10);
					}
				}

				if (location != null && location.Latitude != 0 && location.Longitude != 0)
				{
					Debug.WriteLine(
						$"Latitude: {location.Latitude}, Longitude: {location.Longitude}, Altitude: {location.Altitude}");
					Preferences.Set("LastLatitude", location.Latitude);
					Preferences.Set("LastLongitude", location.Longitude);
					Preferences.Set("LastAltitude", location.Altitude ?? 0);
					Preferences.Set("LocationSaved", true);
				}
			}
			catch (FeatureNotSupportedException fnsEx)
			{
				Debug.WriteLine($"Location not supported: {fnsEx.Message}");
			}
			catch (FeatureNotEnabledException fneEx)
			{
				Debug.WriteLine($"Location not enabled: {fneEx.Message}");
				// Fallback to last known location when GPS is disabled
				if (Preferences.Get("LastLatitude", 0.0) != 0.0 && Preferences.Get("LastLongitude", 0.0) != 0.0)
				{
					location ??= new Location();
					location.Latitude = Preferences.Get("LastLatitude", 0.0);
					location.Longitude = Preferences.Get("LastLongitude", 0.0);
					location.Altitude = Preferences.Get("LastAltitude", 0.0);
				}
			}
			catch (PermissionException pEx)
			{
				Debug.WriteLine($"Location permission denied: {pEx.Message}");
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Location error: {ex.Message}");
			}

        return location;
    }

    #endregion

    #region Monthly Prayer Times

    /// <summary>
    /// Gets monthly prayer times synchronously using unified cache or XML API.
    /// Legacy wrapper for backward compatibility - prefer async version.
    /// </summary>
    /// <param name="location">User's current location.</param>
    /// <param name="forceRefresh">If true, bypasses cache and fetches fresh data.</param>
    /// <returns>Collection of calendar days for the month, or null if unavailable.</returns>
    public ObservableCollection<Calendar>? GetMonthlyPrayerTimes(Location? location, bool forceRefresh = false)
    {
        if (location == null) return null;
        
        // Use unified cache system - synchronous wrapper for legacy callers
        if (!forceRefresh)
        {
            var month = DateTime.Now.Month;
            var year = DateTime.Now.Year;
            var unified = Task.Run(() => TryGetMonthlyFromUnifiedCacheAsync(location, year, month)).Result;
            if (unified != null)
            {
                _monthlyCalendar = unified;
                return _monthlyCalendar;
            }
        }

        if (!HaveInternet()) return null;

        try
        {
            // Delegate to XmlApiService
            var result = Task.Run(() => _xmlApiService.GetMonthlyPrayerTimesAsync(
                location.Latitude,
                location.Longitude,
                location.Altitude ?? 0,
                DateTime.Now.Month,
                DateTime.Now.Year)).Result;

            if (result != null && result.Count > 0)
            {
                _monthlyCalendar = result;
                // Save to unified cache (fire-and-forget)
                _ = Task.Run(async () => await SaveToUnifiedCacheAsync(location, result.ToList()));
            }
            return _monthlyCalendar;
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"GetMonthlyPrayerTimes failed: {exception.Message}");
        }

        return _monthlyCalendar;
    }

		/// <summary>
		/// Async monthly XML fetch to avoid blocking with XDocument.Load(url).
		/// Uses unified year cache when available.
		/// </summary>
		public async Task<ObservableCollection<Calendar>?> GetMonthlyPrayerTimesXmlAsync(Location location, bool forceRefresh = false)
		{
			if (!forceRefresh)
			{
				var month = DateTime.Now.Month;
				var year = DateTime.Now.Year;
				var cached = await TryGetMonthlyFromUnifiedCacheAsync(location, year, month).ConfigureAwait(false);
				if (cached != null) return cached;
			}

			if (!HaveInternet()) return null;

			try
			{
				// Delegate to XmlApiService for fetch + parse
				var result = await _xmlApiService.GetMonthlyPrayerTimesAsync(
					location.Latitude,
					location.Longitude,
					location.Altitude ?? 0,
					DateTime.Now.Month,
					DateTime.Now.Year).ConfigureAwait(false);

				if (result != null && result.Count > 0)
				{
					await SaveToUnifiedCacheAsync(location, result.ToList()).ConfigureAwait(false);
				}
				return result;
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Async XML monthly failed: {ex.Message}");
				return null;
        }
    }

    #endregion

    // XML Parsing methods have been moved to XmlApiService

    #region Network and Internet

    /// <summary>
    /// Checks if the device has internet connectivity.
    /// Considers both full Internet and ConstrainedInternet as online.
    /// </summary>
    /// <returns>True if internet is available; otherwise false.</returns>
    public bool HaveInternet()
    {
        // Consider Internet and ConstrainedInternet as online to avoid false negatives
        var access = Connectivity.NetworkAccess;
        if (access == NetworkAccess.Internet || access == NetworkAccess.ConstrainedInternet)
            return true;

        // Some platforms report Unknown while still having a connection profile
        if (access == NetworkAccess.Unknown)
        {
            var profiles = Connectivity.ConnectionProfiles;
            if (profiles.Contains(ConnectionProfile.WiFi) || profiles.Contains(ConnectionProfile.Cellular))
                return true;
        }

        return false;
    }

    #endregion

    #region Prayer Times Fetching

    /// <summary>
    /// Gets prayer times quickly using cached data or XML API.
    /// Prefers cached data for fast startup.
    /// </summary>
    /// <returns>Today's prayer times or null if unavailable.</returns>
    public async Task<Calendar?> GetPrayerTimesFastAsync()
    {
        // Try cache first
        var cached = GetTakvimFromFile();
        if (cached != null)
        {
            calendar = cached;
            return calendar;
        }

        if (!HaveInternet()) return null;
        
        var location = await GetCurrentLocationAsync(false).ConfigureAwait(false);
        if (location != null)
        {
            try
            {
                // Delegate to XmlApiService
                var result = await _xmlApiService.GetDailyPrayerTimesAsync(
                    location.Latitude,
                    location.Longitude,
                    location.Altitude ?? 0,
                    DateTime.Today).ConfigureAwait(false);

                if (result != null)
                {
                    calendar = result;
                    return calendar;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetPrayerTimesFastAsync failed: {ex.Message}");
            }
        }

        return calendar;
    }

    /// <summary>
    /// Gets prayer times with optional location refresh.
    /// When refreshLocation is true, forces a fresh GPS fix.
    /// </summary>
    /// <param name="refreshLocation">If true, refreshes GPS location.</param>
    /// <returns>Today's prayer times.</returns>
    public async Task<Calendar?> GetPrayerTimesAsync(bool refreshLocation)
    {
        Debug.WriteLine("TimeStamp-GetPrayerTimes-Start", DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss.fff tt"));

        try
        {
            if (!HaveInternet()) return null;
            
            var location = await GetCurrentLocationAsync(refreshLocation).ConfigureAwait(false);
            if (location == null) return calendar;

            // Delegate to XmlApiService
            var result = await _xmlApiService.GetDailyPrayerTimesAsync(
                location.Latitude,
                location.Longitude,
                location.Altitude ?? 0,
                DateTime.Today).ConfigureAwait(false);

            if (result != null)
            {
                calendar = result;
                ShowToast(AppResources.KonumYenilendi);
            }
            else
            {
                ShowToast(AppResources.NamazVaktiAlmaHatasi);
            }
        }
        catch (Exception exception)
        {
            Alert(exception.Message, AppResources.KonumHatasi);
        }

        Debug.WriteLine("TimeStamp-GetPrayerTimes-Finish", DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss.fff tt"));
        return calendar;
    }

    #endregion

    #region Alarm Scheduling

    /// <summary>
    /// Schedules prayer alarms for the next 30 days.
    /// Uses unified cache to ensure coverage across month boundaries.
    /// </summary>
    /// <param name="forceReschedule">If true, bypasses throttling and reschedules immediately.</param>
    /// <remarks>
    /// Key behaviors:
    /// <list type="bullet">
    ///   <item>Cancels existing alarms if reminders disabled</item>
    ///   <item>Throttles auto-reschedule based on coverage and cooldown</item>
    ///   <item>Schedules each prayer type based on user preferences</item>
    ///   <item>Persists coverage date for tracking</item>
    /// </list>
    /// </remarks>
    public async Task SetMonthlyAlarmsAsync(bool forceReschedule = false)
		{
			Debug.WriteLine("TimeStamp-SetWeeklyAlarms-Start", DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss.fff tt"));
			using (_perf.StartTimer("SetMonthlyAlarms"))
			{
				var remindersEnabled = CheckRemindersEnabledAny();

				if (DeviceInfo.Platform == DevicePlatform.Android)
				{
					if (!remindersEnabled)
					{
						_alarmService.CancelAlarm();
						ClearAlarmCoverage();
						return;
					}

					if (ShouldThrottleAutoReschedule(forceReschedule))
					{
						return;
					}

					_alarmService.CancelAlarm();
					try
					{
						var location = await GetCurrentLocationAsync(false).ConfigureAwait(false);
						if (location == null || location.Latitude == 0 || location.Longitude == 0)
						{
							ShowToast(AppResources.AylikTakvimeErisemedi);
							Debug.WriteLine("⚠️ Invalid location data");
							return;
						}

						var next30Days = await EnsureDaysRangeAsync(location, DateTime.Today, 30).ConfigureAwait(false);
						if (next30Days == null || next30Days.Count == 0)
						{
							ShowToast(AppResources.AylikTakvimeErisemedi);
							Debug.WriteLine("⚠️ No days returned from EnsureDaysRangeAsync");
							return;
						}

						Debug.WriteLine($"Setting alarms for {next30Days.Count} days starting {next30Days.First().Date}");
						var dayCounter = 0;
						var now = DateTime.Now;
						DateTime? coverageThrough = null;

						foreach (var todayCalendar in next30Days.OrderBy(d => ParseCalendarDateOrMin(d.Date)))
						{
							var todayDate = ParseCalendarDateOrMin(todayCalendar.Date);
							if (todayDate < DateTime.Today)
							{
								Debug.WriteLine($"⏭️ Skipping past date: {todayCalendar.Date}");
								continue;
							}

							var baseDate = todayDate;
							try
							{
								var falseFajrTime = TimeSpan.Parse(todayCalendar.FalseFajr);
								var fajrTime = TimeSpan.Parse(todayCalendar.Fajr);
								var sunriseTime = TimeSpan.Parse(todayCalendar.Sunrise);
								var dhuhrTime = TimeSpan.Parse(todayCalendar.Dhuhr);
								var asrTime = TimeSpan.Parse(todayCalendar.Asr);
								var maghribTime = TimeSpan.Parse(todayCalendar.Maghrib);
								var ishaTime = TimeSpan.Parse(todayCalendar.Isha);
								var endOfIshaTime = TimeSpan.Parse(todayCalendar.EndOfIsha);

								Debug.WriteLine($"⏰ Processing alarms for {baseDate:dd/MM/yyyy} (day {dayCounter})");

								var isToday = todayDate.Date == DateTime.Today;

								SchedulePrayerAlarmIfEnabled(baseDate, falseFajrTime, now, isToday, "falsefajr", "Fecri Kazip");
								SchedulePrayerAlarmIfEnabled(baseDate, fajrTime, now, isToday, "fajr", "Fecri Sadık");
								SchedulePrayerAlarmIfEnabled(baseDate, sunriseTime, now, isToday, "sunrise", "Sabah Sonu");
								SchedulePrayerAlarmIfEnabled(baseDate, dhuhrTime, now, isToday, "dhuhr", "Öğle");
								SchedulePrayerAlarmIfEnabled(baseDate, asrTime, now, isToday, "asr", "İkindi");
								SchedulePrayerAlarmIfEnabled(baseDate, maghribTime, now, isToday, "maghrib", "Akşam");
								SchedulePrayerAlarmIfEnabled(baseDate, ishaTime, now, isToday, "isha", "Yatsı");
								SchedulePrayerAlarmIfEnabled(baseDate, endOfIshaTime, now, isToday, "endofisha", "Yatsı Sonu");

								dayCounter++;
								coverageThrough = baseDate;
								Debug.WriteLine($"✅ Completed day {dayCounter}: {baseDate:dd/MM/yyyy} (alarms so far)");
								if (dayCounter >= 30)
								{
									break;
								}
							}
							catch (Exception ex)
							{
								Debug.WriteLine($"❌ Error processing day {baseDate:dd/MM/yyyy}: {ex.Message}");
							}
						}

						if (dayCounter > 0 && coverageThrough.HasValue)
						{
							PersistAlarmCoverage(coverageThrough.Value);
							Debug.WriteLine($"✅ Alarm scheduling complete through {coverageThrough.Value:dd/MM/yyyy}");
						}
					}
					catch (Exception exception)
					{
						Debug.WriteLine($"❌ SetMonthlyAlarmsAsync failed: {exception.Message}\n{exception.StackTrace}");
					}
				}

				if (DeviceInfo.Platform == DevicePlatform.iOS)
				{
#if __IOS__
					Platforms.iOS.NotificationService.CancelAllNotifications();

					if (!remindersEnabled)
					{
						ClearAlarmCoverage();
						return;
					}

					try
					{
						var location = await GetCurrentLocationAsync(false);
						if (location != null && location.Latitude != 0 && location.Longitude != 0)
						{
							var next30Days = await EnsureDaysRangeAsync(location, DateTime.Today, 30);
							if (next30Days?.Count > 0)
							{
								var notificationMinutes = new Dictionary<string, int>
								{
									{ "falsefajr", Preferences.Get("falsefajrNotificationTime", 0) },
									{ "fajr", Preferences.Get("fajrNotificationTime", 0) },
									{ "sunrise", Preferences.Get("sunriseNotificationTime", 0) },
									{ "dhuhr", Preferences.Get("dhuhrNotificationTime", 0) },
									{ "asr", Preferences.Get("asrNotificationTime", 0) },
									{ "maghrib", Preferences.Get("maghribNotificationTime", 0) },
									{ "isha", Preferences.Get("ishaNotificationTime", 0) },
									{ "endofisha", Preferences.Get("endofishaNotificationTime", 0) }
								};

								var orderedDays = next30Days.OrderBy(d => ParseCalendarDateOrMin(d.Date)).ToList();
								await Platforms.iOS.NotificationService.ScheduleMonthlyNotificationsAsync(orderedDays, notificationMinutes);
								var coverageCandidate = ParseCalendarDateOrMin(orderedDays.Last().Date);
								if (coverageCandidate != DateTime.MinValue)
								{
									PersistAlarmCoverage(coverageCandidate);
								}

								ShowToast(AppResources.OtuzGunHatirlaticiPlanlandi);
							}
							else
							{
								ShowToast(AppResources.OtuzGunHatirlaticiHatasi);
							}
						}
					}
					catch (Exception ex)
					{
						Debug.WriteLine($"❌ iOS notification scheduling failed: {ex.Message}");
					}
#endif
				}
			}
        Debug.WriteLine("TimeStamp-SetMonthlyAlarms-Finish", DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss.fff tt"));
    }

    /// <summary>
    /// Ensures a range of calendar days is available, fetching from cache or API as needed.
    /// Spans multiple months/years if necessary for 30-day alarm scheduling.
    /// </summary>
    /// <param name="location">User's current location.</param>
    /// <param name="startDate">First date needed.</param>
    /// <param name="daysNeeded">Number of consecutive days required.</param>
    /// <returns>List of calendar days covering the requested range.</returns>
    private async Task<List<Calendar>> EnsureDaysRangeAsync(Location location, DateTime startDate, int daysNeeded)
		{
			Debug.WriteLine($"EnsureDaysRangeAsync: Ensuring {daysNeeded} days from {startDate:dd/MM/yyyy} for location {location.Latitude},{location.Longitude}");
            // Invalidate yearly caches if location changed meaningfully
            ClearYearCachesIfLocationChanged(location);

			var result = new List<Calendar>();
			var endDate = startDate.AddDays(daysNeeded - 1);

			// Load caches for years involved
			var years = Enumerable.Range(startDate.Year, endDate.Year - startDate.Year + 1).ToArray();
			var yearCaches = new Dictionary<int, List<Calendar>>();
			foreach (var y in years)
			{
				List<Calendar> cached;
				using (_perf.StartTimer($"Cache.LoadYear.{y}"))
				{
					cached = await LoadYearCacheAsync(location, y).ConfigureAwait(false);
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
					Debug.WriteLine($"EnsureDaysRangeAsync: Year {year} Month {month} found {monthDays.Count} days in cache");
                    if (monthDays.Count > 27) return monthDays; // assume month is sufficiently complete
				}

				// Fetch
				ObservableCollection<Calendar> fetched = null;
				if (year == DateTime.Now.Year)
				{
					// JSON monthly supports current-year by monthId
					fetched = await _jsonApiService.GetMonthlyPrayerTimesAsync(location.Latitude, location.Longitude, month, location.Altitude ?? 0).ConfigureAwait(false);
				}

				// If monthly not available (different year or failed), fetch missing days via daily endpoint for each date
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

				Debug.WriteLine($"EnsureDaysRangeAsync: Year {year} Month {month} fetched {fetched?.Count ?? 0} days from API");

                // Merge into cache and persist
                if (fetched != null && fetched.Count > 0)
				{
					var toAdd = fetched.ToList();
					if (!yearCaches.ContainsKey(year)) yearCaches[year] = new List<Calendar>();
					yearCaches[year] = MergeCalendars(yearCaches[year], toAdd);
					using (_perf.StartTimer($"Cache.SaveYear.{year}"))
					{
						await SaveYearCacheAsync(location, year, yearCaches[year]).ConfigureAwait(false);
					}
					Debug.WriteLine($"EnsureDaysRangeAsync: Year {year} cache now has {yearCaches[year].Count} days after merge");
                    return toAdd;
				}

				return new List<Calendar>();
			}

			Debug.WriteLine($"EnsureDaysRangeAsync: Collecting days from {startDate:dd/MM/yyyy} to {endDate:dd/MM/yyyy}");

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
                    if (!TryParseCalendarDate(d.Date, out var dd)) return false;
                    return dd >= startDate && dd <= endDate;
                })
                .GroupBy(d => d.Date)
                .Select(g => g.First())
                .OrderBy(d => ParseCalendarDateOrMin(d.Date))
                .Take(daysNeeded)
                .ToList();

			return inRange;
		}

		private static List<Calendar> MergeCalendars(List<Calendar> existing, List<Calendar> additions)
		{
			var dict = existing.ToDictionary(c => c.Date, c => c);
			foreach (var a in additions)
			{
				dict[a.Date] = a;
			}
			return dict.Values.OrderBy(c => ParseCalendarDateOrMin(c.Date)).ToList();
		}

		/// <summary>
    /// <summary>
    /// Schedules a single prayer alarm if enabled and the time is in the future.
    /// </summary>
    /// <param name="baseDate">The date of the prayer.</param>
    /// <param name="prayerTime">Time of day for the prayer.</param>
    /// <param name="now">Current time for comparison.</param>
    /// <param name="isToday">Whether this is today's schedule.</param>
    /// <param name="preferenceKey">Preference key prefix for this prayer (e.g., "fajr").</param>
    /// <param name="prayerName">Display name of the prayer for notifications.</param>
    private void SchedulePrayerAlarmIfEnabled(DateTime baseDate, TimeSpan prayerTime, DateTime now, bool isToday,
        string preferenceKey, string prayerName)
    {
        var notificationMinutes = Preferences.Get($"{preferenceKey}NotificationTime", 0);
        var alarmDateTime = baseDate + prayerTime - TimeSpan.FromMinutes(notificationMinutes);
        var isEnabled = Preferences.Get($"{preferenceKey}Enabled", false);

        if (isEnabled && (!isToday || now < alarmDateTime))
        {
            _alarmService.SetAlarm(baseDate, prayerTime, notificationMinutes, prayerName);
        }
    }

    #endregion

    #region Unified Cache System (Delegating to PrayerCacheService)

    /// <summary>
    /// Loads cached prayer times for a specific year and location.
    /// Delegates to PrayerCacheService.
    /// </summary>
    private Task<List<Calendar>?> LoadYearCacheAsync(Location location, int year)
        => _cacheService.LoadYearCacheAsync(location, year);

    /// <summary>
    /// Saves prayer times for a year to cache.
    /// Delegates to PrayerCacheService.
    /// </summary>
    private Task SaveYearCacheAsync(Location location, int year, List<Calendar> days)
        => _cacheService.SaveYearCacheAsync(location, year, days);

    #endregion

    #region Prayer Data Builders

    /// <summary>
    /// Builds a list of Prayer objects from a Calendar day.
    /// Maps raw calendar times to UI-ready Prayer objects with enabled states.
    /// </summary>
    /// <param name="day">The calendar day containing prayer times.</param>
    /// <returns>List of 8 prayers for the day.</returns>
    /// <remarks>
    /// Calendar is the canonical storage format; Prayer carries UI state and notification toggles.
    /// </remarks>
    public List<Prayer> BuildPrayersFromCalendar(Calendar day)
		{
			var list = new List<Prayer>();
			if (day is null)
				return list;

			// Create prayers with persisted Enabled flags; State is UI-time-dependent and set by ViewModels
			list.Add(new Prayer { Id = "falsefajr", Name = AppResources.FecriKazip, Time = day.FalseFajr, Enabled = Preferences.Get("falsefajrEnabled", false) });
			list.Add(new Prayer { Id = "fajr",      Name = AppResources.FecriSadik, Time = day.Fajr,       Enabled = Preferences.Get("fajrEnabled", false) });
			list.Add(new Prayer { Id = "sunrise",   Name = AppResources.SabahSonu, Time = day.Sunrise,    Enabled = Preferences.Get("sunriseEnabled", false) });
			list.Add(new Prayer { Id = "dhuhr",     Name = AppResources.Ogle,      Time = day.Dhuhr,      Enabled = Preferences.Get("dhuhrEnabled", false) });
			list.Add(new Prayer { Id = "asr",       Name = AppResources.Ikindi,    Time = day.Asr,        Enabled = Preferences.Get("asrEnabled", false) });
			list.Add(new Prayer { Id = "maghrib",   Name = AppResources.Aksam,     Time = day.Maghrib,    Enabled = Preferences.Get("maghribEnabled", false) });
			list.Add(new Prayer { Id = "isha",      Name = AppResources.Yatsi,     Time = day.Isha,       Enabled = Preferences.Get("ishaEnabled", false) });
			list.Add(new Prayer { Id = "endofisha", Name = AppResources.YatsiSonu, Time = day.EndOfIsha,  Enabled = Preferences.Get("endofishaEnabled", false) });

			// Assign animated weather icons based on prayer ID (not localized name)
			foreach (var prayer in list)
			{
				PrayerIconService.AssignIconById(prayer);
			}

			return list;
		}

		public void SavePrayerTimesToPreferences(Calendar day)
		{
			if (day is null)
				return;
			try
			{
				Preferences.Set("falsefajr", day.FalseFajr);
				Preferences.Set("fajr", day.Fajr);
				Preferences.Set("sunrise", day.Sunrise);
				Preferences.Set("dhuhr", day.Dhuhr);
				Preferences.Set("asr", day.Asr);
				Preferences.Set("maghrib", day.Maghrib);
				Preferences.Set("isha", day.Isha);
				Preferences.Set("endofisha", day.EndOfIsha);
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"[DataService] SavePrayerTimesToPreferences failed: {ex.Message}");
			}
		}

		/// <summary>
		/// Gets prayer data for a specific date from cache or network.
		/// </summary>
		/// <param name="date">Target date.</param>
		/// <param name="forceRefresh">Whether to bypass cache.</param>
		/// <returns>List of prayers for the specified date.</returns>
		public async Task<List<Prayer>> GetPrayersForDateAsync(DateTime date, bool forceRefresh = false)
		{
			var location = await GetCurrentLocationAsync(false).ConfigureAwait(false);
			var days = await EnsureDaysRangeAsync(location, date.Date, 1).ConfigureAwait(false);
			var day = days?.FirstOrDefault();
			return BuildPrayersFromCalendar(day);
		}

		#endregion

		#region Cache Maintenance

		/// <summary>
		/// Ensures today's calendar entry exists in the unified yearly cache and updates the in-memory calendar.
		/// Lightweight method for returning users when auto-renew is off.
		/// Uses last-known location and JSON monthly/daily APIs.
		/// </summary>
		/// <returns>True if cache was updated with new data, false if cache already had today's data or update failed.</returns>
		public async Task<bool> EnsureTodayInCacheAsync()
		{
			try
			{
				var today = DateTime.Today;
				var location = await GetCurrentLocationAsync(false).ConfigureAwait(false);
				if (location is null || location.Latitude == 0 && location.Longitude == 0)
					return false;

				// Check year cache first
				var cachedYear = await LoadYearCacheAsync(location, today.Year).ConfigureAwait(false);
				var cachedToday = cachedYear?.FirstOrDefault(d => ParseCalendarDateOrMin(d.Date) == today);
				if (cachedToday is not null)
				{
					calendar = cachedToday;
					return false; // no update required
				}

				if (!HaveInternet()) return false;

				// Try JSON monthly first for current month
				ObservableCollection<Calendar> month = null;
				try
				{
					month = await _jsonApiService.GetMonthlyPrayerTimesAsync(location.Latitude, location.Longitude, today.Month, location.Altitude ?? 0).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					Debug.WriteLine($"EnsureTodayInCacheAsync: monthly fetch failed: {ex.Message}");
				}

				Calendar newToday = null;
				if (month != null && month.Count > 0)
				{
					await SaveToUnifiedCacheAsync(location, month.ToList()).ConfigureAwait(false);
					newToday = month.FirstOrDefault(d => ParseCalendarDateOrMin(d.Date) == today);
				}

				// Fallback: daily
				if (newToday is null)
				{
					try
					{
						var daily = await _jsonApiService.GetDailyPrayerTimesAsync(location.Latitude, location.Longitude, today, location.Altitude ?? 0).ConfigureAwait(false);
						if (daily != null)
						{
							await SaveToUnifiedCacheAsync(location, new List<Calendar> { daily }).ConfigureAwait(false);
							newToday = daily;
						}
					}
					catch (Exception ex)
					{
						Debug.WriteLine($"EnsureTodayInCacheAsync: daily fetch failed: {ex.Message}");
					}
				}

				if (newToday is not null)
				{
					calendar = newToday;
					return true; // updated
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"EnsureTodayInCacheAsync error: {ex.Message}");
			}

			return false;
		}

		/// <summary>
		/// Clears all year caches if user location has changed significantly (~2km).
		/// Delegates to PrayerCacheService.
		/// </summary>
		/// <param name="location">Current user location.</param>
		private void ClearYearCachesIfLocationChanged(Location location)
			=> _cacheService.ClearCachesIfLocationChanged(location);

		/// <summary>
		/// Tries to retrieve a specific month's prayer data from the unified year cache.
		/// Delegates to PrayerCacheService.
		/// </summary>
		private Task<ObservableCollection<Calendar>?> TryGetMonthlyFromUnifiedCacheAsync(Location location, int year, int month)
			=> _cacheService.TryGetMonthlyFromCacheAsync(location, year, month);

		/// <summary>
		/// Returns the current month's data from unified cache if present (even if incomplete).
		/// Delegates to PrayerCacheService.
		/// </summary>
		public Task<ObservableCollection<Calendar>> GetMonthlyFromCacheOrEmptyAsync(Location location)
			=> _cacheService.GetMonthlyFromCacheOrEmptyAsync(location);

		/// <summary>
		/// Saves a list of calendar days to the unified year cache.
		/// Delegates to PrayerCacheService.
		/// </summary>
		private Task SaveToUnifiedCacheAsync(Location location, List<Calendar> days)
			=> _cacheService.SaveToUnifiedCacheAsync(location, days);

		#endregion

		#region User Interaction Utilities

		/// <summary>
		/// Checks if any prayer reminder notifications are enabled.
		/// </summary>
		/// <returns>True if at least one prayer has notifications enabled.</returns>
		internal bool CheckRemindersEnabledAny()
		{
			return Preferences.Get("falsefajrEnabled", false) || Preferences.Get("fajrEnabled", false) ||
				   Preferences.Get("sunriseEnabled", false) || Preferences.Get("dhuhrEnabled", false) ||
				   Preferences.Get("asrEnabled", false) || Preferences.Get("maghribEnabled", false) ||
				   Preferences.Get("ishaEnabled", false) || Preferences.Get("endofishaEnabled", false);
		}

		/// <summary>
		/// Displays a toast notification message to the user.
		/// Runs on the main UI thread.
		/// </summary>
		/// <param name="message">Message to display.</param>
		public static void ShowToast(string message)
		{
			MainThread.BeginInvokeOnMainThread(() =>
			{
				CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
				ToastDuration duration = ToastDuration.Long;
				double fontSize = 14;
				var toast = Toast.Make(message, duration, fontSize);
				toast.Show(cancellationTokenSource.Token);
			});
		}

		/// <summary>
		/// Displays a modal alert dialog to the user.
		/// Runs on the main UI thread.
		/// </summary>
		/// <param name="title">Alert title.</param>
		/// <param name="message">Alert message body.</param>
		public static void Alert(string title, string message)
		{
			MainThread.BeginInvokeOnMainThread(async () =>
			{
				await Shell.Current.DisplayAlert(title, message, AppResources.Tamam);
			});
		}

		/// <summary>
		/// Checks and requests location permission from the user.
		/// Handles Windows bypass, soft denial, and permanent denial scenarios.
		/// </summary>
		/// <returns>The current permission status after check/request.</returns>
		public async Task<PermissionStatus> CheckAndRequestLocationPermission()
		{
			// Windows: Completely bypass permission flow (mock/static location strategy)
			if (DeviceInfo.Platform == DevicePlatform.WinUI)
			{
				return PermissionStatus.Granted;
			}
			// Check can be done off the UI thread
			var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
			if (status == PermissionStatus.Granted)
				return status;

			var firstAskKey = "LocationPermissionAsked";
			var alreadyAsked = Preferences.Get(firstAskKey, false);

			// Request must run on main thread
			status = await MainThread.InvokeOnMainThreadAsync(async () =>
			{
				return await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
			});
			Preferences.Set(firstAskKey, true);

			if (status == PermissionStatus.Denied)
			{
				var shouldShow = Permissions.ShouldShowRationale<Permissions.LocationWhenInUse>();
				if (shouldShow)
				{
					// Soft denial: show a non-blocking toast so next explicit action can re-request.
					ShowToast(AppResources.KonumIzniIcerik);
				}
				else if (alreadyAsked)
				{
					// Permanent denial path (Don't ask again) – show modal guidance once.
					Alert(AppResources.KonumIzniBaslik, AppResources.KonumIzniIcerik);
				}
			}
			return status;
		}

		/// <summary>
		/// Requests the device's current GPS location with fallback strategies.
		/// Uses medium accuracy first (fast), then falls back to low accuracy (coarse).
		/// </summary>
		/// <param name="waitDelay">Maximum seconds to wait for GPS fix (default 5).</param>
		/// <returns>Location if successful, null if failed or platform doesn't support geolocation.</returns>
		/// <remarks>
		/// Windows returns null by design - caller should use saved coordinates.
		/// </remarks>
		public async Task<Location?> RequestLocationAsync(int waitDelay = 5)
		{
			// Windows: do not invoke Geolocation APIs – return null so caller uses saved/mock coordinates
			if (DeviceInfo.Platform == DevicePlatform.WinUI)
			{
				return null;
			}
			Location? location = null;
			try
			{
				// Strategy: fast first fix, then a quick fallback for broader providers.
				// 1) Medium accuracy, short timeout (good balance for speed vs. precision)
				var cts = new CancellationTokenSource();
				var requestFast = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(waitDelay));

				using (_perf.StartTimer("Location.Request.Fast"))
				{
					location = await Geolocation.Default.GetLocationAsync(requestFast, cts.Token).ConfigureAwait(false);
				}

				// 2) If still null, try Low accuracy with a slightly longer timeout to allow coarse providers (cell/Wi‑Fi)
				if (location is null)
				{
					var fallbackTimeout = TimeSpan.FromSeconds(Math.Max(waitDelay + 3, 8));
					var requestFallback = new GeolocationRequest(GeolocationAccuracy.Low, fallbackTimeout);
					using (_perf.StartTimer("Location.Request.Fallback"))
					{
						location = await Geolocation.Default.GetLocationAsync(requestFallback, cts.Token).ConfigureAwait(false);
					}
				}
			}
			catch (FeatureNotSupportedException fnsEx)
			{
				// Handle not supported on device exception
				Alert(AppResources.CihazGPSDesteklemiyor, fnsEx.Message);
			}
			catch (FeatureNotEnabledException fneEx)
			{
				// Handle not enabled on device exception
				Alert(AppResources.KonumKapaliBaslik, AppResources.KonumKapali);//fneEx.Message
				Debug.WriteLine(fneEx.Message);
			}
			catch (PermissionException pEx)
			{
				// Handle permission exception
				Alert(AppResources.KonumIzniBaslik, AppResources.KonumIzniIcerik);//pEx.Message
				Debug.WriteLine(pEx.Message);
			}
			catch (Exception ex)
			{
				// Unable to get location
				Alert(AppResources.KonumHatasi, ex.Message);
			}
			return location;
		}

		#endregion

		#region Hybrid API Methods (New JSON + Old XML Fallback)

		/// <summary>
		/// <summary>
		/// Gets monthly prayer times using a hybrid approach: tries new JSON API first, falls back to legacy XML API.
		/// </summary>
		/// <param name="location">User's geographic location.</param>
		/// <param name="forceRefresh">If true, bypasses cache and fetches fresh data.</param>
		/// <returns>Observable collection of calendar days, or null if both APIs fail.</returns>
		/// <remarks>
		/// Strategy order: 1) Unified cache (if not forcing refresh), 2) JSON API, 3) XML API.
		/// Results are automatically saved to unified cache for future use.
		/// </remarks>
		public async Task<ObservableCollection<Calendar>?> GetMonthlyPrayerTimesHybridAsync(Location location, bool forceRefresh = false)
		{
			Debug.WriteLine("Starting Hybrid Monthly Prayer Times request");

			// Try unified cache first if not forcing refresh
			if (!forceRefresh)
			{
				var month = DateTime.Now.Month;
				var year = DateTime.Now.Year;
				ObservableCollection<Calendar>? unified;
				using (_perf.StartTimer("Cache.TryGetMonthlyUnified"))
				{
					unified = await TryGetMonthlyFromUnifiedCacheAsync(location, year, month).ConfigureAwait(false);
				}
				if (unified != null)
				{
					Debug.WriteLine("Hybrid: Returning unified cache data");
					_monthlyCalendar = unified;
					return unified;
				}
			}

			if (!HaveInternet()) return null;

			var currentMonth = DateTime.Now.Month;
			var altitude = location.Altitude ?? 0;

			// Strategy 1: Try new JSON API first
			Debug.WriteLine("Hybrid: Trying new JSON API");
			try
			{
				ObservableCollection<Calendar>? jsonResult;
				using (_perf.StartTimer("JSON.Monthly"))
				{
					jsonResult = await _jsonApiService.GetMonthlyPrayerTimesAsync(
						location.Latitude, location.Longitude, currentMonth, altitude);
				}
				
				if (jsonResult != null && jsonResult.Count > 0)
				{
					Debug.WriteLine($"Hybrid: JSON API success - {jsonResult.Count} days");
					await SaveToUnifiedCacheAsync(location, jsonResult.ToList()).ConfigureAwait(false);
					_monthlyCalendar = jsonResult;
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
				ObservableCollection<Calendar> xmlResult;
				using (_perf.StartTimer("XML.Monthly.Fallback"))
				{
					xmlResult = await GetMonthlyPrayerTimesXmlAsync(location, forceRefresh).ConfigureAwait(false);
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
		/// Gets daily prayer times using a hybrid approach: tries new JSON API first, falls back to legacy XML API.
		/// </summary>
		/// <param name="location">User's geographic location.</param>
		/// <param name="date">Target date (defaults to today if null).</param>
		/// <returns>Calendar for the specified day, or null if both APIs fail.</returns>
		/// <remarks>
		/// Results are automatically persisted to unified cache for future use.
		/// </remarks>
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
					// Persist into unified cache for the corresponding year
					await SaveToUnifiedCacheAsync(location, new List<Calendar> { jsonResult }).ConfigureAwait(false);
					return jsonResult;
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Hybrid Daily: JSON API failed - {ex.Message}");
			}

			// Strategy 2: Fallback to old XML API
			Debug.WriteLine("Hybrid Daily: Falling back to XML API");
			try
			{
				Calendar? xmlResult;
				using (_perf.StartTimer("XML.Daily.Fallback"))
				{
					xmlResult = await GetPrayerTimesAsync(false);
				}
				if (xmlResult != null)
				{
					Debug.WriteLine("Hybrid Daily: XML API success");
					return xmlResult;
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Hybrid Daily: XML API failed - {ex.Message}");
			}

			Debug.WriteLine("Hybrid Daily: Both APIs failed");
			return null;
		}

		/// <summary>
		/// Fetches prayer times for a specific month and saves to cache.
		/// Used by MonthPage when user navigates to months without cached data.
		/// </summary>
		/// <param name="location">User's location.</param>
		/// <param name="month">Target month (1-12).</param>
		/// <param name="year">Target year.</param>
		/// <returns>Collection of calendar entries for the specified month, or null if failed.</returns>
		/// <remarks>
		/// Saves to year cache file in background (async, non-blocking).
		/// Performance impact is minimal: ~15KB write, async I/O.
		/// </remarks>
		public async Task<ObservableCollection<Calendar>?> FetchSpecificMonthAsync(Location location, int month, int year)
		{
			if (!HaveInternet()) return null;

			try
			{
				Debug.WriteLine($"FetchSpecificMonth: Fetching {month}/{year}");
				ObservableCollection<Calendar>? result = null;

				// Try JSON API first (supports current year by monthId)
				if (year == DateTime.Now.Year)
				{
					var jsonResult = await _jsonApiService.GetMonthlyPrayerTimesAsync(
						location.Latitude, location.Longitude, month, location.Altitude ?? 0
					).ConfigureAwait(false);

					if (jsonResult != null && jsonResult.Count > 0)
					{
						Debug.WriteLine($"FetchSpecificMonth: JSON API success - {jsonResult.Count} days");
						result = jsonResult;
					}
				}

				// Fallback: Fetch each day individually (for different years or JSON failure)
				if (result == null)
				{
					Debug.WriteLine($"FetchSpecificMonth: Falling back to daily API");
					var daysInMonth = DateTime.DaysInMonth(year, month);
					var list = new List<Calendar>(daysInMonth);

					for (int d = 1; d <= daysInMonth; d++)
					{
						var date = new DateTime(year, month, d);
						var day = await _jsonApiService.GetDailyPrayerTimesAsync(
							location.Latitude, location.Longitude, date, location.Altitude ?? 0
						).ConfigureAwait(false);

						if (day != null) list.Add(day);
					}

					if (list.Count > 0)
					{
						Debug.WriteLine($"FetchSpecificMonth: Daily API success - {list.Count} days");
						result = new ObservableCollection<Calendar>(list);
					}
				}

				// Save to cache in background (non-blocking, ~15KB async write)
				if (result != null && result.Count > 0)
				{
					_ = Task.Run(async () =>
					{
						try
						{
							// Load existing year cache, merge with new data, and save
							var existing = await LoadYearCacheAsync(location, year).ConfigureAwait(false) ?? new List<Calendar>();
							var merged = MergeCalendars(existing, result.ToList());
							await SaveYearCacheAsync(location, year, merged).ConfigureAwait(false);
							Debug.WriteLine($"FetchSpecificMonth: Saved {result.Count} days to cache (total {merged.Count} for year {year})");
						}
						catch (Exception cacheEx)
						{
							Debug.WriteLine($"FetchSpecificMonth: Cache save failed - {cacheEx.Message}");
						}
					});
				}

				return result;
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"FetchSpecificMonth: Error - {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Tries to get monthly data from unified cache only (no network calls).
		/// </summary>
		/// <returns>Cached monthly data or null if not available.</returns>
		private async Task<ObservableCollection<Calendar>?> TryGetMonthlyFromCacheAsync()
		{
			try
			{
				var location = new Location()
				{
					Latitude = Preferences.Get("LastLatitude", 0.0),
					Longitude = Preferences.Get("LastLongitude", 0.0),
					Altitude = Preferences.Get("LastAltitude", 0.0)
				};

				if (location.Latitude != 0.0 && location.Longitude != 0.0)
				{
					var month = DateTime.Now.Month;
					var year = DateTime.Now.Year;
					return await TryGetMonthlyFromUnifiedCacheAsync(location, year, month);
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Error reading unified cache: {ex.Message}");
			}

			return null;
		}

		/// <summary>
		/// Tests response time and availability of both JSON and XML APIs.
		/// Useful for diagnostics and determining preferred API strategy.
		/// </summary>
		/// <returns>Report string showing API status and response times.</returns>
		public async Task<string> TestApiPerformanceAsync()
		{
			var tasks = new List<Task<(string api, bool success, long ms)>>();

			// Test JSON API
			tasks.Add(Task.Run(async () =>
			{
				var sw = System.Diagnostics.Stopwatch.StartNew();
				try
				{
					var success = await _jsonApiService.TestConnectionAsync();
					sw.Stop();
					return ("JSON", success, sw.ElapsedMilliseconds);
				}
				catch
				{
					sw.Stop();
					return ("JSON", false, sw.ElapsedMilliseconds);
				}
			}));

			// Test XML API
			tasks.Add(Task.Run(async () =>
			{
				var sw = System.Diagnostics.Stopwatch.StartNew();
				try
				{
					var response = await _xmlHttpClient.GetAsync("http://servis.suleymaniyetakvimi.com/servis.asmx");
					sw.Stop();
					return ("XML", response.IsSuccessStatusCode, sw.ElapsedMilliseconds);
				}
				catch
				{
					sw.Stop();
					return ("XML", false, sw.ElapsedMilliseconds);
				}
			}));

			var results = await Task.WhenAll(tasks);
			var report = string.Join(", ", results.Select(r => $"{r.api}: {(r.success ? "OK" : "FAIL")} ({r.ms}ms)"));
			Debug.WriteLine($"API Performance Test: {report}");
			return report;
		}

		/// <summary>
		/// Gets today's prayer times using hybrid approach.
		/// Primary method used by MainViewModel for displaying current prayer times.
		/// </summary>
		/// <param name="refreshLocation">If true, fetches fresh GPS location.</param>
		/// <returns>Calendar for today, or fallback from cache/memory.</returns>
		public async Task<Calendar?> GetPrayerTimesHybridAsync(bool refreshLocation = false)
		{
			var location = await GetCurrentLocationAsync(refreshLocation).ConfigureAwait(false);
			if (location == null || location.Latitude == 0 || location.Longitude == 0)
			{
				// Fallback to last known or placeholder
				return GetTakvimFromFile() ?? calendar;
			}

			// Try daily hybrid fetch and keep the service-level calendar in sync
			var daily = await GetDailyPrayerTimesHybridAsync(location).ConfigureAwait(false);
			if (daily != null)
			{
				calendar = daily; // ensure consumers reading DataService.calendar see the fresh value
				return daily;
			}

			// If daily fetch failed, keep existing calendar (maybe monthly prepared earlier)
			return calendar ?? GetTakvimFromFile();
		}

		/// <summary>
		/// Gets today's prayer times using hybrid approach - parameterless version for backward compatibility.
		/// </summary>
		/// <returns>Calendar for today.</returns>
		public async Task<Calendar?> GetPrayerTimesHybridAsync()
		{
			return await GetPrayerTimesHybridAsync(false);
		}

		#endregion
}
