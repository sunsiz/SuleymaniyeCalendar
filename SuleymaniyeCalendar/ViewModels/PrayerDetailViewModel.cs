using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SuleymaniyeCalendar.Models;
using SuleymaniyeCalendar.Resources.Strings;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;

namespace SuleymaniyeCalendar.ViewModels
{
	[QueryProperty(nameof(PrayerId), nameof(PrayerId))]
	public partial class PrayerDetailViewModel : BaseViewModel
	{
		[ObservableProperty] private string _time;
		[ObservableProperty] private bool _enabled;
		[ObservableProperty] private bool _vibration;
		[ObservableProperty] private bool _notification;
		[ObservableProperty] private bool _alarm;
		[ObservableProperty] private ObservableCollection<Sound> _availableSounds;
		[ObservableProperty] private Sound _selectedSound;
		[ObservableProperty] private string _prayerId;
		[ObservableProperty] private int _notificationTime;
		[ObservableProperty] private bool _isPlaying;
		[ObservableProperty] private string _previewSource;
		public bool IsNecessary => !((DeviceInfo.Platform == DevicePlatform.Android && DeviceInfo.Version.Major >= 10) || DeviceInfo.Platform == DevicePlatform.iOS);

		public PrayerDetailViewModel()
		{
			Title = AppResources.PageTitle;
			LoadSounds();
			IsPlaying = false;
		}

		[RelayCommand]
		public void EnabledSwitchToggled(bool value)
		{
			if (!IsBusy)
			{
				Preferences.Set(PrayerId + "Enabled", value);
				Debug.WriteLine("Value Set for -> " + PrayerId + "Enabled: " +
								Preferences.Get(PrayerId + "Enabled", value));
				Enabled = value;
				if (value)
				{
					Preferences.Set(PrayerId + "Notification", Preferences.Get(PrayerId + "Notification", true));
					Preferences.Set(PrayerId + "Vibration", Preferences.Get(PrayerId + "Vibration", false));
					Preferences.Set(PrayerId + "Alarm", Preferences.Get(PrayerId + "Alarm", false));
				}
			}
		}

		[RelayCommand]
		public void GoBack()
		{
			MainThread.BeginInvokeOnMainThread(async () =>
			{
				try
				{
					if (PrayerId != null && SelectedSound != null)
						Preferences.Set(PrayerId + "AlarmSound", SelectedSound.FileName);
					await Task.Delay(1000);
				}
				catch (Exception ex)
				{
					Alert("Test", ex.Message);
				}
			});
			Shell.Current.GoToAsync("..");
		}

		[RelayCommand]
		public void NotificationCheckedChanged(bool value)
		{
			if (!IsBusy)
			{
				Preferences.Set(PrayerId + "Notification", value);
				Debug.WriteLine("Value Set for -> " + PrayerId + "Notification: " +
								Preferences.Get(PrayerId + "Notification", value));
				Notification = value;
			}
		}

		[RelayCommand]
		public void VibrationCheckedChanged(bool value)
		{
			if (!IsBusy)
			{
				Preferences.Set(PrayerId + "Vibration", value);
				Debug.WriteLine("Value Set for -> " + PrayerId + "Vibration: " +
								Preferences.Get(PrayerId + "Vibration", value));
				Vibration = value;
				if (value)
				{
					try
					{
						Microsoft.Maui.Devices.Vibration.Default.Vibrate();
						var duration = TimeSpan.FromSeconds(1);
						Microsoft.Maui.Devices.Vibration.Default.Vibrate(duration);
					}
					catch (FeatureNotSupportedException ex)
					{
						Alert(AppResources.TitremeyiDesteklemiyor + ex.Message, AppResources.CihazTitretmeyiDesteklemiyor);
					}
					catch (Exception ex)
					{
						Alert(ex.Message, AppResources.SorunCikti);
					}
				}
			}
		}

