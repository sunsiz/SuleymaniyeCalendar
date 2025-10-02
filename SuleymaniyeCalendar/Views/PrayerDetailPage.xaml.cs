using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SuleymaniyeCalendar.ViewModels;

namespace SuleymaniyeCalendar.Views;

public partial class PrayerDetailPage : ContentPage
{
	private PrayerDetailViewModel _viewModel;

	public PrayerDetailPage(PrayerDetailViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}
	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		// Ensure scheduling happens once (no overlay for system back to avoid duplicate flashes)
		_viewModel.EnsureScheduled();
	}
}