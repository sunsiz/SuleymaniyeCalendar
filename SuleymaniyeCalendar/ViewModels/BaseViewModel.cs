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
		partial void OnFontSizeChanged(int value) => Preferences.Set("FontSize", value);

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
