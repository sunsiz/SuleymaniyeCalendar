using System.Globalization;
using LocalizationResourceManager.Maui;
using SuleymaniyeCalendar.ViewModels;

namespace SuleymaniyeCalendar.Views;

public partial class MainPage : ContentPage
{
	private readonly ILocalizationResourceManager _resourceManager;
	private readonly MainViewModel _viewModel;

	public MainPage(MainViewModel viewModel, ILocalizationResourceManager resourceManager)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		_resourceManager= resourceManager;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		if (Application.Current != null)
			Application.Current.Resources["DefaultFontSize"] = Preferences.Get("FontSize", 14);
		_resourceManager.CurrentCulture = new CultureInfo(Preferences.Get("SelectedLanguage", "en"));
		_viewModel.OnAppearing();
	}
}

