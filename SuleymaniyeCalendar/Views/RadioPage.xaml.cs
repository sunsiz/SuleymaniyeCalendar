using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SuleymaniyeCalendar.ViewModels;

namespace SuleymaniyeCalendar.Views;

public partial class RadioPage : ContentPage
{
	private readonly RadioViewModel _viewModel;

	public RadioPage(RadioViewModel viewModel)
	{
		InitializeComponent();
		_viewModel = viewModel;
		BindingContext = _viewModel;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		
		// Connect the MediaElement to the radio service
		var radioService = _viewModel.GetRadioService();
		if (radioService is SuleymaniyeCalendar.Services.RadioService service)
		{
			service.SetMediaElement(RadioPlayer);
		}
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		
		// Radio continues playing in background - user must explicitly stop it
	}
}