using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SuleymaniyeCalendar.Resources.Strings;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LocalizationResourceManager.Maui;
using SuleymaniyeCalendar.Models;

namespace SuleymaniyeCalendar.ViewModels
{
	public partial class SettingsViewModel : BaseViewModel
	{
		[ObservableProperty] private IList<Language> supportedLanguages = Enumerable.Empty<Language>().ToList();
		[ObservableProperty] private Language selectedLanguage = new Language(AppResources.English, "en");
		[ObservableProperty] private bool dark;
		[ObservableProperty] private int currentTheme;
		[ObservableProperty] private bool lightChecked;
		[ObservableProperty] private bool darkChecked;
		[ObservableProperty] private bool systemChecked;
		[ObservableProperty] private int alarmDuration;
		[ObservableProperty] private bool alwaysRenewLocationEnabled;
		[ObservableProperty] private bool notificationPrayerTimesEnabled;
		[ObservableProperty] private bool foregroundServiceEnabled;
		private readonly ILocalizationResourceManager resourceManager;
		//partial void OnAlarmDurationChanged(int value) { if (AlarmDuration != value) Preferences.Set("AlarmDuration", value); }
		partial void OnAlwaysRenewLocationEnabledChanged(bool value) { if (AlwaysRenewLocationEnabled != value) Preferences.Set("AlwaysRenewLocationEnabled", value); }
		partial void OnNotificationPrayerTimesEnabledChanged(bool value) { if (NotificationPrayerTimesEnabled != value) Preferences.Set("NotificationPrayerTimesEnabled", value); }
		partial void OnForegroundServiceEnabledChanged(bool value) { if (ForegroundServiceEnabled != value) Preferences.Set("ForegroundServiceEnabled", value); }
		// handled below (single definition)

		public bool IsNecessary => !((DeviceInfo.Platform == DevicePlatform.Android && DeviceInfo.Version.Major >= 10) || DeviceInfo.Platform == DevicePlatform.iOS);

		public SettingsViewModel(ILocalizationResourceManager resourceManager)
		{
			IsBusy = true;
			this.resourceManager = resourceManager;
			// Ensure resource manager reflects saved language before loading list/selection
			var savedCi = Preferences.Get("SelectedLanguage", "tr");
			this.resourceManager.CurrentCulture = CultureInfo.GetCultureInfo(savedCi);
			LoadLanguages(); // sets SelectedLanguage based on resourceManager culture
			// Use saved theme (0=Dark,1=Light,2=System)
			CurrentTheme = Theme.Tema;
			// Initialize radio flags from current theme
			LightChecked = CurrentTheme == 1;
			DarkChecked = CurrentTheme == 0;
			SystemChecked = CurrentTheme == 2;
			//AlarmDuration = Preferences.Get("AlarmDuration", 4);
			ForegroundServiceEnabled = Preferences.Get("ForegroundServiceEnabled", true);
			NotificationPrayerTimesEnabled = Preferences.Get("NotificationPrayerTimesEnabled", false);
			AlwaysRenewLocationEnabled = Preferences.Get("AlwaysRenewLocationEnabled", false);
			IsBusy = false;
		}
		partial void OnSelectedLanguageChanged(Language value)
		{
			if (value is null) return;
			resourceManager.CurrentCulture = CultureInfo.GetCultureInfo(value.CI);
			Preferences.Set("SelectedLanguage", value.CI);
			// keep current selection and labels fresh
			Title = AppResources.UygulamaAyarlari;
		}

		[RelayCommand]
		private void RadioButtonCheckedChanged(object obj)
		{
			var radiobutton = obj as RadioButton;
			// Only react when the button becomes checked (ignore uncheck events)
			if (radiobutton is null || !radiobutton.IsChecked || radiobutton.Value is null)
				return;

			int themeValue = Convert.ToInt32(radiobutton.Value.ToString()); // 0=Dark,1=Light,2=System
			ApplyThemeInternal(themeValue);

			// Apply immediately on the UI thread
			MainThread.BeginInvokeOnMainThread(() =>
			{
				SetUserAppTheme(themeValue);
			});
		}

		partial void OnCurrentThemeChanged(int value)
		{
			MainThread.BeginInvokeOnMainThread(() => SetUserAppTheme(value));
		}

		partial void OnLightCheckedChanged(bool value)
		{
			if (value)
			{
				ApplyThemeInternal(1);
				MainThread.BeginInvokeOnMainThread(() => SetUserAppTheme(1));
			}
		}

		partial void OnDarkCheckedChanged(bool value)
		{
			if (value)
			{
				ApplyThemeInternal(0);
				MainThread.BeginInvokeOnMainThread(() => SetUserAppTheme(0));
			}
		}

		partial void OnSystemCheckedChanged(bool value)
		{
			if (value)
			{
				ApplyThemeInternal(2);
				MainThread.BeginInvokeOnMainThread(() => SetUserAppTheme(2));
			}
		}

		private void ApplyThemeInternal(int themeValue)
		{
			Theme.Tema = themeValue;
			CurrentTheme = themeValue;
			Dark = themeValue == 0;
		}

		private static void SetUserAppTheme(int themeValue)
		{
			var app = Application.Current;
			if (app == null) return;
			app.UserAppTheme = themeValue switch
			{
				0 => AppTheme.Dark,
				1 => AppTheme.Light,
				2 => AppTheme.Unspecified,
				_ => AppTheme.Unspecified
			};
		}

		[RelayCommand]
		private void GotoSettings(){AppInfo.ShowSettingsUI();}

		[RelayCommand]
		private void GoBack() { Shell.Current.GoToAsync(".."); }
		void LoadLanguages()
		{
			SupportedLanguages = new List<Language>()
			{
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
				{ new Language(AppResources.Uzbek, "uz") }
			};
			SelectedLanguage = SupportedLanguages.FirstOrDefault(lan => lan.CI == resourceManager?.CurrentCulture.TwoLetterISOLanguageName);
		}
	}
}
