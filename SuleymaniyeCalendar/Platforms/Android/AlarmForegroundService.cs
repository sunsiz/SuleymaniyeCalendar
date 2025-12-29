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
		
		private NotificationManager? _notificationManager;
		private readonly string _channelName = AppResources.SuleymaniyeVakfiTakvimi;
		private Notification? _notification;
		private bool _isStarted;
		private bool _isInForeground;
		private Handler? _handler;
		private Action? _runnable;
		private int _updateCounter;
		private DateTime _lastRescheduleAttemptUtc = DateTime.MinValue;
		private DateTime _lastKnownDate = DateTime.Today; // Track date for midnight crossing detection
		private string? _lastNotificationTitle; // Cache last successful notification title to avoid "Refreshing" stuck state
		
		/// <summary>
		/// Critical system events that should bypass anti-spam protection for alarm scheduling.
		/// These events require immediate rescheduling to maintain accurate prayer notifications.
		/// </summary>
		private static readonly string[] CriticalSystemEvents =
		{
			Intent.ActionBootCompleted,
			Intent.ActionTimeChanged,
			Intent.ActionTimezoneChanged,
			Intent.ActionMyPackageReplaced
		};
		
		/// <summary>
		/// Determines if the start reason is a critical system event that should bypass anti-spam protection.
		/// </summary>
		private static bool IsCriticalSystemEvent(string startReason)
		{
			if (string.IsNullOrEmpty(startReason)) return false;
			
			foreach (var evt in CriticalSystemEvents)
			{
				if (startReason == evt) return true;
			}
			return false;
		}
		
		public override IBinder? OnBind(Intent? intent)
		{
			return null;
		}

        public void SetAlarm(DateTime alarmTime, int requestCode, NotificationSettings settings)
        {
            var intent = new Intent(Application.Context, typeof(AlarmNotificationReceiver));
            intent.PutExtra("name", settings.PrayerName);
            intent.PutExtra("prayerId", settings.PrayerId);
            // Use actual prayer time for display, not the alarm trigger time
            intent.PutExtra("time", string.IsNullOrEmpty(settings.PrayerTime) ? alarmTime.ToString("HH:mm") : settings.PrayerTime);
            
            var pendingIntent = PendingIntent.GetBroadcast(
                Application.Context, 
                requestCode, 
                intent, 
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            var alarmManager = Application.Context.GetSystemService(Context.AlarmService) as AlarmManager;
            if (alarmManager == null || pendingIntent == null) return;
            
            // Convert to UTC milliseconds
            long triggerMillis = (long)(alarmTime.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;

            if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            {
                alarmManager.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, triggerMillis, pendingIntent);
            }
            else
            {
                alarmManager.SetExact(AlarmType.RtcWakeup, triggerMillis, pendingIntent);
            }
        }

        public void CancelAllAlarms()
        {
            var alarmManager = Application.Context.GetSystemService(Context.AlarmService) as AlarmManager;
            if (alarmManager == null) return;
            
            var intent = new Intent(Application.Context, typeof(AlarmNotificationReceiver));
            
            // Loop through next 33 days * 8 prayers to cover all potential alarms
            var startDate = DateTime.Today.AddDays(-1);
            for (int i = 0; i < 33; i++)
            {
                var date = startDate.AddDays(i);
                for (int p = 0; p < 8; p++)
                {
                    int requestCode = (date.DayOfYear * 100) + p;
                    var pendingIntent = PendingIntent.GetBroadcast(
                        Application.Context, 
                        requestCode, 
                        intent, 
                        PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
                    
                    if (pendingIntent != null)
                    {
                        alarmManager.Cancel(pendingIntent);
                        pendingIntent.Cancel();
                    }
                }
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
                        if (dataService != null)
                        {
                            var location = await dataService.GetCurrentLocationAsync(false);
                            if (location != null)
                            {
						        await dataService.SetMonthlyAlarmsAsync(location).ConfigureAwait(false);
                            }
                        }
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

		private DataService? GetDataService()
		{
            return IPlatformApplication.Current?.Services?.GetService<DataService>();
		}

		public override void OnCreate()
		{
			base.OnCreate();
			var mainLooper = Looper.MainLooper;
			if (mainLooper != null)
			{
				_handler = new Handler(mainLooper);
			}
			_notificationManager = Application.Context.GetSystemService(Context.NotificationService) as NotificationManager;
			NotificationChannelManager.CreateAlarmNotificationChannels();

			_runnable = new Action(() =>
			{
				if (_handler is null || _runnable is null)
				{
					return;
				}

				_handler.PostDelayed(_runnable, NOTIFICATION_UPDATE_INTERVAL_MS);
				
				// Check for date change (midnight crossing)
				var today = DateTime.Today;
				if (today != _lastKnownDate)
				{
					System.Diagnostics.Debug.WriteLine($"[AlarmForegroundService] Date changed from {_lastKnownDate:yyyy-MM-dd} to {today:yyyy-MM-dd} - refreshing data");
					_lastKnownDate = today;
					
					// Refresh calendar data for the new day
					_ = Task.Run(async () =>
					{
						try
						{
							var dataService = GetDataService();
							if (dataService != null)
							{
								var cal = await dataService.GetTodayPrayerTimesAsync().ConfigureAwait(false);
								if (cal != null)
								{
									dataService.calendar = cal;
								}
							}
						}
						catch (Exception ex)
						{
							System.Diagnostics.Debug.WriteLine($"[AlarmForegroundService] Date change refresh failed: {ex.Message}");
						}
					});
				}
				
				SetNotification();
				if (_notification != null)
				{
					_notificationManager?.Notify(NOTIFICATION_ID, _notification);
				}
				_updateCounter++;
				if (_updateCounter < WIDGET_REFRESH_CYCLES)
				{
					return; // Refresh widget every 30 minutes
				}

				var intent = new Intent(ApplicationContext ?? Application.Context, typeof(WidgetService));
				try
				{
					(ApplicationContext ?? Application.Context).StartService(intent);
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
				_notificationManager?.CreateNotificationChannel(channel);
				
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
					.SetContentTitle(notificationTitle)!
					.SetStyle(new NotificationCompat.BigTextStyle().BigText((Preferences.Get("NotificationPrayerTimesEnabled", false) ? GetTodaysPrayerTimes() : string.Empty)))!
					.SetSmallIcon(Resource.Drawable.app_logo)!
					.SetContentIntent(BuildIntentToShowMainActivity())!
					.SetWhen(Java.Lang.JavaSystem.CurrentTimeMillis())!
					.SetShowWhen(true)!
					.SetOngoing(true)!
					.SetOnlyAlertOnce(true)!;
				_notification = compat.Build();
			}
		}

		/// <summary>
		/// Builds a PendingIntent that will display the main activity of the app. This is used when the 
		/// user taps on the notification; it will take them to the main activity of the app.
		/// </summary>
		/// <returns>The content intent.</returns>
		PendingIntent? BuildIntentToShowMainActivity()
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
			var calendar = dataService?.calendar;
			
			// If calendar data is unavailable, return cached title or fallback to "Refreshing" only on first load
			if (calendar is null)
			{
				System.Diagnostics.Debug.WriteLine("[AlarmForegroundService] Calendar is null, using cached notification title");
				return _lastNotificationTitle ?? AppResources.Yenileniyor;
			}

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
				
				// Cache successful result to avoid "Refreshing" stuck state on subsequent failures
				_lastNotificationTitle = message;
				return message;
			}
			catch (Exception exception)
			{
				System.Diagnostics.Debug.WriteLine($"GetFormattedRemainingTime exception: {exception.Message}. Location: {calendar.Latitude}, {calendar.Longitude}");
				Log.Error("GetFormattedRemainingTime", $"GetFormattedRemainingTime exception: {exception.Message}. Location: {calendar.Latitude}, {calendar.Longitude}");
				
				// Return cached title if available, otherwise show a generic message
				return _lastNotificationTitle ?? AppResources.KonumIzniIcerik;
			}
		}

		private string GetTodaysPrayerTimes()
		{
			var dataService = GetDataService();
			var calendar = dataService?.calendar;
            if (calendar == null) return string.Empty;
			
			return $"{AppResources.FecriKazip}: {calendar.FalseFajr}\n" +
				   $"{AppResources.FecriSadik}: {calendar.Fajr}\n" +
				   $"{AppResources.SabahSonu}: {calendar.Sunrise}\n" +
				   $"{AppResources.Ogle}: {calendar.Dhuhr}\n" +
				   $"{AppResources.Ikindi}: {calendar.Asr}\n" +
				   $"{AppResources.Aksam}: {calendar.Maghrib}\n" +
				   $"{AppResources.Yatsi}: {calendar.Isha}\n" +
				   $"{AppResources.YatsiSonu}: {calendar.EndOfIsha}";
		}

		public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
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
					var startReason = intent?.GetStringExtra(ExtraStartReason) ?? string.Empty;
					if (!_isInForeground)
					{
						_isInForeground = TryStartForeground(startReason);
					}

					if (!CheckRemindersEnabledAny())
					{
						System.Diagnostics.Debug.WriteLine("OnStartCommand: No reminders enabled, skipping alarm setup.");
						break;
					}

					// System events (boot, time change, etc.) should bypass anti-spam protection
					var forceReschedule = IsCriticalSystemEvent(startReason);
					
					_ = Task.Run(async () =>
					{
						try
						{
                            // Ensure data is loaded for the notification
                            var dataService = GetDataService();
                            if (dataService != null && dataService.calendar == null)
                            {
                                var cal = await dataService.GetTodayPrayerTimesAsync().ConfigureAwait(false);
                                if (cal != null)
                                {
                                    dataService.calendar = cal;
                                    // Refresh notification immediately with loaded data
                                    MainThread.BeginInvokeOnMainThread(() => 
                                    {
                                        SetNotification();
                                        _notificationManager.Notify(NOTIFICATION_ID, _notification);
                                    });
                                }
                            }

							await Task.Delay(7000).ConfigureAwait(false);
							System.Diagnostics.Debug.WriteLine($"OnStartCommand: Starting alarm scheduling at {DateTime.Now} (forceReschedule={forceReschedule}, reason={startReason})");

                            if (dataService != null)
                            {
                                var loc = await dataService.GetCurrentLocationAsync(false);
                                if (loc != null)
                                {
							        await dataService.SetMonthlyAlarmsAsync(loc, forceReschedule).ConfigureAwait(false);
                                }
                            }
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

        public void RefreshNotification()
        {
            try
            {
                if (!Preferences.Get("ForegroundServiceEnabled", true)) return;

                var ctx = Application.Context;
                var refreshIntent = new Intent(ctx, typeof(AlarmForegroundService))
                    .SetAction(RefreshAction);

                if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                    ctx.StartForegroundService(refreshIntent);
                else
                    ctx.StartService(refreshIntent);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing notification: {ex}");
            }
        }

        public bool SupportsForegroundService => true;

		// Starts the periodic notification refresh loop if it is not already running.
		private void BeginNotificationLoop()
		{
			if (_isStarted || _handler is null || _runnable is null)
			{
				return;
			}

			SetNotification();
			_notificationManager?.Notify(NOTIFICATION_ID, _notification);
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
			
			// Ensure notification is created before starting foreground
			if (_notification == null)
			{
				Log.Warn(nameof(AlarmForegroundService), "Failed to create notification for foreground service");
				return false;
			}
			
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
