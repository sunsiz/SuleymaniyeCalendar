using SuleymaniyeCalendar.Helpers;
using SuleymaniyeCalendar.Models;
using SuleymaniyeCalendar.Views;

#if IOS
using UIKit;
using CoreGraphics;
#elif ANDROID
using AndroidX.Fragment.App;
using Google.Android.Material.BottomNavigation;
#endif

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

        // Set initial FlowDirection based on saved language
        var savedLanguage = Preferences.Get("SelectedLanguage", "tr");
        this.FlowDirection = AppConstants.IsRtlLanguage(savedLanguage) 
            ? FlowDirection.RightToLeft 
            : FlowDirection.LeftToRight;

        Navigated += OnNavigated;
    }

    /// <summary>
    /// Ensures theme is applied correctly after navigation transitions.
    /// </summary>
    private static void OnNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        if (Application.Current is null) return;

        Application.Current.UserAppTheme = Theme.CurrentTheme switch
        {
            ThemeMode.Dark => AppTheme.Dark,
            ThemeMode.Light => AppTheme.Light,
            _ => AppTheme.Unspecified
        };
    }
    
    /// <summary>
    /// Call this method when font size changes to update tab bar height dynamically.
    /// Should be called from SettingsViewModel after font size is changed.
    /// </summary>
    public void UpdateTabBarForFontSize()
    {
#if IOS
        if (Handler?.PlatformView is UIKit.UITabBarController tabBarController)
        {
            var fontSize = Preferences.Get("FontSize", 14.0);
            var extraHeight = Math.Max(0, (fontSize - 14) * 2);
            var minHeight = 49 + extraHeight;
            
            var tabBar = tabBarController.TabBar;
            if (tabBar != null)
            {
                tabBar.Frame = new CoreGraphics.CGRect(
                    tabBar.Frame.X,
                    tabBar.Frame.Y,
                    tabBar.Frame.Width,
                    Math.Max(minHeight, 49)
                );
                tabBar.SetNeedsLayout();
            }
        }
#elif ANDROID
        if (Handler?.PlatformView is AndroidX.Fragment.App.FragmentActivity fragmentActivity)
        {
            var fontSize = Preferences.Get("FontSize", 14.0);
            var extraHeight = Math.Max(0, (fontSize - 14) * 2);
            var minHeight = (int)(56 + extraHeight);
            
            var bottomNav = fragmentActivity.FindViewById<Google.Android.Material.BottomNavigation.BottomNavigationView>(
                Android.Resource.Id.Action0);
            if (bottomNav != null)
            {
                bottomNav.LayoutParameters.Height = (int)Android.Util.TypedValue.ApplyDimension(
                    Android.Util.ComplexUnitType.Dip,
                    minHeight,
                    bottomNav.Context.Resources.DisplayMetrics
                );
                bottomNav.RequestLayout();
            }
        }
#endif
    }
}
