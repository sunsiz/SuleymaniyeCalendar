using CommunityToolkit.Mvvm.Input;
using SuleymaniyeCalendar.Resources.Strings;
using System.Globalization;
using LocalizationResourceManager.Maui;
using SuleymaniyeCalendar.Models;
using SuleymaniyeCalendar.Services;
#if ANDROID
using Android.Content;
using Android.OS;
#endif

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

	private Language _selectedLanguage = new(AppResources.English, "en");

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

			// Refresh language names asynchronously
			_ = RefreshLanguageNamesAsync();
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
			MainThread.BeginInvokeOnMainThread(() => SetUserAppTheme(1));
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
			MainThread.BeginInvokeOnMainThread(() => SetUserAppTheme(0));
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
			MainThread.BeginInvokeOnMainThread(() => SetUserAppTheme(2));
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
#if ANDROID
			RefreshForegroundServiceNotification();
#endif
		}
	}

	/// <summary>
	/// Whether to show notification content option.
	/// Only visible when sticky notification is enabled on Android.
	/// </summary>
	public bool ShowNotificationPrayerOption =>
		DeviceInfo.Platform == DevicePlatform.Android && ForegroundServiceEnabled;

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
#if ANDROID
			// When turning off, also disable prayer times in notification
			if (!value)
			{
				NotificationPrayerTimesEnabled = false;
			}
#endif
			OnPropertyChanged(nameof(ShowNotificationPrayerOption));
#if ANDROID
			ToggleForegroundService(value);
#endif
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
	/// <param name="perf">Optional performance monitoring service.</param>
	public SettingsViewModel(ILocalizationResourceManager resourceManager, IRtlService rtlService, PerformanceService perf = null)
	{
		_perf = perf ?? new PerformanceService();
		_resourceManager = resourceManager;
		_rtlService = rtlService;

		IsBusy = true;
		InitializeSettings();
		IsBusy = false;
	}

	#endregion

	#region Commands

	/// <summary>
	/// Handles radio button theme selection.
	/// </summary>
	[RelayCommand]
	private void RadioButtonCheckedChanged(object obj)
	{
		if (obj is not RadioButton { IsChecked: true, Value: not null } radiobutton) return;

		int themeValue = Convert.ToInt32(radiobutton.Value.ToString());
		ApplyThemeInternal(themeValue);
		MainThread.BeginInvokeOnMainThread(() => SetUserAppTheme(themeValue));
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
		MainThread.BeginInvokeOnMainThread(() => SetUserAppTheme(index));
	}

	/// <summary>Opens system app settings.</summary>
	[RelayCommand]
	private void GotoSettings() => AppInfo.ShowSettingsUI();

	/// <summary>Navigates back to previous page.</summary>
	[RelayCommand]
	private void GoBack() => Shell.Current.GoToAsync("..");

	#endregion

	#region Private Methods - Initialization

	/// <summary>
	/// Initializes all settings from saved preferences.
	/// </summary>
	private void InitializeSettings()
	{
		// Restore saved language and apply RTL
		var savedCi = Preferences.Get("SelectedLanguage", "tr");
		_resourceManager.CurrentCulture = CultureInfo.GetCultureInfo(savedCi);
		_rtlService.ApplyFlowDirection(savedCi);

		// Load language list
		LoadLanguagesInternal();

		// Restore theme settings
		CurrentTheme = Theme.Tema;
		_suppressThemeUpdates = true;
		LightChecked = CurrentTheme == 1;
		DarkChecked = CurrentTheme == 0;
		SystemChecked = CurrentTheme == 2;
		_suppressThemeUpdates = false;

		// Restore service settings
		ForegroundServiceEnabled = Preferences.Get("ForegroundServiceEnabled", true);
		NotificationPrayerTimesEnabled = Preferences.Get("NotificationPrayerTimesEnabled", false);
		AlwaysRenewLocationEnabled = Preferences.Get("AlwaysRenewLocationEnabled", false);
	}

	/// <summary>
	/// Loads supported languages and sets current selection.
	/// </summary>
	/// <param name="reselectCi">Optional culture to reselect after loading.</param>
	private void LoadLanguagesInternal(string reselectCi = null)
	{
		SupportedLanguages =
		[
			new Language(AppResources.Arabic, "ar"),
			new Language(AppResources.Azerbaijani, "az"),
			new Language(AppResources.Chinese, "zh"),
			new Language(AppResources.Deutsch, "de"),
			new Language(AppResources.English, "en"),
			new Language(AppResources.Farsi, "fa"),
			new Language(AppResources.French, "fr"),
			new Language(AppResources.Russian, "ru"),
			new Language(AppResources.Turkish, "tr"),
			new Language(AppResources.Uyghur, "ug"),
			new Language(AppResources.Uzbek, "uz")
		];

		var targetCi = reselectCi ?? _resourceManager?.CurrentCulture.TwoLetterISOLanguageName;
		var match = SupportedLanguages.FirstOrDefault(l => l.CI == targetCi) ?? SupportedLanguages.FirstOrDefault();
		_selectedLanguage = match;
		OnPropertyChanged(nameof(SelectedLanguage));
	}

	#endregion

	#region Private Methods - Language

	/// <summary>
	/// Applies language change to app culture, resources, and widgets.
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
			Title = AppResources.UygulamaAyarlari;
