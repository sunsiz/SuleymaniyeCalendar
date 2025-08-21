using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SuleymaniyeCalendar.ViewModels;

namespace SuleymaniyeCalendar.Views;

public partial class RadioPage : ContentPage
{
	public RadioPage(RadioViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}