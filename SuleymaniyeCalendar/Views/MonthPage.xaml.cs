using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SuleymaniyeCalendar.ViewModels;

namespace SuleymaniyeCalendar.Views;

public partial class MonthPage : ContentPage
{
	public MonthPage(MonthViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}