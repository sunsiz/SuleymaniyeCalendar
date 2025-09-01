using Android.App;
using Android.Content;
using Android.Icu.Util;
using Android.OS;
using Android.Util;
using SuleymaniyeCalendar.Resources.Strings;
using SuleymaniyeCalendar.Services;
using Application = Android.App.Application;

namespace SuleymaniyeCalendar
{
	[Service]
	public class AlarmForegroundService : Service, IAlarmService
	{
		private NotificationManager _notificationManager;
		private readonly int DELAY_BETWEEN_MESSAGES = 30000;
		private readonly int NOTIFICATION_ID = 1993;
		private readonly string NOTIFICATION_CHANNEL_ID = "SuleymaniyeTakvimichannelId";
		private readonly string channelName = AppResources.SuleymaniyeVakfiTakvimi;
		private readonly string channelDescription = "The Suleymaniye Takvimi notification channel.";
		private Notification _notification;
		private bool _isStarted;
		private Handler _handler;
		private Action _runnable;
		private int _counter;
		
		public override IBinder OnBind(Intent intent)
		{
			// Return null because this is a pure started service. A hybrid service would return a binder that would
			// allow access to the GetFormattedRemainingTime() method.
			return null;
		}

		public void SetAlarm(DateTime date, TimeSpan triggerTimeSpan, int timeOffset, string name)
		{
            using (var alarmManager = (AlarmManager)Application.Context.GetSystemService(AlarmService))
			using (var calendar = Calendar.Instance)
			{
				var prayerTimeSpan = triggerTimeSpan;
				triggerTimeSpan -= TimeSpan.FromMinutes(timeOffset);
				//Log.Info("SetAlarm", $"Before Alarm set the Calendar time is {calendar.Time} for {name}");
				calendar.Set(date.Year, date.Month-1, date.Day, triggerTimeSpan.Hours, triggerTimeSpan.Minutes, 0);
				//var activityIntent = new Intent(Application.Context, typeof(AlarmActivity))
				//	.PutExtra("name", name)
				//	.PutExtra("time", prayerTimeSpan.ToString())
				//	.AddFlags(ActivityFlags.ReceiverForeground);
				var intent = new Intent(Application.Context, typeof(NotificationChannelManager))
					.PutExtra("name", name)
					.PutExtra("time", prayerTimeSpan.ToString())
					.AddFlags(ActivityFlags.IncludeStoppedPackages);
				intent.AddFlags(ActivityFlags.ReceiverForeground);
				//without the different reuestCode there will be only one pending intent and it updates every schedule, so only one alarm will be active at the end.
				var requestCode = name switch
				{
					"Fecri Kazip" => date.DayOfYear + 1000,
					"Fecri Sadık" => date.DayOfYear + 2000,
					"Sabah Sonu" => date.DayOfYear + 3000,
					"Öğle" => date.DayOfYear + 4000,
					"İkindi" => date.DayOfYear + 5000,
					"Akşam" => date.DayOfYear + 6000,
					"Yatsı" => date.DayOfYear + 7000,
					"Yatsı Sonu" => date.DayOfYear + 8000,
					_ => 0
				};
				var pendingIntentFlags = (Build.VERSION.SdkInt > BuildVersionCodes.R)
					? PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable
					: PendingIntentFlags.UpdateCurrent;
				//var pendingActivityIntent = PendingIntent.GetActivity(Application.Context, requestCode, activityIntent, pendingIntentFlags);
				var broadcastIntent = new Intent(Application.Context, typeof(AlarmNotificationReceiver))
					.PutExtra("name", name)
					.PutExtra("time", prayerTimeSpan.ToString())
					.AddFlags(ActivityFlags.IncludeStoppedPackages | ActivityFlags.ReceiverForeground);

				var flags = (Build.VERSION.SdkInt > BuildVersionCodes.R)
					? PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable
					: PendingIntentFlags.UpdateCurrent;

				var pendingIntent = PendingIntent.GetBroadcast(Application.Context, requestCode, broadcastIntent, flags);

				// Prefer AlarmClockInfo to improve reliability on Android 12+
				if (Build.VERSION.SdkInt >= BuildVersionCodes.Kitkat)
				{
					// Optional affordance to open MainActivity when tapping the system alarm affordance
					var showIntent = PendingIntent.GetActivity(Application.Context, requestCode,
						new Intent(Application.Context, typeof(MainActivity)), flags);

					var info = new AlarmManager.AlarmClockInfo(calendar.TimeInMillis, showIntent);
					alarmManager?.SetAlarmClock(info, pendingIntent);
				}
				else
				{
					alarmManager?.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, calendar.TimeInMillis, pendingIntent);
				}
				//else
				//    alarmManager?.SetExact(AlarmType.RtcWakeup, calendar.TimeInMillis, pendingIntent);
				System.Diagnostics.Debug.WriteLine("SetAlarm", $"Alarm set for {calendar.Time} for {name}");
			}
		}

