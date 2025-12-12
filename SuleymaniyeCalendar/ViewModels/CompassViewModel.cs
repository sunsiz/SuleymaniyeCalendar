#nullable enable

using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SuleymaniyeCalendar.Resources.Strings;
using System.Diagnostics;
using System.Globalization;
using SuleymaniyeCalendar.Services;

namespace SuleymaniyeCalendar.ViewModels;

/// <summary>
/// ViewModel for Qibla compass page showing direction to Kaaba in Mecca.
/// Uses device compass sensor to calculate and display Qibla direction.
/// Implements IDisposable to properly clean up compass sensor resources.
/// </summary>
public partial class CompassViewModel : BaseViewModel, IDisposable
{
	#region Private Fields

	private readonly PerformanceService _perf = new();
	private readonly DataService _dataService;

	/// <summary>Kaaba latitude in Mecca, Saudi Arabia.</summary>
	private readonly double _qiblaLatitude = 21.4224779;
	/// <summary>Kaaba longitude in Mecca, Saudi Arabia.</summary>
	private readonly double _qiblaLongitude = 39.8251832;
	
	// Current device location
	private double _currentLatitude = 41.0;
	private double _currentLongitude = 29.0;
	private double _currentAltitude = 114;
	
	internal readonly SensorSpeed Speed = SensorSpeed.UI;

	#endregion

	#region Display Properties

	/// <summary>Formatted latitude and altitude display (e.g., "Latitude: 41.00 | Altitude: 114").</summary>
	private string _latitudeAltitude = string.Empty;
	public string LatitudeAltitude
	{
		get => _latitudeAltitude;
		set => SetProperty(ref _latitudeAltitude, value);
	}

	/// <summary>Formatted longitude and heading display (e.g., "Longitude: 29.00 | Angle: 123°").</summary>
	private string _degreeLongitude = string.Empty;
	public string DegreeLongitude
	{
		get => _degreeLongitude;
		set => SetProperty(ref _degreeLongitude, value);
	}

	/// <summary>Human-readable address from reverse geocoding.</summary>
	private string _address = string.Empty;
	public string Address
	{
		get => _address;
		set => SetProperty(ref _address, value);
	}

	/// <summary>Compass heading in degrees (rotation angle for compass UI).</summary>
	private double _heading;
	public double Heading
	{
		get => _heading;
		set => SetProperty(ref _heading, value);
	}

	/// <summary>Raw latitude value for data binding.</summary>
	private double _latitude;
	public double Latitude
	{
		get => _latitude;
		set => SetProperty(ref _latitude, value);
	}

	/// <summary>Raw longitude value for data binding.</summary>
	private double _longitude;
	public double Longitude
	{
		get => _longitude;
		set => SetProperty(ref _longitude, value);
	}

	/// <summary>Raw altitude value for data binding.</summary>
	private double _altitude;
	public double Altitude
	{
		get => _altitude;
		set => SetProperty(ref _altitude, value);
	}

	#endregion

	#region Constructor

