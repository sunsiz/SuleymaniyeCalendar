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
            // Check if we have data already (subsequent visits)
            if (_viewModel.HasData)
            {
                // We have data, show table immediately
                CreateAndShowTable();
            }
            else
            {
                // First time - start loading data, table will be created when loading completes
                _ = LoadDataAndShowTable();
            }
        }
    }

    private void CreateAndShowTable()
    {
        // This is only called when we have data ready
        if (!_tableBuilt)
        {
            var table = new MonthTableView { BindingContext = BindingContext };
            ListHost.Content = table;
            _tableBuilt = true;
        }
    }

    private async Task LoadDataAndShowTable()
    {
        // Start loading data - this will automatically show the loading indicator via IsBusy
        await _viewModel.InitializeAsync();
        
        // Only create and show table after data is fully loaded
        if (_viewModel.HasData)
        {
            CreateAndShowTable();
        }
    }
}