		public void CancelAlarm()
		{
			//Analytics.TrackEvent("CancelAlarm in the AlarmForegroundService Triggered: " + $" at {DateTime.Now}");
			//AlarmManager alarmManager = (AlarmManager)Application.Context.GetSystemService(Context.AlarmService);
			//Intent intent = new Intent(Application.Context, typeof(AlarmActivity));
			//var pendingIntentFlags = (Build.VERSION.SdkInt > BuildVersionCodes.R)
			//	? PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable
			//	: PendingIntentFlags.UpdateCurrent;
			//PendingIntent pendingIntent = PendingIntent.GetBroadcast(Application.Context, 0, intent, pendingIntentFlags);
			//alarmManager?.Cancel(pendingIntent);
		}

		public override void OnCreate()
		{
			base.OnCreate();
			_handler = new Handler();
			_notificationManager = (NotificationManager)Application.Context.GetSystemService(Context.NotificationService);
            // Ensure all alarm channels (with sounds) exist before any alarm fires
            NotificationChannelManager.CreateAlarmNotificationChannels();
            SetNotification();

			if(Preferences.Get("ForegroundServiceEnabled",true))this.StartForeground(NOTIFICATION_ID, _notification);

			// This Action will run every 30 second as foreground service running.
			_runnable = new Action(() =>
			{
				_handler.PostDelayed(_runnable, DELAY_BETWEEN_MESSAGES);
				SetNotification();
				_notificationManager.Notify(NOTIFICATION_ID, _notification);
				_counter++;
				if (_counter != 60) return; //When the 60th time (30 minute) refresh widget manually.
				var intent = new Intent(ApplicationContext, typeof(WidgetService));
				try
				{
					ApplicationContext.StartService(intent);
				}
				catch (Exception exception)
				{
					System.Diagnostics.Debug.WriteLine($"An exception occured when starting widget service, details: {exception.Message}");
				}
				_counter = 0;
			});
			_handler.PostDelayed(_runnable, DELAY_BETWEEN_MESSAGES);
			_isStarted = true;
			//CancelAlarm();
		}
		

        private void SetNotification()
		{
			Notification.BigTextStyle textStyle = new Notification.BigTextStyle();
			if (Preferences.Get("NotificationPrayerTimesEnabled", false))
			{
				textStyle.BigText(GetTodaysPrayerTimes());
				textStyle.SetSummaryText(AppResources.BugunkuNamazVakitleri);
			}
			if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
			{
				var channelNameJava = new Java.Lang.String(channelName);
				var channel = new NotificationChannel(NOTIFICATION_CHANNEL_ID, channelNameJava, NotificationImportance.Default)
				{
					Description = channelDescription,
					LightColor = 1,
					LockscreenVisibility = NotificationVisibility.Public
				};
				_notificationManager.CreateNotificationChannel(channel);
				_notification = new Notification.Builder(this, NOTIFICATION_CHANNEL_ID)
					.SetContentTitle(GetFormattedRemainingTime())
					.SetStyle(textStyle)
					.SetSmallIcon(Resource.Drawable.app_logo)
					.SetContentIntent(BuildIntentToShowMainActivity())
					.SetWhen(Java.Lang.JavaSystem.CurrentTimeMillis())
					.SetShowWhen(true)
					.SetOngoing(true)
					.SetOnlyAlertOnce(true)
					.Build();
			}
			else
			{
				_notification = new Notification.Builder(this)
					.SetContentTitle(GetFormattedRemainingTime())
					.SetStyle(textStyle)
					.SetSmallIcon(Resource.Drawable.app_logo)
					.SetContentIntent(BuildIntentToShowMainActivity())
					.SetWhen(Java.Lang.JavaSystem.CurrentTimeMillis())
					.SetShowWhen(true)
					.SetOngoing(true)
					.SetOnlyAlertOnce(true)
					.Build();
			}
		}

		/// <summary>
		/// Builds a PendingIntent that will display the main activity of the app. This is used when the 
		/// user taps on the notification; it will take them to the main activity of the app.
		/// </summary>
		/// <returns>The content intent.</returns>
		PendingIntent BuildIntentToShowMainActivity()
		{
			var notificationIntent = new Intent(this, typeof(MainActivity));
			notificationIntent.SetAction("Alarm.action.MAIN_ACTIVITY");
			notificationIntent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTask);
			
