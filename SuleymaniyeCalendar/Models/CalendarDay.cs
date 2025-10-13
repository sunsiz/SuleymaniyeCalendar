using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;

namespace SuleymaniyeCalendar.Models;

/// <summary>
/// Represents a single day in the calendar grid view.
/// </summary>
public partial class CalendarDay : ObservableObject
{
    public DateTime Date { get; set; }
    public int Day => Date.Day;
    public bool IsCurrentMonth { get; set; }
    public bool IsToday => Date.Date == DateTime.Today;
    [ObservableProperty]
    private bool _isSelected;
    public bool HasData { get; set; }
    public Calendar PrayerData { get; set; }

    // The following properties are now controlled by styles in MonthCalendarView.xaml.cs
    // to centralize design and improve performance.
    // BackgroundColor, TextColor, BorderColor, BorderThickness, and FontAttributes
    // are dynamically applied in the view's code-behind.

    partial void OnIsSelectedChanged(bool value)
    {
        // This is a trigger for the view to update the cell's appearance.
        // The actual style change is handled in MonthCalendarView.xaml.cs.
        OnPropertyChanged(nameof(IsSelected));
    }
}
