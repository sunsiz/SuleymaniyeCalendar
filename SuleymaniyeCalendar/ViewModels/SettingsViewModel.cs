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
using SuleymaniyeCalendar.Services;
#if ANDROID
using Android.Content;
using Android.OS;
#endif

namespace SuleymaniyeCalendar.ViewModels
{
	public partial class SettingsViewModel : BaseViewModel
	{
		private IList<Language> supportedLanguages = Enumerable.Empty<Language>().ToList();
		public IList<Language> SupportedLanguages { get => supportedLanguages; set => SetProperty(ref supportedLanguages, value); }

		private Language selectedLanguage = new Language(AppResources.English, "en");
		public Language SelectedLanguage
		{
			get => selectedLanguage;
			set
			{
				if (SetProperty(ref selectedLanguage, value))
				{
					if (value is not null)
					{
						var ci = CultureInfo.GetCultureInfo(value.CI);
						resourceManager.CurrentCulture = ci;
						AppResources.Culture = ci;
						Preferences.Set("SelectedLanguage", value.CI);
						rtlService.ApplyFlowDirection(value.CI);
						Title = AppResources.UygulamaAyarlari;
#if ANDROID
						UpdateWidget();
#endif
					}
				}
			}
		}

		private bool dark;
		public bool Dark { get => dark; set => SetProperty(ref dark, value); }

		private int currentTheme;
		public int CurrentTheme
		{
			get => currentTheme;
			set
			{
				if (SetProperty(ref currentTheme, value))
				{
					MainThread.BeginInvokeOnMainThread(() => SetUserAppTheme(value));
				}
			}
		}

		private bool lightChecked;
		public bool LightChecked
		{
			get => lightChecked;
			set
			{
				if (SetProperty(ref lightChecked, value) && value)
				{
					ApplyThemeInternal(1);
					MainThread.BeginInvokeOnMainThread(() => SetUserAppTheme(1));
				}
			}
		}

		private bool darkChecked;
		public bool DarkChecked
		{
			get => darkChecked;
			set
			{
				if (SetProperty(ref darkChecked, value) && value)
				{
					ApplyThemeInternal(0);
					MainThread.BeginInvokeOnMainThread(() => SetUserAppTheme(0));
				}
			}
		}

		private bool systemChecked;
		public bool SystemChecked
		{
			get => systemChecked;
			set
			{
				if (SetProperty(ref systemChecked, value) && value)
				{
					ApplyThemeInternal(2);
					MainThread.BeginInvokeOnMainThread(() => SetUserAppTheme(2));
				}
			}
		}

		//private int alarmDuration;
		//public int AlarmDuration { get => alarmDuration; set => SetProperty(ref alarmDuration, value); }

		private bool alwaysRenewLocationEnabled;
		public bool AlwaysRenewLocationEnabled
		{
			get => alwaysRenewLocationEnabled;
			set
			{
				if (SetProperty(ref alwaysRenewLocationEnabled, value))
				{
					Preferences.Set("AlwaysRenewLocationEnabled", value);
				}
			}
		}

		private bool notificationPrayerTimesEnabled;
		public bool NotificationPrayerTimesEnabled
		{
			get => notificationPrayerTimesEnabled;
			set
			{
				if (SetProperty(ref notificationPrayerTimesEnabled, value))
				{
					Preferences.Set("NotificationPrayerTimesEnabled", value);
#if ANDROID
					// If the service is running, ask it to refresh the notification now
					if (Preferences.Get("ForegroundServiceEnabled", true))
					{
						var ctx = Android.App.Application.Context;
						var refreshIntent = new Intent(ctx, typeof(AlarmForegroundService))
							.SetAction("SuleymaniyeTakvimi.action.REFRESH_NOTIFICATION");
						if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
							ctx.StartForegroundService(refreshIntent);
						else
							ctx.StartService(refreshIntent);
					}
#endif
				}
			}
		}

		// UI helper: Only show the notification content option when sticky notification (foreground service) is enabled on Android
		public bool ShowNotificationPrayerOption
		{
			get => DeviceInfo.Platform == DevicePlatform.Android && ForegroundServiceEnabled;
		}

		private bool foregroundServiceEnabled;
		public bool ForegroundServiceEnabled
		{
			get => foregroundServiceEnabled;
			set
			{
				if (SetProperty(ref foregroundServiceEnabled, value))
				{
					Preferences.Set("ForegroundServiceEnabled", value);
#if ANDROID
					// When turning off the sticky notification, also disable showing prayer times in it and update UI visibility
					if (!value)
					{
						NotificationPrayerTimesEnabled = false;
						Preferences.Set("ForegroundServiceEnabled", value);
					}
#endif
					OnPropertyChanged(nameof(ShowNotificationPrayerOption));
#if ANDROID
					var ctx = Android.App.Application.Context;
					if (!value)
					{
						// Stop the foreground service if it's running
						var stopIntent = new Intent(ctx, typeof(AlarmForegroundService));
						stopIntent.SetAction("SuleymaniyeTakvimi.action.STOP_SERVICE");
						if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
							ctx.StartForegroundService(stopIntent);
						else
							ctx.StartService(stopIntent);
					}
					else
					{
						// Start the foreground service
						var startIntent = new Intent(ctx, typeof(AlarmForegroundService));
						startIntent.SetAction("SuleymaniyeTakvimi.action.START_SERVICE");
						if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
							ctx.StartForegroundService(startIntent);
						else
							ctx.StartService(startIntent);
					}
#endif
				}
			}
		}
		private readonly ILocalizationResourceManager resourceManager;
		private readonly IRtlService rtlService;
		// Property change side-effects moved into property setters below for AOT safety

		public bool IsNecessary => !((DeviceInfo.Platform == DevicePlatform.Android && DeviceInfo.Version.Major >= 10) || DeviceInfo.Platform == DevicePlatform.iOS);

		public SettingsViewModel(ILocalizationResourceManager resourceManager, IRtlService rtlService)
		{
			IsBusy = true;
			this.resourceManager = resourceManager;
			this.rtlService = rtlService;
			// Ensure resource manager reflects saved language before loading list/selection
			var savedCi = Preferences.Get("SelectedLanguage", "tr");
			this.resourceManager.CurrentCulture = CultureInfo.GetCultureInfo(savedCi);
			
			// Apply RTL for the saved language
			rtlService.ApplyFlowDirection(savedCi);
			
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
		// Property change side-effects are applied in SelectedLanguage setter

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

		// CurrentTheme setter contains the UI theme application logic

		// Radio button check setters below trigger ApplyThemeInternal

		// See property setters

		// See property setters

		private void ApplyThemeInternal(int themeValue)
		{
			Theme.Tema = themeValue;
			CurrentTheme = themeValue;
			Dark = themeValue == 0;
			
			// Save theme preference for widget
			Preferences.Set("SelectedTheme", themeValue switch
			{
				0 => "Dark",
				1 => "Light",
				2 => "System",
				_ => "System"
			});

#if ANDROID
			// Update widget when theme changes
			UpdateWidget();
#endif
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

#if ANDROID
		private void UpdateWidget()
		{
			try
			{
				var context = Platform.CurrentActivity ?? Android.App.Application.Context;
				if (context != null)
				{
					context.StartService(new Android.Content.Intent(context, typeof(WidgetService)));
				}
			}
			catch (System.Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Error updating widget: {ex.Message}");
			}
		}
#endif

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
