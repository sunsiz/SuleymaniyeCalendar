using SuleymaniyeCalendar.ViewModels;

namespace SuleymaniyeCalendar.Views;

public partial class PrayerDetailPage : ContentPage
{
	private readonly PrayerDetailViewModel _viewModel;
	private bool _hasAppeared;

	public PrayerDetailPage(PrayerDetailViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		
		// On first appearance, just mark as appeared
		// On subsequent appearances (e.g., returning from permission settings), notify ViewModel
		if (_hasAppeared)
		{
			await _viewModel.OnPageResumedAsync();
		}
		_hasAppeared = true;
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		// Ensure scheduling happens once (no overlay for system back to avoid duplicate flashes)
		_viewModel.EnsureScheduled();
	}
}