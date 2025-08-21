using SuleymaniyeCalendar.Views;

namespace SuleymaniyeCalendar;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute(nameof(PrayerDetailPage), typeof(PrayerDetailPage));
		Routing.RegisterRoute(nameof(MonthPage), typeof(MonthPage));
		Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
	}
}
