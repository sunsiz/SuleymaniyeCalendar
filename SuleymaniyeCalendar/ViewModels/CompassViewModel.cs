using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SuleymaniyeCalendar.Resources.Strings;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SuleymaniyeCalendar.Services;
using Microsoft.Maui.ApplicationModel;

namespace SuleymaniyeCalendar.ViewModels
{
	public partial class CompassViewModel:BaseViewModel
	{
		
		[ObservableProperty]private string _latitudeAltitude;

		[ObservableProperty]private string _address;

		[ObservableProperty]private string _degreeLongitude;

		[ObservableProperty]private double _heading;

		// Expose raw numeric values so labels can be localized in XAML
		[ObservableProperty] private double _latitude;
		[ObservableProperty] private double _altitude;
		[ObservableProperty] private double _longitude;
		
		private double _currentLatitude = 41.0;
		private double _currentLongitude = 29.0;
		private double _currentAltitude = 114;
		private readonly double _qiblaLatitude = 21.4224779;
		private readonly double _qiblaLongitude = 39.8251832;
		internal readonly SensorSpeed Speed = SensorSpeed.UI;

		private readonly DataService _dataService;

		[RelayCommand]
		private void Start()
		{
			Compass.ReadingChanged += Compass_ReadingChanged;
		}

		[RelayCommand]
		private void Stop()
		{
			if (!Compass.IsMonitoring)
				return;

			Compass.ReadingChanged -= Compass_ReadingChanged;
			Compass.Stop();
		}

		[RelayCommand]
		private async Task RefreshLocation()
		{
				//using (UserDialogs.Instance.Loading(AppResources.Yenileniyor))
				//{
			IsBusy = true;
			var location = await _dataService.GetCurrentLocationAsync(true).ConfigureAwait(false);
			if (location != null && location.Latitude != 0 && location.Longitude != 0)
			{
				_currentLatitude = location.Latitude;
				_currentLongitude = location.Longitude;
				_currentAltitude = location.Altitude ?? 0.0;

				MainThread.BeginInvokeOnMainThread(() =>
				{
					Latitude = _currentLatitude;
					Longitude = _currentLongitude;
					Altitude = _currentAltitude;
				});

				// reverse geocode to human-readable address (include country)
				try
				{
					var placemarks = await Geocoding.GetPlacemarksAsync(_currentLatitude, _currentLongitude).ConfigureAwait(false);
					var place = placemarks?.FirstOrDefault();
					var full = BuildAddressFromPlacemark(place);
					if (!string.IsNullOrWhiteSpace(full))
					{
						MainThread.BeginInvokeOnMainThread(() => Address = full);
						Preferences.Set("LastAddress", full);
					}
				}
				catch { /* ignore geocode failures */ }
			}

			IsBusy = false;
			//}
		}

		private async Task InitializeAddressAsync()
		{
			try
			{
				// Skip if we already have an address for current location, unless it was cleared
				if (!string.IsNullOrWhiteSpace(Address))
					return;

				if (_currentLatitude != 0 && _currentLongitude != 0)
				{
					var placemarks = await Geocoding.GetPlacemarksAsync(_currentLatitude, _currentLongitude).ConfigureAwait(false);
					var place = placemarks?.FirstOrDefault();
					var full = BuildAddressFromPlacemark(place);
					if (!string.IsNullOrWhiteSpace(full))
					{
						MainThread.BeginInvokeOnMainThread(() => Address = full);
						Preferences.Set("LastAddress", full);
					}
				}
			}
			catch { }
		}

		private static string BuildAddressFromPlacemark(Placemark place)
		{
			if (place == null)
				return string.Empty;

			var parts = new List<string>();
			if (!string.IsNullOrWhiteSpace(place.Thoroughfare)) parts.Add(place.Thoroughfare);
			if (!string.IsNullOrWhiteSpace(place.SubLocality)) parts.Add(place.SubLocality);
			if (!string.IsNullOrWhiteSpace(place.Locality)) parts.Add(place.Locality);
			if (!string.IsNullOrWhiteSpace(place.AdminArea)) parts.Add(place.AdminArea);
			if (!string.IsNullOrWhiteSpace(place.PostalCode)) parts.Add(place.PostalCode);
			if (!string.IsNullOrWhiteSpace(place.CountryName)) parts.Add(place.CountryName);

			return string.Join(", ", parts);
		}