			var pendingIntentFlags = (Build.VERSION.SdkInt > BuildVersionCodes.R)
				? PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable
				: PendingIntentFlags.UpdateCurrent;
			var pendingIntent = PendingIntent.GetActivity(this, 0, notificationIntent, pendingIntentFlags);
			return pendingIntent;
		}

		private string GetFormattedRemainingTime()
		{
			var message = "";
			var data = (Microsoft.Maui.Controls.Application.Current?.Handler?.MauiContext?.Services?.GetService(typeof(DataService)) as DataService)
						?? new DataService(this);
			var calendar = data.calendar;
			var currentTime = DateTime.Now.TimeOfDay;
			try
			{
				if (currentTime < TimeSpan.Parse(calendar.FalseFajr))
					message = AppResources.FecriKazibingirmesinekalanvakit +
							  (TimeSpan.Parse(calendar.FalseFajr) - currentTime).Add(TimeSpan.FromMinutes(1)).ToString(@"hh\:mm");
				else if (currentTime >= TimeSpan.Parse(calendar.FalseFajr) && currentTime <= TimeSpan.Parse(calendar.Fajr))
					message = AppResources.FecriSadikakalanvakit +
							  (TimeSpan.Parse(calendar.Fajr) - currentTime).Add(TimeSpan.FromMinutes(1)).ToString(@"hh\:mm");
				else if (currentTime >= TimeSpan.Parse(calendar.Fajr) && currentTime <= TimeSpan.Parse(calendar.Sunrise))
					message = AppResources.SabahSonunakalanvakit +
							  (TimeSpan.Parse(calendar.Sunrise) - currentTime).Add(TimeSpan.FromMinutes(1)).ToString(@"hh\:mm");
				else if (currentTime >= TimeSpan.Parse(calendar.Sunrise) && currentTime <= TimeSpan.Parse(calendar.Dhuhr))
					message = AppResources.Ogleningirmesinekalanvakit +
							  (TimeSpan.Parse(calendar.Dhuhr) - currentTime).Add(TimeSpan.FromMinutes(1)).ToString(@"hh\:mm");
				else if (currentTime >= TimeSpan.Parse(calendar.Dhuhr) && currentTime <= TimeSpan.Parse(calendar.Asr))
					message = AppResources.Oglenincikmasinakalanvakit +
							  (TimeSpan.Parse(calendar.Asr) - currentTime).Add(TimeSpan.FromMinutes(1)).ToString(@"hh\:mm");
				else if (currentTime >= TimeSpan.Parse(calendar.Asr) && currentTime <= TimeSpan.Parse(calendar.Maghrib))
					message = AppResources.Ikindinincikmasinakalanvakit +
							  (TimeSpan.Parse(calendar.Maghrib) - currentTime).Add(TimeSpan.FromMinutes(1)).ToString(@"hh\:mm");
				else if (currentTime >= TimeSpan.Parse(calendar.Maghrib) && currentTime <= TimeSpan.Parse(calendar.Isha))
					message = AppResources.Aksamincikmasnakalanvakit +
							  (TimeSpan.Parse(calendar.Isha) - currentTime).Add(TimeSpan.FromMinutes(1)).ToString(@"hh\:mm");
				else if (currentTime >= TimeSpan.Parse(calendar.Isha) && currentTime <= TimeSpan.Parse(calendar.EndOfIsha))
					message = AppResources.Yatsinincikmasinakalanvakit +
							  (TimeSpan.Parse(calendar.EndOfIsha) - currentTime).Add(TimeSpan.FromMinutes(1)).ToString(@"hh\:mm");
				else if (currentTime >= TimeSpan.Parse(calendar.EndOfIsha))
					message = AppResources.Yatsininciktigindangecenvakit +
							  (currentTime - TimeSpan.Parse(calendar.EndOfIsha)).Add(TimeSpan.FromMinutes(1)).ToString(@"hh\:mm");
			}
			catch (Exception exception)
			{
				System.Diagnostics.Debug.WriteLine($"GetFormattedRemainingTime exception: {exception.Message}. Location: {calendar.Latitude}, {calendar.Longitude}");
				Log.Error("GetFormattedRemainingTime",$"GetFormattedRemainingTime exception: {exception.Message}. Location: {calendar.Latitude}, {calendar.Longitude}");
				message = AppResources.KonumIzniIcerik;
			}

			return message;
		}

		private string GetTodaysPrayerTimes()
		{
			var message = "";
			var data = (Microsoft.Maui.Controls.Application.Current?.Handler?.MauiContext?.Services?.GetService(typeof(DataService)) as DataService)
						?? new DataService(this);
			var calendar = data.calendar;
			message += AppResources.FecriKazip + ": " + calendar.FalseFajr + "\n";
			message += AppResources.FecriSadik + ": " + calendar.Fajr + "\n";
			message += AppResources.SabahSonu + ": " + calendar.Sunrise + "\n";
			message += AppResources.Ogle + ": " + calendar.Dhuhr + "\n";
			message += AppResources.Ikindi + ": " + calendar.Asr + "\n";
			message += AppResources.Aksam + ": " + calendar.Maghrib + "\n";
			message += AppResources.Yatsi + ": " + calendar.Isha + "\n";
			message += AppResources.YatsiSonu + ": " + calendar.EndOfIsha;
			return message;
		}

		public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
		{
			if (intent == null || intent.Action == null)
			{
				var source = null == intent ? "intent" : "action";
				System.Diagnostics.Debug.WriteLine("OnStartCommand Null Intent Exception: " + source + " was null, flags=" + flags + " bits=" + flags);
				return StartCommandResult.RedeliverIntent;
			}
			//Analytics.TrackEvent("OnStartCommand in the AlarmForegroundService Triggered: " + $" at {DateTime.Now}");
			if (intent.Action.Equals("SuleymaniyeTakvimi.action.START_SERVICE"))
			{
				if (_isStarted)
				{
					//Log.Info(TAG, "OnStartCommand: The service is already running.");
				}
				else
				{
					//Log.Info(TAG, "OnStartCommand: The service is starting.");
					// Enlist this instance of the service as a foreground service
					this.StartForeground(NOTIFICATION_ID, _notification);
					_handler.PostDelayed(_runnable, DELAY_BETWEEN_MESSAGES);
					_isStarted = true;
					Task startupWork = new Task(async () =>
					{
						await Task.Delay(12000).ConfigureAwait(true);
						System.Diagnostics.Debug.WriteLine("OnStartCommand: " + $"Starting Set Alarm at {DateTime.Now}");
						var data = (Microsoft.Maui.Controls.Application.Current?.Handler?.MauiContext?.Services?.GetService(typeof(DataService)) as DataService)
									?? new DataService(this);
						data.SetWeeklyAlarmsAsync();
					});
					startupWork.Start();
					
				}
			}
			else if (intent.Action.Equals("SuleymaniyeTakvimi.action.STOP_SERVICE"))
			{
				//Log.Info(TAG, "OnStartCommand: The service is stopping.");
				StopForeground(true);
				StopSelf(NOTIFICATION_ID);
				_isStarted = false;
			}
            else if (intent.Action.Equals("SuleymaniyeTakvimi.action.REFRESH_NOTIFICATION"))
            {
                // Rebuild notification based on latest Preferences, and update immediately
                SetNotification();
                _notificationManager.Notify(NOTIFICATION_ID, _notification);
            }

			return StartCommandResult.Sticky;
		}
		
		public void StartAlarmForegroundService()
		{
			Log.Info("Main Activity", $"Main Activity SetAlarmForegroundService Started: {DateTime.Now:HH:m:s.fff}");
			var startServiceIntent = new Intent(Application.Context, typeof(AlarmForegroundService));
			startServiceIntent.SetAction("SuleymaniyeTakvimi.action.START_SERVICE");
			MainThread.BeginInvokeOnMainThread(() =>
			{
				if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
				{
					Application.Context?.StartForegroundService(startServiceIntent);
				}
				else
				{
					Application.Context?.StartService(startServiceIntent);
				}
			});
			System.Diagnostics.Debug.WriteLine("Main Activity" + $"Main Activity SetAlarmForegroundService Finished: {DateTime.Now:HH:m:s.fff}");
		}

		public void StopAlarmForegroundService()
		{
			Log.Info("Main Activity", $"Main Activity StopAlarmForegroundService Started: {DateTime.Now:HH:m:s.fff}");
			var stopServiceIntent = new Intent(Application.Context, typeof(AlarmForegroundService));
			stopServiceIntent.SetAction("SuleymaniyeTakvimi.action.STOP_SERVICE");
			MainThread.BeginInvokeOnMainThread(() =>
			{
				if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
				{
					Application.Context?.StartForegroundService(stopServiceIntent);
				}
				else
				{
					Application.Context?.StartService(stopServiceIntent);
				}
			});
			System.Diagnostics.Debug.WriteLine("Main Activity" + $"Main Activity StopAlarmForegroundService Finished: {DateTime.Now:HH:m:s.fff}");
		}
	}
}