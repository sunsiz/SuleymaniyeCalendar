using CommunityToolkit.Mvvm.Input;
using SuleymaniyeCalendar.Resources.Strings;
using System.Globalization;
using LocalizationResourceManager.Maui;
using SuleymaniyeCalendar.Models;
using SuleymaniyeCalendar.Services;

namespace SuleymaniyeCalendar.ViewModels;

/// <summary>
/// ViewModel for the settings page.
/// Handles theme selection, language switching, and service toggles.
/// </summary>
/// <remarks>
/// Features:
/// - Light/Dark/System theme modes with instant switching
/// - 11 supported languages with RTL support
/// - Android foreground service toggle for persistent notifications
/// - Widget updates on theme/language changes
/// </remarks>
public partial class SettingsViewModel : BaseViewModel
{
	#region Fields

	private readonly PerformanceService _perf;
	private readonly ILocalizationResourceManager _resourceManager;
	private readonly IRtlService _rtlService;
    private readonly IAlarmService _alarmService;
    private readonly IWidgetService _widgetService;

	/// <summary>Guard flag to prevent recursive language reload loops.</summary>
	private bool _updatingLanguages;

	/// <summary>Prevents re-entrant theme updates when toggling via gestures and radios.</summary>
	private bool _suppressThemeUpdates;

	#endregion

	#region Properties - Language

	private IList<Language> _supportedLanguages = [];

	/// <summary>List of supported UI languages.</summary>
	public IList<Language> SupportedLanguages
	{
		get => _supportedLanguages;
		set => SetProperty(ref _supportedLanguages, value);
	}

	private Language _selectedLanguage = new("English", "en");

	/// <summary>
	/// Currently selected UI language.
	/// Setting this changes the app culture, saves preference, and updates widget.
	/// </summary>
	public Language SelectedLanguage
	{
		get => _selectedLanguage;
		set
		{
			// Prevent infinite recursion when rebuilding SupportedLanguages
			if (_updatingLanguages)
			{
				_selectedLanguage = value;
				OnPropertyChanged(nameof(SelectedLanguage));
				return;
			}

			if (!SetProperty(ref _selectedLanguage, value) || value is null) return;

			// Skip if already the active culture
			if (string.Equals(_resourceManager?.CurrentCulture?.TwoLetterISOLanguageName, 
				value.CI, StringComparison.OrdinalIgnoreCase))
				return;

			_updatingLanguages = true;
			try
			{
				ApplyLanguageChange(value);
			}
			finally
			{
				_updatingLanguages = false;
			}
		}
	}

	#endregion

	#region Properties - Theme

	private bool _dark;
	/// <summary>Whether dark theme is active.</summary>
	public bool Dark
	{
		get => _dark;
		set => SetProperty(ref _dark, value);
	}

	private int _currentTheme;
	/// <summary>Current theme index: 0=Dark, 1=Light, 2=System.</summary>
	public int CurrentTheme
	{
		get => _currentTheme;
		set
		{
			if (SetProperty(ref _currentTheme, value))
				MainThread.BeginInvokeOnMainThread(() => SetUserAppTheme(value));
		}
	}

	private bool _lightChecked;
	/// <summary>Whether light theme radio button is checked.</summary>
	public bool LightChecked
	{
		get => _lightChecked;
		set
		{
			if (!SetProperty(ref _lightChecked, value) || _suppressThemeUpdates || !value) return;
			ApplyThemeInternal(1);
			_ = ApplyThemeDeferredAsync(1);
		}
	}

	private bool _darkChecked;
	/// <summary>Whether dark theme radio button is checked.</summary>
	public bool DarkChecked
	{
		get => _darkChecked;
		set
		{
			if (!SetProperty(ref _darkChecked, value) || _suppressThemeUpdates || !value) return;
			ApplyThemeInternal(0);
			_ = ApplyThemeDeferredAsync(0);
		}
	}

	private bool _systemChecked;
	/// <summary>Whether system theme radio button is checked.</summary>
	public bool SystemChecked
	{
		get => _systemChecked;
		set
		{
			if (!SetProperty(ref _systemChecked, value) || _suppressThemeUpdates || !value) return;
			ApplyThemeInternal(2);
			_ = ApplyThemeDeferredAsync(2);
		}
	}

