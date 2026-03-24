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
using AImageView = Android.Widget.ImageView;
#endif

namespace SuleymaniyeCalendar;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseMauiCommunityToolkitMediaElement(true)
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
                fonts.AddFont("UKIJTuT.ttf", "UyghurFont");
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

        // Guard against Glide IllegalArgumentException when Activity is destroyed during image load.
        // This prevents ~4.5% of crashes (RequestManagerRetriever.get).
        ImageHandler.Mapper.AppendToMapping("GlideCrashGuard", (handler, _) =>
        {
            if (handler.PlatformView is AImageView imageView)
            {
                var context = imageView.Context;
                if (context is Android.App.Activity { IsDestroyed: true } or Android.App.Activity { IsFinishing: true })
                {
                    System.Diagnostics.Debug.WriteLine("[GlideCrashGuard] Skipping image load - Activity is destroyed/finishing");
                    return;
                }
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
#elif IOS
        services.AddSingleton<IAlarmService, SuleymaniyeCalendar.Platforms.iOS.iOSAlarmService>();
#else
        services.AddSingleton<IAlarmService, NullAlarmService>();
#endif
        services.AddSingleton<IAudioPreviewService, AudioPreviewService>();
        services.AddSingleton<IRadioService, RadioService>();
        services.AddSingleton<IRtlService, RtlService>();

#if ANDROID
        services.AddSingleton<IWidgetService, SuleymaniyeCalendar.Platforms.Android.AndroidWidgetServiceHelper>();
#else
        services.AddSingleton<IWidgetService, NullWidgetService>();
#endif

#if IOS
        Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<IAudioSessionService, SuleymaniyeCalendar.Platforms.iOS.iOSAudioSessionService>(services);
#else
        Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<IAudioSessionService, NullAudioSessionService>(services);
#endif

        // Core services
        services.AddSingleton(sp =>
        {
            var perf = new PerformanceService();
#if DEBUG
            perf.VerboseLoggingEnabled = true;
#endif
            return perf;
        });
        services.AddSingleton<LocationService>();
        services.AddSingleton<PrayerTimesRepository>();
        services.AddSingleton<NotificationSchedulerService>();
        services.AddSingleton<JsonApiService>();
        services.AddSingleton<PrayerCacheService>();
        services.AddSingleton<DataService>();
        services.AddSingleton<AccessibilityService>();
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
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<SettingsPage>();

        // Transient pages (fresh instance each navigation)
        services.AddTransient<PrayerDetailViewModel>();
        services.AddTransient<PrayerDetailPage>();
        services.AddTransient<MonthViewModel>();
        services.AddTransient<MonthPage>();
    }
}
