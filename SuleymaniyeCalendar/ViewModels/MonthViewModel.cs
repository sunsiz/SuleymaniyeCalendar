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
		
		public bool HasData => MonthlyCalendar?.Count > 0;
		
		public MonthViewModel(DataService dataService)
		{
			Title = AppResources.AylikTakvim;
			_data = dataService;
			MonthlyCalendar = new ObservableCollection<Calendar>();
			IsBusy = true;
		}

		public async Task InitializeAsync()
		{
			if (MonthlyCalendar?.Count > 0)
			{
				IsBusy = false;
				return;
			}
				
			await LoadMonthlyDataAsync().ConfigureAwait(false);
		}

		private async Task LoadMonthlyDataAsync()
		{
			try
			{
				var place = _data.calendar;
				var location = new Location()
					{ Latitude = place.Latitude, Longitude = place.Longitude, Altitude = place.Altitude };
				
				if (location.Latitude != 0.0 && location.Longitude != 0.0)
				{
					var monthlyData = await Task.Run(() => _data.GetMonthlyPrayerTimes(location, false)).ConfigureAwait(false);
					
					if (monthlyData == null)
					{
						await MainThread.InvokeOnMainThreadAsync(() => 
						{
							Alert(AppResources.TakvimIcinInternet, AppResources.TakvimIcinInternetBaslik);
						});
						return;
					}
					
					await MainThread.InvokeOnMainThreadAsync(() => 
					{
						MonthlyCalendar = new ObservableCollection<Calendar>(monthlyData);
						OnPropertyChanged(nameof(HasData));
					});
				}
				else
				{
					await MainThread.InvokeOnMainThreadAsync(() => 
					{
						ShowToast(AppResources.KonumIzniIcerik);
					});
				}
			}
			finally
			{
				IsBusy = false;
			}
		}

		[RelayCommand]
		private async Task Refresh()
		{
			IsBusy = true;
			
			try
			{
				Location location = await _data.GetCurrentLocationAsync(true).ConfigureAwait(false);
				if (location != null && location.Latitude != 0 && location.Longitude != 0)
				{
					var monthlyData = await Task.Run(() => _data.GetMonthlyPrayerTimes(location, true)).ConfigureAwait(false);
					
					if (monthlyData == null)
					{
						await MainThread.InvokeOnMainThreadAsync(() => 
						{
							Alert(AppResources.TakvimIcinInternet, AppResources.TakvimIcinInternetBaslik);
						});
					}
					else
					{
						await MainThread.InvokeOnMainThreadAsync(() => 
						{
							MonthlyCalendar = new ObservableCollection<Calendar>(monthlyData);
							OnPropertyChanged(nameof(HasData));
							ShowToast(AppResources.AylikTakvimYenilendi);
						});
					}
				}
			}
			finally
			{
				IsBusy = false;
			}
		}

		[RelayCommand]
		private async Task GoBack()
		{
			await Shell.Current.GoToAsync("..").ConfigureAwait(false);
		}
	}
}
