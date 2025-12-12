using System.Diagnostics;
using SuleymaniyeCalendar.ViewModels;

namespace SuleymaniyeCalendar.Views;

public partial class PrayerDetailPage : ContentPage
{
	private readonly PrayerDetailViewModel _viewModel;
	private bool _hasAppeared;

	public PrayerDetailPage(PrayerDetailViewModel viewModel)
	{
		Debug.WriteLine("📱 PrayerDetailPage: Constructor started");
		try
		{
			InitializeComponent();
			Debug.WriteLine("📱 PrayerDetailPage: InitializeComponent completed");
			BindingContext = _viewModel = viewModel;
			Debug.WriteLine("📱 PrayerDetailPage: BindingContext set");
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"❌ PrayerDetailPage Constructor CRASH: {ex.GetType().Name}: {ex.Message}");
			Debug.WriteLine($"   StackTrace: {ex.StackTrace}");
			throw;
		}
	}

	protected override async void OnAppearing()
	{
		Debug.WriteLine("📱 PrayerDetailPage: OnAppearing started");
		try
		{
			base.OnAppearing();
			
			// Reload sounds to ensure localized names are current
			// This handles the case when user changed language and returns to this page
			_viewModel.ReloadSounds();
			
			// On first appearance, just mark as appeared
			// On subsequent appearances (e.g., returning from permission settings), notify ViewModel
			if (_hasAppeared)
			{
				await _viewModel.OnPageResumedAsync();
			}
			_hasAppeared = true;
			Debug.WriteLine("📱 PrayerDetailPage: OnAppearing completed");
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"❌ PrayerDetailPage OnAppearing CRASH: {ex.GetType().Name}: {ex.Message}");
			throw;
		}
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		// Ensure scheduling happens once (no overlay for system back to avoid duplicate flashes)
		_viewModel.EnsureScheduled();
	}
}