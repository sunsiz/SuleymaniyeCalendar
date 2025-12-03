using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SuleymaniyeCalendar.ViewModels;

namespace SuleymaniyeCalendar.Views;

public partial class CompassPage : ContentPage
{
	private CompassViewModel _viewModel;
	
	public CompassPage(CompassViewModel viewModel)
	{
		InitializeComponent();
		_viewModel = viewModel;
		BindingContext = viewModel;
	}

	protected override async void OnAppearing()
	{
		try
		{
			base.OnAppearing();
			
			// Restart compass sensor if it was stopped
			_viewModel.StartCompass();
			
			// Refresh location data from current app state when page appears
			await _viewModel.RefreshLocationFromAppAsync();
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"CompassPage.OnAppearing error: {ex.Message}");
		}
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		// Stop compass to save battery, will restart in OnAppearing
		_viewModel?.StopCompass();
	}
}