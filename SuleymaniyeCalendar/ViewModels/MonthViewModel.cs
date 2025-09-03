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
        public bool ShowShare => Preferences.Get("LastLatitude", 0.0) != 0.0 && Preferences.Get("LastLongitude", 0.0) != 0.0;

        public MonthViewModel(DataService dataService)
		{
			Title = AppResources.AylikTakvim;
			_data = dataService;
			MonthlyCalendar = new ObservableCollection<Calendar>();
			IsBusy = false; // Start with false to show window immediately
		}

		public async Task InitializeAsync()
		{
			if (MonthlyCalendar?.Count > 0)
			{
				return; // Already loaded
			}

			// Set busy state immediately when loading starts
			await MainThread.InvokeOnMainThreadAsync(() => 
			{
				IsBusy = true;
			});
			
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
					// Use new hybrid API approach: JSON first, XML fallback
					var monthlyData = await _data.GetMonthlyPrayerTimesHybridAsync(location, false).ConfigureAwait(false);
					
					if (monthlyData == null)
					{
						await MainThread.InvokeOnMainThreadAsync(() => 
						{
							Alert(AppResources.TakvimIcinInternet, AppResources.TakvimIcinInternetBaslik);
						});
						return;
					}
					
					// Batch update for better performance
					await MainThread.InvokeOnMainThreadAsync(() => 
					{
						MonthlyCalendar.Clear();
						
						// Add all items at once for better performance
						foreach (var item in monthlyData)
						{
							MonthlyCalendar.Add(item);
						}
						
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
				await MainThread.InvokeOnMainThreadAsync(() => 
				{
					IsBusy = false;
				});
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
					// Use new hybrid API approach with force refresh
					var monthlyData = await _data.GetMonthlyPrayerTimesHybridAsync(location, true).ConfigureAwait(false);
					
					if (monthlyData == null)
					{
						await MainThread.InvokeOnMainThreadAsync(() => 
						{
							Alert(AppResources.TakvimIcinInternet, AppResources.TakvimIcinInternetBaslik);
						});
					}
					else
					{
						// Batch update for better performance
						await MainThread.InvokeOnMainThreadAsync(() => 
						{
							MonthlyCalendar.Clear();
							
							// Add all items at once for better performance
							foreach (var item in monthlyData)
							{
								MonthlyCalendar.Add(item);
							}
							
							OnPropertyChanged(nameof(HasData));
							ShowToast(AppResources.AylikTakvimYenilendi);
						});
					}
				}
			}
			finally
			{
				await MainThread.InvokeOnMainThreadAsync(() => 
				{
					IsBusy = false;
				});
			}
		}

		[RelayCommand]
		private async Task GoBack()
		{
			await Shell.Current.GoToAsync("..").ConfigureAwait(false);
		}

        [RelayCommand]
        private async Task Share()
        {
            var latitude = Preferences.Get("LastLatitude", 0.0);
            var longitude = Preferences.Get("LastLongitude", 0.0);
            var url =
                $"https://www.suleymaniyetakvimi.com/monthlyCalendar.html?latitude={latitude}&longitude={longitude}&monthId={DateTime.Today.Month}";
            await Launcher.OpenAsync(url).ConfigureAwait(false);
        }
	}
}
