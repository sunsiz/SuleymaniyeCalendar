using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.OS;
using Android.Widget;
using SuleymaniyeCalendar.Resources.Strings;
using System.Globalization;
using SuleymaniyeCalendar.Services;
using Java.Util;
using Locale = Java.Util.Locale;
using Android.Content.Res;

namespace SuleymaniyeCalendar
{
	[Service]
	public class WidgetService : IntentService
	{
		private const string TAG = "WidgetService";
		
		[Obsolete]
		public override void OnStart (Intent? intent, int startId)
		{
			UpdateWidgetSafely(intent);
		}
		
		public override IBinder? OnBind(Intent? intent) => null;

		protected override void OnHandleIntent(Intent? intent)
		{
			UpdateWidgetSafely(intent);
		}

		/// <summary>
		/// Safe widget update with comprehensive error handling and performance optimization
		/// </summary>
		private void UpdateWidgetSafely(Intent? intent)
		{
			try
			{
				var updateViews = BuildUpdate(this);
				if (updateViews != null)
				{
					var thisWidget = new ComponentName(this, Java.Lang.Class.FromType(typeof(AppWidget)).Name);
					var manager = AppWidgetManager.GetInstance(this);
					manager?.UpdateAppWidget(thisWidget, updateViews);
					
					System.Diagnostics.Debug.WriteLine($"{TAG}: Widget updated successfully");
				}
				else
				{
					System.Diagnostics.Debug.WriteLine($"{TAG}: Widget update failed - null RemoteViews");
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"{TAG}: Widget update error: {ex.Message}");
				// Create fallback widget with minimal info
				try
				{
					var fallbackViews = CreateFallbackWidget(this);
					var thisWidget = new ComponentName(this, Java.Lang.Class.FromType(typeof(AppWidget)).Name);
					var manager = AppWidgetManager.GetInstance(this);
					manager?.UpdateAppWidget(thisWidget, fallbackViews);
				}
				catch (Exception fallbackEx)
				{
					System.Diagnostics.Debug.WriteLine($"{TAG}: Fallback widget creation failed: {fallbackEx.Message}");
				}
			}
		}

		/// <summary>
		/// Create fallback widget when main update fails
		/// </summary>
		private RemoteViews CreateFallbackWidget(Context context)
		{
			var language = Preferences.Get("SelectedLanguage", "tr");
			var rtlService = new RtlService();
			var isRtl = rtlService.IsRtlLanguage(language);
			
			// Use the existing layout determination method
			var layoutResource = GetWidgetLayout(false, isRtl);
			var updateViews = new RemoteViews(context.PackageName, layoutResource);
			
			// Basic error message
			updateViews.SetTextViewText(Resource.Id.widgetLastRefreshed, "⚠️ Error loading prayer times");
			updateViews.SetTextViewText(Resource.Id.widgetAppName, "Süleymaniye Calendar");
			
			// Apply basic theming
			var isDarkMode = IsSystemDarkMode(context);
			ApplyThemeColors(updateViews, isDarkMode);
			
			return updateViews;
		}

		// Build a widget update to show daily prayer times
		private RemoteViews BuildUpdate (Context context)
		{
			var language = Preferences.Get("SelectedLanguage", "tr");
			try 
			{ 
				AppResources.Culture = new CultureInfo(language); 
			} 
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"{TAG}: Culture setting failed: {ex.Message}");
				// Continue with default culture
			}
			
			// Enhanced locale configuration with better error handling
			Configuration? localizedConfiguration = null;
			try
			{
				localizedConfiguration = new Configuration(context.Resources?.Configuration);
				var newLocaleLanguage = new Locale(language);
			if (Build.VERSION.SdkInt >= BuildVersionCodes.N)
				{
					Locale.SetDefault(Locale.Category.Display!, newLocaleLanguage);
					localizedConfiguration.SetLocale(newLocaleLanguage);
					localizedConfiguration.SetLayoutDirection(newLocaleLanguage);
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"{TAG}: Locale configuration failed: {ex.Message}");
			}
			
