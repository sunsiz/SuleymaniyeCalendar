using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SuleymaniyeCalendar.Helpers;
using SuleymaniyeCalendar.ViewModels;
using SuleymaniyeCalendar.Services;

namespace SuleymaniyeCalendar.Views;

public partial class RadioPage : ContentPage
{
	private readonly RadioViewModel _viewModel;
	private readonly PerformanceService _perf = new PerformanceService();

	public RadioPage(RadioViewModel viewModel)
	{
		InitializeComponent();
		_viewModel = viewModel;
		BindingContext = _viewModel;
		
		// Set initial FlowDirection from saved language preference
		var savedLanguage = Preferences.Get("SelectedLanguage", "tr");
		this.FlowDirection = AppConstants.IsRtlLanguage(savedLanguage) 
			? FlowDirection.RightToLeft 
			: FlowDirection.LeftToRight;
	}

	protected override void OnAppearing()
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
		
		using (_perf.StartTimer("Radio.OnAppearing"))
		{
			// Connect the MediaElement to the radio service
			var radioService = _viewModel.GetRadioService();
			if (radioService is SuleymaniyeCalendar.Services.RadioService service)
			{
				service.SetMediaElement(RadioPlayer);
			}
		}
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		
		// Radio continues playing in background - user must explicitly stop it
	}
}