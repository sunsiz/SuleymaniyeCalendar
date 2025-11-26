using System.Globalization;
using SuleymaniyeCalendar.Models;
using SuleymaniyeCalendar.Resources.Strings;
using SuleymaniyeCalendar.ViewModels;

namespace SuleymaniyeCalendar;

/// <summary>
/// Application entry point and lifecycle management.
/// Handles theme initialization, localization, and app state transitions.
/// </summary>
public partial class App : Application
{
    public App()
    {
        // Initialize localization before components load
        var language = Preferences.Get("SelectedLanguage", "tr");
        AppResources.Culture = new CultureInfo(language);

        InitializeComponent();

        // Initialize font scaling system at startup
        BaseViewModel.InitializeFontSize();
    }

    protected override Window CreateWindow(IActivationState activationState)
    {
        return new Window(new AppShell());
    }

    protected override void OnStart()
    {
        base.OnStart();

#if __IOS__
        // Initialize iOS notification permissions
        _ = Platforms.iOS.NotificationService.InitializeNotificationsAsync();
#endif
        Microsoft.Maui.ApplicationModel.VersionTracking.Track();
        ApplyTheme();
    }

    protected override void OnResume()
    {
        ApplyTheme();
        RefreshMainViewPrayerStates();
    }

    /// <summary>
    /// Applies the user's selected theme preference.
    /// </summary>
    private void ApplyTheme()
    {
        if (Application.Current is null) return;

        Application.Current.UserAppTheme = Theme.Tema switch
        {
            0 => AppTheme.Dark,
            1 => AppTheme.Light,
            _ => AppTheme.Unspecified // System default
        };

        BaseViewModel.InitializeFontSize();
    }

    /// <summary>
    /// Recalculates prayer states when app returns from background.
    /// Ensures correct "current prayer" display after system time changes.
    /// </summary>
    private static void RefreshMainViewPrayerStates()
    {
        try
        {
            if (Shell.Current?.CurrentPage?.BindingContext is MainViewModel mainViewModel)
            {
                MainThread.BeginInvokeOnMainThread(() => mainViewModel.OnAppearing());
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnResume refresh error: {ex.Message}");
        }
    }
}
