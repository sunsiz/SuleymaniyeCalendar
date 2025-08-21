using System.Diagnostics;
using Android.App;
using Android.Content;
using Android.Media;
using Android.OS;
using Android.Provider;
using Android.Util;
using Android.Views;
using Android.Widget;
using Uri = Android.Net.Uri;
using SuleymaniyeCalendar.Resources.Strings;
using View = Android.Views.View;

namespace SuleymaniyeCalendar
{
	[Activity(Label = "@string/app_name", Icon = "@mipmap/icon", Theme = "@style/MyTheme.Alarm", NoHistory = true)]
	public class AlarmActivity : Android.App.Activity, Android.Views.View.IOnClickListener
	{
		private static MediaPlayer _player;

		protected override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);

			// Create your application here
			SetContentView(Resource.Layout.AlarmLayout);
			//get the current intent
			var intent = this.Intent;
			var name = intent?.GetStringExtra("name") ?? string.Empty;
			var time = intent?.GetStringExtra("time") ?? string.Empty;
			Log.Info("AlarmActivity", $"Alarm triggered at {DateTime.Now} for {name} and {time}");
			FindViewById<Android.Widget.Button>(Resource.Id.stopButton)?.SetOnClickListener(this);
			var label = FindViewById<TextView>(Resource.Id.textView);
			var timeLabel = FindViewById<TextView>(Resource.Id.textViewTime);
			FindViewById<Android.Widget.Button>(Resource.Id.stopButton)?.SetText(AppResources.Kapat, TextView.BufferType.Normal);
			var layout = FindViewById<LinearLayout>(Resource.Id.linearLayout);
			var lightColor = Android.Graphics.Color.ParseColor("#EFEBE9");
			var darkColor = Android.Graphics.Color.ParseColor("#121212");
			layout?.SetBackgroundColor(Models.Theme.Tema == 1 ? lightColor : darkColor);
			label?.SetTextColor(Models.Theme.Tema == 1 ? darkColor : lightColor);
			timeLabel?.SetTextColor(Models.Theme.Tema == 1 ? darkColor : lightColor);
			_player ??= new MediaPlayer();
			switch (name)
			{
				case "Fecri Kazip":
					label?.SetText(AppResources.FecriKazip + " " + AppResources.Alarmi, TextView.BufferType.Normal);
					timeLabel?.SetText($"{AppResources.FecriKazip} {AppResources.Vakti} {time}",
						TextView.BufferType.Normal);
					if (Preferences.Get("fecrikazipEtkin", false) && Preferences.Get("fecrikazipAlarm", true))
						PlayAlarm(name);
					if (Preferences.Get("fecrikazipEtkin", false) && Preferences.Get("fecrikazipTitreme", true))
						Vibrate();
					if (Preferences.Get("fecrikazipEtkin", false) && Preferences.Get("fecrikazipBildiri", false))
						ShowNotification(name, time);
					break;
				case "Fecri Sadık":
					label?.SetText(AppResources.FecriSadik + " " + AppResources.Alarmi, TextView.BufferType.Normal);
					timeLabel?.SetText($"{AppResources.FecriSadik} {AppResources.Vakti} {time}",
						TextView.BufferType.Normal);
					if (Preferences.Get("fecrisadikEtkin", false) && Preferences.Get("fecrisadikAlarm", true))
						PlayAlarm(name);
					if (Preferences.Get("fecrisadikEtkin", false) && Preferences.Get("fecrisadikTitreme", true))
						Vibrate();
					if (Preferences.Get("fecrisadikEtkin", false) && Preferences.Get("fecrisadikBildiri", false))
						ShowNotification(name, time);
					break;
				case "Sabah Sonu":
					label?.SetText(AppResources.SabahSonu + " " + AppResources.Alarmi, TextView.BufferType.Normal);
					timeLabel?.SetText($"{AppResources.SabahSonu} {AppResources.Vakti} {time}",
						TextView.BufferType.Normal);
					if (Preferences.Get("sabahsonuEtkin", false) && Preferences.Get("sabahsonuAlarm", true))
						PlayAlarm(name);
					if (Preferences.Get("sabahsonuEtkin", false) && Preferences.Get("sabahsonuTitreme", true))
						Vibrate();
					if (Preferences.Get("sabahsonuEtkin", false) && Preferences.Get("sabahsonuBildiri", false))
						ShowNotification(name, time);
					break;
				case "Öğle":
					label?.SetText(AppResources.Ogle + " " + AppResources.Alarmi, TextView.BufferType.Normal);
					timeLabel?.SetText($"{AppResources.Ogle} {AppResources.Vakti} {time}", TextView.BufferType.Normal);
					if (Preferences.Get("ogleEtkin", false) && Preferences.Get("ogleAlarm", true)) PlayAlarm(name);
					if (Preferences.Get("ogleEtkin", false) && Preferences.Get("ogleTitreme", true)) Vibrate();
					if (Preferences.Get("ogleEtkin", false) && Preferences.Get("ogleBildiri", false))
						ShowNotification(name, time);
					break;
				case "İkindi":
					label?.SetText(AppResources.Ikindi + " " + AppResources.Alarmi, TextView.BufferType.Normal);
					timeLabel?.SetText($"{AppResources.Ikindi} {AppResources.Vakti} {time}",
						TextView.BufferType.Normal);
					if (Preferences.Get("ikindiEtkin", false) && Preferences.Get("ikindiAlarm", true)) PlayAlarm(name);
					if (Preferences.Get("ikindiEtkin", false) && Preferences.Get("ikindiTitreme", true)) Vibrate();
					if (Preferences.Get("ikindiEtkin", false) && Preferences.Get("ikindiBildiri", false))
						ShowNotification(name, time);
					break;
				case "Akşam":
					label?.SetText(AppResources.Aksam + " " + AppResources.Alarmi, TextView.BufferType.Normal);
					timeLabel?.SetText($"{AppResources.Aksam} {AppResources.Vakti} {time}", TextView.BufferType.Normal);
					if (Preferences.Get("aksamEtkin", false) && Preferences.Get("aksamAlarm", true)) PlayAlarm(name);
					if (Preferences.Get("aksamEtkin", false) && Preferences.Get("aksamTitreme", true)) Vibrate();
					if (Preferences.Get("aksamEtkin", false) && Preferences.Get("aksamBildiri", false))
						ShowNotification(name, time);
					break;
				case "Yatsı":
					label?.SetText(AppResources.Yatsi + " " + AppResources.Alarmi, TextView.BufferType.Normal);
					timeLabel?.SetText($"{AppResources.Yatsi} {AppResources.Vakti} {time}", TextView.BufferType.Normal);
					if (Preferences.Get("yatsiEtkin", false) && Preferences.Get("yatsiAlarm", true)) PlayAlarm(name);
					if (Preferences.Get("yatsiEtkin", false) && Preferences.Get("yatsiTitreme", true)) Vibrate();
					if (Preferences.Get("yatsiEtkin", false) && Preferences.Get("yatsiBildiri", false))
						ShowNotification(name, time);
					break;
				case "Yatsı Sonu":
					label?.SetText(AppResources.YatsiSonu + " " + AppResources.Alarmi, TextView.BufferType.Normal);
					timeLabel?.SetText($"{AppResources.YatsiSonu} {AppResources.Vakti} {time}",
						TextView.BufferType.Normal);
					if (Preferences.Get("yatsisonuEtkin", false) && Preferences.Get("yatsisonuAlarm", true))
						PlayAlarm(name);
					if (Preferences.Get("yatsisonuEtkin", false) && Preferences.Get("yatsisonuTitreme", true))
						Vibrate();
					if (Preferences.Get("yatsisonuEtkin", false) && Preferences.Get("yatsisonuBildiri", false))
						ShowNotification(name, time);
					break;
				default: Finish();
					return;
					//label?.SetText("Test Alarmı", TextView.BufferType.Normal);
					//timeLabel?.SetText($"şimdiki zaman: {time}", TextView.BufferType.Normal);
					//PlayAlarm(name);
					//break;
			}
		}

		private void PlayAlarm(string name)
		{
			var key = name switch
			{
				"Fecri Kazip" => "fecrikazip",
				"Fecri Sadık" => "fecrisadik",
				"Sabah Sonu"  => "sabahsonu",
				"Öğle"       => "ogle",
				"İkindi"     => "ikindi",
				"Akşam"      => "aksam",
				"Yatsı"      => "yatsi",
				"Yatsı Sonu" => "yatsisonu",
				_ => string.Empty
			};

			try
			{
				var alarmSesi = Preferences.Get(key + "AlarmSesi", "ezan");
				var context = Android.App.Application.Context;

				string resourceName = alarmSesi switch
				{
					"kus"   => "kus",
					"horoz" => "horoz",
					"ezan"  => "ezan",
					"alarm" => "alarm",
					_       => "alarm"
				};

				int resId = context.Resources?.GetIdentifier(resourceName, "raw", context.PackageName) ?? 0;
				Uri uri = resId > 0
					? Uri.Parse($"{ContentResolver.SchemeAndroidResource}://{context.PackageName}/{resId}")
					: Android.Provider.Settings.System.DefaultAlarmAlertUri;

				if (_player == null)
					_player = new MediaPlayer();
				else
					_player.Reset();

				var attr = new AudioAttributes.Builder()
					.SetContentType(AudioContentType.Sonification)
					.SetUsage(AudioUsageKind.Alarm)
					.Build();
				_player.SetDataSource(context, uri);
				_player.Prepare();
				_player.Looping = true;
				_player.SetAudioAttributes(attr);
				_player.Start();
			}
			catch (Exception exception)
			{
				Log.Error("AlarmActivity-PlayAlarm", $"{AppResources.AlarmHatasi}:\n{exception.Message}");
			}
		}

		private static void Vibrate()
		{
			try
			{
				Microsoft.Maui.Devices.Vibration.Vibrate();
				var duration = TimeSpan.FromSeconds(10);
				Microsoft.Maui.Devices.Vibration.Vibrate(duration);
			}
			catch (FeatureNotSupportedException ex)
			{
				Log.Error("AlarmActivity-Vibrate", AppResources.CihazTitretmeyiDesteklemiyor + ex.Message);
			}
			catch (Exception ex)
			{
				Log.Error("AlarmActivity-Vibrate", ex.Message);
			}
		}

		private void ShowNotification(string name, string time)
		{
			AlarmReceiver notificationHelper = new AlarmReceiver();
			var intent = new Intent(Android.App.Application.Context, typeof(AlarmReceiver));
			intent.PutExtra("name", name);
			intent.PutExtra("time", time);
			intent.AddFlags(ActivityFlags.IncludeStoppedPackages);
			intent.AddFlags(ActivityFlags.ReceiverForeground);
			notificationHelper.OnReceive(ApplicationContext, intent);
			StopAlarm();
		}

		public override void OnAttachedToWindow()
		{
			Window?.AddFlags(WindowManagerFlags.ShowWhenLocked |
							 WindowManagerFlags.KeepScreenOn |
							 WindowManagerFlags.DismissKeyguard |
							 WindowManagerFlags.TurnScreenOn);
		}

		public void OnClick(View v) => StopAlarm();

		private void StopAlarm()
		{
			if (_player != null && _player.IsPlaying)
			{
				_player.Stop();
				_player.Reset();
			}

			CheckRemainingReminders();
			Finish();
		}

		private void CheckRemainingReminders()
		{
			var lastAlarmDateStr = Preferences.Get("LastAlarmDate", "Empty");
			if (lastAlarmDateStr != "Empty")
			{
				if ((DateTime.Parse(lastAlarmDateStr) - DateTime.Today).Days > 5)
				{
					var notificationIntent = new Intent(this, typeof(MainActivity));
					notificationIntent.SetAction("Alarm.action.MAIN_ACTIVITY");
					notificationIntent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTask);
					var pendingIntentFlags = (Build.VERSION.SdkInt >= BuildVersionCodes.M)
						? PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable
						: PendingIntentFlags.UpdateCurrent;
					var pendingIntent = PendingIntent.GetActivity(this, 0, notificationIntent, pendingIntentFlags);
					pendingIntent?.Send();
				}
			}
		}

		protected override void OnResume()
		{
			base.OnResume();
			var minute = Preferences.Get("AlarmDuration", 4);
			Task.Run(async () =>
			{
				await Task.Delay(minute*60000).ConfigureAwait(false);
				StopAlarm();
				return false;
			});
		}
	}
}