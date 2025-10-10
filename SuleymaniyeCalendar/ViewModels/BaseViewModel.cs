using System;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using SuleymaniyeCalendar.Resources.Strings;

namespace SuleymaniyeCalendar.ViewModels
{
	public partial class BaseViewModel : ObservableObject
	{
		public BaseViewModel()
		{
			// Initialize font size at startup - this triggers the setter which updates all DynamicResource keys
			var savedFontSize = Preferences.Get("FontSize", 14);
			FontSize = savedFontSize;
		}

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

		private int _fontSize = 14; // Default value, will be set in constructor from Preferences
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
					OnPropertyChanged(nameof(TitleSmallFontSize));
					OnPropertyChanged(nameof(BodyLargeFontSize));
					OnPropertyChanged(nameof(CaptionFontSize));
					OnPropertyChanged(nameof(BodyFontSize));

					var baseSize = 14.0;
					var scale = clampedValue / baseSize;

					if (Application.Current?.Resources != null)
					{
						// NOTE: We now round all calculated font sizes to whole numbers to avoid subtle sub-pixel layout
						// differences between the initial cached month assignment and the later fresh replacement.
						// Fractions like 14.7 could initially rasterize at 14px and later reflow to 15px after a
						// subsequent measure pass, which looks like a font size change. Rounding stabilizes layout.
						Func<double, double> R = v => Math.Round(v); // Round to nearest integer for consistency.

						// Base sizes
						Application.Current.Resources["DefaultFontSize"] = (double)clampedValue;
						Application.Current.Resources["FontScale"] = scale; // Keep scale as double (may be fractional)

						// Material Design 3 Typography Scale (rounded)
						Application.Current.Resources["DisplayFontSize"] = R(clampedValue * 2.0);          // Display Large
						Application.Current.Resources["DisplaySmallFontSize"] = R(clampedValue * 1.7);     // Display Small
						Application.Current.Resources["TitleFontSize"] = R(clampedValue * 1.57);           // Title Large
						Application.Current.Resources["TitleMediumFontSize"] = R(clampedValue * 1.43);     // Title Medium
						Application.Current.Resources["TitleSmallFontSize"] = R(clampedValue * 1.29);      // Title Small (Prayer Times)
						Application.Current.Resources["HeaderFontSize"] = R(clampedValue * 1.35);         // Headline/Header
						Application.Current.Resources["SubHeaderFontSize"] = R(clampedValue * 1.2);       // Headline Small
						Application.Current.Resources["BodyLargeFontSize"] = R(clampedValue * 1.14);      // Body Large (Prayer Names)
						Application.Current.Resources["BodyFontSize"] = R(clampedValue * 1.05);          // Body Medium
						Application.Current.Resources["BodySmallFontSize"] = R(clampedValue * 1.0);       // Body Small
						Application.Current.Resources["CaptionFontSize"] = R(clampedValue * 0.86);        // Caption/Label Small

					// Icons scale proportionally with text; keep a slight bias to avoid oversized icons (rounded)
					Application.Current.Resources["IconSmallFontSize"] = R(clampedValue * 1.1);
					Application.Current.Resources["IconMediumFontSize"] = R(clampedValue * 1.25);
					Application.Current.Resources["IconLargeFontSize"] = R(clampedValue * 1.6);
					Application.Current.Resources["IconXLFontSize"] = R(clampedValue * 3.6);
					Application.Current.Resources["PlayButtonContainerSize"] = R(clampedValue * 4.0); // Container for IconXL with padding
					Application.Current.Resources["PlayButtonCornerRadius"] = R(clampedValue * 2.0); // Half of container (4.0/2=2.0) for perfect circle
				}
			}
		}
	}		public int HeaderFontSize => (int)(FontSize * 1.35);
		public int SubHeaderFontSize => (int)(FontSize * 1.2);
		public int TitleSmallFontSize => (int)(FontSize * 1.29);
		public int BodyLargeFontSize => (int)(FontSize * 1.14);
		public int CaptionFontSize => (int)(FontSize * 0.86);
        public int BodyFontSize => (int)(FontSize * 1.05);

		// Lightweight cross-page modal overlay state; used for long running operations (refreshing, scheduling, etc.)
		private bool showOverlay;
		public bool ShowOverlay
		{
			get => showOverlay;
			set => SetProperty(ref showOverlay, value);
		}

		private string overlayMessage;
		public string OverlayMessage
		{
			get => overlayMessage;
			set => SetProperty(ref overlayMessage, value);
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

		public static void InitializeFontSize()
		{
			var savedFontSize = Preferences.Get("FontSize", 14);
			var clampedValue = Math.Max(12, Math.Min(28, savedFontSize));
			
			if (Application.Current?.Resources != null)
			{
				// NOTE: See comment in FontSize setter about rounding to stabilize layout across refreshes.
				Func<double, double> R = v => Math.Round(v);

				// Base sizes
				Application.Current.Resources["DefaultFontSize"] = (double)clampedValue;
				Application.Current.Resources["FontScale"] = clampedValue / 14.0;

				// Material Design 3 Typography Scale (rounded)
				Application.Current.Resources["DisplayFontSize"] = R(clampedValue * 2.0);          // Display Large
				Application.Current.Resources["DisplaySmallFontSize"] = R(clampedValue * 1.7);     // Display Small
				Application.Current.Resources["TitleFontSize"] = R(clampedValue * 1.57);           // Title Large
				Application.Current.Resources["TitleMediumFontSize"] = R(clampedValue * 1.43);     // Title Medium
				Application.Current.Resources["TitleSmallFontSize"] = R(clampedValue * 1.29);      // Title Small (Prayer Times)
				Application.Current.Resources["HeaderFontSize"] = R(clampedValue * 1.35);         // Headline/Header
				Application.Current.Resources["SubHeaderFontSize"] = R(clampedValue * 1.2);       // Headline Small
				Application.Current.Resources["BodyLargeFontSize"] = R(clampedValue * 1.14);      // Body Large (Prayer Names)
				Application.Current.Resources["BodyFontSize"] = R(clampedValue * 1.05);          // Body Medium
				Application.Current.Resources["BodySmallFontSize"] = R(clampedValue * 1.0);       // Body Small
				Application.Current.Resources["CaptionFontSize"] = R(clampedValue * 0.86);        // Caption/Label Small

			// Icons scale proportionally with text; keep a slight bias to avoid oversized icons (rounded)
			Application.Current.Resources["IconSmallFontSize"] = R(clampedValue * 1.1);
			Application.Current.Resources["IconMediumFontSize"] = R(clampedValue * 1.25);
			Application.Current.Resources["IconLargeFontSize"] = R(clampedValue * 1.6);
			Application.Current.Resources["IconXLFontSize"] = R(clampedValue * 3.6);
			Application.Current.Resources["PlayButtonContainerSize"] = R(clampedValue * 4.0); // Container for IconXL with padding
			Application.Current.Resources["PlayButtonCornerRadius"] = R(clampedValue * 2.0); // Half of container (4.0/2=2.0) for perfect circle
		}
	}		public static void Alert(string title, string message)
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
