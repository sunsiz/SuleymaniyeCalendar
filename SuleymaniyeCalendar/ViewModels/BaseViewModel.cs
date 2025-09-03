using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using SuleymaniyeCalendar.Resources.Strings;

namespace SuleymaniyeCalendar.ViewModels
{
	public partial class BaseViewModel:ObservableObject
	{
		[ObservableProperty] private string _title;

		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(IsNotBusy))]
		private bool _isBusy;
	
		public bool IsNotBusy=>!IsBusy;

		[ObservableProperty]private int _fontSize = Preferences.Get("FontSize", 14);
		
		public int HeaderFontSize => (int)(FontSize * 1.5);
		public int SubHeaderFontSize => (int)(FontSize * 1.25);
		public int CaptionFontSize => (int)(FontSize * 0.875);
		
		partial void OnFontSizeChanged(int value)
		{
			// Clamp font size for better UX
			var clampedValue = Math.Max(12, Math.Min(28, value));
			if (clampedValue != value)
			{
				FontSize = clampedValue;
				return;
			}
			
			Preferences.Set("FontSize", value);
			
			// Notify dependent properties
			OnPropertyChanged(nameof(HeaderFontSize));
			OnPropertyChanged(nameof(SubHeaderFontSize));
			OnPropertyChanged(nameof(CaptionFontSize));
			
			// Calculate responsive scale factors
			var baseSize = 14.0;
			var scale = value / baseSize;
			
			if (Application.Current?.Resources != null)
			{
				Application.Current.Resources["DefaultFontSize"] = (double)value;
				Application.Current.Resources["HeaderFontSize"] = value * 1.5;
				Application.Current.Resources["SubHeaderFontSize"] = value * 1.25;
				Application.Current.Resources["CaptionFontSize"] = value * 0.925;
                Application.Current.Resources["BodyFontSize"] = value * 0.875;
				Application.Current.Resources["FontScale"] = scale;
			}
		}

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
