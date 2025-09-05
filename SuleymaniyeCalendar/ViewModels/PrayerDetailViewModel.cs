using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SuleymaniyeCalendar.Models;
using SuleymaniyeCalendar.Resources.Strings;
using System.Collections.ObjectModel;
using System.Diagnostics;
using SuleymaniyeCalendar.Services;

namespace SuleymaniyeCalendar.ViewModels
{
	[QueryProperty(nameof(PrayerId), nameof(PrayerId))]
	public partial class PrayerDetailViewModel : BaseViewModel
	{
		private readonly IAudioPreviewService _audioPreview;
		private readonly DataService _dataService;
		private string time;
		public string Time { get => time; set => SetProperty(ref time, value); }
		private bool enabled;
		public bool Enabled { get => enabled; set { if (SetProperty(ref enabled, value)) { try { Preferences.Set(PrayerId + "Enabled", value); } catch { } OnPropertyChanged(nameof(ShowAdvancedOptions)); } } }
		private bool vibration;
		public bool Vibration { get => vibration; set => SetProperty(ref vibration, value); }
		private bool notification;
		public bool Notification { get => notification; set => SetProperty(ref notification, value); }
		private bool alarm;
		public bool Alarm { get => alarm; set => SetProperty(ref alarm, value); }
		private ObservableCollection<Sound> availableSounds;
		public ObservableCollection<Sound> AvailableSounds { get => availableSounds; set => SetProperty(ref availableSounds, value); }
		private Sound selectedSound;
		public Sound SelectedSound { get => selectedSound; set => SetProperty(ref selectedSound, value); }
		private string prayerId;
		public string PrayerId { get => prayerId; set { if (SetProperty(ref prayerId, value)) { IsBusy = true; LoadPrayer(); IsBusy = false; } } }
		private int notificationTime;
		public int NotificationTime { get => notificationTime; set { if (SetProperty(ref notificationTime, value)) Preferences.Set(PrayerId + "NotificationTime", value); } }
		private bool isPlaying;
		public bool IsPlaying { get => isPlaying; set => SetProperty(ref isPlaying, value); }
		public bool IsNecessary => !((DeviceInfo.Platform == DevicePlatform.Android && DeviceInfo.Version.Major >= 10) || DeviceInfo.Platform == DevicePlatform.iOS);
		public bool ShowAdvancedOptions => Enabled && IsNecessary;

		public PrayerDetailViewModel(IAudioPreviewService audioPreview, DataService dataService)
		{
			_audioPreview = audioPreview;
			_dataService = dataService;
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
				OnPropertyChanged(nameof(ShowAdvancedOptions));
				//if (value)
				//{
				//	Preferences.Set(PrayerId + "Notification", Preferences.Get(PrayerId + "Notification", true));
				//	Preferences.Set(PrayerId + "Vibration", Preferences.Get(PrayerId + "Vibration", false));
				//	Preferences.Set(PrayerId + "Alarm", Preferences.Get(PrayerId + "Alarm", false));
				//}

				// No messaging: Main page will refresh on OnAppearing
			}
		}

		[RelayCommand]
		public void GoBack()
		{
                    IsBusy = true;
			MainThread.BeginInvokeOnMainThread(async () =>
			{
				try
                {
					if (PrayerId != null && SelectedSound != null)
						Preferences.Set(PrayerId + "AlarmSound", SelectedSound.FileName);
					// Ensure preview is stopped when leaving
					await _audioPreview.StopAsync().ConfigureAwait(false);
					IsPlaying = false;
					await Task.Delay(1000);
					await _dataService.SetMonthlyAlarmsAsync();
                }
				catch (Exception ex)
				{
					Alert("Test", ex.Message);
				}
			});
			Shell.Current.GoToAsync("..");
            IsBusy = false;
        }

		//[RelayCommand]
		//public void NotificationCheckedChanged(bool value)
		//{
		//	if (!IsBusy)
		//	{
		//		Preferences.Set(PrayerId + "Notification", value);
		//		Debug.WriteLine("Value Set for -> " + PrayerId + "Notification: " +
		//						Preferences.Get(PrayerId + "Notification", value));
		//		Notification = value;
		//	}
		//}

		//[RelayCommand]
		//public void VibrationCheckedChanged(bool value)
		//{
		//	if (!IsBusy)
		//	{
		//		Preferences.Set(PrayerId + "Vibration", value);
		//		Debug.WriteLine("Value Set for -> " + PrayerId + "Vibration: " +
		//						Preferences.Get(PrayerId + "Vibration", value));
		//		Vibration = value;
		//		if (value)
		//		{
		//			try
		//			{
		//				Microsoft.Maui.Devices.Vibration.Default.Vibrate();
		//				var duration = TimeSpan.FromSeconds(1);
		//				Microsoft.Maui.Devices.Vibration.Default.Vibrate(duration);
		//			}
		//			catch (FeatureNotSupportedException ex)
		//			{
		//				Alert(AppResources.TitremeyiDesteklemiyor + ex.Message, AppResources.CihazTitretmeyiDesteklemiyor);
		//			}
		//			catch (Exception ex)
		//			{
		//				Alert(ex.Message, AppResources.SorunCikti);
		//			}
		//		}
		//	}
		//}

		//[RelayCommand]
		//public void AlarmCheckedChanged(bool value)
		//{
		//	if (!IsBusy)
		//	{
		//		Preferences.Set(PrayerId + "Alarm", value);
		//		Debug.WriteLine("Value Setted for -> " + PrayerId + "Alarm: " +
		//						Preferences.Get(PrayerId + "Alarm", value));
		//		Alarm = value;
		//	}
		//}

		[RelayCommand]
	public async Task TestButtonClicked()
		{
			if (!IsPlaying)
			{
				var fileKey = SelectedSound?.FileName;
				if (!string.IsNullOrWhiteSpace(fileKey))
				{
		            await _audioPreview.PlayAsync(fileKey, loop: true).ConfigureAwait(false);
		            MainThread.BeginInvokeOnMainThread(() => IsPlaying = _audioPreview.IsPlaying);
				}
			}
			else
			{
				await _audioPreview.StopAsync().ConfigureAwait(false);
				MainThread.BeginInvokeOnMainThread(() => IsPlaying = false);
			}
		}

		// Partial property change hooks migrated into property setters

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
				Enabled = Preferences.Get(PrayerId + "Enabled", false);
				//Notification = Preferences.Get(PrayerId + "Notification", true);
				//Vibration = Preferences.Get(PrayerId + "Vibration", true);
				//Alarm = Preferences.Get(PrayerId + "Alarm", false);
				NotificationTime = Preferences.Get(PrayerId + "NotificationTime", 0);
				OnPropertyChanged(nameof(ShowAdvancedOptions));
				// Ensure SelectedSound reflects saved choice or a default
				var saved = Preferences.Get(PrayerId + "AlarmSound", "kus");
				SelectedSound = AvailableSounds?.FirstOrDefault(n => n.FileName == saved)
								?? AvailableSounds?.FirstOrDefault(n => n.FileName == "kus")
								?? AvailableSounds?.FirstOrDefault();

				// no-op: messaging will be sent explicitly on toggle
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
