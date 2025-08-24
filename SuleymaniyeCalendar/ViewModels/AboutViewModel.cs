using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SuleymaniyeCalendar.Resources.Strings;
using SuleymaniyeCalendar.Views;

namespace SuleymaniyeCalendar.ViewModels
{
	public partial class AboutViewModel:BaseViewModel
	{
		[RelayCommand]
		public async Task LinkButtonClicked(string url)
		{
			await Launcher.OpenAsync(url).ConfigureAwait(false);
			
		}

		[ObservableProperty]private string _versionNumber;
		public bool ShowButtons { get { if (DeviceInfo.Platform == DevicePlatform.iOS) { return IsVoiceOverRunning(); } else return false; } }

		public AboutViewModel()
		{
			Title = AppResources.SuleymaniyeVakfi;
			VersionNumber = " v" + AppInfo.VersionString + " ";
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
