using System.Globalization;
using LocalizationResourceManager.Maui;
using SuleymaniyeCalendar.ViewModels;
using SuleymaniyeCalendar.Resources.Strings; // add
using SuleymaniyeCalendar.Services;

namespace SuleymaniyeCalendar.Views;

public partial class MainPage : ContentPage
{
	private readonly ILocalizationResourceManager _resourceManager;
	private readonly MainViewModel _viewModel;
	private readonly IRtlService _rtlService;

	public MainPage(MainViewModel viewModel, ILocalizationResourceManager resourceManager, IRtlService rtlService)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		_resourceManager = resourceManager;
		_rtlService = rtlService;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();

		if (Application.Current != null)
			Application.Current.Resources["DefaultFontSize"] = Preferences.Get("FontSize", 14);

		var selectedLanguage = Preferences.Get("SelectedLanguage", "tr");
		var ci = new CultureInfo(selectedLanguage);

		// Update both systems: XAML translations and resx-backed C# strings
		_resourceManager.CurrentCulture = ci;
		AppResources.Culture = ci;
		CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = ci;

		// Apply RTL layout direction
		_rtlService.ApplyFlowDirection(selectedLanguage);

		_viewModel.OnAppearing(); // single refresh path
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_viewModel?.OnDisappearing();
	}

	// NOTE: removed the _viewModel.OnAppearing() call here to avoid double refresh.
	// If you still need to re-apply culture here, do so without calling the VM.
	// protected override void OnNavigatedTo(NavigatedToEventArgs args)
	// {
	// 	base.OnNavigatedTo(args);
	// 	var ci = new CultureInfo(Preferences.Get("SelectedLanguage", "en"));
	// 	_resourceManager.CurrentCulture = ci;
	// 	AppResources.Culture = ci;
	// 	CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = ci;
	// }
}

