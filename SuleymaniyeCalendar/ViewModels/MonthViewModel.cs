using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SuleymaniyeCalendar.Models;
using SuleymaniyeCalendar.Resources.Strings;
using SuleymaniyeCalendar.Services;

namespace SuleymaniyeCalendar.ViewModels
{
	public partial class MonthViewModel:BaseViewModel
	{
		private DataService _data;

		[ObservableProperty] public ObservableCollection<Calendar> monthlyCalendar;
		public MonthViewModel(DataService dataService)
		{
			IsBusy = true;
			Title = AppResources.AylikTakvim;
			_data = dataService;
			var place = _data.calendar;
			var location = new Location()
				{ Latitude = place.Latitude, Longitude = place.Longitude, Altitude = place.Altitude };
			if (location.Latitude != 0.0 && location.Longitude != 0.0)
			{
				monthlyCalendar = _data.GetMonthlyPrayerTimes(location, false);
				if (monthlyCalendar == null)
				{
					Alert(AppResources.TakvimIcinInternet, AppResources.TakvimIcinInternetBaslik);
					return;
				}
			}
			else
				ShowToast(AppResources.KonumIzniIcerik);
			IsBusy = false;
		}

		[RelayCommand]
		private async Task Refresh()
		{
			Location location;
			//using (UserDialogs.Instance.Loading(AppResources.Yenileniyor))
			//{
			//var data = new DataService();
			location = await _data.GetCurrentLocationAsync(true).ConfigureAwait(false);
			if (location != null && location.Latitude != 0 && location.Longitude != 0)
				MonthlyCalendar = _data.GetMonthlyPrayerTimes(location, true);
			if (MonthlyCalendar == null)
				Alert(AppResources.TakvimIcinInternet, AppResources.TakvimIcinInternetBaslik);
			else ShowToast(AppResources.AylikTakvimYenilendi);
			//}
		}

		[RelayCommand]
		private async Task GoBack()
		{
			await Shell.Current.GoToAsync("..").ConfigureAwait(false);
		}
	}
}