#if ANDROID
			UpdateWidget();
#endif
		}
	}

	/// <summary>
	/// Refreshes language display names asynchronously after culture switch.
	/// </summary>
	private Task RefreshLanguageNamesAsync()
	{
		return Task.Run(() =>
		{
			try
			{
				var updated = new Dictionary<string, string>
				{
					{ "ar", AppResources.Arabic },
					{ "az", AppResources.Azerbaijani },
					{ "zh", AppResources.Chinese },
					{ "de", AppResources.Deutsch },
					{ "en", AppResources.English },
					{ "fa", AppResources.Farsi },
					{ "fr", AppResources.French },
					{ "ru", AppResources.Russian },
					{ "tr", AppResources.Turkish },
					{ "ug", AppResources.Uyghur },
					{ "uz", AppResources.Uzbek }
				};

				MainThread.BeginInvokeOnMainThread(() =>
				{
					bool anyChanged = false;
					foreach (var lang in SupportedLanguages)
					{
						if (updated.TryGetValue(lang.CI, out var newName) && lang.Name != newName)
						{
							lang.Name = newName;
							anyChanged = true;
						}
					}
					if (anyChanged)
					{
						OnPropertyChanged(nameof(SupportedLanguages));
						OnPropertyChanged(nameof(SelectedLanguage));
					}
				});
			}
			catch { /* ignore background refresh issues */ }
		});
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
	/// </summary>
	private void ApplyThemeInternal(int themeValue)
	{
		// Idempotency check
		if (Theme.Tema == themeValue && CurrentTheme == themeValue && Dark == (themeValue == 0))
			return;

		using (_perf.StartTimer("Settings.ApplyThemeInternal"))
		{
			Theme.Tema = themeValue;
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

			// Optional simplified gradient mode for performance testing
			ApplySimplifiedGradientsIfEnabled();

#if ANDROID
			using (_perf.StartTimer("Settings.ApplyThemeInternal.UpdateWidget"))
			{
				UpdateWidget();
			}
#endif
		}
	}

	/// <summary>
	/// Replaces heavy gradient brushes with flat colors if SimplifiedGradients is enabled.
	/// </summary>
	private static void ApplySimplifiedGradientsIfEnabled()
	{
		if (!Preferences.Get("SimplifiedGradients", false)) return;

		var app = Application.Current;
		if (app?.Resources == null) return;

		if (app.Resources.ContainsKey("PrimaryGradientBrush"))
			app.Resources["PrimaryGradientBrush"] = new SolidColorBrush((Color)app.Resources["PrimaryColor"]);
		if (app.Resources.ContainsKey("SurfaceGlassBrushLight"))
			app.Resources["SurfaceGlassBrushLight"] = new SolidColorBrush((Color)app.Resources["SurfaceVariantColor"]);
		if (app.Resources.ContainsKey("SurfaceGlassBrushDark"))
			app.Resources["SurfaceGlassBrushDark"] = new SolidColorBrush((Color)app.Resources["SurfaceVariantColorDark"]);
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

#if ANDROID
	#region Private Methods - Android Platform

	/// <summary>
	/// Updates Android home screen widget after theme/language change.
	/// </summary>
	private void UpdateWidget()
	{
		try
		{
			var context = Platform.CurrentActivity ?? Android.App.Application.Context;
			context?.StartService(new Intent(context, typeof(WidgetService)));
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error updating widget: {ex.Message}");
		}
	}

	/// <summary>
	/// Refreshes foreground service notification content.
	/// </summary>
	private void RefreshForegroundServiceNotification()
	{
		try
		{
			if (!Preferences.Get("ForegroundServiceEnabled", true)) return;

			var ctx = Android.App.Application.Context;
			var refreshIntent = new Intent(ctx, typeof(AlarmForegroundService))
				.SetAction("SuleymaniyeTakvimi.action.REFRESH_NOTIFICATION");

			if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
				ctx.StartForegroundService(refreshIntent);
			else
				ctx.StartService(refreshIntent);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error refreshing notification: {ex}");
		}
	}

	/// <summary>
	/// Starts or stops the alarm foreground service.
	/// </summary>
	private void ToggleForegroundService(bool enable)
	{
		try
		{
			var ctx = Android.App.Application.Context;
			var intent = new Intent(ctx, typeof(AlarmForegroundService))
				.SetAction(enable 
					? "SuleymaniyeTakvimi.action.START_SERVICE" 
					: "SuleymaniyeTakvimi.action.STOP_SERVICE");

			if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
				ctx.StartForegroundService(intent);
			else
				ctx.StartService(intent);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error toggling foreground service: {ex}");
		}
	}

	#endregion
#endif
}