using LocalizationResourceManager.Maui;
using SuleymaniyeCalendar.Resources.Strings;
using SuleymaniyeCalendar.ViewModels;

namespace SuleymaniyeCalendar.Views;

public partial class AboutPage : ContentPage
{
	public AboutPage(AboutViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		Title = AppResources.SuleymaniyeVakfi;
	}
}