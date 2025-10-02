using System;
using System.Globalization;
using LocalizationResourceManager.Maui;
using SuleymaniyeCalendar.Resources.Strings;
using SuleymaniyeCalendar.ViewModels;

namespace SuleymaniyeCalendar.Views;

public partial class AboutPage : ContentPage
{
    private AppTheme _originalTheme;

	public AboutPage(AboutViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
        _originalTheme = Application.Current?.UserAppTheme ?? AppTheme.Unspecified;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		Title = AppResources.SuleymaniyeVakfi;
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		if (Application.Current != null)
		{
			Application.Current.UserAppTheme = _originalTheme;
		}
	}

	// Event handlers wired in XAML for interactive preview
	private void OnThemeToggleToggled(object sender, ToggledEventArgs e)
	{
		if (Application.Current == null) return;
		Application.Current.UserAppTheme = e.Value ? AppTheme.Dark : AppTheme.Light;
	}

	private void OnElevationSliderValueChanged(object sender, ValueChangedEventArgs e)
	{
		// Names from XAML: ElevationPreviewCard, ElevationValueLabel
		if (ElevationPreviewCard == null) return;
		var radius = Math.Round(e.NewValue, 0);
		var offsetY = Math.Max(1, Math.Round(e.NewValue / 2.6));
		var opacity = Math.Min(0.50, 0.18 + (e.NewValue / 120));

		ElevationPreviewCard.Shadow = new Shadow
		{
			Radius = (float)radius,
			Offset = new Point(0, offsetY),
			Opacity = (float)opacity,
			Brush = (Brush)Application.Current.Resources["StrongShadowOverlayBrush"]
		};

		if (ElevationValueLabel != null)
		{
			ElevationValueLabel.Text = $"Radius: {radius} • Offset: {offsetY} • Opacity: {opacity:F2}";
		}
	}
	
}