			// Create a localizedContext if needed in the future (avoid deprecated UpdateConfiguration)  
			Context? localizedContext = null;
			try
			{
				if (localizedConfiguration != null)
					localizedContext = context.CreateConfigurationContext(localizedConfiguration);
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"{TAG}: Localized context creation failed: {ex.Message}");
			}

			// Detect theme preference
			var selectedTheme = Preferences.Get("SelectedTheme", "System");
			var isDarkMode = selectedTheme switch
			{
				"Dark" => true,
				"Light" => false,
				_ => IsSystemDarkMode(context) // System default
			};

			// Resolve DataService via DI to avoid new DataService()
			var dataService = (Microsoft.Maui.Controls.Application.Current?.Handler?.MauiContext?.Services?.GetService(typeof(DataService)) as DataService);
            
            if (dataService == null)
            {
                // Fallback for widget update when app is not running
                var perf = new PerformanceService();
                var cache = new PrayerCacheService(perf);
                var json = new JsonApiService(perf);
                var xml = new XmlApiService(perf);
                var repo = new PrayerTimesRepository(json, xml, cache, perf);
                var loc = new LocationService(perf);
                var alarm = new NullAlarmService();
                var scheduler = new NotificationSchedulerService(alarm, repo, perf);
                dataService = new DataService(loc, repo, scheduler, perf);
            }
			var calendar = dataService.calendar;
			if (calendar is null)
			{
				// Try to load calendar data from cache synchronously
				try
				{
					// Use Task.Run to avoid blocking but wait for result
					calendar = Task.Run(async () => await dataService.GetTodayPrayerTimesAsync().ConfigureAwait(false)).GetAwaiter().GetResult();
					if (calendar != null)
					{
						dataService.calendar = calendar;
					}
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine($"{TAG}: Failed to load calendar for widget: {ex.Message}");
				}
				
				// If still null after loading attempt, show loading message
				if (calendar is null)
				{
					var emptyViews = new RemoteViews(context.PackageName, Resource.Layout.Widget);
					emptyViews.SetTextViewText(Resource.Id.widgetAppName, AppResources.Yenileniyor);
					return emptyViews;
				}
			}

			// Select appropriate layout based on theme and RTL
			// Recreate RTL language set here (used only for layout selection)
			var rtlLanguages = new HashSet<string> { "ar", "fa", "ug" };
			var layoutResource = GetWidgetLayout(isDarkMode, rtlLanguages.Contains(language));
			var updateViews = new RemoteViews (context.PackageName, layoutResource);
			
			// Apply theme-aware colors
			ApplyThemeColors(updateViews, isDarkMode);
			
			// Apply dynamic font sizes based on user preference
			ApplyFontSizes(updateViews);
			
			updateViews.SetTextViewText(Resource.Id.widgetAppName, AppResources.SuleymaniyeVakfiTakvimi);
			updateViews.SetTextViewText(Resource.Id.widgetFecriKazip, AppResources.FecriKazip);
			updateViews.SetTextViewText(Resource.Id.widgetFecriSadik, AppResources.FecriSadik);
			updateViews.SetTextViewText(Resource.Id.widgetSabahSonu, AppResources.SabahSonu);
			updateViews.SetTextViewText(Resource.Id.widgetOgle, AppResources.Ogle);
			updateViews.SetTextViewText(Resource.Id.widgetIkindi, AppResources.Ikindi);
			updateViews.SetTextViewText(Resource.Id.widgetAksam, AppResources.Aksam);
			updateViews.SetTextViewText(Resource.Id.widgetYatsi, AppResources.Yatsi);
			updateViews.SetTextViewText(Resource.Id.widgetYatsiSonu, AppResources.YatsiSonu);
			updateViews.SetTextViewText(Resource.Id.widgetFecriKazipVakit, calendar.FalseFajr);
			updateViews.SetTextViewText(Resource.Id.widgetFecriSadikVakit, calendar.Fajr);
			updateViews.SetTextViewText(Resource.Id.widgetSabahSonuVakit, calendar.Sunrise);
			updateViews.SetTextViewText(Resource.Id.widgetOgleVakti, calendar.Dhuhr);
			updateViews.SetTextViewText(Resource.Id.widgetIkindiVakit, calendar.Asr);
			updateViews.SetTextViewText(Resource.Id.widgetAksamVakit, calendar.Maghrib);
			updateViews.SetTextViewText(Resource.Id.widgetYatsiVakit, calendar.Isha);
			updateViews.SetTextViewText(Resource.Id.widgetYatsiSonuVakit, calendar.EndOfIsha);
			updateViews.SetTextViewText(Resource.Id.widgetLastRefreshed, DateTime.Now.ToString("HH:mm:ss"));
			updateViews.SetTextViewText(Resource.Id.widgetSehirAdi, AppResources.Sehir);
			updateViews.SetTextViewText(Resource.Id.widgetSehir, Preferences.Get("sehir", AppResources.Sehir));

            updateViews.SetOnClickPendingIntent(Resource.Id.widgetRefreshIcon, GetPendingSelfIntent(context, "android.appwidget.action.APPWIDGET_UPDATE"));
			return updateViews;
		}

		private bool IsSystemDarkMode(Context context)
		{
			try
			{
				var uiMode = context.Resources?.Configuration?.UiMode & UiMode.NightMask;
				return uiMode == UiMode.NightYes;
			}
			catch
			{
				return false; // Default to light mode if detection fails
			}
		}

		private int GetWidgetLayout(bool isDarkMode, bool isRtl)
		{
			// For now, use the same layout but we could create separate layouts for different combinations
			// Future enhancement: Resource.Layout.widget_dark, Resource.Layout.widget_rtl, etc.
			return Resource.Layout.Widget;
		}

		private void ApplyThemeColors(RemoteViews updateViews, bool isDarkMode)
		{
			try
			{
				// Define colors based on theme (matching app colors)
				var primaryColor = isDarkMode ? Android.Graphics.Color.ParseColor("#B07A6B") : Android.Graphics.Color.ParseColor("#5c3021");
				var backgroundColor = isDarkMode ? Android.Graphics.Color.ParseColor("#121212") : Android.Graphics.Color.ParseColor("#EFEBE9");
				var textColor = isDarkMode ? Android.Graphics.Color.ParseColor("#FFFFFF") : Android.Graphics.Color.ParseColor("#000000");
				var secondaryTextColor = isDarkMode ? Android.Graphics.Color.ParseColor("#E0E0E0") : Android.Graphics.Color.ParseColor("#616161");

				// Apply colors to widget elements
				updateViews.SetInt(Resource.Id.widgetBackground, "setBackgroundColor", backgroundColor);
				updateViews.SetInt(Resource.Id.frameLayout, "setBackgroundColor", backgroundColor);
				updateViews.SetTextColor(Resource.Id.widgetAppName, primaryColor);
				
				// Prayer name labels
				updateViews.SetTextColor(Resource.Id.widgetFecriKazip, textColor);
				updateViews.SetTextColor(Resource.Id.widgetFecriSadik, textColor);
				updateViews.SetTextColor(Resource.Id.widgetSabahSonu, textColor);
				updateViews.SetTextColor(Resource.Id.widgetOgle, textColor);
				updateViews.SetTextColor(Resource.Id.widgetIkindi, textColor);
				updateViews.SetTextColor(Resource.Id.widgetAksam, textColor);
				updateViews.SetTextColor(Resource.Id.widgetYatsi, textColor);
				updateViews.SetTextColor(Resource.Id.widgetYatsiSonu, textColor);
				
				// Prayer time values
				updateViews.SetTextColor(Resource.Id.widgetFecriKazipVakit, primaryColor);
				updateViews.SetTextColor(Resource.Id.widgetFecriSadikVakit, primaryColor);
				updateViews.SetTextColor(Resource.Id.widgetSabahSonuVakit, primaryColor);
				updateViews.SetTextColor(Resource.Id.widgetOgleVakti, primaryColor);
				updateViews.SetTextColor(Resource.Id.widgetIkindiVakit, primaryColor);
				updateViews.SetTextColor(Resource.Id.widgetAksamVakit, primaryColor);
				updateViews.SetTextColor(Resource.Id.widgetYatsiVakit, primaryColor);
				updateViews.SetTextColor(Resource.Id.widgetYatsiSonuVakit, primaryColor);
				
				// Secondary elements
				updateViews.SetTextColor(Resource.Id.widgetLastRefreshed, secondaryTextColor);
				updateViews.SetTextColor(Resource.Id.widgetSehirAdi, textColor);
				updateViews.SetTextColor(Resource.Id.widgetSehir, primaryColor);
				
				// FontAwesome refresh icon
				updateViews.SetTextColor(Resource.Id.widgetRefreshIcon, primaryColor);
			}
			catch (System.Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Error applying widget theme colors: {ex.Message}");
			}
		}

		/// <summary>
		/// Applies dynamic font sizes to widget based on user's font size preference.
		/// Uses TypedValue.ComplexUnitSp for scalable pixels that respect accessibility settings.
		/// </summary>
		private void ApplyFontSizes(RemoteViews updateViews)
		{
			try
			{
				// Get user's font size preference (default 14, range 12-28)
				var baseFontSize = Preferences.Get("FontSize", 14);
				
				// Calculate widget font sizes with reasonable scaling
				// Widget needs slightly larger fonts for readability at a glance
				var labelFontSize = (float)(baseFontSize * 1.15);  // Prayer names
				var timeFontSize = (float)(baseFontSize * 1.35);   // Prayer times (more prominent)
				var headerFontSize = (float)(baseFontSize * 1.1);  // App name
				var captionFontSize = (float)(baseFontSize * 1.0); // Last refreshed, city
				var iconFontSize = (float)(baseFontSize * 1.8);    // Refresh icon (larger for touch)
				
				// Use ComplexUnitSp for scalable pixels (respects system accessibility settings)
				var unit = (int)Android.Util.ComplexUnitType.Sp;
				
				// App header
				updateViews.SetTextViewTextSize(Resource.Id.widgetAppName, unit, headerFontSize);
				updateViews.SetTextViewTextSize(Resource.Id.widgetLastRefreshed, unit, captionFontSize);
				updateViews.SetTextViewTextSize(Resource.Id.widgetRefreshIcon, unit, iconFontSize);
				
				// Prayer name labels
				updateViews.SetTextViewTextSize(Resource.Id.widgetFecriKazip, unit, labelFontSize);
				updateViews.SetTextViewTextSize(Resource.Id.widgetFecriSadik, unit, labelFontSize);
				updateViews.SetTextViewTextSize(Resource.Id.widgetSabahSonu, unit, labelFontSize);
				updateViews.SetTextViewTextSize(Resource.Id.widgetOgle, unit, labelFontSize);
				updateViews.SetTextViewTextSize(Resource.Id.widgetIkindi, unit, labelFontSize);
				updateViews.SetTextViewTextSize(Resource.Id.widgetAksam, unit, labelFontSize);
				updateViews.SetTextViewTextSize(Resource.Id.widgetYatsi, unit, labelFontSize);
				updateViews.SetTextViewTextSize(Resource.Id.widgetYatsiSonu, unit, labelFontSize);
				
				// Prayer time values (larger for quick reading)
				updateViews.SetTextViewTextSize(Resource.Id.widgetFecriKazipVakit, unit, timeFontSize);
				updateViews.SetTextViewTextSize(Resource.Id.widgetFecriSadikVakit, unit, timeFontSize);
				updateViews.SetTextViewTextSize(Resource.Id.widgetSabahSonuVakit, unit, timeFontSize);
				updateViews.SetTextViewTextSize(Resource.Id.widgetOgleVakti, unit, timeFontSize);
				updateViews.SetTextViewTextSize(Resource.Id.widgetIkindiVakit, unit, timeFontSize);
				updateViews.SetTextViewTextSize(Resource.Id.widgetAksamVakit, unit, timeFontSize);
				updateViews.SetTextViewTextSize(Resource.Id.widgetYatsiVakit, unit, timeFontSize);
				updateViews.SetTextViewTextSize(Resource.Id.widgetYatsiSonuVakit, unit, timeFontSize);
				
				// City info
				updateViews.SetTextViewTextSize(Resource.Id.widgetSehirAdi, unit, labelFontSize);
				updateViews.SetTextViewTextSize(Resource.Id.widgetSehir, unit, timeFontSize);
			}
			catch (System.Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Error applying widget font sizes: {ex.Message}");
			}
		}

		private PendingIntent? GetPendingSelfIntent(Context context, string action)
		{
			var intent = new Intent(context, typeof(AppWidget));
			intent.SetAction(action);
			var pendingIntentFlags = PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable;
			return PendingIntent.GetBroadcast(context, 0, intent, pendingIntentFlags);
		}
	}
}