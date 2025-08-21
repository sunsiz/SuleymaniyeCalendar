using System.Diagnostics;
using Android.App;
using Android.Appwidget;
using Android.Content;

namespace SuleymaniyeCalendar
{
	[BroadcastReceiver(Label = "Süleymaniye Takvimi", Exported = false)]
	[IntentFilter(new string[] { "android.appwidget.action.APPWIDGET_UPDATE" })]
	// The "Resource" file has to be all in lower caps, "android.appwidget.action.APPWIDGET_UPDATE_OPTIONS"
	[MetaData("android.appwidget.provider", Resource = "@xml/appwidgetprovider")]
	public class AppWidget : AppWidgetProvider
	{
		/// <summary>
		/// This method is called when the 'updatePeriodMillis' from the AppwidgetProvider passes,
		/// or the user manually refreshes/resizes.
		/// </summary>
		public override void OnUpdate(Context context, AppWidgetManager appWidgetManager, int[] appWidgetIds)
		{
			try
			{
				// To prevent any ANR timeouts, we perform the update in a service
				context.StartService(new Intent(context, typeof(WidgetService)));
			}
			catch (System.Exception ex)
			{
				Debug.WriteLine($"Service exception in OnUpdate method: {ex.Message}");
			}
			//var me = new ComponentName(context, Java.Lang.Class.FromType(typeof(AppWidget)).Name);
			//appWidgetManager.UpdateAppWidget(me, BuildRemoteViews(context, appWidgetIds));
		}

		public override void OnReceive(Context context, Intent intent)
		{
			base.OnReceive(context, intent);
			try
			{
				// To prevent any ANR timeouts, we perform the update in a service
				context.StartService(new Intent(context, typeof(WidgetService)));
			}
			catch (System.Exception ex)
			{
				Debug.WriteLine($"Service exception in OnReceive method: {ex.Message}");
			}
		}
	}
}