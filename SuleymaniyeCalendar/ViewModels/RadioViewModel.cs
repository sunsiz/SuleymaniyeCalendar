using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Maui.Core;
using SuleymaniyeCalendar.Resources.Strings;

namespace SuleymaniyeCalendar.ViewModels
{
	public partial class RadioViewModel : BaseViewModel
	{
		[ObservableProperty]
		private bool _isPlaying;

		public string RadioStreamUrl { get; } = "http://shaincast.caster.fm:22344/listen.mp3";

		public RadioViewModel()
		{
			IsBusy = true;
			Title = AppResources.IcerikYukleniyor;
			Title = AppResources.FitratinSesi;
			_ = CheckInternetAsync();
			IsBusy = false;
		}

		[RelayCommand]
		private async Task Play()
		{
			IsBusy = true;

			// Toggle play/pause; MediaElement action happens in the View
			if (await CheckInternetAsync().ConfigureAwait(false))
			{
				IsPlaying = !IsPlaying;
			}
			else
			{
				IsPlaying = false;
			}

			Title = AppResources.FitratinSesi;
			IsBusy = false;
		}

		private static async Task<bool> CheckInternetAsync()
		{
			var current = Connectivity.NetworkAccess;
			if (current != NetworkAccess.Internet)
			{
				CancellationTokenSource cancellationTokenSource = new();
				var toast = Toast.Make(AppResources.RadyoIcinInternet, ToastDuration.Short, 14);
				await toast.Show(cancellationTokenSource.Token);
				return false;
			}

			return true;
		}
	}
}
