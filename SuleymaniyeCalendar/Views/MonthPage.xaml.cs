using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SuleymaniyeCalendar.ViewModels;
using SuleymaniyeCalendar.Views;

namespace SuleymaniyeCalendar.Views;

public partial class MonthPage : ContentPage
{
    private readonly MonthViewModel _viewModel;
    private bool _tableBuilt;

    public MonthPage(MonthViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (!_tableBuilt)
        {
            Dispatcher.Dispatch(async () =>
            {
                // Start loading first
                _ = _viewModel.InitializeAsync();

                // Wait until data exists (quick poll)
                var tries = 0;
                while (_viewModel.MonthlyCalendar?.Count == 0 && tries++ < 20)
                    await Task.Delay(50);

                // Inject heavy content after data exists (fewer measure/layout passes)
                var table = new MonthTableView { BindingContext = BindingContext };
                ListHost.Content = table;
                _tableBuilt = true;
            });
        }
    }
}