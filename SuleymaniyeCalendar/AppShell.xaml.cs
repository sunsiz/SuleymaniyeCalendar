using SuleymaniyeCalendar.Models;
using SuleymaniyeCalendar.Views;

namespace SuleymaniyeCalendar;

/// <summary>
/// Shell navigation configuration and route registration.
/// </summary>
public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Register routes for non-tab pages (navigated to via GoToAsync)
        Routing.RegisterRoute(nameof(PrayerDetailPage), typeof(PrayerDetailPage));
        Routing.RegisterRoute(nameof(MonthPage), typeof(MonthPage));
        Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));

        Navigated += OnNavigated;
    }

    /// <summary>
    /// Ensures theme is applied correctly after navigation transitions.
    /// </summary>
    private static void OnNavigated(object sender, ShellNavigatedEventArgs e)
    {
        if (Application.Current is null) return;

        Application.Current.UserAppTheme = Theme.Tema switch
        {
            0 => AppTheme.Dark,
            1 => AppTheme.Light,
            _ => AppTheme.Unspecified
        };
    }
}
