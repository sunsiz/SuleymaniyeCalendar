using System.Globalization;
using System.Text.Json;
using SuleymaniyeCalendar.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Calendar = SuleymaniyeCalendar.Models.Calendar;

namespace SuleymaniyeCalendar.Services
{
    /// <summary>
    /// Service for interacting with the new JSON-based API at api.suleymaniyetakvimi.com
    /// </summary>
    public class JsonApiService
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "https://api.suleymaniyetakvimi.com/api/";

        public JsonApiService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        /// <summary>
        /// Get prayer times for a specific date using new JSON API
        /// </summary>
        public async Task<Calendar> GetDailyPrayerTimesAsync(double latitude, double longitude, DateTime date, double altitude = 0)
        {
            try
            {
                var url = $"{BaseUrl}TimeCalculation/TimeCalculate" +
                         $"?latitude={latitude.ToString(CultureInfo.InvariantCulture)}" +
                         $"&longitude={longitude.ToString(CultureInfo.InvariantCulture)}" +
                         $"&date={date:yyyy-MM-dd}";

                Debug.WriteLine($"JSON API Daily Request: {url}");

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"JSON API Daily failed with status: {response.StatusCode}");
                    return null;
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                var jsonResponse = JsonSerializer.Deserialize<JsonPrayerTimeResponse>(jsonContent);

                if (jsonResponse?.IsSuccess == true && jsonResponse.Data != null)
                {
                    return ConvertJsonDataToCalendar(jsonResponse.Data, altitude);
                }

                Debug.WriteLine($"JSON API Daily response not successful: {jsonResponse?.Message}");
                return null;
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
        public async Task<ObservableCollection<Calendar>> GetMonthlyPrayerTimesAsync(double latitude, double longitude, int monthId, double altitude = 0)
        {
            try
            {
                var url = $"{BaseUrl}TimeCalculation/TimeCalculateByMonth" +
                         $"?latitude={latitude.ToString(CultureInfo.InvariantCulture)}" +
                         $"&longitude={longitude.ToString(CultureInfo.InvariantCulture)}" +
                         $"&monthId={monthId}";

                Debug.WriteLine($"JSON API Monthly Request: {url}");

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"JSON API Monthly failed with status: {response.StatusCode}");
                    return null;
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                var jsonResponse = JsonSerializer.Deserialize<JsonMonthlyPrayerTimeResponse>(jsonContent);

                if (jsonResponse?.IsSuccess == true && jsonResponse.Data != null)
                {
                    var result = new ObservableCollection<Calendar>();
                    foreach (var item in jsonResponse.Data)
                    {
                        var calendar = ConvertJsonDataToCalendar(item, altitude);
                        if (calendar != null)
                        {
                            result.Add(calendar);
                        }
                    }
                    Debug.WriteLine($"JSON API Monthly returned {result.Count} days");
                    return result;
                }

                Debug.WriteLine($"JSON API Monthly response not successful: {jsonResponse?.Message}");
                return null;
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
        private Calendar ConvertJsonDataToCalendar(JsonPrayerTimeData data, double fallbackAltitude = 0)
        {
            try
            {
                return new Calendar
                {
                    Date = data.Date,
                    Latitude = data.Latitude,
                    Longitude = data.Longitude,
                    Altitude = data.Altitude == 0 ? fallbackAltitude : data.Altitude,
                    TimeZone = data.TimeZone,
                    DayLightSaving = data.DayLightSaving,
                    FalseFajr = data.FalseFajr,
                    Fajr = data.Fajr,
                    Sunrise = data.Sunrise,
                    Dhuhr = data.Dhuhr,
                    Asr = data.Asr,
                    Maghrib = data.Maghrib,
                    Isha = data.Isha,
                    EndOfIsha = data.EndOfIsha
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
                var url = $"{BaseUrl}TimeCalculation/warmup";
                var response = await _httpClient.GetAsync(url);
                var isSuccess = response.IsSuccessStatusCode;
                Debug.WriteLine($"JSON API connectivity test: {(isSuccess ? "SUCCESS" : "FAILED")}");
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
            _httpClient?.Dispose();
        }
    }
}