	#endregion

	#region Properties - Service Settings

	private bool _alwaysRenewLocationEnabled;
	/// <summary>Whether location is refreshed on every app launch.</summary>
	public bool AlwaysRenewLocationEnabled
	{
		get => _alwaysRenewLocationEnabled;
		set
		{
			if (SetProperty(ref _alwaysRenewLocationEnabled, value))
				Preferences.Set("AlwaysRenewLocationEnabled", value);
		}
	}

	private bool _notificationPrayerTimesEnabled;
	/// <summary>Whether prayer times are shown in the persistent notification.</summary>
	public bool NotificationPrayerTimesEnabled
	{
		get => _notificationPrayerTimesEnabled;
		set
		{
			if (!SetProperty(ref _notificationPrayerTimesEnabled, value)) return;
			Preferences.Set("NotificationPrayerTimesEnabled", value);
            _alarmService.RefreshNotification();
		}
	}

	/// <summary>
	/// Whether to show notification content option.
	/// Only visible when sticky notification is enabled on Android.
	/// </summary>
	public bool ShowNotificationPrayerOption =>
		_alarmService.SupportsForegroundService && ForegroundServiceEnabled;

	private bool _foregroundServiceEnabled;
	/// <summary>
	/// Whether Android foreground service is enabled for persistent notifications.
	/// Toggling starts/stops the AlarmForegroundService.
	/// </summary>
	public bool ForegroundServiceEnabled
	{
		get => _foregroundServiceEnabled;
		set
		{
			if (!SetProperty(ref _foregroundServiceEnabled, value)) return;
			Preferences.Set("ForegroundServiceEnabled", value);
			// When turning off, also disable prayer times in notification
			if (!value)
			{
				NotificationPrayerTimesEnabled = false;
			}
			OnPropertyChanged(nameof(ShowNotificationPrayerOption));
            
            if (value)
                _alarmService.StartAlarmForegroundService();
            else
                _alarmService.StopAlarmForegroundService();
		}
	}

	/// <summary>Whether advanced alarm options are necessary (Android 9 and below).</summary>
	public bool IsNecessary => !(DeviceInfo.Platform == DevicePlatform.Android && DeviceInfo.Version.Major >= 10)
							   && DeviceInfo.Platform != DevicePlatform.iOS;

	#endregion

	#region Constructor

