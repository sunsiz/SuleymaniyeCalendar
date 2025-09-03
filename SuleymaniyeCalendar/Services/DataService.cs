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
		public readonly string _fileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "monthlycalendar.xml");
		public readonly string _jsonFileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "monthlycalendar.json");
		private bool askedLocationPermission;

		public DataService(IAlarmService alarmService)
		{
			_alarmService = alarmService;
			_jsonApiService = new JsonApiService();
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

		private Calendar GetTakvimFromFile()
		{
			if (File.Exists(_fileName))
			{
				try
				{
					XDocument xmldoc = XDocument.Load(_fileName);
					var days = ParseXmlList(xmldoc);
					if (days != null && DateTime.Parse(days[0].Date) <= DateTime.Today &&
						DateTime.Parse(days[days.Count() - 1].Date) >= DateTime.Today)
					{
						foreach (var item in days)
						{
							if (DateTime.Parse(item.Date) == DateTime.Today)
							{
								calendar = item;
								calendar.Latitude = Preferences.Get("LastLatitude", 0.0);
								calendar.Longitude = Preferences.Get("LastLongitude", 0.0);
								calendar.Altitude = Preferences.Get("LastAltitude", 0.0);
								return calendar;
							}
						}
					}
				}
				catch (Exception ex)
				{
					Debug.WriteLine(ex.Message);
				}
			}
			return calendar;
		}

		public async Task<Calendar> PrepareMonthlyPrayerTimes()
		{
			var location = await GetCurrentLocationAsync(true).ConfigureAwait(false);
			var monthly = GetMonthlyPrayerTimes(location, true);
			if (monthly != null && monthly.Count > 0)
			{
				// Pick today's entry; fallback to closest
				var today = monthly.FirstOrDefault(d => DateTime.Parse(d.Date) == DateTime.Today)
						   ?? monthly.OrderBy(d => Math.Abs((DateTime.Parse(d.Date) - DateTime.Today).Days)).First();
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
			if (File.Exists(_fileName) && !forceRefresh)
			{
				try
				{
					XDocument xmldoc = XDocument.Load(_fileName);
					var calendarDays = ParseXmlList(xmldoc);
					xmldoc = null;
					if (calendarDays != null)
					{
						var days = (DateTime.Today - DateTime.Parse(calendarDays[0].Date)).Days;
						if (days is < 21 and >= 0)
						{
							_monthlyCalendar = calendarDays;
							return _monthlyCalendar;
						}
					}

					if (!HaveInternet()) return _monthlyCalendar = calendarDays;
				}
				catch (Exception exception)
				{
					Debug.WriteLine($"An error occurred while reading or parsing the file, details: {exception.Message}");
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

			var url = "http://servis.suleymaniyetakvimi.com/servis.asmx/VakitHesabiListesi?";
			url += "Enlem=" + _location.Latitude;
			url += "&Boylam=" + _location.Longitude;
			url += "&Yukseklik=" + _location.Altitude;
			url = url.Replace(',', '.');
			url += "&SaatBolgesi=" + TimeZoneInfo.Local.BaseUtcOffset.Hours; //.StandardName;
			url += "&yazSaati=" + (TimeZoneInfo.Local.IsDaylightSavingTime(DateTime.Now) ? 1 : 0);
			url += "&Tarih=" + DateTime.Today.ToString("dd/MM/yyyy");

			try
			{
				XDocument doc = XDocument.Load(url);
				_monthlyCalendar = ParseXmlList(doc, _location.Latitude, _location.Longitude, _location.Altitude);
				WriteTakvimFile(doc.ToString());
				return _monthlyCalendar;
			}
			catch (Exception exception)
			{
				Debug.WriteLine(
					$"An error occurred while downloading or parsing the xml file, details: {exception.Message}");
			}

			return _monthlyCalendar;
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
								calendarItem.Latitude = Convert.ToDouble(subitem.Value);
								break;
							case "Boylam":
								calendarItem.Longitude = Convert.ToDouble(subitem.Value);
								break;
							case "Yukseklik":
								calendarItem.Altitude = Convert.ToDouble(subitem.Value);
								break;
							case "SaatBolgesi":
								calendarItem.TimeZone = Convert.ToDouble(subitem.Value);
								break;
							case "YazKis":
								calendarItem.DayLightSaving = Convert.ToDouble(subitem.Value);
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
						calendar.Latitude = Convert.ToDouble(item.Value);
						break;
					case "Boylam":
						calendar.Longitude = Convert.ToDouble(item.Value);
						break;
					case "Yukseklik":
						calendar.Altitude = Convert.ToDouble(item.Value);
						break;
					case "SaatBolgesi":
						calendar.TimeZone = Convert.ToDouble(item.Value);
						break;
					case "YazKis":
						calendar.DayLightSaving = Convert.ToDouble(item.Value);
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

		private void WriteTakvimFile(string fileContent)
		{
			File.WriteAllText(_fileName, fileContent);
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

				Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("en");
				var uri = new Uri("http://servis.suleymaniyetakvimi.com/servis.asmx/" +
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

		public async Task SetWeeklyAlarmsAsync()
		{
			Debug.WriteLine("TimeStamp-SetWeeklyAlarms-Start", DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss.fff tt"));
			_alarmService.CancelAlarm();
			if (CheckRemindersEnabledAny())
			{
				try
				{
					if (File.Exists(_fileName))
					{
						XDocument xmldoc = XDocument.Load(_fileName);
						var latitude = calendar.Latitude == 0 ? Preferences.Get("LastLatitude", 0.0) : calendar.Latitude;
						var longitude = calendar.Longitude == 0 ? Preferences.Get("LastLongitude", 0.0) : calendar.Longitude;
						var altitude = calendar.Altitude == 0 ? Preferences.Get("LastAltitude", 0.0) : calendar.Altitude;
						var calendars = ParseXmlList(xmldoc, latitude, longitude, altitude);
						if (calendars != null && (DateTime.Parse(calendars[calendars.Count - 1].Date) - DateTime.Today).Days > 3 && calendars[0].Latitude != 0)
						{
							_monthlyCalendar = calendars;
						}
						else
						{
							var location = await GetCurrentLocationAsync(false).ConfigureAwait(false);
							if (location != null && location.Latitude != 0 && location.Longitude != 0)
							{
								_monthlyCalendar = GetMonthlyPrayerTimes(location, false);
								if (_monthlyCalendar == null)
								{
									Alert(AppResources.TakvimIcinInternet, AppResources.TakvimIcinInternetBaslik);
									return;
								}
							}
						}
					}
					else
					{
						var location = await GetCurrentLocationAsync(false).ConfigureAwait(false);
						if (location != null && location.Latitude != 0 && location.Longitude != 0)
						{
							_monthlyCalendar = GetMonthlyPrayerTimes(location, false);
							if (_monthlyCalendar == null)
							{
								Alert(AppResources.TakvimIcinInternet, AppResources.TakvimIcinInternetBaslik);
								return;
							}
						}
					}

					if (_monthlyCalendar != null)
					{
						int dayCounter = 0;
						foreach (Calendar todayCalendar in _monthlyCalendar)
						{
							if (DateTime.Parse(todayCalendar.Date) >= DateTime.Today)
							{
								var falseFajrTime = TimeSpan.Parse(todayCalendar.FalseFajr);
								var fajrTime = TimeSpan.Parse(todayCalendar.Fajr);
								var sunriseTime = TimeSpan.Parse(todayCalendar.Sunrise);
								var dhuhrTime = TimeSpan.Parse(todayCalendar.Dhuhr);
								var asrTime = TimeSpan.Parse(todayCalendar.Asr);
								var maghribTime = TimeSpan.Parse(todayCalendar.Maghrib);
								var ishaTime = TimeSpan.Parse(todayCalendar.Isha);
								var endOfIshaTime = TimeSpan.Parse(todayCalendar.EndOfIsha);
								var falseFajr = DateTime.Parse(todayCalendar.Date) + falseFajrTime - TimeSpan.FromMinutes(Preferences.Get("falsefajrNotificationTime", 0));
								var fajr = DateTime.Parse(todayCalendar.Date) + fajrTime - TimeSpan.FromMinutes(Preferences.Get("fajrNotificationTime", 0));
								var sunrise = DateTime.Parse(todayCalendar.Date) + sunriseTime - TimeSpan.FromMinutes(Preferences.Get("sunriseNotificationTime", 0));
								var dhuhr = DateTime.Parse(todayCalendar.Date) + dhuhrTime - TimeSpan.FromMinutes(Preferences.Get("dhuhrNotificationTime", 0));
								var asr = DateTime.Parse(todayCalendar.Date) + asrTime - TimeSpan.FromMinutes(Preferences.Get("asrNotificationTime", 0));
								var maghrib = DateTime.Parse(todayCalendar.Date) + maghribTime - TimeSpan.FromMinutes(Preferences.Get("maghribNotificationTime", 0));
								var isha = DateTime.Parse(todayCalendar.Date) + ishaTime - TimeSpan.FromMinutes(Preferences.Get("ishaNotificationTime", 0));
								var endOfIsha = DateTime.Parse(todayCalendar.Date) + endOfIshaTime - TimeSpan.FromMinutes(Preferences.Get("endofishaNotificationTime", 0));
								if (DateTime.Now < falseFajr && Preferences.Get("falsefajrEnabled", false)) _alarmService.SetAlarm(DateTime.Parse(todayCalendar.Date), falseFajrTime, Preferences.Get("falsefajrNotificationTime", 0), "False Fajr");
								if (DateTime.Now < fajr && Preferences.Get("fajrEnabled", false)) _alarmService.SetAlarm(DateTime.Parse(todayCalendar.Date), fajrTime, Preferences.Get("fajrNotificationTime", 0), "Fajr");
								if (DateTime.Now < sunrise && Preferences.Get("sunriseEnabled", false)) _alarmService.SetAlarm(DateTime.Parse(todayCalendar.Date), sunriseTime, Preferences.Get("sunriseNotificationTime", 0), "Sunrise");
								if (DateTime.Now < dhuhr && Preferences.Get("dhuhrEnabled", false)) _alarmService.SetAlarm(DateTime.Parse(todayCalendar.Date), dhuhrTime, Preferences.Get("dhuhrNotificationTime", 0), "Dhuhr");
								if (DateTime.Now < asr && Preferences.Get("asrEnabled", false)) _alarmService.SetAlarm(DateTime.Parse(todayCalendar.Date), asrTime, Preferences.Get("asrNotificationTime", 0), "Asr");
								if (DateTime.Now < maghrib && Preferences.Get("maghribEnabled", false)) _alarmService.SetAlarm(DateTime.Parse(todayCalendar.Date), maghribTime, Preferences.Get("maghribNotificationTime", 0), "Maghrib");
								if (DateTime.Now < isha && Preferences.Get("ishaEnabled", false)) _alarmService.SetAlarm(DateTime.Parse(todayCalendar.Date), ishaTime, Preferences.Get("ishaNotificationTime", 0), "Isha");
								if (DateTime.Now < endOfIsha && Preferences.Get("endofishaEnabled", false)) _alarmService.SetAlarm(DateTime.Parse(todayCalendar.Date), endOfIshaTime, Preferences.Get("endofishaNotificationTime", 0), "End Of Isha");
								dayCounter++;
								if (dayCounter >= 15) break;
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

				Preferences.Set("LastAlarmDate", DateTime.Today.AddDays(7).ToShortDateString());
			}
			Debug.WriteLine("TimeStamp-SetWeeklyAlarms-Finish", DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss.fff tt"));
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

			// Try cache first if not forcing refresh
			if (!forceRefresh)
			{
				var cachedData = await TryGetMonthlyFromCacheAsync();
				if (cachedData != null)
				{
					Debug.WriteLine("Hybrid: Returning cached data");
					return cachedData;
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
					await SaveMonthlyToJsonCacheAsync(jsonResult);
					_monthlyCalendar = jsonResult;
					return jsonResult;
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Hybrid: JSON API failed - {ex.Message}");
			}

			// Strategy 2: Fallback to old XML API
			Debug.WriteLine("Hybrid: Falling back to XML API");
			try
			{
				var xmlResult = GetMonthlyPrayerTimes(location, forceRefresh);
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
		/// Try to get monthly data from cache (both JSON and XML)
		/// </summary>
		private async Task<ObservableCollection<Calendar>> TryGetMonthlyFromCacheAsync()
		{
			// Try JSON cache first
			if (File.Exists(_jsonFileName))
			{
				try
				{
					var jsonContent = await File.ReadAllTextAsync(_jsonFileName);
					var cachedData = JsonSerializer.Deserialize<List<Calendar>>(jsonContent);
					if (cachedData != null && cachedData.Count > 0)
					{
						var firstDate = DateTime.Parse(cachedData[0].Date);
						var lastDate = DateTime.Parse(cachedData[cachedData.Count - 1].Date);
						var daysFromStart = (DateTime.Today - firstDate).Days;
						
						if (daysFromStart >= 0 && daysFromStart < 21 && lastDate >= DateTime.Today)
						{
							Debug.WriteLine("Found valid JSON cache");
							return new ObservableCollection<Calendar>(cachedData);
						}
					}
				}
				catch (Exception ex)
				{
					Debug.WriteLine($"Error reading JSON cache: {ex.Message}");
				}
			}

			// Fallback to XML cache
			if (File.Exists(_fileName))
			{
				try
				{
					XDocument xmldoc = XDocument.Load(_fileName);
					var calendarDays = ParseXmlList(xmldoc);
					if (calendarDays != null && calendarDays.Count > 0)
					{
						var days = (DateTime.Today - DateTime.Parse(calendarDays[0].Date)).Days;
						if (days is < 21 and >= 0)
						{
							Debug.WriteLine("Found valid XML cache");
							return calendarDays;
						}
					}
				}
				catch (Exception ex)
				{
					Debug.WriteLine($"Error reading XML cache: {ex.Message}");
				}
			}

			return null;
		}

		/// <summary>
		/// Save monthly prayer times to JSON cache
		/// </summary>
		private async Task SaveMonthlyToJsonCacheAsync(ObservableCollection<Calendar> monthlyData)
		{
			try
			{
				var jsonContent = JsonSerializer.Serialize(monthlyData.ToList(), new JsonSerializerOptions 
				{ 
					WriteIndented = true 
				});
				await File.WriteAllTextAsync(_jsonFileName, jsonContent);
				Debug.WriteLine("Saved monthly data to JSON cache");
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Error saving JSON cache: {ex.Message}");
			}
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
