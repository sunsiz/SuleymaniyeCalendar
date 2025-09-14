using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SuleymaniyeCalendar.ViewModels;

namespace SuleymaniyeCalendar.Views;

public partial class PrayerDetailPage : ContentPage
{
	private PrayerDetailViewModel _viewModel;

	public PrayerDetailPage(PrayerDetailViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}
	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		// Ensure any pending changes are saved and alarms scheduled even if user uses system back
		_ = _viewModel.SaveAndScheduleAsync();
		//DataService data = new DataService();
		//if (data.CheckRemindersEnabledAny())
		//    DependencyService.Get<IForegroundServiceControlService>().StartService();
		//else DependencyService.Get<IForegroundServiceControlService>().StopService();
	}
}