		[RelayCommand]
		public void AlarmCheckedChanged(bool value)
		{
			if (!IsBusy)
			{
				Preferences.Set(PrayerId + "Alarm", value);
				Debug.WriteLine("Value Setted for -> " + PrayerId + "Alarm: " +
								Preferences.Get(PrayerId + "Alarm", value));
				Alarm = value;
			}
		}

		[RelayCommand]
		public async Task TestButtonClicked()
		{
			if (!IsPlaying)
			{
				var src = await GetPreviewFilePathAsync(SelectedSound?.FileName + ".mp3").ConfigureAwait(false);
				MainThread.BeginInvokeOnMainThread(() =>
				{
					PreviewSource = src;
					IsPlaying = !string.IsNullOrEmpty(PreviewSource);
				});
			}
			else
			{
				IsPlaying = false;
				PreviewSource = null;
			}
		}

		private static async Task<string> GetPreviewFilePathAsync(string fileName)
		{
			if (string.IsNullOrWhiteSpace(fileName)) return null;
			try
			{
				using var stream = await FileSystem.OpenAppPackageFileAsync(fileName).ConfigureAwait(false);
				var cache = FileSystem.CacheDirectory;
				var dest = Path.Combine(cache, fileName);
				using var output = File.Create(dest);
				await stream.CopyToAsync(output).ConfigureAwait(false);
				return dest;
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Preview copy failed: {ex.Message}");
				return null;
			}
		}

		partial void OnNotificationTimeChanged(int value) { Preferences.Set(PrayerId + "NotificationTime", value); }

		partial void OnPrayerIdChanged(string value)
		{
			IsBusy = true;
			PrayerId = value;
			LoadPrayer();
			IsBusy = false;
		}

		private void LoadPrayer()
		{
			try
			{
				switch (PrayerId)
				{
					case "falsefajr":
						Title = AppResources.FecriKazip;
						break;
					case "fajr":
						Title = AppResources.FecriSadik;
						break;
					case "sunrise":
						Title = AppResources.SabahSonu;
						break;
					case "dhuhr":
						Title = AppResources.Ogle;
						break;
					case "asr":
						Title = AppResources.Ikindi;
						break;
					case "maghrib":
						Title = AppResources.Aksam;
						break;
					case "isha":
						Title = AppResources.Yatsi;
						break;
					case "endofisha":
						Title = AppResources.YatsiSonu;
						break;
				}
				Time = Preferences.Get(PrayerId, "");
				Enabled = Preferences.Get(PrayerId + "Enabled", true);
				Notification = Preferences.Get(PrayerId + "Notification", true);
				Vibration = Preferences.Get(PrayerId + "Vibration", true);
				Alarm = Preferences.Get(PrayerId + "Alarm", false);
				NotificationTime = Preferences.Get(PrayerId + "NotificationTime", 0);
				// Ensure SelectedSound reflects saved choice or a default
				var saved = Preferences.Get(PrayerId + "AlarmSound", "alarm");
				SelectedSound = AvailableSounds?.FirstOrDefault(n => n.FileName == saved)
								?? AvailableSounds?.FirstOrDefault(n => n.FileName == "alarm")
								?? AvailableSounds?.FirstOrDefault();
			}
			catch (Exception ex)
			{
				Debug.WriteLine("Failed to Load Prayer Detail\t" + ex.Message);
			}
		}
		private void LoadSounds()
		{
			AvailableSounds = new ObservableCollection<Sound>()
			{
				new Sound(fileName: "kus", name: AppResources.KusCiviltisi),
				new Sound(fileName: "horoz", name: AppResources.HorozOtusu),
				new Sound(fileName: "alarm", name: AppResources.CalarSaat),
				new Sound(fileName: "ezan", name: AppResources.EzanSesi),
				new Sound(fileName: "alarm2", name: AppResources.CalarSaat + " 1"),
				new Sound(fileName: "beep1", name: AppResources.CalarSaat + " 2"),
				new Sound(fileName: "beep2", name: AppResources.CalarSaat + " 3"),
				new Sound(fileName: "beep3", name: AppResources.CalarSaat + " 4")
			};
			// Selection will be set in LoadPrayer when PrayerId is known
		}
	}
}
