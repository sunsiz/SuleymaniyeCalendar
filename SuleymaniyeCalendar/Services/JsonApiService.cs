using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using SuleymaniyeCalendar.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Calendar = SuleymaniyeCalendar.Models.Calendar;

namespace SuleymaniyeCalendar.Services
{
    /// <summary>
    /// Service for interacting with the new JSON-based API at api.suleymaniyetakvimi.com
    /// </summary>
    public class JsonApiService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly PerformanceService _perf;
        private bool _disposed;
        private const string BaseUrl = "https://api.suleymaniyetakvimi.com/api/";
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true
        };

        public JsonApiService(PerformanceService? perf = null)
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            _perf = perf ?? new PerformanceService();
        }

        /// <summary>
        /// Get prayer times for a specific date using new JSON API
        /// </summary>
        public async Task<Calendar?> GetDailyPrayerTimesAsync(double latitude, double longitude, DateTime date, double altitude = 0)
        {
            try
            {
                var url = $"{BaseUrl}TimeCalculation/TimeCalculate" +
                         $"?latitude={latitude.ToString(CultureInfo.InvariantCulture)}" +
                         $"&longitude={longitude.ToString(CultureInfo.InvariantCulture)}" +
                         $"&date={date:yyyy-MM-dd}";

                Debug.WriteLine($"JSON API Daily Request: {url}");

                using var _ = _perf.StartTimer("HTTP.JSON.Daily");
                var response = await _httpClient.GetAsync(url).ConfigureAwait(false);
                var jsonContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"JSON API Daily failed with status: {response.StatusCode}, body: {jsonContent}");
                    return null;
                }

                var normalized = NormalizeJson(jsonContent);
                var dto = JsonSerializer.Deserialize<TimeCalcDto>(normalized, _jsonOptions);
                if (dto is null)
                {
                    Debug.WriteLine("JSON API Daily deserialization returned null. Body:" + jsonContent);
                    return null;
                }

                return ConvertJsonDataToCalendar(dto, altitude);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"JSON API Daily error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get prayer times for a full month using new JSON API
        /// </summary>
        public async Task<ObservableCollection<Calendar>?> GetMonthlyPrayerTimesAsync(double latitude, double longitude, int monthId, double altitude = 0, int? year = null)
        {
            try
            {
                var targetYear = year ?? DateTime.Now.Year;
                var url = $"{BaseUrl}TimeCalculation/TimeCalculateByMonth" +
                         $"?latitude={latitude.ToString(CultureInfo.InvariantCulture)}" +
                         $"&longitude={longitude.ToString(CultureInfo.InvariantCulture)}" +
                         $"&monthId={monthId}" +
                         $"&year={targetYear}";

                Debug.WriteLine($"JSON API Monthly Request: {url}");

                using var _ = _perf.StartTimer("HTTP.JSON.Monthly");
                var response = await _httpClient.GetAsync(url).ConfigureAwait(false);
                var jsonContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"JSON API Monthly failed with status: {response.StatusCode}, body length: {jsonContent?.Length ?? 0}");
                    return null;
                }

                var normalized = NormalizeJson(jsonContent);
                var dtoList = JsonSerializer.Deserialize<List<TimeCalcDto>>(normalized, _jsonOptions);
                if (dtoList is null || dtoList.Count == 0)
                {
                    Debug.WriteLine("JSON API Monthly deserialization returned empty/null. Body length: " + (jsonContent?.Length ?? 0));
                    return null;
                }

                var result = new ObservableCollection<Calendar>();
                foreach (var dto in dtoList)
                {
                    var calendar = ConvertJsonDataToCalendar(dto, altitude);
                    if (calendar != null)
                        result.Add(calendar);
                }
                Debug.WriteLine($"JSON API Monthly returned {result.Count} days");
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"JSON API Monthly error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Convert JSON API data to Calendar model
        /// </summary>
        private Calendar? ConvertJsonDataToCalendar(TimeCalcDto data, double fallbackAltitude = 0)
        {
            try
            {
                // dateTime comes as ISO format; convert to "dd/MM/yyyy" to align with existing XML model
                string dateString;
                if (DateTime.TryParse(data.DateTime, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt))
                {
                    dateString = dt.ToString("dd/MM/yyyy");
                }
                else
                {
                    // gracefully degrade by using the raw string
                    dateString = data.DateTime?.Split('T')[0] ?? DateTime.Today.ToString("dd/MM/yyyy");
                }

                var altitude = data.Height != 0 ? data.Height : (data.Elevation != 0 ? data.Elevation : fallbackAltitude);
                var timeZone = data.Gmt != 0 ? data.Gmt : ParseDoubleSafe(data.Timezone);
                var dls = data.IsDaylightSaving || data.DaylightSavingTime == 1 ? 1 : 0;

                return new Calendar
                {
                    Date = dateString,
                    Latitude = data.Latitude,
                    Longitude = data.Longitude,
                    Altitude = altitude,
                    TimeZone = timeZone,
                    DayLightSaving = dls,
                    FalseFajr = Coalesce(data.DawnTime, data.FalseFajr, data.Imsak),
                    Fajr = Coalesce(data.FajrBeginTime, data.Fajr),
                    Sunrise = Coalesce(data.FajrEndTime, data.Sunrise),
                    Dhuhr = Coalesce(data.DuhrTime, data.Dhuhr),
                    Asr = Coalesce(data.AsrTime),
                    Maghrib = Coalesce(data.Magrib, data.Maghrib),
                    Isha = Coalesce(data.IshaBeginTime, data.Isha),
                    EndOfIsha = Coalesce(data.IshaEndTime, data.EndOfIsha)
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error converting JSON data to Calendar: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Test connectivity to new JSON API
        /// </summary>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                // Prefer the warmup endpoint if available; otherwise try a very light daily query
                var warmupUrl = $"{BaseUrl}TimeCalculation/warmup";
                using var _ = _perf.StartTimer("HTTP.JSON.Warmup");
                var warmupResponse = await _httpClient.GetAsync(warmupUrl).ConfigureAwait(false);
                if (warmupResponse.IsSuccessStatusCode)
                {
                    Debug.WriteLine("JSON API connectivity test: SUCCESS (warmup)");
                    return true;
                }

                var testUrl = $"{BaseUrl}TimeCalculation/TimeCalculate?latitude=41&longitude=29&date={DateTime.Today:yyyy-MM-dd}";
                var response = await _httpClient.GetAsync(testUrl).ConfigureAwait(false);
                var isSuccess = response.IsSuccessStatusCode;
                Debug.WriteLine($"JSON API connectivity test (fallback query): {(isSuccess ? "SUCCESS" : "FAILED")}");
                return isSuccess;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"JSON API connectivity test failed: {ex.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            
            if (disposing)
            {
                _httpClient?.Dispose();
            }
            
            _disposed = true;
        }

        private static string NormalizeJson(string json)
        {
            // Some environments may return property names with different casing or synonyms.
            // We can avoid brittle string.Replace by relying on case-insensitive options already enabled.
            // This method is left for future targeted fixes if upstream schema varies unexpectedly.
            return json;
        }

        private static string Coalesce(params string?[] values)
        {
            foreach (var v in values)
            {
                if (!string.IsNullOrWhiteSpace(v)) return v;
            }
            return string.Empty;
        }

        private static double ParseDoubleSafe(string? value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                return d;
            return 0;
        }

        // DTO that matches the actual JSON payload from the API
        private sealed class TimeCalcDto
        {
            [JsonPropertyName("latitude")]
            public double Latitude { get; set; }

            [JsonPropertyName("longitude")]
            public double Longitude { get; set; }

            [JsonPropertyName("height")]
            public double Height { get; set; }

            [JsonPropertyName("elevation")]
            public double Elevation { get; set; }

            [JsonPropertyName("timezone")]
            public string? Timezone { get; set; }

            [JsonPropertyName("gmt")]
            public double Gmt { get; set; }

            [JsonPropertyName("daylightSavingTime")]
            public double DaylightSavingTime { get; set; }

            [JsonPropertyName("isDaylightSaving")]
            public bool IsDaylightSaving { get; set; }

            [JsonPropertyName("dateTime")]
            public string? DateTime { get; set; }

            [JsonPropertyName("dawnTime")]
            public string? DawnTime { get; set; }

            // Alternate/synonyms observed on some payloads
            [JsonPropertyName("falseFajr")]
            public string? FalseFajr { get; set; }
            [JsonPropertyName("imsak")]
            public string? Imsak { get; set; }

            [JsonPropertyName("fajrBeginTime")]
            public string? FajrBeginTime { get; set; }
            [JsonPropertyName("fajr")]
            public string? Fajr { get; set; }

            [JsonPropertyName("fajrEndTime")]
            public string? FajrEndTime { get; set; }
            [JsonPropertyName("sunrise")]
            public string? Sunrise { get; set; }

            [JsonPropertyName("duhrTime")]
            public string? DuhrTime { get; set; }
            [JsonPropertyName("dhuhr")]
            public string? Dhuhr { get; set; }

            [JsonPropertyName("asrTime")]
            public string? AsrTime { get; set; }

            [JsonPropertyName("magrib")]
            public string? Magrib { get; set; }
            [JsonPropertyName("maghrib")]
            public string? Maghrib { get; set; }

            [JsonPropertyName("ishaBeginTime")]
            public string? IshaBeginTime { get; set; }
            [JsonPropertyName("isha")]
            public string? Isha { get; set; }

            [JsonPropertyName("ishaEndTime")]
            public string? IshaEndTime { get; set; }
            [JsonPropertyName("endOfIsha")]
            public string? EndOfIsha { get; set; }
        }
    }
}
