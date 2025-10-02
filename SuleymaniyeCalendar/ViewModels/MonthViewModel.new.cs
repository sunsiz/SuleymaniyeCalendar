using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SuleymaniyeCalendar.Models;
using SuleymaniyeCalendar.Resources.Strings;
using SuleymaniyeCalendar.Services;

namespace SuleymaniyeCalendar.ViewModels
{
	// Optimized Month ViewModel (cache-first + background refresh). Experimental parallel to original.
	public partial class MonthViewModel_Optimized : BaseViewModel
	{
		private readonly DataService _data;
		private readonly PerformanceService _perf;
		public ObservableCollection<Calendar> MonthlyCalendar { get; private set; } = new();
		public bool HasData => MonthlyCalendar?.Count > 0;
		public bool ShowShare => Preferences.Get("LastLatitude", 0.0) != 0.0 && Preferences.Get("LastLongitude", 0.0) != 0.0;

		public MonthViewModel_Optimized(DataService dataService, PerformanceService perf = null)
		{
			Title = AppResources.AylikTakvim;
			_data = dataService;
			_perf = perf ?? new PerformanceService();
		}

		public async Task InitializeAsync()
		{
			if (MonthlyCalendar.Count > 0) return;
			IsBusy = true;
			await LoadMonthlyDataAsync();
		}

		private async Task LoadMonthlyDataAsync()
		{
			try
			{
				var place = _data.calendar;
				var location = new Location { Latitude = place.Latitude, Longitude = place.Longitude, Altitude = place.Altitude };
				if (location.Latitude == 0 || location.Longitude == 0)
				{
					ShowToast(AppResources.KonumIzniIcerik);
					return;
				}

				var cached = await _data.GetMonthlyFromCacheOrEmptyAsync(location).ConfigureAwait(false);
				await MainThread.InvokeOnMainThreadAsync(() =>
				{
					using (_perf.StartTimer("Month.UI.AssignItemsSource.CacheInitial"))
					{
						MonthlyCalendar = new ObservableCollection<Calendar>(cached.Take(10));
						OnPropertyChanged(nameof(HasData));
					}
				});

				if (cached.Count > 10)
				{
					Application.Current?.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(60), () =>
					{
						using (_perf.StartTimer("Month.UI.AssignItemsSource.CacheStage2"))
						{
							foreach (var cal in cached.Skip(10).Take(10)) MonthlyCalendar.Add(cal);
						}
						if (cached.Count > 20)
						{
							Application.Current?.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(110), () =>
							{
								using (_perf.StartTimer("Month.UI.AssignItemsSource.CacheStage3"))
								{
									for (int i = 20; i < cached.Count; i++) MonthlyCalendar.Add(cached[i]);
								}
							});
						}
					});
				}

				_ = Task.Run(async () => await FetchAndUpdateMonthlyAsync(location, cached.Count));
			}
			finally
			{
				IsBusy = false;
			}
		}

		private async Task FetchAndUpdateMonthlyAsync(Location location, int previousCount)
		{
			try
			{
				ObservableCollection<Calendar> fresh;
				using (_perf.StartTimer("Month.HybridMonthly"))
				{
					fresh = await _data.GetMonthlyPrayerTimesHybridAsync(location, false).ConfigureAwait(false);
				}
				if (fresh == null || fresh.Count == 0 || fresh.Count <= previousCount) return;
				await MainThread.InvokeOnMainThreadAsync(() =>
				{
					using (_perf.StartTimer("Month.UI.AssignItemsSource.Stage1"))
					{
						MonthlyCalendar = new ObservableCollection<Calendar>(fresh.Take(10));
						OnPropertyChanged(nameof(HasData));
					}
					Application.Current?.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(70), () =>
					{
						using (_perf.StartTimer("Month.UI.AssignItemsSource.Stage2"))
						{
							foreach (var c in fresh.Skip(10).Take(10)) MonthlyCalendar.Add(c);
						}
						Application.Current?.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(120), () =>
						{
							using (_perf.StartTimer("Month.UI.AssignItemsSource.Stage3"))
							{
								for (int i = 20; i < fresh.Count; i++) MonthlyCalendar.Add(fresh[i]);
							}
						});
					});
				});
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"FetchAndUpdateMonthlyAsync error(opt): {ex.Message}");
			}
		}

		[RelayCommand]
		private async Task Refresh()
		{
			IsBusy = true;
			try
			{
				var location = await _data.GetCurrentLocationAsync(false).ConfigureAwait(false);
				if (location == null || location.Latitude == 0 || location.Longitude == 0)
				{
					ShowToast(AppResources.KonumIzniIcerik);
					return;
				}
				var fresh = await _data.GetMonthlyPrayerTimesHybridAsync(location, true).ConfigureAwait(false);
				if (fresh == null)
				{
					Alert(AppResources.TakvimIcinInternet, AppResources.TakvimIcinInternetBaslik);
					return;
				}
				await MainThread.InvokeOnMainThreadAsync(() =>
				{
					using (_perf.StartTimer("Month.UI.AssignItemsSource.RefreshFull"))
					{
						MonthlyCalendar = new ObservableCollection<Calendar>(fresh);
						OnPropertyChanged(nameof(HasData));
					}
					ShowToast(AppResources.AylikTakvimYenilendi);
				});
			}
			finally
			{
				IsBusy = false;
			}
		}

		[RelayCommand]
		private Task GoBack() => Shell.Current.GoToAsync("..");

		[RelayCommand]
		private async Task Share()
		{
			var latitude = Preferences.Get("LastLatitude", 0.0);
			var longitude = Preferences.Get("LastLongitude", 0.0);
			var url = $"https://www.suleymaniyetakvimi.com/monthlyCalendar.html?latitude={latitude}&longitude={longitude}&monthId={DateTime.Today.Month}";
			await Launcher.OpenAsync(url).ConfigureAwait(false);
		}
	}
}