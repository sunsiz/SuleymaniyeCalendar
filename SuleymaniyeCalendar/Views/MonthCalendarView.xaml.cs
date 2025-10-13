using SuleymaniyeCalendar.ViewModels;
using SuleymaniyeCalendar.Models;
using System.Collections.Specialized;
using Microsoft.Maui.Controls.Shapes;

namespace SuleymaniyeCalendar.Views;

public partial class MonthCalendarView : ContentView
{
    private MonthViewModel _viewModel;

    public MonthCalendarView()
    {
        InitializeComponent();
        BindingContextChanged += OnBindingContextChanged;
    }

    private void OnBindingContextChanged(object sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            if (_viewModel.CalendarDays != null)
            {
                _viewModel.CalendarDays.CollectionChanged -= OnCalendarDaysChanged;
            }
        }

        if (BindingContext is MonthViewModel viewModel)
        {
            _viewModel = viewModel;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            if (_viewModel.CalendarDays != null)
            {
                _viewModel.CalendarDays.CollectionChanged += OnCalendarDaysChanged;
                RenderCalendarGrid();
            }
        }
    }

    private void OnViewModelPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MonthViewModel.CalendarDays))
        {
            if (_viewModel.CalendarDays != null)
            {
                _viewModel.CalendarDays.CollectionChanged -= OnCalendarDaysChanged;
                _viewModel.CalendarDays.CollectionChanged += OnCalendarDaysChanged;
                RenderCalendarGrid();
            }
        }
    }

    private void OnCalendarDaysChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(RenderCalendarGrid);
    }

    private void RenderCalendarGrid()
    {
        if (_viewModel?.CalendarDays == null || CalendarGrid == null || !_viewModel.CalendarDays.Any()) return;

        CalendarGrid.Clear();
        int row = 0;
        int col = 0;

        foreach (var day in _viewModel.CalendarDays)
        {
            var border = new Border
            {
                Style = (Style)Application.Current.Resources["CalendarDay"],
                BindingContext = day
            };
            
            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += async (s, e) =>
            {
                if (_viewModel != null && s is Border tappedBorder)
                {
                    await tappedBorder.ScaleTo(0.95, 80, Easing.CubicOut);
                    await tappedBorder.ScaleTo(1.0, 120, Easing.CubicIn);
                    _viewModel.SelectDayCommand.Execute(day.Date);
                }
            };
            border.GestureRecognizers.Add(tapGesture);

            Grid.SetRow(border, row);
            Grid.SetColumn(border, col);
            CalendarGrid.Add(border);

            col++;
            if (col >= 7)
            {
                col = 0;
                row++;
            }
        }
    }
}
