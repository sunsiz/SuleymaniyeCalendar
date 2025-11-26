using CommunityToolkit.Maui;
using LocalizationResourceManager.Maui;
using Microsoft.Extensions.Logging;
using SuleymaniyeCalendar.Resources.Strings;
using SuleymaniyeCalendar.Services;
using SuleymaniyeCalendar.ViewModels;
using SuleymaniyeCalendar.Views;

#if ANDROID
using Microsoft.Maui.Controls.Handlers.Items;
using Microsoft.Maui.Handlers;
using AndroidX.RecyclerView.Widget;
using AndroidX.Core.View;
using AndroidX.Core.Widget;
#endif

namespace SuleymaniyeCalendar;

/// <summary>
/// Application configuration and dependency injection setup.
/// </summary>
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseMauiCommunityToolkitMediaElement()
            .UseLocalizationResourceManager(settings =>
            {
                settings.AddResource(AppResources.ResourceManager);
                settings.RestoreLatestCulture(true);
            })
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("FontAwesome6FreeSolid.otf", "FontAwesomeSolid");
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        ConfigurePlatformHandlers();
        RegisterServices(builder.Services);

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    /// <summary>
    /// Configure platform-specific handlers (e.g., Android nested scrolling).
    /// </summary>
    private static void ConfigurePlatformHandlers()
    {
#if ANDROID
        // Enable nested scrolling for CollectionView inside ScrollView
        CollectionViewHandler.Mapper.AppendToMapping("EnableNestedScrolling", (handler, _) =>
        {
            if (handler.PlatformView is RecyclerView rv)
            {
                rv.NestedScrollingEnabled = true;
                ViewCompat.SetNestedScrollingEnabled(rv, true);
            }
        });

        ScrollViewHandler.Mapper.AppendToMapping("EnableNestedScrolling", (handler, _) =>
        {
            if (handler.PlatformView is NestedScrollView nsv)
            {
                nsv.NestedScrollingEnabled = true;
                ViewCompat.SetNestedScrollingEnabled(nsv, true);
                nsv.FillViewport = true;
            }
        });
#endif
    }

    /// <summary>
    /// Register all services and view models with dependency injection.
    /// </summary>
    private static void RegisterServices(IServiceCollection services)
    {
        // Platform services
#if ANDROID
        services.AddSingleton<IAlarmService, AlarmForegroundService>();
#else
        services.AddSingleton<IAlarmService, NullAlarmService>();
#endif
        services.AddSingleton<IAudioPreviewService, AudioPreviewService>();
        services.AddSingleton<IRadioService, RadioService>();
        services.AddSingleton<IRtlService, RtlService>();

        // Core services
        services.AddSingleton<JsonApiService>();
        services.AddSingleton<DataService>();
        services.AddSingleton<AccessibilityService>();
        services.AddSingleton<PerformanceService>();
        services.AddSingleton<BackgroundDataPreloader>();

        // Singleton pages (main tabs)
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainPage>();
        services.AddSingleton<AboutViewModel>();
        services.AddSingleton<AboutPage>();
        services.AddSingleton<RadioViewModel>();
        services.AddSingleton<RadioPage>();
        services.AddSingleton<CompassViewModel>();
        services.AddSingleton<CompassPage>();
        services.AddSingleton<PrayerDetailViewModel>();
        services.AddSingleton<PrayerDetailPage>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<SettingsPage>();

        // Transient pages (fresh instance each navigation)
        services.AddTransient<MonthViewModel>();
        services.AddTransient<MonthPage>();
    }

    /// <summary>
    /// Set up global exception handlers for debugging.
    /// </summary>
    public static void Initialize()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            System.Diagnostics.Debug.WriteLine($"Unhandled exception: {e.ExceptionObject}");

        TaskScheduler.UnobservedTaskException += (_, e) =>
            System.Diagnostics.Debug.WriteLine($"Unobserved task exception: {e.Exception}");
    }
}
