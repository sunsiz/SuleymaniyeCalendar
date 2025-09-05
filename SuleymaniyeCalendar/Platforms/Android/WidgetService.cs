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
		[Obsolete]
		public override void OnStart (Intent intent, int startId)
		{
			var updateViews = BuildUpdate (this);
			var thisWidget = new ComponentName (this, Java.Lang.Class.FromType (typeof (AppWidget)).Name);
			var manager = AppWidgetManager.GetInstance (this);
			manager?.UpdateAppWidget (thisWidget, updateViews);
		}
		public override IBinder OnBind(Intent intent) => null;

		protected override void OnHandleIntent(Intent intent)
		{
			var updateViews = BuildUpdate (this);
			var thisWidget = new ComponentName (this, Java.Lang.Class.FromType (typeof (AppWidget)).Name);
			var manager = AppWidgetManager.GetInstance (this);
			manager?.UpdateAppWidget (thisWidget, updateViews);
		}

		// Build a widget update to show daily prayer times
		private RemoteViews BuildUpdate (Context context)
		{
			var language = Preferences.Get("SelectedLanguage", "tr");
			try { AppResources.Culture = new CultureInfo(language); } catch { }
			
			// Configure locale in a modern way (minSdk 26): create a localized configuration/context
			var configuration = new Configuration(context.Resources?.Configuration);
			var newLocaleLanguage = new Locale(language);
			if (Build.VERSION.SdkInt >= BuildVersionCodes.N)
			{
				Locale.SetDefault(Locale.Category.Display, newLocaleLanguage);
				configuration.SetLocale(newLocaleLanguage);
				configuration.SetLayoutDirection(newLocaleLanguage);
			}
			// Create a localizedContext if needed in the future (avoid deprecated UpdateConfiguration)
			var localizedContext = context.CreateConfigurationContext(configuration);

			// Detect theme preference
			var selectedTheme = Preferences.Get("SelectedTheme", "System");
			var isDarkMode = selectedTheme switch
			{
				"Dark" => true,
				"Light" => false,
				_ => IsSystemDarkMode(context) // System default
			};

			// Resolve DataService via DI to avoid new DataService()
			var dataService = (Microsoft.Maui.Controls.Application.Current?.Handler?.MauiContext?.Services?.GetService(typeof(DataService)) as DataService)
						?? new DataService(new NullAlarmService());
			var calendar = dataService.calendar;

			// Select appropriate layout based on theme and RTL
			// Recreate RTL language set here (used only for layout selection)
			var rtlLanguages = new HashSet<string> { "ar", "fa", "he", "ug", "ps", "sd", "ku", "dv" };
			var layoutResource = GetWidgetLayout(isDarkMode, rtlLanguages.Contains(language));
			var updateViews = new RemoteViews (context.PackageName, layoutResource);
			
			// Apply theme-aware colors
			ApplyThemeColors(updateViews, isDarkMode);
			
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
		private PendingIntent GetPendingSelfIntent(Context context, string action)
		{
			var intent = new Intent(context, typeof(AppWidget));
			intent.SetAction(action);
			var pendingIntentFlags = PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable;
			return PendingIntent.GetBroadcast(context, 0, intent, pendingIntentFlags);
		}
	}
}