using System.Globalization;
using Android.App;
using Android.Content;
using Android.Icu.Util;
using Android.OS;
using Android.Util;
using AndroidX.Core.App;
using SuleymaniyeCalendar.Resources.Strings;
using SuleymaniyeCalendar.Services;
using Application = Android.App.Application;

namespace SuleymaniyeCalendar
{
    [Service(Exported = false, ForegroundServiceType = Android.Content.PM.ForegroundService.TypeDataSync)]
	public class AlarmForegroundService : Service, IAlarmService
	{
		internal NotificationManager _notificationManager;
		private readonly int DELAY_BETWEEN_MESSAGES = 30000;
		internal readonly int NOTIFICATION_ID = 1993;
		private readonly string NOTIFICATION_CHANNEL_ID = "SuleymaniyeTakvimichannelId";
		private readonly string channelName = AppResources.SuleymaniyeVakfiTakvimi;
		private readonly string channelDescription = "The Suleymaniye Takvimi notification channel.";
		internal Notification _notification;
		private bool _isStarted;
		private Handler _handler;
		private Action _runnable;
		private int _counter;
		private DateTime _lastRescheduleAttemptUtc = DateTime.MinValue;
		
		public override IBinder OnBind(Intent intent)
		{
			// Return null because this is a pure started service. A hybrid service would return a binder that would
			// allow access to the GetFormattedRemainingTime() method.
			return null;
		}

		public void SetAlarm(DateTime date, TimeSpan triggerTimeSpan, int timeOffset, string name)
		{
			System.Diagnostics.Debug.WriteLine("TimeStamp-SetAlarm-Start", $"Setting alarm for {date} {triggerTimeSpan} {timeOffset} for {name} at {DateTime.Now:MM/dd/yyyy hh:mm:ss.fff tt}");
			try
			{
				using (var alarmManager = (AlarmManager)Application.Context.GetSystemService(AlarmService))
				using (var calendar = Java.Util.Calendar.Instance)
				{
					var prayerTimeSpan = triggerTimeSpan;
					triggerTimeSpan -= TimeSpan.FromMinutes(timeOffset);
					//Log.Info("SetAlarm", $"Before Alarm set the Calendar time is {calendar.Time} for {name}");
					calendar.Set(date.Year, date.Month - 1, date.Day, triggerTimeSpan.Hours, triggerTimeSpan.Minutes, 0);
					//var activityIntent = new Intent(Application.Context, typeof(AlarmActivity))
					//	.PutExtra("name", name)
					//	.PutExtra("time", prayerTimeSpan.ToString())
					//	.AddFlags(ActivityFlags.ReceiverForeground);
					// var intent = new Intent(Application.Context, typeof(NotificationChannelManager))
					// 	.PutExtra("name", name)
					// 	.PutExtra("time", prayerTimeSpan.ToString())
					// 	.AddFlags(ActivityFlags.IncludeStoppedPackages);
					// intent.AddFlags(ActivityFlags.ReceiverForeground);
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

					// Min SDK 26: Always use AlarmClockInfo for exact, doze-resilient delivery with system affordance.
					var showIntent = PendingIntent.GetActivity(Application.Context, requestCode,
						new Intent(Application.Context, Java.Lang.Class.FromType(typeof(MainActivity))), flags);
					var info = new AlarmManager.AlarmClockInfo(calendar.TimeInMillis, showIntent);
					alarmManager?.SetAlarmClock(info, pendingIntent);
					//else
					//    alarmManager?.SetExact(AlarmType.RtcWakeup, calendar.TimeInMillis, pendingIntent);
					System.Diagnostics.Debug.WriteLine($"✅ Alarm scheduled: {name} on {date:dd/MM/yyyy} at {triggerTimeSpan} (request: {requestCode})");
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"❌ SetAlarm failed for {name} on {date:dd/MM/yyyy}: {ex.Message}\n{ex.StackTrace}");
			}
		}

