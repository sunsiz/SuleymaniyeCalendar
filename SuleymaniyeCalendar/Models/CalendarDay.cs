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
    private DateTime _date;
    public DateTime Date 
    {
        get => _date;
        set => SetProperty(ref _date, value);
    }

    private bool _isCurrentMonth;
    public bool IsCurrentMonth
    {
        get => _isCurrentMonth;
        set => SetProperty(ref _isCurrentMonth, value);
    }

    private bool _isToday;
    public bool IsToday
    {
        get => _isToday;
        set => SetProperty(ref _isToday, value);
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    private bool _hasData;
    public bool HasData
    {
        get => _hasData;
        set => SetProperty(ref _hasData, value);
    }

    private Calendar _prayerData;
    public Calendar PrayerData
    {
        get => _prayerData;
        set => SetProperty(ref _prayerData, value);
    }
    public int Day => Date.Day;

    public CalendarDay(DateTime date, bool isCurrentMonth)
    {
        Date = date;
        IsCurrentMonth = isCurrentMonth;
        IsToday = date.Date == DateTime.Today;
    }

    // The following properties are now controlled by styles in MonthCalendarView.xaml.cs
    // to centralize design and improve performance.
    // BackgroundColor, TextColor, BorderColor, BorderThickness, and FontAttributes
    // are dynamically applied in the view's code-behind.


}
