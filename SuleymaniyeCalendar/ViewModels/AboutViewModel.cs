using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SuleymaniyeCalendar.Resources.Strings;
using SuleymaniyeCalendar.Views;

namespace SuleymaniyeCalendar.ViewModels
{
	public partial class AboutViewModel : BaseViewModel
	{
		private string versionNumber = string.Empty;

		/// <summary>
		/// Gets or sets the app version string displayed on the About page.
		/// AOT-safe explicit property, avoids MVVMTK0045 in WinUI.
		/// </summary>
		public string VersionNumber
		{
			get => versionNumber;
			set => SetProperty(ref versionNumber, value);
		}

		public bool ShowButtons
		{
			get
			{
				if (DeviceInfo.Platform == DevicePlatform.iOS)
				{
					return IsVoiceOverRunning();
				}
				else return false;
			}
		}

		[ObservableProperty]
		private bool _showDesignShowcase = false;

		[RelayCommand]
		private void ToggleShowcase()
		{
			ShowDesignShowcase = !ShowDesignShowcase;
		}

		public AboutViewModel()
		{
			Title = AppResources.SuleymaniyeVakfi;
			VersionNumber = " v" + AppInfo.VersionString + " ";
		}

		[RelayCommand]
		public async Task LinkButtonClicked(string url)
		{
			await Launcher.OpenAsync(url).ConfigureAwait(false);
		}

		[RelayCommand]
		public async Task Settings()
		{
			IsBusy = true;
			// This will push the SettingsPage onto the navigation stack
			await Shell.Current.GoToAsync($"{nameof(SettingsPage)}").ConfigureAwait(false);
			IsBusy = false;
		}
	}
}
