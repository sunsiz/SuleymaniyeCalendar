using CommunityToolkit.Mvvm.Input;
using SuleymaniyeCalendar.Models;
using SuleymaniyeCalendar.Resources.Strings;
using System.Collections.ObjectModel;
using System.Diagnostics;
using SuleymaniyeCalendar.Services;

namespace SuleymaniyeCalendar.ViewModels;

/// <summary>
/// ViewModel for prayer alarm configuration page.
/// Allows enabling/disabling alarms, selecting notification time offset, and choosing alarm sounds.
/// </summary>
[QueryProperty(nameof(PrayerId), nameof(PrayerId))]
public partial class PrayerDetailViewModel : BaseViewModel
{
	private readonly IAudioPreviewService _audioPreview;
	private readonly DataService _dataService;
	private readonly PerformanceService _perf = new();
	
	private bool _scheduling;
	private bool _awaitingPermissionReturn;

	#region Properties

	/// <summary>Prayer time display string (e.g., "05:30").</summary>
	private string _time = string.Empty;
	public string Time
	{
		get => _time;
		set => SetProperty(ref _time, value);
	}

	/// <summary>Whether this prayer's alarm is enabled.</summary>
	private bool _enabled;
	public bool Enabled
	{
		get => _enabled;
		set
		{
			if (SetProperty(ref _enabled, value))
			{
				OnPropertyChanged(nameof(ShowAdvancedOptions));
			}
		}
	}

	/// <summary>Vibration setting (reserved for future use).</summary>
	private bool _vibration;
	public bool Vibration
	{
		get => _vibration;
		set => SetProperty(ref _vibration, value);
	}

	/// <summary>Notification setting (reserved for future use).</summary>
	private bool _notification;
	public bool Notification
	{
		get => _notification;
		set => SetProperty(ref _notification, value);
	}

	/// <summary>Full alarm setting (reserved for future use).</summary>
	private bool _alarm;
	public bool Alarm
	{
		get => _alarm;
		set => SetProperty(ref _alarm, value);
	}

	/// <summary>Available alarm sounds for selection.</summary>
	private ObservableCollection<Sound> _availableSounds = new();
	public ObservableCollection<Sound> AvailableSounds
	{
		get => _availableSounds;
		set => SetProperty(ref _availableSounds, value);
	}

	/// <summary>Currently selected alarm sound.</summary>
	private Sound? _selectedSound;
	public Sound? SelectedSound
	{
		get => _selectedSound;
		set => SetProperty(ref _selectedSound, value);
	}

	/// <summary>
	/// Prayer identifier (e.g., "fajr", "dhuhr").
	/// Setting this triggers prayer data loading.
	/// </summary>
	private string _prayerId = string.Empty;
	public string PrayerId
	{
		get => _prayerId;
		set
		{
			if (SetProperty(ref _prayerId, value))
			{
				IsBusy = true;
				LoadPrayer();
				IsBusy = false;
			}
		}
	}

	/// <summary>Minutes before prayer time to trigger notification (0-60).</summary>
	private int _notificationTime;
	public int NotificationTime
	{
		get => _notificationTime;
		set
		{
			SetProperty(ref _notificationTime, value);
		}
	}

	/// <summary>Whether sound preview is currently playing.</summary>
	private bool _isPlaying;
	public bool IsPlaying
	{
		get => _isPlaying;
		set => SetProperty(ref _isPlaying, value);
	}

	/// <summary>Whether advanced options should be shown (Android 9 and below only).</summary>
	public bool IsNecessary => !((DeviceInfo.Platform == DevicePlatform.Android && DeviceInfo.Version.Major >= 10) || DeviceInfo.Platform == DevicePlatform.iOS);

	/// <summary>Whether to show advanced alarm options.</summary>
	public bool ShowAdvancedOptions => Enabled && IsNecessary;

	#endregion

	#region Constructor