		public void CancelAlarm()
		{
			// Cancel all scheduled alarms created by SetAlarm (monthly exact alarms).
			// Strategy:
			// - We deterministically derive the requestCode for each prayer/day using the same scheme as SetAlarm.
			// - Iterate a safe window around today (past few days + ~2 months ahead) and cancel matching PendingIntents.
			// - Use FLAG_NO_CREATE to avoid creating new PendingIntents; if found, cancel via AlarmManager and PI.Cancel().
			try
			{
				var alarmManager = (AlarmManager)Application.Context.GetSystemService(Context.AlarmService);
				if (alarmManager is null)
					return;

				// Offsets mirror SetAlarm's switch mapping. We only need offsets; not the localized names.
				// Keys are descriptive; values are offsets added to DayOfYear.
				var offsets = new Dictionary<string, int>
				{
					{"FalseFajr", 1000},
					{"Fajr", 2000},
					{"Sunrise", 3000},
					{"Dhuhr", 4000},
					{"Asr", 5000},
					{"Maghrib", 6000},
					{"Isha", 7000},
					{"EndOfIsha", 8000}
				};

				// Cover slight past and all future we schedule (30 days) with a buffer.
				var start = DateTime.Today.AddDays(-2);
				var end = DateTime.Today.AddDays(60);

				// Flags to retrieve existing PendingIntents (don't create new ones)
				var lookupFlags = (Build.VERSION.SdkInt > BuildVersionCodes.R)
					? PendingIntentFlags.NoCreate | PendingIntentFlags.Immutable
					: PendingIntentFlags.NoCreate;

				for (var dt = start; dt <= end; dt = dt.AddDays(1))
				{
					var dayOfYear = dt.DayOfYear;
					foreach (var offset in offsets.Values)
					{
						var requestCode = dayOfYear + offset;
						var intent = new Intent(Application.Context, typeof(AlarmNotificationReceiver));
						var pi = PendingIntent.GetBroadcast(Application.Context, requestCode, intent, lookupFlags);
						if (pi is not null)
						{
							try
							{
								alarmManager.Cancel(pi);
								pi.Cancel();
							}
							catch (Exception ex)
							{
								System.Diagnostics.Debug.WriteLine($"CancelAlarm: Failed to cancel PI {requestCode}: {ex.Message}");
							}
						}
					}
				}

				// Clear last scheduled date so auto-rescheduler in the service won't reschedule immediately.
				Preferences.Set("LastAlarmDate", string.Empty);
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"CancelAlarm error: {ex.Message}");
			}
		}