		[RelayCommand]
		private async Task GoToMap()
		{
				try
				{
					var location = new Location(Convert.ToDouble(_currentLatitude, CultureInfo.InvariantCulture.NumberFormat),
						Convert.ToDouble(_currentLongitude, CultureInfo.InvariantCulture.NumberFormat));
					var placemark = await Geocoding
						.GetPlacemarksAsync(Convert.ToDouble(_currentLatitude, CultureInfo.InvariantCulture.NumberFormat),
							Convert.ToDouble(_currentLongitude, CultureInfo.InvariantCulture.NumberFormat))
						.ConfigureAwait(true);
					var options = new MapLaunchOptions
						{ Name = placemark.FirstOrDefault()?.Thoroughfare ?? placemark.FirstOrDefault()?.CountryName };

					await Map.OpenAsync(location, options).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
					ToastDuration duration = ToastDuration.Long;
					double fontSize = 14;
					var toast = Toast.Make(AppResources.HaritaHatasi, duration, fontSize);
					await toast.Show(cancellationTokenSource.Token);
					//UserDialogs.Instance.Toast(AppResources.HaritaHatasi + ex.Message);
					Debug.WriteLine(ex.Message);
				}
		}
		public CompassViewModel(DataService dataService)
		{
			_dataService = dataService;
			Title = AppResources.KibleGostergesi;
			
			// Initialize from saved preferences first
			UpdateLocationFromPreferences();
			
			// initialize address asynchronously so UI shows saved or reverse-geocoded address without user action
			_ = InitializeAddressAsync();
			
			try
			{
				if (!Compass.IsMonitoring)
				{
					Compass.Start(Speed, applyLowPassFilter: true);
				}
			}
			catch (FeatureNotSupportedException fnsEx)
			{
				// Feature not supported on device
				CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
				ToastDuration duration = ToastDuration.Long;
				double fontSize = 14;
				var toast = Toast.Make(AppResources.CihazPusulaDesteklemiyor, duration, fontSize);
				toast.Show(cancellationTokenSource.Token);
				//UserDialogs.Instance.Toast(AppResources.CihazPusulaDesteklemiyor, TimeSpan.FromSeconds(4));
				Debug.WriteLine($"**** {this.GetType().Name}.{nameof(Compass_ReadingChanged)}: {fnsEx.Message}");
			}
			catch (Exception ex)
			{
				CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
				ToastDuration duration = ToastDuration.Long;
				double fontSize = 14;
				var toast = Toast.Make(ex.Message, duration, fontSize);
				toast.Show(cancellationTokenSource.Token);
				//UserDialogs.Instance.Alert(ex.Message);
				Debug.WriteLine(ex.Message);
				LatitudeAltitude =
					$"{AppResources.EnlemFormatsiz}: {_currentLatitude:F2}  |  {AppResources.YukseklikFormatsiz}: {_currentAltitude:N0}";
				DegreeLongitude =
					$"{AppResources.BoylamFormatsiz}: {_currentLongitude:F2}  |  {AppResources.Aci}: {Heading:####}";
			}
		}

		// Call this method to refresh location data from current app state
		public async Task RefreshLocationFromAppAsync()
		{
			// Get current location from DataService (which has the latest location)
			var currentCalendar = _dataService.calendar;
			if (currentCalendar != null)
			{
				var hasLocationChanged = 
					Math.Abs(_currentLatitude - currentCalendar.Latitude) > 0.001 ||
					Math.Abs(_currentLongitude - currentCalendar.Longitude) > 0.001;

				if (hasLocationChanged)
				{
					_currentLatitude = currentCalendar.Latitude;
					_currentLongitude = currentCalendar.Longitude;
					_currentAltitude = currentCalendar.Altitude;

					// Update UI
					MainThread.BeginInvokeOnMainThread(() =>
					{
						Latitude = _currentLatitude;
						Longitude = _currentLongitude;
						Altitude = _currentAltitude;
						LatitudeAltitude = $"{AppResources.EnlemFormatsiz}: {_currentLatitude:F2}  |  {AppResources.YukseklikFormatsiz}: {_currentAltitude:N0}";
						DegreeLongitude = $"{AppResources.BoylamFormatsiz}: {_currentLongitude:F2}  |  {AppResources.Aci}: {Heading:####}";
					});

					// Clear old address and get new one
					Address = string.Empty;
					await InitializeAddressAsync();
				}
			}
		}

