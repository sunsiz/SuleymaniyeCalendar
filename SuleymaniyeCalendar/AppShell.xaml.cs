using SuleymaniyeCalendar.Views;
using SuleymaniyeCalendar.Services;

namespace SuleymaniyeCalendar;

public partial class AppShell : Shell
{
       public AppShell()
       {
	       InitializeComponent();
	       Routing.RegisterRoute(nameof(PrayerDetailPage), typeof(PrayerDetailPage));
	       Routing.RegisterRoute(nameof(MonthPage), typeof(MonthPage));
	       Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
	       Navigated += AppShell_Navigated;

	       // Register the global ModernDialog instance for use throughout the app
	       if (this.FindByName("GlobalModernDialog") is ModernDialog dialog)
	       {
		       ModernDialogService.Register(dialog);
	       }
       }

	private void AppShell_Navigated(object sender, ShellNavigatedEventArgs e)
	{
		// Ensure theme is reflected after navigation transitions
		var theme = Models.Theme.Tema;
		Application.Current!.UserAppTheme = theme switch
		{
			0 => AppTheme.Dark,
			1 => AppTheme.Light,
			2 => AppTheme.Unspecified,
			_ => AppTheme.Unspecified
		};
	}
}
