using System;
using System.Diagnostics;
using System.Globalization;
using LocalizationResourceManager.Maui;
using SuleymaniyeCalendar.Helpers;
using SuleymaniyeCalendar.Resources.Strings;
using SuleymaniyeCalendar.ViewModels;

namespace SuleymaniyeCalendar.Views;

public partial class AboutPage : ContentPage
{
	public AboutPage(AboutViewModel viewModel)
	{
		Debug.WriteLine("📱 AboutPage: Constructor started");
		try
		{
			InitializeComponent();
			Debug.WriteLine("📱 AboutPage: InitializeComponent completed");
			BindingContext = viewModel;
			Debug.WriteLine("📱 AboutPage: BindingContext set");
			
			// Set initial FlowDirection from saved language preference
			var savedLanguage = Preferences.Get("SelectedLanguage", "tr");
			this.FlowDirection = AppConstants.IsRtlLanguage(savedLanguage) 
				? FlowDirection.RightToLeft 
				: FlowDirection.LeftToRight;
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"❌ AboutPage Constructor CRASH: {ex.GetType().Name}: {ex.Message}");
			Debug.WriteLine($"   StackTrace: {ex.StackTrace}");
			throw;
		}
	}

	protected override void OnAppearing()
	{
		Debug.WriteLine("📱 AboutPage: OnAppearing started");
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
			
			Title = AppResources.SuleymaniyeVakfi;
			Debug.WriteLine("📱 AboutPage: OnAppearing completed");
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"❌ AboutPage OnAppearing CRASH: {ex.GetType().Name}: {ex.Message}");
			throw;
		}
	}
}