	/// <summary>
	/// Initializes settings ViewModel with localization and RTL services.
	/// </summary>
	/// <param name="resourceManager">Localization resource manager for culture switching.</param>
	/// <param name="rtlService">RTL layout service for bidirectional languages.</param>
    /// <param name="alarmService">Service for managing alarms and notifications.</param>
    /// <param name="widgetService">Service for updating widgets.</param>
	/// <param name="perf">Optional performance monitoring service.</param>
	public SettingsViewModel(
        ILocalizationResourceManager resourceManager, 
        IRtlService rtlService, 
        IAlarmService alarmService,
        IWidgetService widgetService,
        PerformanceService? perf = null)
	{
		_perf = perf ?? new PerformanceService();
		_resourceManager = resourceManager;
		_rtlService = rtlService;
        _alarmService = alarmService;
        _widgetService = widgetService;

		using (_perf.StartTimer("Settings.Constructor.Total"))
		{
			IsBusy = true;
			InitializeSettings();
			IsBusy = false;
		}

		// Log perf summary after a short delay to capture any async operations (wrapped in try-catch for safety)
		try
		{
			Application.Current?.Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(1), () =>
			{
				try { _perf.LogSummary("SettingsView"); }
				catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"SettingsView perf log failed: {ex.Message}"); }
			});
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"SettingsView DispatchDelayed setup failed: {ex.Message}");
		}
	}

	#endregion

	#region Commands

	/// <summary>
	/// Handles radio button theme selection.
	/// Defers theme application to avoid blocking UI during animation.
	/// </summary>
	[RelayCommand]
	private void RadioButtonCheckedChanged(object obj)
	{
		if (obj is not RadioButton { IsChecked: true, Value: not null } radiobutton) return;

		int themeValue = Convert.ToInt32(radiobutton.Value.ToString());
		ApplyThemeInternal(themeValue);
		// Defer theme application to next frame to avoid blocking radio button animation
		_ = ApplyThemeDeferredAsync(themeValue);
	}

	/// <summary>
	/// Sets theme by index when tapping entire row.
	/// </summary>
	/// <param name="parameter">Theme index: 0=Dark, 1=Light, 2=System.</param>
	[RelayCommand]
	private void SetThemeByIndex(object parameter)
	{
		int index = ParseThemeIndex(parameter);
		try
		{
			_suppressThemeUpdates = true;
			LightChecked = index == 1;
			DarkChecked = index == 0;
			SystemChecked = index == 2;
		}
		finally
		{
			_suppressThemeUpdates = false;
		}
		ApplyThemeInternal(index);
		// Defer theme application to next frame
		_ = ApplyThemeDeferredAsync(index);
	}
	
	/// <summary>
	/// Applies theme change on next frame to avoid blocking current UI operations.
	/// </summary>
	private async Task ApplyThemeDeferredAsync(int themeValue)
	{
		try
		{
			await Task.Yield(); // Yield to next frame
			MainThread.BeginInvokeOnMainThread(() =>
			{
				try { SetUserAppTheme(themeValue); }
				catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"SetUserAppTheme failed: {ex.Message}"); }
			});
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"ApplyThemeDeferredAsync failed: {ex.Message}");
		}
	}

	/// <summary>Opens system app settings.</summary>
	[RelayCommand]
	private void GotoSettings() => AppInfo.ShowSettingsUI();

	/// <summary>Navigates back to previous page.</summary>
	[RelayCommand]
	private async Task GoBackAsync() => await Shell.Current.GoToAsync("..");

	#endregion

	#region Private Methods - Initialization

	/// <summary>
	/// Initializes all settings from saved preferences.
	/// </summary>
	private void InitializeSettings()
	{
		// Restore saved language, RTL, and font
		using (_perf.StartTimer("Settings.Init.Language"))
		{
			var savedCi = Preferences.Get("SelectedLanguage", "tr");
			_resourceManager.CurrentCulture = CultureInfo.GetCultureInfo(savedCi);
			_rtlService.ApplyFlowDirection(savedCi);
			ApplyLanguageFont(savedCi);
		}

		// Load language list
		using (_perf.StartTimer("Settings.Init.LoadLanguages"))
		{
			LoadLanguagesInternal();
		}

		// Restore theme settings
		using (_perf.StartTimer("Settings.Init.Theme"))
		{
			CurrentTheme = (int)Theme.CurrentTheme;
			_suppressThemeUpdates = true;
			LightChecked = CurrentTheme == 1;
			DarkChecked = CurrentTheme == 0;
			SystemChecked = CurrentTheme == 2;
			_suppressThemeUpdates = false;
		}

		// Restore service settings
		using (_perf.StartTimer("Settings.Init.ServiceSettings"))
		{
			ForegroundServiceEnabled = Preferences.Get("ForegroundServiceEnabled", true);
			NotificationPrayerTimesEnabled = Preferences.Get("NotificationPrayerTimesEnabled", false);
			AlwaysRenewLocationEnabled = Preferences.Get("AlwaysRenewLocationEnabled", false);
		}
	}

	/// <summary>
	/// Loads supported languages and sets current selection.
	/// Uses native script names for universal recognition.
	/// </summary>
	/// <param name="reselectCi">Optional culture to reselect after loading.</param>
	private void LoadLanguagesInternal(string? reselectCi = null)
	{
		// Use native script names so users can find their language regardless of current UI language
		SupportedLanguages =
		[
			new Language("العربية", "ar"),           // Arabic
			new Language("Azərbaycan", "az"),        // Azerbaijani
			new Language("中文", "zh"),               // Chinese
			new Language("Deutsch", "de"),           // German
			new Language("English", "en"),           // English
			new Language("فارسی", "fa"),             // Farsi/Persian
			new Language("Français", "fr"),          // French
			new Language("Русский", "ru"),           // Russian
			new Language("Türkçe", "tr"),            // Turkish
			new Language("ئۇيغۇرچە", "ug"),          // Uyghur
			new Language("Oʻzbekcha", "uz")          // Uzbek
		];

		var targetCi = reselectCi ?? _resourceManager?.CurrentCulture.TwoLetterISOLanguageName;
		var match = SupportedLanguages.FirstOrDefault(l => l.CI == targetCi) ?? SupportedLanguages.First();
		_selectedLanguage = match;
		OnPropertyChanged(nameof(SelectedLanguage));
	}

	#endregion

	#region Private Methods - Language

	/// <summary>
	/// Applies language change to app culture, resources, and widgets.
	/// Widget update is dispatched to background to avoid UI blocking.
	/// </summary>
	private void ApplyLanguageChange(Language language)
	{
		using (_perf.StartTimer("Settings.ChangeLanguage"))
		{
			var ci = CultureInfo.GetCultureInfo(language.CI);
			_resourceManager.CurrentCulture = ci;
			AppResources.Culture = ci;
			Preferences.Set("SelectedLanguage", language.CI);
			_rtlService.ApplyFlowDirection(language.CI);
			ApplyLanguageFont(language.CI);
			Title = AppResources.UygulamaAyarlari;
		}
		// Update widget asynchronously to avoid blocking UI
		_ = Task.Run(() => MainThread.BeginInvokeOnMainThread(_widgetService.UpdateWidget));
	}

	/// <summary>
	/// Applies language-specific font family.
	/// Uyghur uses UKIJTuT font; other languages use OpenSans.
	/// </summary>
	/// <param name="cultureCode">Two-letter ISO language code.</param>
	private static void ApplyLanguageFont(string cultureCode)
	{
		var app = Application.Current;
		if (app?.Resources == null) return;

		var fontFamily = cultureCode == "ug" ? "UyghurFont" : "OpenSansRegular";
		app.Resources["AppFontFamily"] = fontFamily;
	}

	#endregion

	#region Private Methods - Theme

	/// <summary>
	/// Parses theme index from command parameter.
	/// </summary>
	private static int ParseThemeIndex(object parameter)
	{
		int index = parameter switch
		{
			int i => i,
			string s when int.TryParse(s, out var parsed) => parsed,
			_ => 2 // Default to System
		};
		return Math.Clamp(index, 0, 2);
	}

	/// <summary>
	/// Applies theme internally and persists selection.
	/// Widget update is dispatched asynchronously to avoid blocking UI.
	/// </summary>
	private void ApplyThemeInternal(int themeValue)
	{
		var themeMode = (ThemeMode)themeValue;

		// Idempotency check
		if (Theme.CurrentTheme == themeMode && CurrentTheme == themeValue && Dark == (themeValue == 0))
			return;

		using (_perf.StartTimer("Settings.ApplyThemeInternal"))
		{
			Theme.CurrentTheme = themeMode;
			CurrentTheme = themeValue;
			Dark = themeValue == 0;

			using (_perf.StartTimer("Settings.ApplyThemeInternal.SavePrefs"))
			{
				Preferences.Set("SelectedTheme", themeValue switch
				{
					0 => "Dark",
					1 => "Light",
					_ => "System"
				});
			}
		}

		// Update widget asynchronously to avoid blocking UI
		_ = Task.Run(() =>
		{
			using (_perf.StartTimer("Settings.ApplyThemeInternal.UpdateWidget"))
			{
				MainThread.BeginInvokeOnMainThread(_widgetService.UpdateWidget);
			}
		});
	}

	/// <summary>
	/// Sets the application theme mode.
	/// </summary>
	private static void SetUserAppTheme(int themeValue)
	{
		var app = Application.Current;
		if (app == null) return;

		app.UserAppTheme = themeValue switch
		{
			0 => AppTheme.Dark,
			1 => AppTheme.Light,
			_ => AppTheme.Unspecified
		};
	}

	#endregion
}