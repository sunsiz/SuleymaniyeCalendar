using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Maui.Core;
using SuleymaniyeCalendar.Resources.Strings;
using SuleymaniyeCalendar.Services;

namespace SuleymaniyeCalendar.ViewModels
{
	public partial class RadioViewModel : BaseViewModel
	{
		private readonly IRadioService _radioService;

		private bool isPlaying;
		public bool IsPlaying { get => isPlaying; set => SetProperty(ref isPlaying, value); }

        public Command TapCommand => new Command<string>(async (url) => await Launcher.OpenAsync(url).ConfigureAwait(false));

		public RadioViewModel(IRadioService radioService)
		{
			_radioService = radioService;
			IsBusy = true;
			Title = AppResources.IcerikYukleniyor;
			
			// Subscribe to radio service events
			_radioService.PlaybackStateChanged += OnPlaybackStateChanged;
			_radioService.LoadingStateChanged += OnLoadingStateChanged;
			_radioService.TitleChanged += OnTitleChanged;
			
			Title = AppResources.FitratinSesi;
			_ = CheckInternetAsync();
			IsBusy = false;
		}

		[RelayCommand]
		private async Task Play()
		{
			if (await CheckInternetAsync().ConfigureAwait(false))
			{
				if (IsPlaying)
				{
					await _radioService.PauseAsync().ConfigureAwait(false);
				}
				else
				{
					await _radioService.PlayAsync().ConfigureAwait(false);
				}
			}
			else
			{
				await _radioService.StopAsync().ConfigureAwait(false);
			}
		}

		private void OnPlaybackStateChanged(object sender, bool isPlaying)
		{
			MainThread.InvokeOnMainThreadAsync(() =>
			{
				IsPlaying = isPlaying;
			});
		}

		private void OnLoadingStateChanged(object sender, bool isLoading)
		{
			MainThread.InvokeOnMainThreadAsync(() =>
			{
				IsBusy = isLoading;
			});
		}

		private void OnTitleChanged(object sender, string title)
		{
			MainThread.InvokeOnMainThreadAsync(() =>
			{
				Title = title;
			});
		}

		private static Task<bool> CheckInternetAsync()
		{
			var current = Connectivity.NetworkAccess;
			if (current != NetworkAccess.Internet)
			{
				// Enhanced network feedback with contextual guidance
				var message = $"{AppResources.RadyoIcinInternet} 📶";
				
				MainThread.BeginInvokeOnMainThread(() =>
				{
					CancellationTokenSource cancellationTokenSource = new();
					var toast = Toast.Make(message, ToastDuration.Long, 16);
					toast.Show(cancellationTokenSource.Token);
				});
				return Task.FromResult(false);
			}

			return Task.FromResult(true);
		}

		public IRadioService GetRadioService() => _radioService;
	}
}