		public override void OnCreate()
		{
			base.OnCreate();
			// Use main looper-bound Handler; default ctor is obsolete on modern Android
			_handler = new Handler(Looper.MainLooper);
			_notificationManager = (NotificationManager)Application.Context.GetSystemService(Context.NotificationService);
            // Ensure all alarm channels (with sounds) exist before any alarm fires
            NotificationChannelManager.CreateAlarmNotificationChannels();
            
            SetNotification();

			if(Preferences.Get("ForegroundServiceEnabled",true))if (Build.VERSION.SdkInt >= BuildVersionCodes.UpsideDownCake) // API 34
				{
					this.StartForeground(NOTIFICATION_ID, _notification, Android.Content.PM.ForegroundService.TypeDataSync);
				}
				else
				{
					this.StartForeground(NOTIFICATION_ID, _notification);
				}

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

				// Auto-reschedule monthly alarms when approaching the end of the current window.
				// This keeps alarms reliable even if the app isn't opened for a long time.
				try
				{
					var lastStr = Preferences.Get("LastAlarmDate", string.Empty);
					if (!string.IsNullOrWhiteSpace(lastStr) && DateTime.TryParse(lastStr, out var last))
					{
						var nowUtc = DateTime.UtcNow;
						// Avoid reattempting too frequently
						if (nowUtc - _lastRescheduleAttemptUtc > TimeSpan.FromHours(6))
						{
							if (DateTime.Today >= last.AddDays(-3))
							{
								_lastRescheduleAttemptUtc = nowUtc;
								Task.Run(async () =>
								{
									try
									{
										var data = (Microsoft.Maui.Controls.Application.Current?.Handler?.MauiContext?.Services?.GetService(typeof(DataService)) as DataService)
												?? new DataService(this);
										await data.SetMonthlyAlarmsAsync();
									}
									catch (Exception ex)
									{
										System.Diagnostics.Debug.WriteLine($"Auto-reschedule error: {ex.Message}");
									}
								});
							}
						}
					}
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine($"Reschedule check failed: {ex.Message}");
				}
			});
			_handler.PostDelayed(_runnable, DELAY_BETWEEN_MESSAGES);
			_isStarted = true;
			//CancelAlarm();
		}

		public override void OnDestroy()
		{
			base.OnDestroy();
		}
		

        internal void SetNotification()
		{
			Notification.BigTextStyle textStyle = new Notification.BigTextStyle();
			var notificationTitle = GetFormattedRemainingTime();
			
			if (Preferences.Get("NotificationPrayerTimesEnabled", false))
			{
				var prayerTimes = GetTodaysPrayerTimes();
				textStyle.BigText(prayerTimes);
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
				
				var builder = new Notification.Builder(this, NOTIFICATION_CHANNEL_ID)
					.SetContentTitle(notificationTitle)
					.SetStyle(textStyle)
					.SetSmallIcon(Resource.Drawable.app_logo)
					.SetContentIntent(BuildIntentToShowMainActivity())
					.SetWhen(Java.Lang.JavaSystem.CurrentTimeMillis())
					.SetShowWhen(true)
					.SetOngoing(true)
					.SetOnlyAlertOnce(true);
				
				_notification = builder.Build();
			}
			else
			{
				var compat = new NotificationCompat.Builder(this, NOTIFICATION_CHANNEL_ID)
					.SetContentTitle(notificationTitle)
					.SetStyle(new NotificationCompat.BigTextStyle().BigText((Preferences.Get("NotificationPrayerTimesEnabled", false) ? GetTodaysPrayerTimes() : string.Empty)))
					.SetSmallIcon(Resource.Drawable.app_logo)
					.SetContentIntent(BuildIntentToShowMainActivity())
					.SetWhen(Java.Lang.JavaSystem.CurrentTimeMillis())
					.SetShowWhen(true)
					.SetOngoing(true)
					.SetOnlyAlertOnce(true);
				_notification = compat.Build();
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
				if (currentTime < TimeSpan.Parse(calendar.FalseFajr, CultureInfo.InvariantCulture))
					message = AppResources.FecriKazibingirmesinekalanvakit +
							  (TimeSpan.Parse(calendar.FalseFajr, CultureInfo.InvariantCulture) - currentTime).Add(TimeSpan.FromMinutes(1)).ToString(@"hh\:mm");
				else if (currentTime >= TimeSpan.Parse(calendar.FalseFajr, CultureInfo.InvariantCulture) && currentTime <= TimeSpan.Parse(calendar.Fajr, CultureInfo.InvariantCulture))
					message = AppResources.FecriSadikakalanvakit +
							  (TimeSpan.Parse(calendar.Fajr, CultureInfo.InvariantCulture) - currentTime).Add(TimeSpan.FromMinutes(1)).ToString(@"hh\:mm");
				else if (currentTime >= TimeSpan.Parse(calendar.Fajr, CultureInfo.InvariantCulture) && currentTime <= TimeSpan.Parse(calendar.Sunrise, CultureInfo.InvariantCulture))
					message = AppResources.SabahSonunakalanvakit +
							  (TimeSpan.Parse(calendar.Sunrise, CultureInfo.InvariantCulture) - currentTime).Add(TimeSpan.FromMinutes(1)).ToString(@"hh\:mm");
				else if (currentTime >= TimeSpan.Parse(calendar.Sunrise, CultureInfo.InvariantCulture) && currentTime <= TimeSpan.Parse(calendar.Dhuhr, CultureInfo.InvariantCulture))
					message = AppResources.Ogleningirmesinekalanvakit +
							  (TimeSpan.Parse(calendar.Dhuhr, CultureInfo.InvariantCulture) - currentTime).Add(TimeSpan.FromMinutes(1)).ToString(@"hh\:mm");
				else if (currentTime >= TimeSpan.Parse(calendar.Dhuhr, CultureInfo.InvariantCulture) && currentTime <= TimeSpan.Parse(calendar.Asr, CultureInfo.InvariantCulture))
					message = AppResources.Oglenincikmasinakalanvakit +
							  (TimeSpan.Parse(calendar.Asr, CultureInfo.InvariantCulture) - currentTime).Add(TimeSpan.FromMinutes(1)).ToString(@"hh\:mm");
				else if (currentTime >= TimeSpan.Parse(calendar.Asr, CultureInfo.InvariantCulture) && currentTime <= TimeSpan.Parse(calendar.Maghrib, CultureInfo.InvariantCulture))
					message = AppResources.Ikindinincikmasinakalanvakit +
							  (TimeSpan.Parse(calendar.Maghrib, CultureInfo.InvariantCulture) - currentTime).Add(TimeSpan.FromMinutes(1)).ToString(@"hh\:mm");
				else if (currentTime >= TimeSpan.Parse(calendar.Maghrib, CultureInfo.InvariantCulture) && currentTime <= TimeSpan.Parse(calendar.Isha, CultureInfo.InvariantCulture))
					message = AppResources.Aksamincikmasnakalanvakit +
							  (TimeSpan.Parse(calendar.Isha, CultureInfo.InvariantCulture) - currentTime).Add(TimeSpan.FromMinutes(1)).ToString(@"hh\:mm");
				else if (currentTime >= TimeSpan.Parse(calendar.Isha, CultureInfo.InvariantCulture) && currentTime <= TimeSpan.Parse(calendar.EndOfIsha, CultureInfo.InvariantCulture))
					message = AppResources.Yatsinincikmasinakalanvakit +
							  (TimeSpan.Parse(calendar.EndOfIsha, CultureInfo.InvariantCulture) - currentTime).Add(TimeSpan.FromMinutes(1)).ToString(@"hh\:mm");
				else if (currentTime >= TimeSpan.Parse(calendar.EndOfIsha, CultureInfo.InvariantCulture))
					message = AppResources.Yatsininciktigindangecenvakit +
							  (currentTime - TimeSpan.Parse(calendar.EndOfIsha, CultureInfo.InvariantCulture)).Add(TimeSpan.FromMinutes(1)).ToString(@"hh\:mm");
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
					// Call StartForeground immediately to prevent ANR
					this.StartForeground(NOTIFICATION_ID, _notification);
					_handler.PostDelayed(_runnable, DELAY_BETWEEN_MESSAGES);
					_isStarted = true;
					
					// Start alarm setup work in background (not blocking)
					_ = Task.Run(async () =>
					{
						try
						{
							// Small delay to let the app finish startup
							await Task.Delay(7000).ConfigureAwait(false);
							System.Diagnostics.Debug.WriteLine($"OnStartCommand: Starting Set Alarm at {DateTime.Now}");
							
							var data = (Microsoft.Maui.Controls.Application.Current?.Handler?.MauiContext?.Services?.GetService(typeof(DataService)) as DataService)
										?? new DataService(this);
							
							await data.SetMonthlyAlarmsAsync().ConfigureAwait(false);
						}
						catch (Exception ex)
						{
							System.Diagnostics.Debug.WriteLine($"Error in alarm startup: {ex}");
						}
					});					
				}
			}
			else if (intent.Action.Equals("SuleymaniyeTakvimi.action.STOP_SERVICE"))
			{
				//Log.Info(TAG, "OnStartCommand: The service is stopping.");
				if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
				{
					StopForeground(StopForegroundFlags.Remove);
				}
				else
				{
					#pragma warning disable CA1422
					StopForeground(true);
					#pragma warning restore CA1422
				}
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