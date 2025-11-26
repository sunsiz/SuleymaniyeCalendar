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
	private bool _scheduledOnce;

	#region Properties

	/// <summary>Prayer time display string (e.g., "05:30").</summary>
	private string _time;
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
				try { Preferences.Set(PrayerId + "Enabled", value); } catch { }
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
	private ObservableCollection<Sound> _availableSounds;
	public ObservableCollection<Sound> AvailableSounds
	{
		get => _availableSounds;
		set => SetProperty(ref _availableSounds, value);
	}

	/// <summary>Currently selected alarm sound.</summary>
	private Sound _selectedSound;
	public Sound SelectedSound
	{
		get => _selectedSound;
		set => SetProperty(ref _selectedSound, value);
	}

	/// <summary>
	/// Prayer identifier (e.g., "fajr", "dhuhr").
	/// Setting this triggers prayer data loading.
	/// </summary>
	private string _prayerId;
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
			if (SetProperty(ref _notificationTime, value))
				Preferences.Set(PrayerId + "NotificationTime", value);
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
		LoadSounds();
		IsPlaying = false;
	}

	#endregion

	#region Commands

	/// <summary>
	/// Handles enabled switch toggle - saves preference immediately.
	/// </summary>
	[RelayCommand]
	public void EnabledSwitchToggled(bool value)
	{
		if (IsBusy) return;
		
		Preferences.Set(PrayerId + "Enabled", value);
		Debug.WriteLine($"Value Set for {PrayerId}Enabled: {value}");
		Enabled = value;
		OnPropertyChanged(nameof(ShowAdvancedOptions));
	}

	/// <summary>
	/// Saves settings and schedules alarms, then navigates back.
	/// Shows overlay during scheduling for user feedback.
	/// </summary>
	[RelayCommand]
	public async Task GoBack()
	{
		IsBusy = true;
		OverlayMessage = AppResources.AlarmlarPlanlaniyor + "...";
		ShowOverlay = true;
		
		try
		{
			await SaveAndScheduleInternalAsync().ConfigureAwait(false);
			// Brief delay so user perceives progress
			await Task.Delay(400).ConfigureAwait(false);
			await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync(".."));
		}
		finally
		{
			ShowOverlay = false;
			OverlayMessage = null;
			IsBusy = false;
			_scheduledOnce = true;
		}
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

	#region Public Methods

	/// <summary>
	/// Ensures scheduling occurs (used by page OnDisappearing for system back).
	/// Avoids overlay and duplicate runs.
	/// </summary>
	public void EnsureScheduled()
	{
		if (_scheduledOnce || _scheduling) return;
		_ = SaveAndScheduleInternalAsync();
	}

	#endregion

	#region Private Methods

	/// <summary>
	/// Saves alarm settings and schedules monthly alarms.
	/// Handles Android permission checks (exact alarms, notifications).
	/// </summary>
	private async Task SaveAndScheduleInternalAsync()
	{
		if (_scheduling) return; // Reentrancy guard
		_scheduling = true;
		
		try
		{
			using (_perf.StartTimer("PrayerDetail.SaveAndSchedule"))
			{
				// Save selected sound preference
				if (!string.IsNullOrWhiteSpace(PrayerId) && SelectedSound != null)
				{
					Preferences.Set(PrayerId + "AlarmSound", SelectedSound.FileName);
				}
				
				// Stop any playing preview
				await _audioPreview.StopAsync().ConfigureAwait(false);
				await MainThread.InvokeOnMainThreadAsync(() => IsPlaying = false);

				// Platform permission checks
				#if ANDROID
				if (!await CheckAndroidPermissionsAsync()) return;
				#endif

				// Schedule alarms
				await _dataService.SetMonthlyAlarmsAsync(forceReschedule: true).ConfigureAwait(false);
			}
		}
		catch (Exception ex)
		{
			Alert(AppResources.SorunCikti, ex.Message);
		}
		finally
		{
			_scheduling = false;
		}
	}

	#if ANDROID
	/// <summary>
	/// Checks and requests Android permissions for exact alarms and notifications.
	/// </summary>
	/// <returns>True if all permissions granted, false if scheduling should abort.</returns>
	private async Task<bool> CheckAndroidPermissionsAsync()
	{
		try
		{
			// Exact alarms (API 31+): Open system settings if not granted
			if (OperatingSystem.IsAndroidVersionAtLeast(31))
			{
				var am = (Android.App.AlarmManager)Android.App.Application.Context.GetSystemService(Android.Content.Context.AlarmService);
				if (am != null && !am.CanScheduleExactAlarms())
				{
					var intent = new Android.Content.Intent(Android.Provider.Settings.ActionRequestScheduleExactAlarm);
					intent.SetData(Android.Net.Uri.Parse($"package:{Android.App.Application.Context.PackageName}"));
					intent.AddFlags(Android.Content.ActivityFlags.NewTask);
					Android.App.Application.Context.StartActivity(intent);
					_scheduling = false;
					return false;
				}
			}

			// Notification permission (Android 13+): Request directly
			if (OperatingSystem.IsAndroidVersionAtLeast(33))
			{
				var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
				if (status != PermissionStatus.Granted)
				{
					var newStatus = await Permissions.RequestAsync<Permissions.PostNotifications>();
					if (newStatus != PermissionStatus.Granted)
					{
						DataService.ShowToast(AppResources.BildirimIzniGerekli);
						_scheduling = false;
						return false;
					}
				}
			}
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Permission checks failed: {ex.Message}");
			_scheduling = false;
			return false;
		}
		
		return true;
	}
	#endif

	/// <summary>
	/// Loads prayer data from preferences based on PrayerId.
	/// Sets title, time, enabled state, and selected sound.
	/// </summary>
	private void LoadPrayer()
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

	/// <summary>
	/// Initializes the available alarm sounds collection.
	/// Includes bird chirping, rooster, alarm clocks, and adhan sounds.
	/// </summary>
	private void LoadSounds()
	{
		AvailableSounds =
		[
			new Sound(fileName: "kus", name: AppResources.KusCiviltisi),
			new Sound(fileName: "horoz", name: AppResources.HorozOtusu),
			new Sound(fileName: "alarm", name: AppResources.CalarSaat),
			new Sound(fileName: "ezan", name: AppResources.EzanSesi),
			new Sound(fileName: "alarm2", name: AppResources.CalarSaat + " 1"),
			new Sound(fileName: "beep1", name: AppResources.CalarSaat + " 2"),
			new Sound(fileName: "beep2", name: AppResources.CalarSaat + " 3"),
			new Sound(fileName: "beep3", name: AppResources.CalarSaat + " 4")
		];
	}

	#endregion
}
