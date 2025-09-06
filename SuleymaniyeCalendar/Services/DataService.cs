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

namespace SuleymaniyeCalendar.Services
{
	public class DataService
	{
		private readonly IAlarmService _alarmService;
		private readonly JsonApiService _jsonApiService;
		public Calendar calendar;
		private Calendar _location;
		private ObservableCollection<Calendar> _monthlyCalendar;
		private bool askedLocationPermission;
		private static readonly HttpClient _xmlHttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
		private const int UnifiedCacheVersion = 1;

		// Primary constructor for DI: JsonApiService is injected
		public DataService(IAlarmService alarmService, JsonApiService jsonApiService)
		{
			_alarmService = alarmService;
			_jsonApiService = jsonApiService;
			calendar = GetTakvimFromFile() ?? new Calendar()
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

		// Backward-compatible constructor for callers without DI (e.g., Android widget fallback)
		public DataService(IAlarmService alarmService) : this(alarmService, new JsonApiService())
		{
		}

		// Robust, culture-invariant parsing for Calendar.Date which may arrive as "dd/MM/yyyy", "dd-MM-yyyy", "yyyy-MM-dd", etc.
		private static readonly string[] KnownDateFormats = new[]
		{
			"dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy",
			"yyyy-MM-dd", "yyyy/MM/dd", "MM/dd/yyyy", "M/d/yyyy",
			"dd.MM.yyyy", "d.MM.yyyy"
		};

		private static bool TryParseCalendarDate(string input, out DateTime date)
		{
			date = default;
			if (string.IsNullOrWhiteSpace(input)) return false;
			var s = input.Trim();
			return DateTime.TryParseExact(s, KnownDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
				|| DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
		}

		private static DateTime ParseCalendarDateOrMin(string input)
		{
			return TryParseCalendarDate(input, out var dt) ? dt : DateTime.MinValue;
		}

		private Calendar GetTakvimFromFile()
		{
			try
			{
				// Use unified cache for today's data
				var location = new Location()
				{
					Latitude = Preferences.Get("LastLatitude", 0.0),
					Longitude = Preferences.Get("LastLongitude", 0.0),
					Altitude = Preferences.Get("LastAltitude", 0.0)
				};

				if (location.Latitude != 0.0 && location.Longitude != 0.0)
				{
					var currentYear = DateTime.Today.Year;
					var cachedYearData = LoadYearCacheAsync(location, currentYear).GetAwaiter().GetResult();
					
					if (cachedYearData != null)
					{
						var todayData = cachedYearData.FirstOrDefault(d => 
							ParseCalendarDateOrMin(d.Date) == DateTime.Today);
						
						if (todayData != null)
						{
							calendar = todayData;
							calendar.Latitude = location.Latitude;
							calendar.Longitude = location.Longitude;
							calendar.Altitude = location.Altitude ?? 0;
							return calendar;
						}
					}
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"GetTakvimFromFile unified cache error: {ex.Message}");
			}
			
			return calendar;
		}

		public async Task<Calendar> PrepareMonthlyPrayerTimes()
		{
			// On app startup, respect the user's auto-renew preference:
			// - When AlwaysRenewLocationEnabled is ON, force a fresh GPS fix to renew location
			// - Otherwise, use last known/saved location to avoid prompting at launch
			var forceLocationRefresh = Preferences.Get("AlwaysRenewLocationEnabled", false);
			var location = await GetCurrentLocationAsync(forceLocationRefresh).ConfigureAwait(false);
			var monthly = await GetMonthlyPrayerTimesHybridAsync(location, true).ConfigureAwait(false);
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

		public async Task<Location> GetCurrentLocationAsync(bool refreshLocation)
		{
			var location = new Location(0, 0);
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
			if (!askedLocationPermission)
			{
				//var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
				//var status = await DependencyService.Get<IPermissionService>().HandlePermissionAsync().ConfigureAwait(false);
				//var permissionService=new PermissionService();
				//var status=await permissionService.HandlePermissionAsync().ConfigureAwait(false);
				var status = PermissionStatus.Unknown;
				if (DeviceInfo.Platform == DevicePlatform.WinUI) { status = PermissionStatus.Granted; }
				else
				{
					//var permissionService = new PermissionService();
					//status = await permissionService.HandlePermissionAsync().ConfigureAwait(false);
					status = await CheckAndRequestLocationPermission().ConfigureAwait(false);
				}
				if (status != PermissionStatus.Granted)
				{
					// Notify user permission was denied
					//UserDialogs.Instance.Alert(AppResources.KonumIzniIcerik, AppResources.KonumIzniBaslik);
					askedLocationPermission = true;
					return location;
				}
			}
			try
			{
				if (!refreshLocation) location = await Geolocation.Default.GetLastKnownLocationAsync().ConfigureAwait(false);

				if (location == null || refreshLocation)
				{

					//var request = new GeolocationRequest(GeolocationAccuracy.Low, TimeSpan.FromSeconds(10));
					//CancellationTokenSource cts = new CancellationTokenSource();
					//location = await Geolocation.GetLocationAsync(request, cts.Token).ConfigureAwait(false);
					//var permissionService = new PermissionService();
					//location = await permissionService.RequestLocationAsync(10).ConfigureAwait(false);
					location = await RequestLocationAsync().ConfigureAwait(false);
					if (location == null) location = new Location(42.142, 29.218, 10);
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
				//else
				//{
				//    if (!DependencyService.Get<IPermissionService>().IsLocationServiceEnabled())
				//        UserDialogs.Instance.Toast(AppResources.KonumKapaliBaslik, TimeSpan.FromSeconds(5));
				//}
			}
			catch (FeatureNotSupportedException fnsEx)
			{
				// Handle not supported on device exception
				//UserDialogs.Instance.Alert(AppResources.CihazGPSDesteklemiyor, AppResources.CihazGPSDesteklemiyor);
				Debug.WriteLine($"**** {this.GetType().Name}.{nameof(GetCurrentLocationAsync)}: {fnsEx.Message}");
			}
			catch (FeatureNotEnabledException fneEx)
			{
				// Handle not enabled on device exception
				Debug.WriteLine($"**** {this.GetType().Name}.{nameof(GetCurrentLocationAsync)}: {fneEx.Message}");
				//UserDialogs.Instance.Alert(AppResources.KonumKapali, AppResources.KonumKapaliBaslik);
				//await App.Current.MainPage.DisplayAlert("Konum Servisi Hatası", "Cihazın konum servisi kapalı, Öce konum servisini açmanız lazım!", "Tamam");
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
				// Handle permission exception
				Debug.WriteLine($"**** {this.GetType().Name}.{nameof(GetCurrentLocationAsync)}: {pEx.Message}");
			}
			catch (Exception ex)
			{
				// Unable to get location
				Debug.WriteLine($"**** {this.GetType().Name}.{nameof(GetCurrentLocationAsync)}: {ex.Message}");
			}

			return location;
		}

		public ObservableCollection<Calendar> GetMonthlyPrayerTimes(Location location, bool forceRefresh = false)
		{
			// **UNIFIED CACHE APPROACH** - Use only the modern unified cache system
			if (!forceRefresh)
			{
				var month = DateTime.Now.Month;
				var year = DateTime.Now.Year;
				var unified = TryGetMonthlyFromUnifiedCacheAsync(location, year, month).GetAwaiter().GetResult();
				if (unified != null)
				{
					_monthlyCalendar = unified;
					return _monthlyCalendar;
				}
			}

			if (!HaveInternet()) return null;
			_location = new Calendar()
			{
				Latitude = location.Latitude,
				Longitude = location.Longitude,
				Altitude = location.Altitude ?? 0,
				TimeZone = TimeZoneInfo.Local.BaseUtcOffset.Hours,
				DayLightSaving = TimeZoneInfo.Local.IsDaylightSavingTime(DateTime.Now) ? 1 : 0,
				Date = DateTime.Today.ToString("dd/MM/yyyy")
			};

			var url =
				$"http://servis.suleymaniyetakvimi.com/servis.asmx/VakitHesabiListesi?" +
				$"Enlem={_location.Latitude.ToString(CultureInfo.InvariantCulture)}" +
				$"&Boylam={_location.Longitude.ToString(CultureInfo.InvariantCulture)}" +
				$"&Yukseklik={_location.Altitude.ToString(CultureInfo.InvariantCulture)}" +
				$"&SaatBolgesi={TimeZoneInfo.Local.BaseUtcOffset.Hours}" +
				$"&yazSaati={(TimeZoneInfo.Local.IsDaylightSavingTime(DateTime.Now) ? 1 : 0)}" +
				$"&Tarih={DateTime.Today:dd/MM/yyyy}";

			try
			{
				XDocument doc = XDocument.Load(url);
				_monthlyCalendar = ParseXmlList(doc, _location.Latitude, _location.Longitude, _location.Altitude);
				// Save to unified cache only (synchronous version)
				_ = Task.Run(async () => await SaveToUnifiedCacheAsync(location, _monthlyCalendar.ToList()));
				return _monthlyCalendar;
			}
			catch (Exception exception)
			{
				Debug.WriteLine(
					$"An error occurred while downloading or parsing the xml file, details: {exception.Message}");
			}

			return _monthlyCalendar;
		}

		/// <summary>
		/// Async monthly XML fetch to avoid blocking with XDocument.Load(url).
		/// Uses unified year cache when available.
		/// </summary>
		public async Task<ObservableCollection<Calendar>> GetMonthlyPrayerTimesXmlAsync(Location location, bool forceRefresh = false)
		{
			if (!forceRefresh)
			{
				var month = DateTime.Now.Month;
				var year = DateTime.Now.Year;
				var cached = await TryGetMonthlyFromUnifiedCacheAsync(location, year, month).ConfigureAwait(false);
				if (cached != null) return cached;
			}

			if (!HaveInternet()) return null;

			var tz = TimeZoneInfo.Local.BaseUtcOffset.Hours;
			var dls = TimeZoneInfo.Local.IsDaylightSavingTime(DateTime.Now) ? 1 : 0;
			var url =
				$"http://servis.suleymaniyetakvimi.com/servis.asmx/VakitHesabiListesi?" +
				$"Enlem={location.Latitude.ToString(CultureInfo.InvariantCulture)}" +
				$"&Boylam={location.Longitude.ToString(CultureInfo.InvariantCulture)}" +
				$"&Yukseklik={(location.Altitude ?? 0).ToString(CultureInfo.InvariantCulture)}" +
				$"&SaatBolgesi={tz}" +
				$"&yazSaati={dls}" +
				$"&Tarih={DateTime.Today:dd/MM/yyyy}";

			try
			{
				var resp = await _xmlHttpClient.GetAsync(url).ConfigureAwait(false);
				resp.EnsureSuccessStatusCode();
				var xml = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
				var doc = XDocument.Parse(xml);
				var result = ParseXmlList(doc, location.Latitude, location.Longitude, location.Altitude ?? 0);
				// Save to unified cache only
				await SaveToUnifiedCacheAsync(location, result.ToList()).ConfigureAwait(false);
				return result;
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Async XML monthly failed: {ex.Message}");
				return null;
			}
		}

		private ObservableCollection<Calendar> ParseXmlList(XDocument doc, double latitude = 0.0, double longitude = 0.0, double altitude = 0.0)
		{
			ObservableCollection<Calendar> monthlyCalendar = new ObservableCollection<Calendar>();
			if (doc.Root == null) return monthlyCalendar;
			foreach (var item in doc.Root.Descendants())
			{
				if (item.Name.LocalName == "TakvimListesi")
				{
					var calendarItem = new Calendar();
					foreach (var subitem in item.Descendants())
					{
						switch (subitem.Name.LocalName)
						{
							case "Tarih":
								calendarItem.Date = subitem.Value;
								break;
							case "Enlem":
								calendarItem.Latitude = Convert.ToDouble(subitem.Value, CultureInfo.InvariantCulture.NumberFormat);
								break;
							case "Boylam":
								calendarItem.Longitude = Convert.ToDouble(subitem.Value, CultureInfo.InvariantCulture.NumberFormat);
								break;
							case "Yukseklik":
								calendarItem.Altitude = Convert.ToDouble(subitem.Value, CultureInfo.InvariantCulture.NumberFormat);
								break;
							case "SaatBolgesi":
								calendarItem.TimeZone = Convert.ToDouble(subitem.Value, CultureInfo.InvariantCulture.NumberFormat);
								break;
							case "YazKis":
								calendarItem.DayLightSaving = Convert.ToDouble(subitem.Value, CultureInfo.InvariantCulture.NumberFormat);
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

					calendarItem.Latitude = calendarItem.Latitude == 0 ? latitude : calendarItem.Latitude;
					calendarItem.Longitude = calendarItem.Longitude == 0 ? longitude : calendarItem.Longitude;
					calendarItem.Altitude = calendarItem.Altitude == 0 ? altitude : calendarItem.Altitude;
					monthlyCalendar.Add(calendarItem);
				}
			}

			return monthlyCalendar;
		}


		private Calendar ParseXml(string xmlResult)
		{
			calendar = new Calendar();
			XDocument doc = XDocument.Parse(xmlResult);
			//if(doc.Descendants("Takvim")!=null)
			if (doc.Root == null) return calendar;
			foreach (var item in doc.Root.Descendants())
			{
				switch (item.Name.LocalName)
				{
					case "Enlem":
						calendar.Latitude = Convert.ToDouble(item.Value, CultureInfo.InvariantCulture.NumberFormat);
						break;
					case "Boylam":
						calendar.Longitude = Convert.ToDouble(item.Value, CultureInfo.InvariantCulture.NumberFormat);
						break;
					case "Yukseklik":
						calendar.Altitude = Convert.ToDouble(item.Value, CultureInfo.InvariantCulture.NumberFormat);
						break;
					case "SaatBolgesi":
						calendar.TimeZone = Convert.ToDouble(item.Value, CultureInfo.InvariantCulture.NumberFormat);
						break;
					case "YazKis":
						calendar.DayLightSaving = Convert.ToDouble(item.Value, CultureInfo.InvariantCulture.NumberFormat);
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

		public bool HaveInternet()
		{
			var current = Connectivity.NetworkAccess;
			if (current != Microsoft.Maui.Networking.NetworkAccess.Internet)
			{
				//UserDialogs.Instance.Toast(AppResources.TakvimIcinInternet, TimeSpan.FromSeconds(7));
				return false;
			}

			return true;
		}

		public async Task<Calendar> GetPrayerTimesFastAsync()
		{
			calendar = GetTakvimFromFile();
			if (calendar != null) return calendar;
			calendar = new Calendar();

			if (!HaveInternet()) return null;
			var location = await GetCurrentLocationAsync(false).ConfigureAwait(false);
			if (location != null)
			{

				var url = "http://servis.suleymaniyetakvimi.com/servis.asmx/VakitHesabi?";
				url += "Enlem=" + location.Latitude;
				url += "&Boylam=" + location.Longitude;
				url += "&Yukseklik=" + location.Altitude;
				url = url.Replace(',', '.');
				url += "&SaatBolgesi=" + TimeZoneInfo.Local.BaseUtcOffset.Hours;//.StandardName;
				url += "&yazSaati=" + (TimeZoneInfo.Local.IsDaylightSavingTime(DateTime.Now) ? 1 : 0);
				url += "&Tarih=" + DateTime.Today.ToString("dd/MM/yyyy");

				//Xml Parsing  
				XDocument doc = XDocument.Load(url);
				if (doc.Root == null) return null;
				foreach (var item in doc.Root.Descendants())
				{
					switch (item.Name.LocalName)
					{
						//Without the Convert.ToDouble conversion it confuses the , and . when UI culture changed. like latitude=50.674367348783 become latitude= 50674367348783 then throw exception.
						case "Enlem":
							calendar.Latitude = Convert.ToDouble(item.Value, CultureInfo.InvariantCulture.NumberFormat);
							break;
						case "Boylam":
							calendar.Longitude = Convert.ToDouble(item.Value, CultureInfo.InvariantCulture.NumberFormat);
							break;
						case "Yukseklik":
							calendar.Altitude =
								Convert.ToDouble(item.Value, CultureInfo.InvariantCulture.NumberFormat);
							break;
						case "SaatBolgesi":
							calendar.TimeZone =
								Convert.ToDouble(item.Value, CultureInfo.InvariantCulture.NumberFormat);
							break;
						case "YazKis":
							calendar.DayLightSaving = Convert.ToDouble(item.Value, CultureInfo.InvariantCulture.NumberFormat);
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
			}

			return calendar;
		}

		//When refreshLocation true, force refresh location not using last known location.
		public async Task<Calendar> GetPrayerTimesAsync(bool refreshLocation)
		{
			//Analytics.TrackEvent("GetPrayerTimes in the DataService Triggered: " + $" at {DateTime.Now}");
			Debug.WriteLine("TimeStamp-GetPrayerTimes-Start", DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss.fff tt"));

			try
			{
				if (!HaveInternet()) return null;
				var location = await GetCurrentLocationAsync(refreshLocation).ConfigureAwait(false);
				var loc = new Calendar()
				{
					Latitude = location.Latitude,
					Longitude = location.Longitude,
					Altitude = location.Altitude ?? 0,
					TimeZone = TimeZoneInfo.Local.BaseUtcOffset.Hours, //.StandardName;
					DayLightSaving = TimeZoneInfo.Local.IsDaylightSavingTime(DateTime.Now) ? 1 : 0,
					Date = DateTime.Today.ToString("dd/MM/yyyy")
				};
				DateTime now = DateTime.Now;
				DateTime withKind = DateTime.SpecifyKind(now, DateTimeKind.Local);
				Debug.WriteLine("{0} - Time is ambiguous? {1}", TimeZoneInfo.Local.DaylightName, TimeZoneInfo.Local.IsAmbiguousTime(withKind));
				Debug.WriteLine("{0} - Time is daylight saving time? {1}", TimeZoneInfo.Local.StandardName, TimeZoneInfo.Local.IsDaylightSavingTime(withKind));
				Debug.WriteLine("{0} - Time is support daylight saving time? {1}", TimeZoneInfo.Local.DisplayName, TimeZoneInfo.Local.SupportsDaylightSavingTime);
				if (TimeZoneInfo.Local.IsAmbiguousTime(withKind) || TimeZoneInfo.Local.SupportsDaylightSavingTime ||
					TimeZoneInfo.Local.IsDaylightSavingTime(withKind))
					Debug.WriteLine("{0} may be daylight saving time in {1}.",
						withKind, TimeZoneInfo.Local.DisplayName);
				var client = new HttpClient();
				var uri = new Uri($"http://servis.suleymaniyetakvimi.com/servis.asmx/" +
								  $"VakitHesabi?Enlem={Convert.ToDouble(loc.Latitude, CultureInfo.InvariantCulture.NumberFormat)}" +
								  $"&Boylam={Convert.ToDouble(loc.Longitude, CultureInfo.InvariantCulture.NumberFormat)}" +
								  $"&Yukseklik={Convert.ToDouble(loc.Altitude, CultureInfo.InvariantCulture.NumberFormat)}" +
								  $"&SaatBolgesi={loc.TimeZone}&yazSaati={loc.DayLightSaving}&Tarih={loc.Date}");
				var response = await client.GetAsync(uri).ConfigureAwait(false);
				var xmlResult = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
				if (!string.IsNullOrEmpty(xmlResult) && xmlResult.StartsWith("<?xml"))
				{
					calendar = ParseXml(xmlResult);
					ShowToast(AppResources.KonumYenilendi);
				}
				else
					ShowToast(AppResources.NamazVaktiAlmaHatasi);

			}
			catch (Exception exception)
			{
				Alert(exception.Message, AppResources.KonumHatasi);
			}

			Debug.WriteLine("TimeStamp-GetPrayerTimes-Finish", DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss.fff tt"));
			return calendar;
		}

		public async Task SetMonthlyAlarmsAsync()
		{
			Debug.WriteLine("TimeStamp-SetWeeklyAlarms-Start", DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss.fff tt"));
			_alarmService.CancelAlarm();
			if (CheckRemindersEnabledAny())
			{
				try
				{
					// Ensure we have 30 days of data starting today, potentially spanning next month/year
					var location = await GetCurrentLocationAsync(false).ConfigureAwait(false);
					if (location != null && location.Latitude != 0 && location.Longitude != 0)
					{
						var next30Days = await EnsureDaysRangeAsync(location, DateTime.Today, 30).ConfigureAwait(false);
						if (next30Days == null || next30Days.Count == 0)
						{
							ShowToast(AppResources.AylikTakvimeErisemedi);
							return;
						}

						int dayCounter = 0;
						foreach (var todayCalendar in next30Days.OrderBy(d => ParseCalendarDateOrMin(d.Date)))
						{
							var todayDate = ParseCalendarDateOrMin(todayCalendar.Date);
							if (todayDate >= DateTime.Today)
							{
								var baseDate = todayDate;
								var falseFajrTime = TimeSpan.Parse(todayCalendar.FalseFajr);
								var fajrTime = TimeSpan.Parse(todayCalendar.Fajr);
								var sunriseTime = TimeSpan.Parse(todayCalendar.Sunrise);
								var dhuhrTime = TimeSpan.Parse(todayCalendar.Dhuhr);
								var asrTime = TimeSpan.Parse(todayCalendar.Asr);
								var maghribTime = TimeSpan.Parse(todayCalendar.Maghrib);
								var ishaTime = TimeSpan.Parse(todayCalendar.Isha);
								var endOfIshaTime = TimeSpan.Parse(todayCalendar.EndOfIsha);

								var falseFajr = baseDate + falseFajrTime - TimeSpan.FromMinutes(Preferences.Get("falsefajrNotificationTime", 0));
								var fajr = baseDate + fajrTime - TimeSpan.FromMinutes(Preferences.Get("fajrNotificationTime", 0));
								var sunrise = baseDate + sunriseTime - TimeSpan.FromMinutes(Preferences.Get("sunriseNotificationTime", 0));
								var dhuhr = baseDate + dhuhrTime - TimeSpan.FromMinutes(Preferences.Get("dhuhrNotificationTime", 0));
								var asr = baseDate + asrTime - TimeSpan.FromMinutes(Preferences.Get("asrNotificationTime", 0));
								var maghrib = baseDate + maghribTime - TimeSpan.FromMinutes(Preferences.Get("maghribNotificationTime", 0));
								var isha = baseDate + ishaTime - TimeSpan.FromMinutes(Preferences.Get("ishaNotificationTime", 0));
								var endOfIsha = baseDate + endOfIshaTime - TimeSpan.FromMinutes(Preferences.Get("endofishaNotificationTime", 0));

								// Use canonical Turkish names to match receiver/channel mappings and request-code strategy
								if (DateTime.Now < falseFajr && Preferences.Get("falsefajrEnabled", false)) _alarmService.SetAlarm(baseDate, falseFajrTime, Preferences.Get("falsefajrNotificationTime", 0), "Fecri Kazip");
								if (DateTime.Now < fajr && Preferences.Get("fajrEnabled", false)) _alarmService.SetAlarm(baseDate, fajrTime, Preferences.Get("fajrNotificationTime", 0), "Fecri Sadık");
								if (DateTime.Now < sunrise && Preferences.Get("sunriseEnabled", false)) _alarmService.SetAlarm(baseDate, sunriseTime, Preferences.Get("sunriseNotificationTime", 0), "Sabah Sonu");
								if (DateTime.Now < dhuhr && Preferences.Get("dhuhrEnabled", false)) _alarmService.SetAlarm(baseDate, dhuhrTime, Preferences.Get("dhuhrNotificationTime", 0), "Öğle");
								if (DateTime.Now < asr && Preferences.Get("asrEnabled", false)) _alarmService.SetAlarm(baseDate, asrTime, Preferences.Get("asrNotificationTime", 0), "İkindi");
								if (DateTime.Now < maghrib && Preferences.Get("maghribEnabled", false)) _alarmService.SetAlarm(baseDate, maghribTime, Preferences.Get("maghribNotificationTime", 0), "Akşam");
								if (DateTime.Now < isha && Preferences.Get("ishaEnabled", false)) _alarmService.SetAlarm(baseDate, ishaTime, Preferences.Get("ishaNotificationTime", 0), "Yatsı");
								if (DateTime.Now < endOfIsha && Preferences.Get("endofishaEnabled", false)) _alarmService.SetAlarm(baseDate, endOfIshaTime, Preferences.Get("endofishaNotificationTime", 0), "Yatsı Sonu");
								dayCounter++;
								if (dayCounter >= 30) break;
							}
						}
					}
					else
					{
						ShowToast(AppResources.AylikTakvimeErisemedi);
					}
				}
				catch (Exception exception)
				{
					Debug.WriteLine($"**** {this.GetType().Name}.{nameof(GetCurrentLocationAsync)}: {exception.Message}");
				}

				Preferences.Set("LastAlarmDate", DateTime.Today.AddDays(30).ToShortDateString());
			}
			Debug.WriteLine("TimeStamp-SetMonthlyAlarms-Finish", DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss.fff tt"));
		}

		// Returns >= daysNeeded Calendar entries starting at startDate, potentially spanning next month/year.
		// Uses unified yearly JSON cache keyed by location/year; fills gaps via JSON monthly (same year) or JSON daily (any date).
		private async Task<List<Calendar>> EnsureDaysRangeAsync(Location location, DateTime startDate, int daysNeeded)
		{
			// Invalidate yearly caches if location changed meaningfully
			ClearYearCachesIfLocationChanged(location);

			var result = new List<Calendar>();
			var endDate = startDate.AddDays(daysNeeded - 1);

			// Load caches for years involved
			var years = Enumerable.Range(startDate.Year, endDate.Year - startDate.Year + 1).ToArray();
			var yearCaches = new Dictionary<int, List<Calendar>>();
			foreach (var y in years)
			{
				var cached = await LoadYearCacheAsync(location, y).ConfigureAwait(false);
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

				// Merge into cache and persist
				if (fetched != null && fetched.Count > 0)
				{
					var toAdd = fetched.ToList();
					if (!yearCaches.ContainsKey(year)) yearCaches[year] = new List<Calendar>();
					yearCaches[year] = MergeCalendars(yearCaches[year], toAdd);
					await SaveYearCacheAsync(location, year, yearCaches[year]).ConfigureAwait(false);
					return toAdd;
				}

				return new List<Calendar>();
			}

			// Collect days covering the requested span
			var cursor = new DateTime(startDate.Year, startDate.Month, 1);
			while (cursor <= endDate)
			{
				var monthDays = await GetMonthAsync(cursor.Year, cursor.Month).ConfigureAwait(false);
				result.AddRange(monthDays);
				cursor = cursor.AddMonths(1);
			}

			// Filter to range and ensure order/distinct
			var inRange = result
				.Where(d =>
				{
					if (!DateTime.TryParse(d.Date, out var dd)) return false;
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

		private string GetYearCachePath(Location location, int year)
		{
			var lat = Math.Round(location.Latitude, 4).ToString(CultureInfo.InvariantCulture);
			var lon = Math.Round(location.Longitude, 4).ToString(CultureInfo.InvariantCulture);
			var file = $"prayercache_{lat}_{lon}_{year}.json";
			return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), file);
		}

		private async Task<List<Calendar>> LoadYearCacheAsync(Location location, int year)
		{
			try
			{
				var path = GetYearCachePath(location, year);
				if (!File.Exists(path)) return null;
				var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
				var wrapper = JsonSerializer.Deserialize<YearCacheWrapper>(json);
				if (wrapper != null && wrapper.Version == UnifiedCacheVersion && wrapper.Year == year)
				{
					return wrapper.Days ?? new List<Calendar>();
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Year cache load failed: {ex.Message}");
			}
			return null;
		}

		private async Task SaveYearCacheAsync(Location location, int year, List<Calendar> days)
		{
			try
			{
				var wrapper = new YearCacheWrapper
				{
					Version = UnifiedCacheVersion,
					Latitude = location.Latitude,
					Longitude = location.Longitude,
					Altitude = location.Altitude ?? 0,
					Year = year,
					Days = days
				};
				var json = JsonSerializer.Serialize(wrapper, new JsonSerializerOptions { WriteIndented = false });
				var path = GetYearCachePath(location, year);
				await File.WriteAllTextAsync(path, json).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Year cache save failed: {ex.Message}");
			}
		}

		// ---------------------
		// Unified Prayer builders
		// These provide a single source of truth to map a Calendar day into UI-ready Prayer objects
		// and to persist the day's raw times to Preferences. Keeping Calendar as the canonical
		// storage/cache format is simpler and API-agnostic; Prayer carries UI state and toggles.
		// ---------------------
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

		public async Task<List<Prayer>> GetPrayersForDateAsync(DateTime date, bool forceRefresh = false)
		{
			var location = await GetCurrentLocationAsync(false).ConfigureAwait(false);
			var days = await EnsureDaysRangeAsync(location, date.Date, 1).ConfigureAwait(false);
			var day = days?.FirstOrDefault();
			return BuildPrayersFromCalendar(day);
		}

		/// <summary>
		/// Ensure that today's Calendar entry exists in the unified yearly cache and update the in-memory calendar.
		/// Lightweight for returning users when auto-renew is off. Uses last-known location and JSON monthly/daily.
		/// </summary>
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

		private sealed class YearCacheWrapper
		{
			public int Version { get; set; }
			public double Latitude { get; set; }
			public double Longitude { get; set; }
			public double Altitude { get; set; }
			public int Year { get; set; }
			public List<Calendar> Days { get; set; }
		}

		private void ClearYearCachesIfLocationChanged(Location location)
		{
			try
			{
				const double threshold = 0.02; // ~2km latitude delta; coarse but effective
				var lastLat = Preferences.Get("CacheLastLat", double.NaN);
				var lastLon = Preferences.Get("CacheLastLon", double.NaN);
				if (double.IsNaN(lastLat) || double.IsNaN(lastLon) ||
					Math.Abs(lastLat - location.Latitude) > threshold ||
					Math.Abs(lastLon - location.Longitude) > threshold)
				{
					var dir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
					var files = Directory.GetFiles(dir, "prayercache_*.json");
					foreach (var f in files)
					{
						try { File.Delete(f); } catch { /* ignore */ }
					}
					Preferences.Set("CacheLastLat", location.Latitude);
					Preferences.Set("CacheLastLon", location.Longitude);
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Cache invalidation check failed: {ex.Message}");
			}
		}

		// Unified cache helpers
		private async Task<ObservableCollection<Calendar>> TryGetMonthlyFromUnifiedCacheAsync(Location location, int year, int month)
		{
			try
			{
				var list = await LoadYearCacheAsync(location, year).ConfigureAwait(false);
				if (list == null || list.Count == 0) return null;
					var monthDays = list.Where(d =>
					{
						var dd = ParseCalendarDateOrMin(d.Date);
						if (dd == DateTime.MinValue) return false;
						return dd.Year == year && dd.Month == month;
					}).OrderBy(d => ParseCalendarDateOrMin(d.Date)).ToList();
				if (monthDays.Count >= 28)
					return new ObservableCollection<Calendar>(monthDays);
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Unified cache monthly read failed: {ex.Message}");
			}
			return null;
		}

		private async Task SaveToUnifiedCacheAsync(Location location, List<Calendar> days)
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
					var existing = await LoadYearCacheAsync(location, year).ConfigureAwait(false) ?? new List<Calendar>();
					existing = MergeCalendars(existing, g.ToList());
					await SaveYearCacheAsync(location, year, existing).ConfigureAwait(false);
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Unified cache save failed: {ex.Message}");
			}
		}

		private bool CheckRemindersEnabledAny()
		{
			return Preferences.Get("falsefajrEnabled", false) || Preferences.Get("fajrEnabled", false) ||
				   Preferences.Get("sunriseEnabled", false) || Preferences.Get("dhuhrEnabled", false) ||
				   Preferences.Get("asrEnabled", false) || Preferences.Get("maghribEnabled", false) ||
				   Preferences.Get("ishaEnabled", false) || Preferences.Get("endofishaEnabled", false);
		}

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

		public static void Alert(string title, string message)
		{
			MainThread.BeginInvokeOnMainThread(async () =>
			{
				await Shell.Current.DisplayAlert(title, message, AppResources.Tamam);
			});
		}

		public async Task<PermissionStatus> CheckAndRequestLocationPermission()
		{
			var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
			if (status == PermissionStatus.Granted)
				return status;

			if (Permissions.ShouldShowRationale<Permissions.LocationWhenInUse>())
			{
				Alert(AppResources.KonumIzniBaslik, AppResources.KonumIzniIcerik);
			}

			status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
			if (status == PermissionStatus.Denied && DeviceInfo.Platform == DevicePlatform.iOS)
			{
				// On iOS once denied it may require manual settings
				Alert(AppResources.KonumIzniBaslik, AppResources.KonumIzniIcerik);
			}
			else if (status == PermissionStatus.Denied)
			{
				MainThread.BeginInvokeOnMainThread(() => AppInfo.ShowSettingsUI());
			}

			return status;
		}
		public async Task<Location> RequestLocationAsync(int waitDelay = 10)
		{
			Location location = null;
			try
			{
				var request = new GeolocationRequest(GeolocationAccuracy.Low, TimeSpan.FromSeconds(waitDelay));
				CancellationTokenSource cts = new CancellationTokenSource();
				if (DeviceInfo.Platform == DevicePlatform.WinUI)
				{
					Application.Current?.Dispatcher.Dispatch(async () =>
					{
						location = await Geolocation.Default.GetLocationAsync(request, cts.Token).ConfigureAwait(false);
						if (location != null)
							Debug.WriteLine($"Latitude: {location.Latitude}, Longitude: {location.Longitude}, Altitude: {location.Altitude}");
					});
				}
				else
				{
					//MainThread.BeginInvokeOnMainThread(async () =>
					//{
					location = await Geolocation.Default.GetLocationAsync(request, cts.Token).ConfigureAwait(false);
					//});
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

		#region Hybrid API Methods (New JSON + Old XML Fallback)

		/// <summary>
		/// Get monthly prayer times using hybrid approach: Try new JSON API first, fallback to old XML API
		/// </summary>
		public async Task<ObservableCollection<Calendar>> GetMonthlyPrayerTimesHybridAsync(Location location, bool forceRefresh = false)
		{
			Debug.WriteLine("Starting Hybrid Monthly Prayer Times request");

			// Try unified cache first if not forcing refresh
			if (!forceRefresh)
			{
				var month = DateTime.Now.Month;
				var year = DateTime.Now.Year;
				var unified = await TryGetMonthlyFromUnifiedCacheAsync(location, year, month).ConfigureAwait(false);
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
				var jsonResult = await _jsonApiService.GetMonthlyPrayerTimesAsync(
					location.Latitude, location.Longitude, currentMonth, altitude);
				
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
				var xmlResult = await GetMonthlyPrayerTimesXmlAsync(location, forceRefresh).ConfigureAwait(false);
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
		/// Get daily prayer times using hybrid approach: Try new JSON API first, fallback to old XML API
		/// </summary>
		public async Task<Calendar> GetDailyPrayerTimesHybridAsync(Location location, DateTime? date = null)
		{
			if (!HaveInternet()) return null;

			var targetDate = date ?? DateTime.Today;
			var altitude = location.Altitude ?? 0;

			// Strategy 1: Try new JSON API first
			Debug.WriteLine("Hybrid Daily: Trying new JSON API");
			try
			{
				var jsonResult = await _jsonApiService.GetDailyPrayerTimesAsync(
					location.Latitude, location.Longitude, targetDate, altitude);
				
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
				var xmlResult = await GetPrayerTimesAsync(false);
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
		/// Try to get monthly data from unified cache only
		/// </summary>
		private async Task<ObservableCollection<Calendar>> TryGetMonthlyFromCacheAsync()
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
		/// Test which API is faster/available
		/// </summary>
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
					var client = new HttpClient();
					client.Timeout = TimeSpan.FromSeconds(10);
					var response = await client.GetAsync("http://servis.suleymaniyetakvimi.com/servis.asmx");
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
		/// Get today's prayer times using hybrid approach - used by MainViewModel
		/// </summary>
		public async Task<Calendar> GetPrayerTimesHybridAsync(bool refreshLocation = false)
		{
			var location = await GetCurrentLocationAsync(refreshLocation).ConfigureAwait(false);
			if (location == null || location.Latitude == 0 || location.Longitude == 0)
				return GetTakvimFromFile() ?? calendar;

			return await GetDailyPrayerTimesHybridAsync(location);
		}

		/// <summary>
		/// Get today's prayer times using hybrid approach - parameterless version for backward compatibility
		/// </summary>
		public async Task<Calendar> GetPrayerTimesHybridAsync()
		{
			return await GetPrayerTimesHybridAsync(false);
		}

		#endregion
	}
}
