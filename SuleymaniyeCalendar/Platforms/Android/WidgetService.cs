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
			Configuration configuration = new Configuration();
			Locale newLocaleLanguage = new Locale(language);
			if (Build.VERSION.SdkInt >= BuildVersionCodes.N)
			{
				Locale.SetDefault(Locale.Category.Display, newLocaleLanguage);
				configuration.SetLocale(newLocaleLanguage);
			}
			else
			{
				Locale.Default = newLocaleLanguage;
				configuration.Locale = newLocaleLanguage;
			}

			context.Resources?.UpdateConfiguration(configuration, context.Resources.DisplayMetrics);

			// Resolve DataService via DI to avoid new DataService()
			var dataService = (Microsoft.Maui.Controls.Application.Current?.Handler?.MauiContext?.Services?.GetService(typeof(DataService)) as DataService)
						?? new DataService(new NullAlarmService());
			var calendar = dataService.calendar;

			var updateViews = new RemoteViews (context.PackageName, Resource.Layout.Widget);
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
			
			updateViews.SetOnClickPendingIntent(Resource.Id.widgetRefreshIcon, GetPendingSelfIntent(context, "android.appwidget.action.APPWIDGET_UPDATE"));
			return updateViews;
		}
		private PendingIntent GetPendingSelfIntent(Context context, string action)
		{
			var intent = new Intent(context, typeof(AppWidget));
			intent.SetAction(action);
			var pendingIntentFlags = (Build.VERSION.SdkInt >= BuildVersionCodes.M)
				? PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable
				: PendingIntentFlags.UpdateCurrent;
			return PendingIntent.GetBroadcast(context, 0, intent, pendingIntentFlags);
		}
	}
}