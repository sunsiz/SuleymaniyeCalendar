using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.Input;
using SuleymaniyeCalendar.Resources.Strings;
using SuleymaniyeCalendar.Services;

namespace SuleymaniyeCalendar.ViewModels;

/// <summary>
/// ViewModel for Süleymaniye radio streaming page.
/// Handles play/pause, displays track metadata, and manages audio state.
/// Implements IDisposable to properly unsubscribe from RadioService events.
/// </summary>
public partial class RadioViewModel : BaseViewModel, IDisposable
{
	private readonly IRadioService _radioService;
	private readonly PerformanceService _perf = new();

	#region Properties

	/// <summary>Whether audio is currently playing.</summary>
	private bool _isPlaying;
	public bool IsPlaying
	{
		get => _isPlaying;
		set => SetProperty(ref _isPlaying, value);
	}

	/// <summary>Command to open URL in browser (for social links).</summary>
	public Command TapCommand => new(async (url) => await Launcher.OpenAsync((string)url).ConfigureAwait(false));

	#endregion

	#region Constructor

	public RadioViewModel(IRadioService radioService)
	{
		_radioService = radioService;
		
		using (_perf.StartTimer("Radio.Constructor"))
		{
			Title = AppResources.IcerikYukleniyor;

			// Subscribe to radio service events (playback state for ViewModel sync only)
			_radioService.PlaybackStateChanged += OnPlaybackStateChanged;
			_radioService.TitleChanged += OnTitleChanged;
			// Note: Loading indicator is now bound directly to MediaElement.CurrentState in XAML (Microsoft best practice)

			// Sync initial state from service (in case radio was already playing)
			IsPlaying = _radioService.IsPlaying;
			
			Title = AppResources.FitratinSesi;
			_ = CheckInternetAsync();
		}
		
		// Log perf summary after delay
		Application.Current?.Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(1), () => _perf.LogSummary("RadioView"));
	}

	#endregion

	#region Commands

	/// <summary>
	/// Toggles radio playback (play/pause).
	/// Loading indicator is shown via XAML DataTrigger bound to MediaElement.CurrentState.
	/// </summary>
	[RelayCommand]
	private async Task Play()
	{
		if (await CheckInternetAsync().ConfigureAwait(false))
		{
			using (_perf.StartTimer("Radio.TogglePlay"))
			{
				if (IsPlaying)
				{
					await _radioService.PauseAsync().ConfigureAwait(false);
				}
				else
				{
					// Loading indicator now handled by XAML DataTrigger on MediaElement.CurrentState
					await _radioService.PlayAsync().ConfigureAwait(false);
				}
			}
		}
		else
		{
			await _radioService.StopAsync().ConfigureAwait(false);
		}
	}

	#endregion

	#region Event Handlers

	/// <summary>Handles playback state changes from RadioService.</summary>
	private void OnPlaybackStateChanged(object? sender, bool isPlaying)
	{
		_ = MainThread.InvokeOnMainThreadAsync(() => IsPlaying = isPlaying);
	}

	/// <summary>Handles track title/metadata changes.</summary>
	private void OnTitleChanged(object? sender, string title)
	{
		_ = MainThread.InvokeOnMainThreadAsync(() => Title = title);
	}

	#endregion

	#region Helpers

	/// <summary>
	/// Checks internet connectivity and shows toast if offline.
	/// </summary>
	private static Task<bool> CheckInternetAsync()
	{
		if (Connectivity.NetworkAccess != NetworkAccess.Internet)
		{
			ShowToast($"{AppResources.RadyoIcinInternet} 📶");
			return Task.FromResult(false);
		}
		return Task.FromResult(true);
	}

	/// <summary>Gets the underlying radio service (for page-level access).</summary>
	public IRadioService GetRadioService() => _radioService;

	/// <summary>
	/// Disposes resources and unsubscribes from RadioService events.
	/// Prevents memory leaks when navigating away from radio page.
	/// </summary>
	public void Dispose()
	{
		_radioService.PlaybackStateChanged -= OnPlaybackStateChanged;
		_radioService.TitleChanged -= OnTitleChanged;
		GC.SuppressFinalize(this);
	}

	#endregion
}
