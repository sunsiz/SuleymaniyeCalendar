using System;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using SuleymaniyeCalendar.Resources.Strings;

namespace SuleymaniyeCalendar.ViewModels
{
	public partial class BaseViewModel : ObservableObject
	{
		private string _title = string.Empty;
		public string Title
		{
			get => _title;
			set => SetProperty(ref _title, value);
		}

		private bool _isBusy;
		public bool IsBusy
		{
			get => _isBusy;
			set
			{
				if (SetProperty(ref _isBusy, value))
				{
					OnPropertyChanged(nameof(IsNotBusy));
				}
			}
		}

		public bool IsNotBusy => !IsBusy;

		private int _fontSize = Preferences.Get("FontSize", 14);
		public int FontSize
		{
			get => _fontSize;
			set
			{
				var clampedValue = Math.Max(12, Math.Min(28, value));
				if (SetProperty(ref _fontSize, clampedValue))
				{
					Preferences.Set("FontSize", clampedValue);
					OnPropertyChanged(nameof(HeaderFontSize));
					OnPropertyChanged(nameof(SubHeaderFontSize));
					OnPropertyChanged(nameof(CaptionFontSize));

					var baseSize = 14.0;
					var scale = clampedValue / baseSize;

					if (Application.Current?.Resources != null)
					{
						Application.Current.Resources["DefaultFontSize"] = (double)clampedValue;
						Application.Current.Resources["HeaderFontSize"] = clampedValue * 1.5;
						Application.Current.Resources["SubHeaderFontSize"] = clampedValue * 1.25;
						Application.Current.Resources["CaptionFontSize"] = clampedValue * 0.925;
						Application.Current.Resources["BodyFontSize"] = clampedValue * 0.875;
						Application.Current.Resources["FontScale"] = scale;
						// Icons scale proportionally with text; keep a slight bias to avoid oversized icons
						Application.Current.Resources["IconSmallFontSize"] = Math.Round(clampedValue * 1.1);
						Application.Current.Resources["IconMediumFontSize"] = Math.Round(clampedValue * 1.25);
						Application.Current.Resources["IconLargeFontSize"] = Math.Round(clampedValue * 1.6);
						Application.Current.Resources["IconXLFontSize"] = Math.Round(clampedValue * 3.6);
					}
				}
			}
		}

		public int HeaderFontSize => (int)(FontSize * 1.5);
		public int SubHeaderFontSize => (int)(FontSize * 1.25);
		public int CaptionFontSize => (int)(FontSize * 0.875);

		public static void ShowToast(string message)
		{
			MainThread.BeginInvokeOnMainThread(() =>
			{
				CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
				ToastDuration duration = ToastDuration.Long;
				double fontSize = 14;
				var toast = Toast.Make(message, duration, fontSize);
				toast.Show(cancellationTokenSource.Token);
			});
		}

		public static void Alert(string title, string message)
		{
			MainThread.BeginInvokeOnMainThread(async () =>
			{
				await Shell.Current.DisplayAlert(title, message, AppResources.Tamam);
			});
		}

		public static bool IsVoiceOverRunning()
		{
			return true;
		}
	}
}
