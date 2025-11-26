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
	[Service(Exported = false, ForegroundServiceType = Android.Content.PM.ForegroundService.TypeSpecialUse)]
	public class AlarmForegroundService : Service, IAlarmService
	{
		internal const string StartAction = "SuleymaniyeTakvimi.action.START_SERVICE";
		internal const string StopAction = "SuleymaniyeTakvimi.action.STOP_SERVICE";
		internal const string RefreshAction = "SuleymaniyeTakvimi.action.REFRESH_NOTIFICATION";
		internal const string ExtraStartReason = "extra_start_reason";
		internal const string PendingForegroundRestartPreferenceKey = "AlarmForegroundService.PendingRestart";

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
		private bool _isInForeground;
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
			AlarmScheduler.SchedulePrayer(date, triggerTimeSpan, timeOffset, name);
		}

		public void CancelAlarm()
		{
			AlarmScheduler.CancelAll();
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
			_handler = new Handler(Looper.MainLooper);
			_notificationManager = (NotificationManager)Application.Context.GetSystemService(Context.NotificationService);
			NotificationChannelManager.CreateAlarmNotificationChannels();

			_runnable = new Action(() =>
			{
				if (_handler is null)
				{
					return;
				}

				_handler.PostDelayed(_runnable, NOTIFICATION_UPDATE_INTERVAL_MS);
				SetNotification();
				_notificationManager.Notify(NOTIFICATION_ID, _notification);
				_updateCounter++;
				if (_updateCounter < WIDGET_REFRESH_CYCLES)
				{
					return; // Refresh widget every 30 minutes
				}

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
	                if (CheckRemindersEnabledAny())
	                {
					TryRescheduleAlarms();
	                }
			});
		}

		public override void OnDestroy()
		{
			StopNotificationLoop();
			if (_isInForeground)
			{
				StopForegroundService();
				_isInForeground = false;
			}
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
			var action = intent?.Action;
			if (string.IsNullOrWhiteSpace(action))
			{
				var source = intent == null ? "intent" : "action";
				System.Diagnostics.Debug.WriteLine($"OnStartCommand null {source}, flags={flags}");
				// CRITICAL: Must call StartForeground even for null intent to avoid Android crash
				if (!_isInForeground)
				{
					_isInForeground = TryStartForeground("null_intent_recovery");
				}
				return StartCommandResult.RedeliverIntent;
			}

			switch (action)
			{
				case StartAction:
					BeginNotificationLoop();
					if (!_isInForeground)
					{
						var startReason = intent?.GetStringExtra(ExtraStartReason) ?? string.Empty;
						_isInForeground = TryStartForeground(startReason);
					}

					if (!CheckRemindersEnabledAny())
					{
						System.Diagnostics.Debug.WriteLine("OnStartCommand: No reminders enabled, skipping alarm setup.");
						break;
					}

					_ = Task.Run(async () =>
					{
						try
						{
							await Task.Delay(7000).ConfigureAwait(false);
							System.Diagnostics.Debug.WriteLine($"OnStartCommand: Starting alarm scheduling at {DateTime.Now}");

							var dataService = GetDataService();
							await dataService.SetMonthlyAlarmsAsync().ConfigureAwait(false);
						}
						catch (System.Exception ex)
						{
							System.Diagnostics.Debug.WriteLine($"Alarm startup error: {ex.Message}");
						}
					});
					break;

				case StopAction:
					StopNotificationLoop();
					if (_isInForeground)
					{
						StopForegroundService();
						_isInForeground = false;
					}
					StopSelfResult(startId);
					break;

				case RefreshAction:
					SetNotification();
					_notificationManager.Notify(NOTIFICATION_ID, _notification);
					break;

				default:
					System.Diagnostics.Debug.WriteLine($"OnStartCommand: Unknown action {action}");
					break;
			}

			return StartCommandResult.Sticky;
		}
		
		public void StartAlarmForegroundService()
		{
			Log.Info("Main Activity", $"Main Activity SetAlarmForegroundService Started: {DateTime.Now:HH:m:s.fff}");
			var startServiceIntent = new Intent(Application.Context, typeof(AlarmForegroundService));
			startServiceIntent.SetAction(StartAction);
			startServiceIntent.PutExtra(ExtraStartReason, "manual_start");
			MainThread.BeginInvokeOnMainThread(() =>
			{
				if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
				{
					AndroidX.Core.Content.ContextCompat.StartForegroundService(Application.Context, startServiceIntent);
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
			stopServiceIntent.SetAction(StopAction);
			MainThread.BeginInvokeOnMainThread(() =>
			{
				if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
				{
					AndroidX.Core.Content.ContextCompat.StartForegroundService(Application.Context, stopServiceIntent);
				}
				else
				{
					Application.Context?.StartService(stopServiceIntent);
				}
			});
			System.Diagnostics.Debug.WriteLine("Main Activity" + $"Main Activity StopAlarmForegroundService Finished: {DateTime.Now:HH:m:s.fff}");
		}

		// Starts the periodic notification refresh loop if it is not already running.
		private void BeginNotificationLoop()
		{
			if (_isStarted || _handler is null || _runnable is null)
			{
				return;
			}

			SetNotification();
			_notificationManager.Notify(NOTIFICATION_ID, _notification);
			_handler.PostDelayed(_runnable, NOTIFICATION_UPDATE_INTERVAL_MS);
			_isStarted = true;
		}

		// Stops the periodic notification updates to release handler resources.
		private void StopNotificationLoop()
		{
			if (!_isStarted || _handler is null)
			{
				return;
			}

			_handler.RemoveCallbacksAndMessages(null);
			_isStarted = false;
		}

		// Drops the service out of the foreground in a version-safe way.
		private void StopForegroundService()
		{
			try
			{
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
			}
			catch (System.Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"StopForegroundService error: {ex.Message}");
			}
		}

		// Attempts to promote the service to foreground priority, tracking failures for retries.
		private bool TryStartForeground(string reason)
		{
			if (!Preferences.Get("ForegroundServiceEnabled", true))
			{
				return false;
			}

			SetNotification();
			try
			{
#pragma warning disable CA1416 // Platform compatibility
				if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
				{
					StartForeground(NOTIFICATION_ID, _notification, Android.Content.PM.ForegroundService.TypeSpecialUse);
				}
				else
				{
					StartForeground(NOTIFICATION_ID, _notification);
				}
#pragma warning restore CA1416
				Preferences.Set(PendingForegroundRestartPreferenceKey, false);
				return true;
			}
			catch (ForegroundServiceStartNotAllowedException ex)
			{
				Log.Warn(nameof(AlarmForegroundService), $"StartForeground rejected: {ex.Message}");
				Preferences.Set(PendingForegroundRestartPreferenceKey, true);
				AlarmRescheduleJobIntentService.Enqueue(Application.Context, reason ?? "fgs_not_allowed");
				StopNotificationLoop();
				return false;
			}
			catch (Java.Lang.IllegalStateException ex)
			{
				Log.Warn(nameof(AlarmForegroundService), $"StartForeground state error: {ex.Message}");
				Preferences.Set(PendingForegroundRestartPreferenceKey, true);
				StopNotificationLoop();
				return false;
			}
			catch (System.Exception ex)
			{
				Log.Error(nameof(AlarmForegroundService), $"StartForeground failed: {ex.Message}");
				Preferences.Set(PendingForegroundRestartPreferenceKey, true);
				StopNotificationLoop();
				return false;
			}
		}

		private bool CheckRemindersEnabledAny()
        {
            return Preferences.Get("falsefajrEnabled", false) || Preferences.Get("fajrEnabled", false) ||
                   Preferences.Get("sunriseEnabled", false) || Preferences.Get("dhuhrEnabled", false) ||
                   Preferences.Get("asrEnabled", false) || Preferences.Get("maghribEnabled", false) ||
                   Preferences.Get("ishaEnabled", false) || Preferences.Get("endofishaEnabled", false);
        }
    }
}