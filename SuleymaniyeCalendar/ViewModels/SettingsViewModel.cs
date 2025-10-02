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
		private readonly PerformanceService _perf;
		// Guard flags to prevent recursive language reload loops
		private bool _updatingLanguages;
		// Prevent re-entrant/duplicated theme updates when toggling via gestures and radios
		private bool suppressThemeUpdates;
		private IList<Language> supportedLanguages = Enumerable.Empty<Language>().ToList();
		public IList<Language> SupportedLanguages { get => supportedLanguages; set => SetProperty(ref supportedLanguages, value); }

		// Original simple placeholder; real selection loaded in constructor via LoadLanguagesInternal.
		private Language selectedLanguage = new Language(AppResources.English, "en");
		// (Reverted) Simplified language change logic – advanced debounce removed due to hang issues.
		public Language SelectedLanguage
		{
			get => selectedLanguage;
			set
			{
				// Prevent infinite recursion when we rebuild SupportedLanguages or set selection programmatically
				if (_updatingLanguages)
				{
					selectedLanguage = value;
					OnPropertyChanged(nameof(SelectedLanguage));
					return;
				}

				if (SetProperty(ref selectedLanguage, value))
				{
					if (value is null) return;
					// Skip if already active culture
					if (string.Equals(resourceManager?.CurrentCulture?.TwoLetterISOLanguageName, value.CI, StringComparison.OrdinalIgnoreCase))
						return;
					_updatingLanguages = true;
					try
					{
						using (_perf.StartTimer("Settings.ChangeLanguage"))
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
					finally
					{
						_updatingLanguages = false;
					}

					// OPTIONAL: Refresh displayed language names asynchronously without rebuilding the list structure.
					_ = Task.Run(() =>
					{
						try
						{
							// Capture localized names after culture switch
							var updated = new Dictionary<string, string>
							{
								{"ar", AppResources.Arabic},
								{"az", AppResources.Azerbaijani},
								{"zh", AppResources.Chinese},
								{"de", AppResources.Deutsch},
								{"en", AppResources.English},
								{"fa", AppResources.Farsi},
								{"fr", AppResources.French},
								{"ru", AppResources.Russian},
								{"tr", AppResources.Turkish},
								{"ug", AppResources.Uyghur},
								{"uz", AppResources.Uzbek}
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
									// Also trigger SelectedLanguage PropertyChanged so Picker refreshes display
									OnPropertyChanged(nameof(SelectedLanguage));
								}
							});
						}
						catch { /* ignore background refresh issues */ }
					});
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
				if (!SetProperty(ref lightChecked, value)) return;
				if (suppressThemeUpdates) return;
				if (value)
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
				if (!SetProperty(ref darkChecked, value)) return;
				if (suppressThemeUpdates) return;
				if (value)
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
				if (!SetProperty(ref systemChecked, value)) return;
				if (suppressThemeUpdates) return;
				if (value)
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
					try
					{
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
					}
					catch (System.Exception ex)
					{
						System.Diagnostics.Debug.WriteLine($"Error refreshing notification: {ex}");
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
					try
					{
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
					}
					catch (System.Exception ex)
					{
						System.Diagnostics.Debug.WriteLine($"Error toggling foreground service: {ex}");
					}
#endif
				}
			}
		}
		private readonly ILocalizationResourceManager resourceManager;
		private readonly IRtlService rtlService;
		// Property change side-effects moved into property setters below for AOT safety

		public bool IsNecessary => !((DeviceInfo.Platform == DevicePlatform.Android && DeviceInfo.Version.Major >= 10) || DeviceInfo.Platform == DevicePlatform.iOS);

		public SettingsViewModel(ILocalizationResourceManager resourceManager, IRtlService rtlService, PerformanceService perf = null)
		{
			_perf = perf ?? new PerformanceService();
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
			// Initialize radio flags from current theme (suppress side-effects to avoid loops)
			suppressThemeUpdates = true;
			LightChecked = CurrentTheme == 1;
			DarkChecked = CurrentTheme == 0;
			SystemChecked = CurrentTheme == 2;
			suppressThemeUpdates = false;
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

		// Allow tapping the entire row to set theme: 0=Dark,1=Light,2=System
		[RelayCommand]
		private void SetThemeByIndex(object parameter)
		{
			int index = 2; // default to System
			try
			{
				// Convert parameter to int robustly (handles string, boxed int, null)
				if (parameter is int i)
					index = i;
				else if (parameter is string s && int.TryParse(s, out var parsed))
					index = parsed;
			}
			catch { /* ignore and use default */ }
			index = Math.Clamp(index, 0, 2);
			try
			{
				suppressThemeUpdates = true;
				LightChecked = index == 1;
				DarkChecked = index == 0;
				SystemChecked = index == 2;
			}
			finally
			{
				suppressThemeUpdates = false;
			}
			ApplyThemeInternal(index);
			MainThread.BeginInvokeOnMainThread(() => SetUserAppTheme(index));
		}

		// CurrentTheme setter contains the UI theme application logic

		// Radio button check setters below trigger ApplyThemeInternal

		// See property setters

		// See property setters

		private void ApplyThemeInternal(int themeValue)
		{
			// Idempotency: skip full apply if no change
			if (Theme.Tema == themeValue && CurrentTheme == themeValue && Dark == (themeValue == 0))
				return;

			using (_perf.StartTimer("Settings.ApplyThemeInternal"))
			{
				// Persist selection first
				Theme.Tema = themeValue;
				var previousTheme = CurrentTheme;
				CurrentTheme = themeValue; // triggers SetUserAppTheme via setter if used elsewhere
				Dark = themeValue == 0;

				using (_perf.StartTimer("Settings.ApplyThemeInternal.SavePrefs"))
				{
					Preferences.Set("SelectedTheme", themeValue switch
					{
						0 => "Dark",
						1 => "Light",
						2 => "System",
						_ => "System"
					});
				}

				// Optional simplified gradient mode to test theme speed differences
				bool simplifiedGradients = Preferences.Get("SimplifiedGradients", false);
				if (simplifiedGradients)
				{
					using (_perf.StartTimer("Settings.ApplyThemeInternal.Simplify"))
					{
						var app = Application.Current;
						if (app?.Resources != null)
						{
							// Replace heavy gradient brushes with flat color placeholders (non-destructive test)
							if (app.Resources.ContainsKey("PrimaryGradientBrush"))
								app.Resources["PrimaryGradientBrush"] = new SolidColorBrush((Color)app.Resources["PrimaryColor"]);
							if (app.Resources.ContainsKey("SurfaceGlassBrushLight"))
								app.Resources["SurfaceGlassBrushLight"] = new SolidColorBrush((Color)app.Resources["SurfaceVariantColor"]);
							if (app.Resources.ContainsKey("SurfaceGlassBrushDark"))
								app.Resources["SurfaceGlassBrushDark"] = new SolidColorBrush((Color)app.Resources["SurfaceVariantColorDark"]);
						}
					}
				}

#if ANDROID
				using (_perf.StartTimer("Settings.ApplyThemeInternal.UpdateWidget"))
				{
					UpdateWidget();
				}
#endif
			}
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
		private void LoadLanguagesInternal(string reselectCi = null)
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
			var targetCi = reselectCi ?? resourceManager?.CurrentCulture.TwoLetterISOLanguageName;
			var match = SupportedLanguages.FirstOrDefault(l => l.CI == targetCi) ?? SupportedLanguages.FirstOrDefault();
			// Directly set backing field under guard to avoid recursion
			selectedLanguage = match;
			OnPropertyChanged(nameof(SelectedLanguage));
		}

		void LoadLanguages() => LoadLanguagesInternal();
	}
}
