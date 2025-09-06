using CommunityToolkit.Maui;
using LocalizationResourceManager.Maui;
using Microsoft.Extensions.Logging;
using SuleymaniyeCalendar.Resources.Strings;
using SuleymaniyeCalendar.Services;
using SuleymaniyeCalendar.ViewModels;
using SuleymaniyeCalendar.Views;

// Keep this if you use other handlers elsewhere
using Microsoft.Maui.Handlers;

#if ANDROID
using Microsoft.Maui.Controls.Handlers.Items;     // CollectionViewHandler
using AndroidX.RecyclerView.Widget;               // RecyclerView
using AndroidX.Core.View;                         // ViewCompat.SetNestedScrollingEnabled
using AndroidX.Core.Widget;                       // NestedScrollView
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

#if ANDROID
        // Allow CollectionView (RecyclerView) to scroll vertically inside a horizontal ScrollView
        CollectionViewHandler.Mapper.AppendToMapping("EnableNestedScrolling", (handler, view) =>
        {
            if (handler.PlatformView is RecyclerView rv)
            {
                rv.NestedScrollingEnabled = true;
                ViewCompat.SetNestedScrollingEnabled(rv, true);
            }
        });

        // Also ensure ScrollView participates in nested scrolling
        ScrollViewHandler.Mapper.AppendToMapping("EnableNestedScrolling", (handler, view) =>
        {
            if (handler.PlatformView is NestedScrollView nsv)
            {
                nsv.NestedScrollingEnabled = true;
                ViewCompat.SetNestedScrollingEnabled(nsv, true);
                nsv.FillViewport = true; // improves child measurement in nested scenarios
            }
        });
#endif

#if ANDROID
        builder.Services.AddSingleton<IAlarmService, AlarmForegroundService>();
#else
        builder.Services.AddSingleton<IAlarmService, NullAlarmService>();
#endif
        builder.Services.AddSingleton<IAudioPreviewService, AudioPreviewService>();
        builder.Services.AddSingleton<IRadioService, RadioService>();
        builder.Services.AddSingleton<IRtlService, RtlService>();
        builder.Services.AddSingleton<JsonApiService>();
        builder.Services.AddSingleton<DataService>();
        
        // Enhanced services for better user experience  
        builder.Services.AddSingleton<AccessibilityService>();
        builder.Services.AddSingleton<PerformanceService>();
        builder.Services.AddSingleton<BackgroundDataPreloader>();

        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddSingleton<AboutViewModel>();
        builder.Services.AddSingleton<AboutPage>();
        builder.Services.AddSingleton<RadioViewModel>();
        builder.Services.AddSingleton<RadioPage>();
        builder.Services.AddSingleton<PrayerDetailViewModel>();
        builder.Services.AddSingleton<PrayerDetailPage>();
        builder.Services.AddSingleton<SettingsViewModel>();
        builder.Services.AddSingleton<SettingsPage>();
        builder.Services.AddSingleton<CompassViewModel>();
        builder.Services.AddSingleton<CompassPage>();

        // Transient Month page/VM
        builder.Services.AddTransient<MonthViewModel>();
        builder.Services.AddTransient<MonthPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
