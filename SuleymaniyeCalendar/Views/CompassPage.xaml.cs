using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SuleymaniyeCalendar.ViewModels;

namespace SuleymaniyeCalendar.Views;

public partial class CompassPage : ContentPage
{
	public CompassPage(CompassViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}