		private void UpdateLocationFromPreferences()
		{
			_currentLatitude = Preferences.Get("LastLatitude", 0.0);
			_currentLongitude = Preferences.Get("LastLongitude", 0.0);
			_currentAltitude = Preferences.Get("LastAltitude", 0.0);
			Address = Preferences.Get("LastAddress", string.Empty);

			// initialize numeric bindings for XAML
			MainThread.BeginInvokeOnMainThread(() =>
			{
				Latitude = _currentLatitude;
				Longitude = _currentLongitude;
				Altitude = _currentAltitude;
			});
			
			LatitudeAltitude = $"{AppResources.EnlemFormatsiz}: {_currentLatitude:F2}  |  {AppResources.YukseklikFormatsiz}: {_currentAltitude:N0}";
			DegreeLongitude = $"{AppResources.BoylamFormatsiz}: {_currentLongitude:F2}  |  {AppResources.Aci}: {Heading:####}";
		}

		private void Compass_ReadingChanged(object sender, CompassChangedEventArgs e)
		{
			try
			{
				var qiblaLocation = new Location(_qiblaLatitude, _qiblaLongitude);
				var position = new Location(_currentLatitude, _currentLongitude);
				var res = DistanceCalculator.Bearing(position, qiblaLocation);
				var targetHeading = (360 - res) % 360;

				var currentHeading = 360 - e.Reading.HeadingMagneticNorth;
				Heading = currentHeading - targetHeading;
				
				LatitudeAltitude =
					$"{AppResources.EnlemFormatsiz}: {_currentLatitude:F2}  |  {AppResources.YukseklikFormatsiz}: {_currentAltitude:N0}";
				DegreeLongitude =
					$"{AppResources.BoylamFormatsiz}: {_currentLongitude:F2}  |  {AppResources.Aci}: {Heading:####}";

			}
			catch (FeatureNotSupportedException fnsEx)
			{
				// Feature not supported on device
				CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
				ToastDuration duration = ToastDuration.Long;
				double fontSize = 14;
				var toast = Toast.Make(AppResources.CihazPusulaDesteklemiyor, duration, fontSize);
				toast.Show(cancellationTokenSource.Token);
				//UserDialogs.Instance.Alert(AppResources.CihazPusulaDesteklemiyor, AppResources.CihazPusulaDesteklemiyor);
				Debug.WriteLine($"**** {this.GetType().Name}.{nameof(Compass_ReadingChanged)}: {fnsEx.Message}");
			}
			catch (Exception ex)
			{
				CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
				ToastDuration duration = ToastDuration.Long;
				double fontSize = 14;
				var toast = Toast.Make(ex.Message, duration, fontSize);
				toast.Show(cancellationTokenSource.Token);
				//UserDialogs.Instance.Alert(ex.Message);
				Debug.WriteLine(ex.Message);
			}
		}
		public void Dispose()
		{
			try
			{
				if (Compass.IsMonitoring)
				{
					Compass.ReadingChanged -= Compass_ReadingChanged;
					Compass.Stop();
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Error disposing compass: {ex.Message}");
			}
		}
	}

	internal class DistanceCalculator
	{
		private const double KDegreesToRadians = Math.PI / 180.0;
		private const double KRadiansToDegrees = 180.0 / Math.PI;

		public static double Bearing(Location position, Location location)
		{
			double fromLong = position.Longitude * KDegreesToRadians;
			double toLong = location.Longitude * KDegreesToRadians;
			double toLat = location.Latitude * KDegreesToRadians;
			double fromLat = position.Latitude * KDegreesToRadians;

			double dlon = toLong - fromLong;
			double y = Math.Sin(dlon);
			double x = Math.Cos(fromLat) * Math.Tan(toLat) - Math.Sin(fromLat) * Math.Cos(dlon);

			double direction = Math.Atan2(y, x);

			// convert to degrees
			direction *= KRadiansToDegrees;

			return direction;
		}
	}
}
