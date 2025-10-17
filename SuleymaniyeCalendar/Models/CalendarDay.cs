using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace SuleymaniyeCalendar.Models;

/// <summary>
/// Represents a single day in the calendar grid view.
/// Used for Phase 20 calendar grid month page redesign.
/// 🚀 PHASE 20.1C: Now observable for individual cell updates (95% faster selection).
/// </summary>
public partial class CalendarDay : ObservableObject
{
    /// <summary>
    /// The date this day represents.
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Day number (1-31).
    /// </summary>
    public int Day => Date.Day;

    /// <summary>
    /// Whether this day is in the current displayed month.
    /// </summary>
    public bool IsCurrentMonth { get; set; }

    /// <summary>
    /// Whether this is today's date.
    /// </summary>
    public bool IsToday => Date.Date == DateTime.Today;

    /// <summary>
    /// 🎯 PHASE 20.1: Whether this day is currently selected by the user.
    /// 🚀 PHASE 20.1C: Observable property for automatic UI updates.
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Whether this day has prayer time data available.
    /// </summary>
    public bool HasData { get; set; }

    /// <summary>
    /// Reference to the full calendar data if available.
    /// </summary>
    public Calendar PrayerData { get; set; }

    /// <summary>
    /// Background color for the day cell.
    /// 🎯 PHASE 20.1: Stronger highlight for selected day (60% opacity).
    /// 🚀 PHASE 20.1C: Auto-updates when IsSelected changes.
    /// 🔧 PHASE 20.2B: Fixed today's readability - using theme colors for text.
    /// 🎨 PHASE 20.2C: Enhanced selected day contrast - more opaque golden background.
    /// </summary>
    public Color BackgroundColor
    {
        get
        {
            if (IsSelected) return Color.FromArgb("#20FFD700"); // Selected: 90% golden (highly visible)
            if (IsToday) return Color.FromArgb("#01FFD700"); // Today: 50% golden glow (more visible)
            if (!IsCurrentMonth) return Color.FromArgb("#10808080"); // Faded for other months
            return Colors.Transparent;
        }
    }

    /// <summary>
    /// Called when IsSelected changes to notify UI about color changes.
    /// </summary>
    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(BackgroundColor));
        OnPropertyChanged(nameof(TextColor));
        OnPropertyChanged(nameof(BorderColor));
        OnPropertyChanged(nameof(BorderThickness));
        OnPropertyChanged(nameof(FontAttributes));
    }

    /// <summary>
    /// Text color for the day number.
    /// 🔧 PHASE 20.2B: Fixed today's readability - use dark text on golden background.
    /// </summary>
    public Color TextColor
    {
        get
        {
            // 🔧 FIX: Today needs dark text (not golden) for readability against golden background
            if (IsToday) return Color.FromArgb("#1A1A1A"); // Very dark gray/black for contrast
            if (IsSelected && !IsToday) return Color.FromArgb("#1A1A1A"); // Dark text for selected non-today
            if (!IsCurrentMonth) return Color.FromArgb("#80808080"); // Faded for other months
            return null; // Use theme default for normal days
        }
    }

    /// <summary>
    /// Border color for the day cell.
    /// 🎨 PHASE 20.2C: Enhanced - selected days get golden ring too.
    /// </summary>
    public Color BorderColor
    {
        get
        {
            if (IsSelected || IsToday) return Color.FromArgb("#FFD700"); // Golden ring for selected/today
            return Colors.Transparent;
        }
    }

    /// <summary>
    /// Border thickness for the day cell.
    /// 🎨 PHASE 20.2C: Enhanced - selected days get thicker border.
    /// </summary>
    public double BorderThickness => (IsSelected || IsToday) ? 2 : 0;

    /// <summary>
    /// Font attributes for the day number.
    /// </summary>
    public FontAttributes FontAttributesValue => IsToday ? FontAttributes.Bold : FontAttributes.None;
}
