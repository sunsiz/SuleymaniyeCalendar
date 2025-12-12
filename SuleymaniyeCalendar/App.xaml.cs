using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using SuleymaniyeCalendar.Models;
using SuleymaniyeCalendar.Resources.Strings;
using SuleymaniyeCalendar.Services;
using SuleymaniyeCalendar.ViewModels;

namespace SuleymaniyeCalendar;

/// <summary>
/// Application entry point and lifecycle management.
/// Handles theme initialization, localization, and app state transitions.
/// </summary>
public partial class App : Application
{
    private readonly BackgroundDataPreloader _preloader;

    public App(BackgroundDataPreloader preloader)
    {
        _preloader = preloader;

        AppDomain.CurrentDomain.UnhandledException += HandleUnhandledException;
        TaskScheduler.UnobservedTaskException += HandleUnobservedTaskException;

        // Initialize localization before components load
        var language = Preferences.Get("SelectedLanguage", "tr");
        AppResources.Culture = new CultureInfo(language);

        InitializeComponent();

        // Initialize font scaling system at startup
        BaseViewModel.InitializeFontSize();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }

    protected override void OnStart()
    {
        base.OnStart();

        // Start background data preloading (wrapped in Task.Run with exception handling)
        _ = Task.Run(async () =>
        {
            try { await _preloader.StartBackgroundPreloadAsync().ConfigureAwait(false); }
            catch (Exception ex) { Debug.WriteLine($"Background preload failed: {ex.Message}"); }
        });

#if __IOS__
        // Initialize iOS notification permissions (wrapped with exception handling)
        _ = Task.Run(async () =>
        {
            try { await Platforms.iOS.NotificationService.InitializeNotificationsAsync().ConfigureAwait(false); }
            catch (Exception ex) { Debug.WriteLine($"iOS notification init failed: {ex.Message}"); }
        });
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

        Application.Current.UserAppTheme = Theme.CurrentTheme switch
        {
            ThemeMode.Dark => AppTheme.Dark,
            ThemeMode.Light => AppTheme.Light,
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

    private static void HandleUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        LogUnhandledException("Unhandled", e.ExceptionObject as Exception);
    }

    private static void HandleUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogUnhandledException("UnobservedTask", e.Exception);
    }

    private static void LogUnhandledException(string source, Exception? ex)
    {
        if (ex is null) return;

        var message = $"{DateTime.UtcNow:O} [{source}] {ex}";

        try
        {
            var logPath = Path.Combine(FileSystem.AppDataDirectory, "crash.log");
            File.AppendAllText(logPath, message + Environment.NewLine);
        }
        catch
        {
            // Swallow logging errors to avoid masking the original crash.
        }

        Debug.WriteLine($"[CRASH] {message}");
    }
}
