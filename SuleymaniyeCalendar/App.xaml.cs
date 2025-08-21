using System.Globalization;
using CommunityToolkit.Maui.Alerts;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using SuleymaniyeCalendar.Models;
using SuleymaniyeCalendar.Resources.Strings;

namespace SuleymaniyeCalendar;

public partial class App : Application
{
    public App()
    {
        // Localization init
        var language = Preferences.Get("SelectedLanguage", "en");
        AppResources.Culture = new CultureInfo(language);

        InitializeComponent();

        Resources["DefaultFontSize"] = Preferences.Get("FontSize", 14);
        MainPage = new AppShell();
    }

    protected override void OnStart()
    {
        Microsoft.Maui.ApplicationModel.VersionTracking.Track();
        ApplyTheme();
    }
    protected override void OnResume() => ApplyTheme();

    void ApplyTheme()
    {
        Current.UserAppTheme = Theme.Tema == 1 ? AppTheme.Light : AppTheme.Dark;
        Resources["DefaultFontSize"] = Preferences.Get("FontSize", 14);
    }
}