	public CompassViewModel(DataService dataService)
	{
		_dataService = dataService;
		Title = AppResources.KibleGostergesi;
		
		using (_perf.StartTimer("Compass.Constructor"))
		{
			// Load saved location from preferences
			UpdateLocationFromPreferences();
			
			// Initialize address asynchronously (reverse geocoding)
			_ = InitializeAddressAsync();
			
			// Note: Compass sensor is started in StartCompass() called from CompassPage.OnAppearing()
			// This allows proper lifecycle management when page appears/disappears
		}
		
		// Log perf summary after delay to capture async operations (wrapped in try-catch for safety)
		try
		{
			Application.Current?.Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(1), () =>
			{
				try { _perf.LogSummary("CompassView"); }
				catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"CompassView perf log failed: {ex.Message}"); }
			});
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"CompassView DispatchDelayed setup failed: {ex.Message}");
		}
	}

	#endregion

	#region Commands

	/// <summary>
	/// Refreshes location from GPS with high accuracy.
	/// Forces fresh GPS fix and reverse geocoding.
	/// </summary>
	[RelayCommand]
	private async Task RefreshLocation()
	{
		IsBusy = true;
		
		// Track start time to ensure minimum visible duration for user feedback
		var startTime = DateTime.UtcNow;
		const int MinimumVisibleDurationMs = 500;
		
		try
		{
			// Force fresh GPS fix (user-initiated action)
			Location? location;
			using (_perf.StartTimer("Compass.RefreshLocation"))
			{
				location = await _dataService.GetCurrentLocationAsync(true).ConfigureAwait(false);
			}
			
			if (location != null && location.Latitude != 0 && location.Longitude != 0)
			{
				_currentLatitude = location.Latitude;
				_currentLongitude = location.Longitude;
				_currentAltitude = location.Altitude ?? 0.0;

				// Update UI on main thread
				await MainThread.InvokeOnMainThreadAsync(() =>
				{
					Latitude = _currentLatitude;
					Longitude = _currentLongitude;
					Altitude = _currentAltitude;
				});

				// Reverse geocode to address
				await UpdateAddressFromLocationAsync();
			}
		}
		finally
		{
			// Ensure minimum visible duration so user sees feedback
			var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
			if (elapsed < MinimumVisibleDurationMs)
			{
				await Task.Delay(MinimumVisibleDurationMs - (int)elapsed);
			}
			IsBusy = false;
		}
	}

	/// <summary>
	/// Opens device maps app showing current location.
	/// </summary>
	[RelayCommand]
	private async Task GoToMap()
	{
		try
		{
			var location = new Location(
				Convert.ToDouble(_currentLatitude, CultureInfo.InvariantCulture),
				Convert.ToDouble(_currentLongitude, CultureInfo.InvariantCulture)
			);
			
			var placemarks = await Geocoding.GetPlacemarksAsync(
				Convert.ToDouble(_currentLatitude, CultureInfo.InvariantCulture),
				Convert.ToDouble(_currentLongitude, CultureInfo.InvariantCulture)
			).ConfigureAwait(true);
			
			var options = new MapLaunchOptions
			{
				Name = placemarks.FirstOrDefault()?.Thoroughfare ?? placemarks.FirstOrDefault()?.CountryName
			};

			await Map.OpenAsync(location, options).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			ShowToast(AppResources.HaritaHatasi);
			Debug.WriteLine($"Map launch error: {ex.Message}");
		}
	}

	#endregion

	#region Public Methods

	/// <summary>
	/// Refreshes location from DataService calendar (after location change in other pages).
	/// </summary>
	public async Task RefreshLocationFromAppAsync()
	{
		var currentCalendar = _dataService.calendar;
		if (currentCalendar is null) return;

		// Check if location changed significantly (>0.001° = ~110m)
		// Check if location changed significantly (>0.001° = ~110m)
		var hasLocationChanged = 
			Math.Abs(_currentLatitude - currentCalendar.Latitude) > 0.001 ||
			Math.Abs(_currentLongitude - currentCalendar.Longitude) > 0.001;

		if (hasLocationChanged)
		{
			_currentLatitude = currentCalendar.Latitude;
			_currentLongitude = currentCalendar.Longitude;
			_currentAltitude = currentCalendar.Altitude;

			// Update UI
			await MainThread.InvokeOnMainThreadAsync(() =>
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

	/// <summary>
	/// Starts the compass sensor if not already running.
	/// Called when CompassPage appears.
	/// </summary>
	public void StartCompass()
	{
		try
		{
			if (!Compass.IsMonitoring)
			{
				// MUST subscribe BEFORE Compass.Start() or no readings received
				Compass.ReadingChanged += Compass_ReadingChanged;
				Compass.Start(Speed, applyLowPassFilter: true);
			}
		}
		catch (FeatureNotSupportedException fnsEx)
		{
			ShowToast(AppResources.CihazPusulaDesteklemiyor);
			Debug.WriteLine($"Compass not supported: {fnsEx.Message}");
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Compass start error: {ex.Message}");
		}
	}

	/// <summary>
	/// Stops the compass sensor to save battery.
	/// Called when CompassPage disappears.
	/// </summary>
	public void StopCompass()
	{
		try
		{
			if (Compass.IsMonitoring)
			{
				Compass.ReadingChanged -= Compass_ReadingChanged;  // Unsubscribe first
				Compass.Stop();  // Then stop sensor
			}
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Compass stop error: {ex.Message}");
		}
	}

	/// <summary>
	/// Disposes compass sensor resources.
	/// CRITICAL: Must unsubscribe ReadingChanged before Stop() to prevent memory leaks.
	/// </summary>
	public void Dispose()
	{
		StopCompass();
	}

	#endregion

	#region Private Methods

	/// <summary>
	/// Loads saved location from preferences (from previous app session).
	/// </summary>
	private void UpdateLocationFromPreferences()
	{
		_currentLatitude = Preferences.Get("LastLatitude", 0.0);
		_currentLongitude = Preferences.Get("LastLongitude", 0.0);
		_currentAltitude = Preferences.Get("LastAltitude", 0.0);
		Address = Preferences.Get("LastAddress", string.Empty);

		// Initialize UI bindings on main thread
		MainThread.BeginInvokeOnMainThread(() =>
		{
			Latitude = _currentLatitude;
			Longitude = _currentLongitude;
			Altitude = _currentAltitude;
		});
		
		LatitudeAltitude = $"{AppResources.EnlemFormatsiz}: {_currentLatitude:F2}  |  {AppResources.YukseklikFormatsiz}: {_currentAltitude:N0}";
		DegreeLongitude = $"{AppResources.BoylamFormatsiz}: {_currentLongitude:F2}  |  {AppResources.Aci}: {Heading:####}";
	}

	/// <summary>
	/// Initializes address from reverse geocoding (async startup).
	/// </summary>
	private async Task InitializeAddressAsync()
	{
		try
		{
			// Skip if we already have an address
			if (!string.IsNullOrWhiteSpace(Address))
				return;

			if (_currentLatitude != 0 && _currentLongitude != 0)
			{
				await UpdateAddressFromLocationAsync();
			}
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Address initialization failed: {ex.Message}");
		}
		
		// Fallback to cached address if still empty (iOS geocoding can fail silently)
		if (string.IsNullOrWhiteSpace(Address))
		{
			var cached = Preferences.Get("LastAddress", string.Empty);
			if (!string.IsNullOrWhiteSpace(cached))
			{
				await MainThread.InvokeOnMainThreadAsync(() => Address = cached);
			}
		}
	}

	/// <summary>
	/// Updates address from reverse geocoding and saves to preferences.
	/// </summary>
	private async Task UpdateAddressFromLocationAsync()
	{
		try
		{
			using (_perf.StartTimer("Compass.ReverseGeocode"))
			{
				var placemarks = await Geocoding.GetPlacemarksAsync(_currentLatitude, _currentLongitude).ConfigureAwait(false);
				var place = placemarks?.FirstOrDefault();
				if (place == null) return;
				var fullAddress = BuildAddressFromPlacemark(place);
				
				if (!string.IsNullOrWhiteSpace(fullAddress))
				{
					await MainThread.InvokeOnMainThreadAsync(() => Address = fullAddress);
					Preferences.Set("LastAddress", fullAddress);
				}
			}
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Reverse geocoding failed: {ex.Message}");
		}
	}

	/// <summary>
	/// Builds human-readable address from Placemark.
	/// </summary>
	private static string BuildAddressFromPlacemark(Placemark place)
	{
		if (place is null) return string.Empty;

		var parts = new List<string>();
		if (!string.IsNullOrWhiteSpace(place.Thoroughfare)) parts.Add(place.Thoroughfare);
		if (!string.IsNullOrWhiteSpace(place.SubLocality)) parts.Add(place.SubLocality);
		if (!string.IsNullOrWhiteSpace(place.Locality)) parts.Add(place.Locality);
		if (!string.IsNullOrWhiteSpace(place.AdminArea)) parts.Add(place.AdminArea);
		if (!string.IsNullOrWhiteSpace(place.PostalCode)) parts.Add(place.PostalCode);
		if (!string.IsNullOrWhiteSpace(place.CountryName)) parts.Add(place.CountryName);

		return string.Join(", ", parts);
	}

	/// <summary>
	/// Compass sensor reading changed event handler.
	/// Calculates Qibla direction relative to device heading.
	/// </summary>
	private void Compass_ReadingChanged(object? sender, CompassChangedEventArgs e)
	{
		try
		{
			// Calculate bearing from current position to Kaaba
			var qiblaLocation = new Location(_qiblaLatitude, _qiblaLongitude);
			var currentPosition = new Location(_currentLatitude, _currentLongitude);
			var targetBearing = DistanceCalculator.Bearing(currentPosition, qiblaLocation);
			var targetHeading = (360 - targetBearing) % 360;

			// Get current device heading (magnetic north-based)
			var currentHeading = 360 - e.Reading.HeadingMagneticNorth;
			
			// Calculate relative rotation for UI
			Heading = currentHeading - targetHeading;
			
			// Update display strings
			LatitudeAltitude = $"{AppResources.EnlemFormatsiz}: {_currentLatitude:F2}  |  {AppResources.YukseklikFormatsiz}: {_currentAltitude:N0}";
			DegreeLongitude = $"{AppResources.BoylamFormatsiz}: {_currentLongitude:F2}  |  {AppResources.Aci}: {Heading:####}";
		}
		catch (FeatureNotSupportedException fnsEx)
		{
			ShowToast(AppResources.CihazPusulaDesteklemiyor);
			Debug.WriteLine($"Compass feature not supported: {fnsEx.Message}");
		}
		catch (Exception ex)
		{
			ShowToast(ex.Message);
			Debug.WriteLine($"Compass reading error: {ex.Message}");
		}
	}

	#endregion
}

/// <summary>
/// Helper class for calculating bearing between two geographic locations.
/// </summary>
internal static class DistanceCalculator
{
	private const double DegreesToRadians = Math.PI / 180.0;
	private const double RadiansToDegrees = 180.0 / Math.PI;

	/// <summary>
	/// Calculates bearing from position to target location.
	/// </summary>
	/// <param name="position">Starting position.</param>
	/// <param name="location">Target location.</param>
	/// <returns>Bearing in degrees (0-360).</returns>
	public static double Bearing(Location position, Location location)
	{
		var fromLongRad = position.Longitude * DegreesToRadians;
		var toLongRad = location.Longitude * DegreesToRadians;
		var toLatRad = location.Latitude * DegreesToRadians;
		var fromLatRad = position.Latitude * DegreesToRadians;

		var dlon = toLongRad - fromLongRad;
		var y = Math.Sin(dlon);
		var x = Math.Cos(fromLatRad) * Math.Tan(toLatRad) - Math.Sin(fromLatRad) * Math.Cos(dlon);

		var direction = Math.Atan2(y, x);

		// Convert to degrees
		direction *= RadiansToDegrees;

		return direction;
	}
}
