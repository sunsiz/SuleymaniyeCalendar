using System.Globalization;
#nullable enable
using CommunityToolkit.Maui.Alerts;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using SuleymaniyeCalendar.Models;
using SuleymaniyeCalendar.Resources.Strings;
using SuleymaniyeCalendar.ViewModels;
using SuleymaniyeCalendar.Services;

namespace SuleymaniyeCalendar;

public partial class App : Application
{
    public App()
    {
        // Localization init
        var language = Preferences.Get("SelectedLanguage", "tr");
        AppResources.Culture = new CultureInfo(language);

        InitializeComponent();

        // Initialize complete font scaling system at app startup
        BaseViewModel.InitializeFontSize();
        // Apply selected color palette (default or MTB overrides)
        ThemePaletteManager.ApplyCurrentPalette();
        // Do not set MainPage here (deprecated in .NET 9)
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Set the root here, recommended in .NET 9
        return new Window(new AppShell());
    }

    protected override void OnStart()
    {
        Microsoft.Maui.ApplicationModel.VersionTracking.Track();
        ApplyTheme();
    }

    protected override void OnResume()
    {
        ApplyTheme();
        
        // 🔄 PHASE 18: Recalculate prayer states when app returns from background
        // This ensures correct "current prayer" display when system time changes
        // Note: Does NOT re-fetch from server (location changes require manual refresh)
        try
        {
            if (Shell.Current?.CurrentPage?.BindingContext is MainViewModel mainViewModel)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    // Just recalculate states from cached data, don't fetch from server
                    mainViewModel.OnAppearing();
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnResume refresh error: {ex}");
        }
    }

    void ApplyTheme()
    {
        // 0 = Dark, 1 = Light, 2 = System
        var app = Application.Current;
        if (app is null)
            return;

        app.UserAppTheme = Theme.Tema switch
        {
            0 => AppTheme.Dark,
            1 => AppTheme.Light,
            2 => AppTheme.Unspecified,
            _ => AppTheme.Unspecified
        };
        // Initialize complete font scaling system
        BaseViewModel.InitializeFontSize();
    }
}
#nullable disable
