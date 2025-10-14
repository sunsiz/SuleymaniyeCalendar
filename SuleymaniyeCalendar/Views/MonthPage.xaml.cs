using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SuleymaniyeCalendar.ViewModels;
using SuleymaniyeCalendar.Views;
using SuleymaniyeCalendar.Services;

namespace SuleymaniyeCalendar.Views;

public partial class MonthPage : ContentPage
{
    private readonly MonthViewModel _viewModel;
    private bool _tableBuilt;
    private readonly PerformanceService _perf = new PerformanceService();

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
            // Always create table immediately for instant UI
            using (_perf.StartTimer("MonthPage.CreateTable"))
            {
                CreateTableImmediately();
            }
            
            // Check if we have data already (subsequent visits)
            if (_viewModel.HasData)
            {
                // Data is already available, no need to load
                return;
            }
            else
            {
                // First time - start delayed loading for smooth UX
                using (_perf.StartTimer("MonthPage.TriggerLoad"))
                {
                    _ = LoadDataWithDelay();
                }
            }
        }
    }

    private void CreateTableImmediately()
    {
        // 🗓️ PHASE 20: Create beautiful calendar grid instead of table
        if (!_tableBuilt)
        {
            var calendarView = new MonthCalendarView { BindingContext = BindingContext };
            ListHost.Content = calendarView;
            _tableBuilt = true;
        }
    }

    private async Task LoadDataWithDelay()
    {
        // Use new delayed loading approach for better UX
        await _viewModel.InitializeWithDelayAsync();
    }
}