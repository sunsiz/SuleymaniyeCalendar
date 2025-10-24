using System.Globalization;
using Android.App;
using Android.Content;
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
		// Notification constants
		private const int NOTIFICATION_UPDATE_INTERVAL_MS = 30000; // 30 seconds
		private const int WIDGET_REFRESH_CYCLES = 60; // 60 cycles = 30 minutes
		private const int NOTIFICATION_ID = 1993;
		private const string NOTIFICATION_CHANNEL_ID = "SuleymaniyeTakvimichannelId";
		private const string CHANNEL_DESCRIPTION = "The Suleymaniye Takvimi notification channel.";
		
		// Reschedule protection
		private static readonly TimeSpan MinRescheduleInterval = TimeSpan.FromHours(6);
		private static readonly TimeSpan RescheduleThreshold = TimeSpan.FromDays(-3);
		
		private NotificationManager _notificationManager;
		private readonly string _channelName = AppResources.SuleymaniyeVakfiTakvimi;
		private Notification _notification;
		private bool _isStarted;
		private Handler _handler;
		private Action _runnable;
		private int _updateCounter;
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
					calendar.Set(date.Year, date.Month - 1, date.Day, triggerTimeSpan.Hours, triggerTimeSpan.Minutes, 0);
					
					// Different request codes ensure multiple alarms can coexist
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
					
					var broadcastIntent = new Intent(Application.Context, typeof(AlarmNotificationReceiver))
						.PutExtra("name", name)
						.PutExtra("time", prayerTimeSpan.ToString())
						.AddFlags(ActivityFlags.IncludeStoppedPackages | ActivityFlags.ReceiverForeground);

					var flags = (Build.VERSION.SdkInt > BuildVersionCodes.R)
						? PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable
						: PendingIntentFlags.UpdateCurrent;

					var pendingIntent = PendingIntent.GetBroadcast(Application.Context, requestCode, broadcastIntent, flags);

					// Use AlarmClockInfo for exact, doze-resilient delivery with system affordance
					var showIntent = PendingIntent.GetActivity(Application.Context, requestCode,
						new Intent(Application.Context, Java.Lang.Class.FromType(typeof(MainActivity))), flags);
					var info = new AlarmManager.AlarmClockInfo(calendar.TimeInMillis, showIntent);
					alarmManager?.SetAlarmClock(info, pendingIntent);
					
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

				// Clear last scheduled date so auto-rescheduler in the service won't reschedule immediately
				Preferences.Set("LastAlarmDate", string.Empty);
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"CancelAlarm error: {ex.Message}");
			}
		}

		private void TryRescheduleAlarms()
		{
			try
			{
				var lastStr = Preferences.Get("LastAlarmDate", string.Empty);
				if (string.IsNullOrWhiteSpace(lastStr) || !DateTime.TryParse(lastStr, out var lastDate))
					return;

				var nowUtc = DateTime.UtcNow;
				// Avoid reattempting too frequently
				if (nowUtc - _lastRescheduleAttemptUtc <= MinRescheduleInterval)
					return;

				// Check if approaching the end of the alarm window
				if (DateTime.Today < lastDate.Add(RescheduleThreshold))
					return;

				_lastRescheduleAttemptUtc = nowUtc;
				
				// Run rescheduling in background
				_ = Task.Run(async () =>
				{
					try
					{
						var dataService = GetDataService();
						await dataService.SetMonthlyAlarmsAsync().ConfigureAwait(false);
					}
					catch (Exception ex)
					{
						System.Diagnostics.Debug.WriteLine($"Auto-reschedule error: {ex.Message}");
					}
				});
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Reschedule check failed: {ex.Message}");
			}
		}

		private DataService GetDataService()
		{
			return (Microsoft.Maui.Controls.Application.Current?.Handler?.MauiContext?.Services?.GetService(typeof(DataService)) as DataService)
				?? new DataService(this);
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

			if (Preferences.Get("ForegroundServiceEnabled", true))
			{
#pragma warning disable CA1416 // Platform compatibility
				if (Build.VERSION.SdkInt >= BuildVersionCodes.Q) // API 29+
				{
					StartForeground(NOTIFICATION_ID, _notification, Android.Content.PM.ForegroundService.TypeDataSync);
				}
				else
				{
					StartForeground(NOTIFICATION_ID, _notification);
				}
#pragma warning restore CA1416
			}

			// This Action will run every 30 seconds while foreground service is running
			_runnable = new Action(() =>
			{
				_handler.PostDelayed(_runnable, NOTIFICATION_UPDATE_INTERVAL_MS);
				SetNotification();
				_notificationManager.Notify(NOTIFICATION_ID, _notification);
				_updateCounter++;
				if (_updateCounter < WIDGET_REFRESH_CYCLES) return; // Refresh widget every 30 minutes
				var intent = new Intent(ApplicationContext, typeof(WidgetService));
				try
				{
					ApplicationContext.StartService(intent);
				}
				catch (Exception exception)
				{
					System.Diagnostics.Debug.WriteLine($"Widget service start failed: {exception.Message}");
				}
				_updateCounter = 0;

				// Auto-reschedule monthly alarms when approaching the end of the current window
				TryRescheduleAlarms();
			});
			_handler.PostDelayed(_runnable, NOTIFICATION_UPDATE_INTERVAL_MS);
			_isStarted = true;
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
				var channelNameJava = new Java.Lang.String(_channelName);
				var channel = new NotificationChannel(NOTIFICATION_CHANNEL_ID, channelNameJava, NotificationImportance.Default)
				{
					Description = CHANNEL_DESCRIPTION,
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
			var dataService = GetDataService();
			var calendar = dataService.calendar;
			var currentTime = DateTime.Now.TimeOfDay;
			
			try
			{
				string message;
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
				else // currentTime >= EndOfIsha
					message = AppResources.Yatsininciktigindangecenvakit +
							  (currentTime - TimeSpan.Parse(calendar.EndOfIsha, CultureInfo.InvariantCulture)).Add(TimeSpan.FromMinutes(1)).ToString(@"hh\:mm");
				
				return message;
			}
			catch (Exception exception)
			{
				System.Diagnostics.Debug.WriteLine($"GetFormattedRemainingTime exception: {exception.Message}. Location: {calendar.Latitude}, {calendar.Longitude}");
				Log.Error("GetFormattedRemainingTime", $"GetFormattedRemainingTime exception: {exception.Message}. Location: {calendar.Latitude}, {calendar.Longitude}");
				return AppResources.KonumIzniIcerik;
			}
		}

		private string GetTodaysPrayerTimes()
		{
			var dataService = GetDataService();
			var calendar = dataService.calendar;
			
			return $"{AppResources.FecriKazip}: {calendar.FalseFajr}\n" +
				   $"{AppResources.FecriSadik}: {calendar.Fajr}\n" +
				   $"{AppResources.SabahSonu}: {calendar.Sunrise}\n" +
				   $"{AppResources.Ogle}: {calendar.Dhuhr}\n" +
				   $"{AppResources.Ikindi}: {calendar.Asr}\n" +
				   $"{AppResources.Aksam}: {calendar.Maghrib}\n" +
				   $"{AppResources.Yatsi}: {calendar.Isha}\n" +
				   $"{AppResources.YatsiSonu}: {calendar.EndOfIsha}";
		}

		public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
		{
			if (intent?.Action == null)
			{
				var source = intent == null ? "intent" : "action";
				System.Diagnostics.Debug.WriteLine($"OnStartCommand null {source}, flags={flags}");
				return StartCommandResult.RedeliverIntent;
			}
			
			switch (intent.Action)
			{
				case "SuleymaniyeTakvimi.action.START_SERVICE":
					if (!_isStarted)
					{
						// Call StartForeground immediately to prevent ANR
						StartForeground(NOTIFICATION_ID, _notification);
						_handler.PostDelayed(_runnable, NOTIFICATION_UPDATE_INTERVAL_MS);
						_isStarted = true;
						
						// Start alarm setup work in background (non-blocking)
						_ = Task.Run(async () =>
						{
							try
							{
								// Small delay to let the app finish startup
								await Task.Delay(7000).ConfigureAwait(false);
								System.Diagnostics.Debug.WriteLine($"OnStartCommand: Starting alarm scheduling at {DateTime.Now}");
								
								var dataService = GetDataService();
								await dataService.SetMonthlyAlarmsAsync().ConfigureAwait(false);
							}
							catch (Exception ex)
							{
								System.Diagnostics.Debug.WriteLine($"Alarm startup error: {ex.Message}");
							}
						});
					}
					break;
					
				case "SuleymaniyeTakvimi.action.STOP_SERVICE":
					if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
					{
						StopForeground(StopForegroundFlags.Remove);
					}
					else
					{
#pragma warning disable CA1422 // Validate platform compatibility
						StopForeground(true);
#pragma warning restore CA1422
					}
					StopSelf(NOTIFICATION_ID);
					_isStarted = false;
					break;
					
				case "SuleymaniyeTakvimi.action.REFRESH_NOTIFICATION":
					// Rebuild notification based on latest Preferences and update immediately
					SetNotification();
					_notificationManager.Notify(NOTIFICATION_ID, _notification);
					break;
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