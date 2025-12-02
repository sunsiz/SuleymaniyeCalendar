using CommunityToolkit.Mvvm.ComponentModel;

namespace SuleymaniyeCalendar.Models;

/// <summary>
/// Represents a single day cell in the monthly calendar grid.
/// Observable for efficient UI updates when selection changes.
/// </summary>
public sealed partial class CalendarDay : ObservableObject
{
    // Color constants for consistent styling
    private static readonly Color SelectedBackground = Color.FromArgb("#20FFD700");
    private static readonly Color TodayBackground = Color.FromArgb("#01FFD700");
    private static readonly Color OtherMonthBackground = Color.FromArgb("#10808080");
    private static readonly Color GoldenBorder = Color.FromArgb("#FFD700");
    private static readonly Color DarkText = Color.FromArgb("#1A1A1A");
    private static readonly Color FadedText = Color.FromArgb("#80808080");

    /// <summary>The date this cell represents.</summary>
    public DateTime Date { get; set; }

    /// <summary>Day of month (1-31).</summary>
    public int Day => Date.Day;

    /// <summary>Whether this day belongs to the currently displayed month.</summary>
    public bool IsCurrentMonth { get; set; }

    /// <summary>Whether this is today's date.</summary>
    public bool IsToday => Date.Date == DateTime.Today;

    /// <summary>Whether this day is currently selected by the user.</summary>
    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                OnPropertyChanged(nameof(BackgroundColor));
                OnPropertyChanged(nameof(TextColor));
                OnPropertyChanged(nameof(BorderColor));
                OnPropertyChanged(nameof(BorderThickness));
                OnPropertyChanged(nameof(FontAttributesValue));
            }
        }
    }

    /// <summary>Whether prayer time data is available for this day.</summary>
    public bool HasData { get; set; }

    /// <summary>Full prayer times data for this day (if available).</summary>
    public Calendar? PrayerData { get; set; }

    #region Computed Visual Properties

    /// <summary>Background color based on selection and today status.</summary>
    public Color BackgroundColor
    {
        get
        {
            if (IsSelected) return SelectedBackground;
            if (IsToday) return TodayBackground;
            if (!IsCurrentMonth) return OtherMonthBackground;
            return Colors.Transparent;
        }
    }

    /// <summary>Text color optimized for readability.</summary>
    public Color? TextColor
    {
        get
        {
            if (IsToday || IsSelected) return DarkText;
            if (!IsCurrentMonth) return FadedText;
            return null; // Use theme default
        }
    }

    /// <summary>Border color for selection/today indicator.</summary>
    public Color BorderColor => (IsSelected || IsToday) ? GoldenBorder : Colors.Transparent;

    /// <summary>Border thickness for selection/today indicator.</summary>
    public double BorderThickness => (IsSelected || IsToday) ? 2 : 0;

    /// <summary>Font weight - bold for today.</summary>
    public FontAttributes FontAttributesValue => IsToday ? FontAttributes.Bold : FontAttributes.None;

    #endregion
}
