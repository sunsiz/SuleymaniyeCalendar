using System.Diagnostics;
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
		
		// Set initial FlowDirection from saved language preference
		var savedLanguage = Preferences.Get("SelectedLanguage", "tr");
		this.FlowDirection = _rtlService.GetFlowDirection(savedLanguage);
		Debug.WriteLine($"📱 MainPage: Constructor, language={savedLanguage}, FlowDirection={this.FlowDirection}");
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		
		var selectedLanguage = Preferences.Get("SelectedLanguage", "tr");
		
		// Get expected FlowDirection BEFORE any other operations
		var expectedDirection = _rtlService.GetFlowDirection(selectedLanguage);
		Debug.WriteLine($"📱 MainPage.OnAppearing: language={selectedLanguage}, expected={expectedDirection}, current={this.FlowDirection}");

		// Optimize font loading - only update if actually changed
		var currentFontSize = Application.Current?.Resources.TryGetValue("DefaultFontSize", out var existingSize) == true 
			? Convert.ToDouble(existingSize) : 14.0;
		var preferredFontSize = Preferences.Get("FontSize", 14);
		
		if (Math.Abs(currentFontSize - preferredFontSize) > 0.1)
		{
			if (Application.Current != null)
				Application.Current.Resources["DefaultFontSize"] = preferredFontSize;
		}
		
		// Optimize culture setting - only update if actually changed
		// Use TwoLetterISOLanguageName for comparison to avoid 'tr-TR' vs 'tr' mismatch
		if (CultureInfo.CurrentCulture.TwoLetterISOLanguageName != selectedLanguage)
		{
			var ci = new CultureInfo(selectedLanguage);

			// Update both systems: XAML translations and resx-backed C# strings
			_resourceManager.CurrentCulture = ci;
			AppResources.Culture = ci;
			CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = ci;
		}
		
		// Apply RTL flow direction - set directly on this page to avoid Shell caching issues
		// Don't rely on DynamicResource as Shell can cache the old value
		if (this.FlowDirection != expectedDirection)
		{
			Debug.WriteLine($"📱 MainPage.OnAppearing: Updating FlowDirection from {this.FlowDirection} to {expectedDirection}");
			this.FlowDirection = expectedDirection;
		}
		
		// Also set on the Content element for full propagation
		if (this.Content is VisualElement content && content.FlowDirection != expectedDirection)
		{
			Debug.WriteLine($"📱 MainPage.OnAppearing: Updating Content.FlowDirection from {content.FlowDirection} to {expectedDirection}");
			content.FlowDirection = expectedDirection;
		}
		
		// Also update the global resource for other pages
		_rtlService.ApplyFlowDirection(selectedLanguage);
		
		Debug.WriteLine($"📱 MainPage.OnAppearing: After ApplyFlowDirection, FlowDirection={this.FlowDirection}");
		
		// Use dispatcher to ensure FlowDirection is set after layout pass
		// This is a workaround for iOS Shell caching issues
		var direction = expectedDirection;
		Dispatcher.Dispatch(() =>
		{
			if (this.FlowDirection != direction)
			{
				Debug.WriteLine($"📱 MainPage.OnAppearing.Dispatch: Forcing FlowDirection from {this.FlowDirection} to {direction}");
				this.FlowDirection = direction;
			}
			
			// Also force Content FlowDirection
			if (this.Content is VisualElement contentElement && contentElement.FlowDirection != direction)
			{
				Debug.WriteLine($"📱 MainPage.OnAppearing.Dispatch: Forcing Content.FlowDirection from {contentElement.FlowDirection} to {direction}");
				contentElement.FlowDirection = direction;
			}
			
			// Force CollectionView to update FlowDirection
			// iOS CollectionView doesn't always propagate FlowDirection to its items
			if (PrayersView != null && PrayersView.FlowDirection != direction)
			{
				Debug.WriteLine($"📱 MainPage.OnAppearing.Dispatch: Forcing PrayersView.FlowDirection from {PrayersView.FlowDirection} to {direction}");
				PrayersView.FlowDirection = direction;
				
				// Force CollectionView to refresh its visible items by toggling ItemsSource
				// This is needed because iOS doesn't re-layout items when FlowDirection changes
				var currentSource = PrayersView.ItemsSource;
				if (currentSource != null)
				{
					Debug.WriteLine($"📱 MainPage.OnAppearing.Dispatch: Refreshing PrayersView items for RTL update");
					PrayersView.ItemsSource = null;
					PrayersView.ItemsSource = currentSource;
				}
			}
		});

		_viewModel.OnAppearing(); // single refresh path
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_viewModel?.OnDisappearing();
		// Safety: clear any lingering pull-to-refresh spinner when leaving the page
		if (_viewModel?.IsRefreshing == true)
		{
			Application.Current?.Dispatcher.Dispatch(() => _viewModel.IsRefreshing = false);
		}
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

