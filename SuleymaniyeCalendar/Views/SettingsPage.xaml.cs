using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SuleymaniyeCalendar.ViewModels;

namespace SuleymaniyeCalendar.Views;

public partial class SettingsPage : ContentPage
{
	public SettingsPage(SettingsViewModel viewModel)
	{
		Debug.WriteLine("📱 SettingsPage: Constructor started");
		try
		{
			InitializeComponent();
			Debug.WriteLine("📱 SettingsPage: InitializeComponent completed");
			BindingContext = viewModel;
			Debug.WriteLine("📱 SettingsPage: BindingContext set");
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"❌ SettingsPage Constructor CRASH: {ex.GetType().Name}: {ex.Message}");
			Debug.WriteLine($"   StackTrace: {ex.StackTrace}");
			throw;
		}
	}

	protected override void OnAppearing()
	{
		Debug.WriteLine("📱 SettingsPage: OnAppearing started");
		try
		{
			base.OnAppearing();
			Debug.WriteLine("📱 SettingsPage: OnAppearing completed");
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"❌ SettingsPage OnAppearing CRASH: {ex.GetType().Name}: {ex.Message}");
			throw;
		}
	}

    private void OnLanguageCardTapped(object sender, EventArgs e)
    {
        // Show the language picker when the language card is tapped
        LanguagePicker.Focus();
    }
}