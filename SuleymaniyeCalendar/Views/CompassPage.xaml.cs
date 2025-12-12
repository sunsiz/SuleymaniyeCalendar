using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SuleymaniyeCalendar.Helpers;
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
		
		// Set initial FlowDirection from saved language preference
		var savedLanguage = Preferences.Get("SelectedLanguage", "tr");
		this.FlowDirection = AppConstants.IsRtlLanguage(savedLanguage) 
			? FlowDirection.RightToLeft 
			: FlowDirection.LeftToRight;
	}

	protected override async void OnAppearing()
	{
		try
		{
			base.OnAppearing();
			
			// Update FlowDirection in case language changed while on another page
			var selectedLanguage = Preferences.Get("SelectedLanguage", "tr");
			var expectedDirection = AppConstants.IsRtlLanguage(selectedLanguage) 
				? FlowDirection.RightToLeft 
				: FlowDirection.LeftToRight;
			if (this.FlowDirection != expectedDirection)
			{
				this.FlowDirection = expectedDirection;
			}
			
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