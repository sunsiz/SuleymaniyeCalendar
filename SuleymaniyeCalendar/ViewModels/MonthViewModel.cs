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
	public partial class MonthViewModel : BaseViewModel
	{
		private DataService _data;
        private readonly PerformanceService _perf;

		private ObservableCollection<Calendar> monthlyCalendar = new();
		public ObservableCollection<Calendar> MonthlyCalendar
		{
			get => monthlyCalendar;
			set => SetProperty(ref monthlyCalendar, value);
		}
		
		public bool HasData => MonthlyCalendar?.Count > 0;
        public bool ShowShare => Preferences.Get("LastLatitude", 0.0) != 0.0 && Preferences.Get("LastLongitude", 0.0) != 0.0;

		public MonthViewModel(DataService dataService, PerformanceService perf = null)
		{
			Title = AppResources.AylikTakvim;
			_data = dataService;
            _perf = perf ?? new PerformanceService();
			MonthlyCalendar = new ObservableCollection<Calendar>();
			IsBusy = false; // Start with false to show window immediately
		}

		/// <summary>
		/// Initialize with instant UI - loads data after a delay to show empty page first
		/// </summary>
		public async Task InitializeWithDelayAsync()
		{
			if (MonthlyCalendar?.Count > 0)
			{
				return; // Already loaded
			}

			// Show empty UI first for instant appearance
			await MainThread.InvokeOnMainThreadAsync(() => 
			{
				// UI is already shown with empty collection
			});
			
			// Small delay to let UI render first
			await Task.Delay(100);
			
			// Then start loading with indicator
			await MainThread.InvokeOnMainThreadAsync(() => 
			{
				IsBusy = true;
			});
			
			// Small additional delay for smooth UX
			await Task.Delay(500);
			
			using (_perf.StartTimer("Month.LoadData.Delayed"))
			{
				await LoadMonthlyDataAsync().ConfigureAwait(false);
			}
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
			
			using (_perf.StartTimer("Month.LoadData"))
			{
				await LoadMonthlyDataAsync().ConfigureAwait(false);
			}
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
					ObservableCollection<Calendar> monthlyData;
					using (_perf.StartTimer("Month.HybridMonthly"))
					{
						monthlyData = await _data.GetMonthlyPrayerTimesHybridAsync(location, false).ConfigureAwait(false);
					}
					
					if (monthlyData == null)
					{
						await MainThread.InvokeOnMainThreadAsync(() => 
						{
							Alert(AppResources.TakvimIcinInternet, AppResources.TakvimIcinInternetBaslik);
						});
						return;
					}
					
					// Performance optimized update: Use ReplaceRange if available, otherwise batch clear/add
					await MainThread.InvokeOnMainThreadAsync(async () => 
					{
						// Performance optimization: Use collection replacement for better UI responsiveness
						if (monthlyData.Count <= 10)
						{
							// Small datasets: Direct replacement for instant display
							using (_perf.StartTimer("Month.UI.ReplaceSmall")) MonthlyCalendar.Clear();
							foreach (var item in monthlyData)
								MonthlyCalendar.Add(item);
						}
						else
						{
							// Large datasets: Smooth progressive loading to prevent UI blocking
							using (_perf.StartTimer("Month.UI.ReplaceLarge.Clear")) MonthlyCalendar.Clear();
							const int batchSize = 8; // Optimized batch size for better perceived performance
							
							for (int i = 0; i < monthlyData.Count; i += batchSize)
							{
								var batch = monthlyData.Skip(i).Take(batchSize);
								foreach (var item in batch)
								{
									MonthlyCalendar.Add(item);
								}
								
								// Micro-delay for UI thread breathing room on large datasets
								if (i > 0 && monthlyData.Count > 50)
									await Task.Delay(1);
							}
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
			catch (Exception ex)
			{
				await MainThread.InvokeOnMainThreadAsync(() => 
				{
					Alert($"Error: {ex.Message}", "Error");
				});
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
				// Avoid permission prompt on first load; refresh only on user action
				Location location = await _data.GetCurrentLocationAsync(false).ConfigureAwait(false);
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