	public PrayerDetailViewModel(IAudioPreviewService audioPreview, DataService dataService)
	{
		_audioPreview = audioPreview;
		_dataService = dataService;
		Title = AppResources.PageTitle;
		
		using (_perf.StartTimer("PrayerDetail.Constructor"))
		{
			ReloadSounds();
		}
		
		IsPlaying = false;

		// Log perf summary after delay to capture LoadPrayer metrics
		// Use defensive guards: Application.Current and Dispatcher may be null after background kill
		try
		{
			var dispatcher = Application.Current?.Dispatcher;
			if (dispatcher != null)
			{
				dispatcher.DispatchDelayed(TimeSpan.FromSeconds(1), () =>
				{
					try { _perf.LogSummary("PrayerDetailView"); }
					catch (Exception ex) { Debug.WriteLine($"PrayerDetailView perf log failed: {ex.Message}"); }
				});
			}
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"PrayerDetailView DispatchDelayed setup failed: {ex.Message}");
		}
	}

	#endregion

	#region Commands

	/// <summary>
	/// Updates the staged enabled state.
	/// </summary>
	[RelayCommand]
	public void EnabledSwitchToggled(bool value)
	{
		if (IsBusy) return;
		
		Enabled = value;
		OnPropertyChanged(nameof(ShowAdvancedOptions));
	}

	/// <summary>
	/// Saves settings and schedules alarms, then navigates back.
	/// Shows overlay during scheduling for user feedback.
	/// If permission is required, waits for user to grant it before navigating.
	/// </summary>
	[RelayCommand]
	public async Task Save()
	{
		IsBusy = true;
		OverlayMessage = AppResources.AlarmlarPlanlaniyor + "...";
		ShowOverlay = true;
		
		try
		{
			var success = await SaveAndScheduleInternalAsync();
			
			// Only navigate back if scheduling succeeded or no permission was needed
			// If user was sent to permission settings, don't navigate - they'll return to this page
			if (success)
			{
				// Brief delay so user perceives progress
				await Task.Delay(400);
				await Shell.Current.GoToAsync("..");
			}
		}
		finally
		{
			ShowOverlay = false;
			OverlayMessage = null;
			IsBusy = false;
		}
	}

	/// <summary>
	/// Called when user returns to the page (e.g., after granting permission).
	/// Retries scheduling if we were waiting for permission.
	/// </summary>
	public async Task OnPageResumedAsync()
	{
		if (!_awaitingPermissionReturn) return;
		
		_awaitingPermissionReturn = false;
#if ANDROID
		if (OperatingSystem.IsAndroidVersionAtLeast(31))
		{
			var manager = Android.App.Application.Context.GetSystemService(Android.Content.Context.AlarmService) as Android.App.AlarmManager;
			if (manager is null || !manager.CanScheduleExactAlarms())
			{
				Alert(AppResources.AlarmPermissionTitle, AppResources.AlarmExactPermissionRequired);
				return;
			}
		}
#endif
		await Save();
	}

	/// <summary>
	/// Plays/stops sound preview for selected alarm tone.
	/// </summary>
	[RelayCommand]
	public async Task TestButtonClicked()
	{
		if (!IsPlaying)
		{
			var fileKey = SelectedSound?.FileName;

#if __IOS__
			// Use shorter Athan sound on iOS while keeping preference unchanged
			if (string.Equals(fileKey, "ezan", StringComparison.OrdinalIgnoreCase))
			{
				fileKey = "ezanios";
			}
#endif
			if (!string.IsNullOrWhiteSpace(fileKey))
			{
				await _audioPreview.PlayAsync(fileKey, loop: true).ConfigureAwait(false);
				MainThread.BeginInvokeOnMainThread(() => IsPlaying = _audioPreview.IsPlaying);
			}
		}
		else
		{
			await _audioPreview.StopAsync().ConfigureAwait(false);
			MainThread.BeginInvokeOnMainThread(() => IsPlaying = false);
		}
	}

	#endregion

	#region Private Methods

	/// <summary>
	/// Saves alarm settings and schedules monthly alarms.
	/// Handles Android permission checks (exact alarms, notifications).
	/// </summary>
	/// <returns>True if scheduling completed successfully, false if user was redirected to permission settings.</returns>
	private async Task<bool> SaveAndScheduleInternalAsync()
	{
		if (_scheduling) return false; // Reentrancy guard
		_scheduling = true;
		
		try
		{
			using (_perf.StartTimer("PrayerDetail.SaveAndSchedule"))
			{
				// Stop any playing preview
				await _audioPreview.StopAsync();
				await MainThread.InvokeOnMainThreadAsync(() => IsPlaying = false);

				// Platform permission checks
				#if ANDROID
				var permissionResult = Enabled ? await CheckAndroidPermissionsAsync() : new PermissionCheckResult(true, false);
				if (!permissionResult.Success)
				{
					_awaitingPermissionReturn = permissionResult.RedirectedToSettings;
					return false;
				}
				#elif __IOS__
				var permissionResult = Enabled ? await CheckiOSPermissionsAsync() : new PermissionCheckResult(true, false);
				if (!permissionResult.Success)
				{
					_awaitingPermissionReturn = permissionResult.RedirectedToSettings;
					return false;
				}
				#endif

				if (string.IsNullOrWhiteSpace(PrayerId))
					throw new InvalidOperationException(AppResources.OtuzGunHatirlaticiHatasi);

				await _dataService.SavePrayerAlarmSettingsAsync(PrayerId, Enabled, NotificationTime, SelectedSound?.FileName);
				return true;
			}
		}
		catch (AlarmSettingsSaveException ex)
		{
			Debug.WriteLine($"Alarm settings save failed: {ex}");
			Alert(AppResources.SorunCikti, ex.RestoreFailed ? AppResources.AlarmRestoreFailed : AppResources.AlarmSaveFailed);
			return false;
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Alarm settings save failed: {ex}");
			Alert(AppResources.SorunCikti, AppResources.AlarmSaveFailed);
			return false;
		}
		finally
		{
			_scheduling = false;
		}
	}

	#if ANDROID
	/// <summary>
	/// Result of Android permission check.
	/// </summary>
	private readonly record struct PermissionCheckResult(bool Success, bool RedirectedToSettings);

	/// <summary>
	/// Checks and requests Android permissions for exact alarms and notifications.
	/// </summary>
	/// <returns>Result indicating if permissions granted and whether user was redirected to settings.</returns>
	private async Task<PermissionCheckResult> CheckAndroidPermissionsAsync()
	{
		try
		{
			// Exact alarms (API 31+): Open system settings if not granted
			if (OperatingSystem.IsAndroidVersionAtLeast(31))
			{
				var am = (Android.App.AlarmManager?)Android.App.Application.Context.GetSystemService(Android.Content.Context.AlarmService);
				if (am is null)
					throw new InvalidOperationException("Android alarm service is unavailable.");
				if (!am.CanScheduleExactAlarms())
				{
					var page = GetMainPage();
					if (page is null)
						throw new InvalidOperationException("Main page is unavailable.");
					if (!await page.DisplayAlert(AppResources.AlarmPermissionTitle, AppResources.AlarmExactPermissionRequired,
						AppResources.GotoSettings, AppResources.Kapat))
						return new PermissionCheckResult(false, false);
					var intent = new Android.Content.Intent(Android.Provider.Settings.ActionRequestScheduleExactAlarm);
					intent.SetData(Android.Net.Uri.Parse($"package:{Android.App.Application.Context.PackageName}"));
					intent.AddFlags(Android.Content.ActivityFlags.NewTask);
					Android.App.Application.Context.StartActivity(intent);
					return new PermissionCheckResult(Success: false, RedirectedToSettings: true);
				}
			}

			// Notification permission (Android 13+): Check first, then request or guide to settings
			if (OperatingSystem.IsAndroidVersionAtLeast(33))
			{
				var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
				if (status != PermissionStatus.Granted)
				{
					// Try requesting permission directly first
					var newStatus = await Permissions.RequestAsync<Permissions.PostNotifications>();
					if (newStatus != PermissionStatus.Granted)
					{
						// Permission denied - guide user to settings
						var mainPage = GetMainPage();
						bool shouldOpenSettings = false;
						if (mainPage != null)
						{
							shouldOpenSettings = await mainPage.DisplayAlert(
								AppResources.BildirimIzniGerekli,
								AppResources.BildirimIzniAciklama,
								AppResources.GotoSettings,
								AppResources.Kapat);
						}
						else
						{
							Debug.WriteLine("Main page is not available for DisplayAlert.");
						}
						
						if (shouldOpenSettings)
						{
							OpenAndroidNotificationSettings();
							return new PermissionCheckResult(Success: false, RedirectedToSettings: true);
						}
						
						ShowToast(AppResources.BildirimIzniGerekli);
						return new PermissionCheckResult(Success: false, RedirectedToSettings: false);
					}
				}
			}
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Permission checks failed: {ex.Message}");
			throw;
		}
		
		return new PermissionCheckResult(Success: true, RedirectedToSettings: false);
	}

	/// <summary>
	/// Opens Android notification settings for the app.
	/// </summary>
	private void OpenAndroidNotificationSettings()
	{
		try
		{
			var intent = new Android.Content.Intent();
			
			if (OperatingSystem.IsAndroidVersionAtLeast(26))
			{
				// Android 8.0+ - Open app notification settings
				intent.SetAction(Android.Provider.Settings.ActionAppNotificationSettings);
				intent.PutExtra(Android.Provider.Settings.ExtraAppPackage, Android.App.Application.Context.PackageName);
			}
			else
			{
				// Fallback to general app settings
				intent.SetAction(Android.Provider.Settings.ActionApplicationDetailsSettings);
				intent.SetData(Android.Net.Uri.Parse($"package:{Android.App.Application.Context.PackageName}"));
			}
			
			intent.AddFlags(Android.Content.ActivityFlags.NewTask);
			Android.App.Application.Context.StartActivity(intent);
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Failed to open notification settings: {ex.Message}");
			throw;
		}
	}
	#endif

	#if __IOS__
	/// <summary>
	/// Result of iOS permission check.
	/// </summary>
	private readonly record struct PermissionCheckResult(bool Success, bool RedirectedToSettings);

	/// <summary>
	/// Checks and requests iOS notification permissions.
	/// </summary>
	/// <returns>Result indicating if permissions granted and whether user was redirected to settings.</returns>
	private async Task<PermissionCheckResult> CheckiOSPermissionsAsync()
	{
		try
		{
			// Check current permission status
			var hasPermission = await Platforms.iOS.NotificationService.CheckNotificationPermissionAsync();
			
			if (!hasPermission)
			{
				// Try requesting permission
				var granted = await Platforms.iOS.NotificationService.RequestNotificationPermissionAsync();
				
				if (!granted)
				{
					// Permission denied - guide user to settings
					var mainPage = GetMainPage();
					bool shouldOpenSettings = false;
					if (mainPage != null)
					{
						shouldOpenSettings = await mainPage.DisplayAlert(
							AppResources.BildirimIzniGerekli,
							AppResources.BildirimIzniAciklama,
							AppResources.GotoSettings,
							AppResources.Kapat);
					}
					else
					{
						Debug.WriteLine("Main page is not available for DisplayAlert.");
					}
					
					if (shouldOpenSettings)
					{
						OpeniOSAppSettings();
						return new PermissionCheckResult(Success: false, RedirectedToSettings: true);
					}
					
					ShowToast(AppResources.BildirimIzniGerekli);
					return new PermissionCheckResult(Success: false, RedirectedToSettings: false);
				}
			}
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"iOS permission checks failed: {ex.Message}");
			throw;
		}
		
		return new PermissionCheckResult(Success: true, RedirectedToSettings: false);
	}

	/// <summary>
	/// Opens iOS app settings page.
	/// </summary>
	private void OpeniOSAppSettings()
	{
		try
		{
			UIKit.UIApplication.SharedApplication.OpenUrl(
				new Foundation.NSUrl(UIKit.UIApplication.OpenSettingsUrlString),
				new UIKit.UIApplicationOpenUrlOptions(),
				null);
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Failed to open iOS settings: {ex.Message}");
			throw;
		}
	}
	#endif

	/// <summary>
	/// Loads prayer data from preferences based on PrayerId.
	/// Sets title, time, enabled state, and selected sound.
	/// </summary>
	private void LoadPrayer()
	{
		using (_perf.StartTimer("PrayerDetail.LoadPrayer"))
		{
			try
			{
				// Set localized prayer title using switch expression
				Title = PrayerId switch
				{
					"falsefajr" => AppResources.FecriKazip,
					"fajr" => AppResources.FecriSadik,
					"sunrise" => AppResources.SabahSonu,
					"dhuhr" => AppResources.Ogle,
					"asr" => AppResources.Ikindi,
					"maghrib" => AppResources.Aksam,
					"isha" => AppResources.Yatsi,
					"endofisha" => AppResources.YatsiSonu,
					_ => Title
				};

				// Load saved preferences
				Time = Preferences.Get(PrayerId, "");
				Enabled = Preferences.Get(PrayerId + "Enabled", false);
				NotificationTime = Preferences.Get(PrayerId + "NotificationTime", 0);
				OnPropertyChanged(nameof(ShowAdvancedOptions));

				// Load selected sound or default to "kus" (bird chirping)
				var savedSound = Preferences.Get(PrayerId + "AlarmSound", "kus");
				SelectedSound = AvailableSounds?.FirstOrDefault(n => n.FileName == savedSound)
								?? AvailableSounds?.FirstOrDefault(n => n.FileName == "kus")
								?? AvailableSounds?.FirstOrDefault();
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Failed to load prayer detail: {ex.Message}");
			}
		}
	}

	/// <summary>
	/// Reloads the available alarm sounds collection with current localized names.
	/// Call this when language changes or page reappears to update sound names.
	/// Preserves current selection by matching on FileName.
	/// </summary>
	public void ReloadSounds()
	{
		// Remember current selection
		var currentFileName = SelectedSound?.FileName;
		
		AvailableSounds =
		[
			new Sound(fileName: "kus", name: AppResources.KusCiviltisi),
			new Sound(fileName: "horoz", name: AppResources.HorozOtusu),
			new Sound(fileName: "alarm", name: AppResources.CalarSaat),
			new Sound(fileName: "ezan", name: AppResources.EzanSesi),
			new Sound(fileName: "alarm2", name: AppResources.CalarSaat + " 1"),
			new Sound(fileName: "beep1", name: AppResources.CalarSaat + " 2"),
			new Sound(fileName: "beep2", name: AppResources.CalarSaat + " 3"),
			new Sound(fileName: "beep3", name: AppResources.CalarSaat + " 4"),
            new Sound(fileName: "beep4", name: AppResources.CalarSaat + " 5")
        ];
		
		// Restore selection if there was one
		if (!string.IsNullOrEmpty(currentFileName))
		{
			SelectedSound = AvailableSounds.FirstOrDefault(s => s.FileName == currentFileName);
		}
	}

	#endregion

	private static Page? GetMainPage()
	{
		var app = Application.Current;
		if (app?.Windows?.Count > 0)
		{
			return app.Windows[0].Page;
		}
		return null;
	}
}
