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
		[ObservableProperty] private int alarmDuration;
		[ObservableProperty] private bool alwaysRenewLocationEnabled;
		[ObservableProperty] private bool notificationPrayerTimesEnabled;
		[ObservableProperty] private bool foregroundServiceEnabled;
		private readonly ILocalizationResourceManager resourceManager;
		partial void OnAlarmDurationChanged(int value) { if (AlarmDuration != value) Preferences.Set("AlarmDuration", value); }
		partial void OnAlwaysRenewLocationEnabledChanged(bool value) { if (AlwaysRenewLocationEnabled != value) Preferences.Set("AlwaysRenewLocationEnabled", value); }
		partial void OnNotificationPrayerTimesEnabledChanged(bool value) { if (NotificationPrayerTimesEnabled != value) Preferences.Set("NotificationPrayerTimesEnabled", value); }
		partial void OnForegroundServiceEnabledChanged(bool value) { if (ForegroundServiceEnabled != value) Preferences.Set("ForegroundServiceEnabled", value); }

		public bool IsNecessary => !((DeviceInfo.Platform == DevicePlatform.Android && DeviceInfo.Version.Major >= 10) || DeviceInfo.Platform == DevicePlatform.iOS);

		public SettingsViewModel(ILocalizationResourceManager resourceManager)
		{
			IsBusy = true;
			LoadLanguages();
			CurrentTheme = Application.Current != null && Application.Current.RequestedTheme == AppTheme.Dark ? 0 : 1;
			AlarmDuration = Preferences.Get("AlarmDuration", 4);
			ForegroundServiceEnabled = Preferences.Get("ForegroundServiceEnabled", true);
			NotificationPrayerTimesEnabled = Preferences.Get("NotificationPrayerTimesEnabled", false);
			AlwaysRenewLocationEnabled = Preferences.Get("AlwaysRenewLocationEnabled", false);
			this.resourceManager = resourceManager;
			IsBusy = false;
		}
		[RelayCommand]
		private void ChangeLanguage()
		{
			resourceManager.CurrentCulture = CultureInfo.GetCultureInfo(SelectedLanguage.CI);
			Preferences.Set("SelectedLanguage", SelectedLanguage.CI);
			LoadLanguages();
			GoBack();
		}

		[RelayCommand]
		private void RadioButtonCheckedChanged(object obj)
		{
			if (!IsBusy)
			{
				var radiobutton = obj as RadioButton;
				Theme.Tema = Convert.ToInt32(radiobutton?.Value.ToString());
				Dark = radiobutton?.Value.ToString() == "0";
			}
		}

		[RelayCommand]
		private void GotoSettings(){AppInfo.ShowSettingsUI();}

		[RelayCommand]
		private void GoBack()
		{
			if (Application.Current != null)
				Application.Current.UserAppTheme = Theme.Tema == 1 ? AppTheme.Light : AppTheme.Dark;
			Shell.Current.GoToAsync("..");
		}
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
