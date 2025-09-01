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
		base.OnAppearing();
		
		// Refresh location data from current app state when page appears
		await _viewModel.RefreshLocationFromAppAsync();
	}
}