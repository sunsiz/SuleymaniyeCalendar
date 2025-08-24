using CommunityToolkit.Maui;
using LocalizationResourceManager.Maui;
using Microsoft.Extensions.Logging;
using SuleymaniyeCalendar.Resources.Strings;
using SuleymaniyeCalendar.Services;
using SuleymaniyeCalendar.ViewModels;
using SuleymaniyeCalendar.Views;

namespace SuleymaniyeCalendar;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			// Initialize the .NET MAUI Community Toolkit by adding the below line of code
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

		// DI registrations
#if ANDROID
		builder.Services.AddSingleton<IAlarmService, AlarmForegroundService>();
#else
		builder.Services.AddSingleton<IAlarmService, NullAlarmService>();
#endif
		builder.Services.AddSingleton<IAudioPreviewService, AudioPreviewService>();
		builder.Services.AddSingleton<DataService>();
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
		builder.Services.AddSingleton<MonthViewModel>();
		builder.Services.AddSingleton<MonthPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
