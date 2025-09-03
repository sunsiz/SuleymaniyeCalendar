using System.Text.Json.Serialization;

namespace SuleymaniyeCalendar.Models
{
    // JSON API Response Models for new api.suleymaniyetakvimi.com API

    public class JsonPrayerTimeResponse
    {
        [JsonPropertyName("isSuccess")]
        public bool IsSuccess { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("data")]
        public JsonPrayerTimeData Data { get; set; }
    }

    public class JsonPrayerTimeData
    {
        [JsonPropertyName("date")]
        public string Date { get; set; }

        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }

        [JsonPropertyName("altitude")]
        public double Altitude { get; set; }

        [JsonPropertyName("timeZone")]
        public double TimeZone { get; set; }

        [JsonPropertyName("dayLightSaving")]
        public double DayLightSaving { get; set; }

        [JsonPropertyName("falseFajr")]
        public string FalseFajr { get; set; }

        [JsonPropertyName("fajr")]
        public string Fajr { get; set; }

        [JsonPropertyName("sunrise")]
        public string Sunrise { get; set; }

        [JsonPropertyName("dhuhr")]
        public string Dhuhr { get; set; }

        [JsonPropertyName("asr")]
        public string Asr { get; set; }

        [JsonPropertyName("maghrib")]
        public string Maghrib { get; set; }

        [JsonPropertyName("isha")]
        public string Isha { get; set; }

        [JsonPropertyName("endOfIsha")]
        public string EndOfIsha { get; set; }
    }

    public class JsonMonthlyPrayerTimeResponse
    {
        [JsonPropertyName("isSuccess")]
        public bool IsSuccess { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("data")]
        public List<JsonPrayerTimeData> Data { get; set; }
    }
}
