using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SuleymaniyeCalendar.Models;

/// <summary>
/// Observable model representing a single prayer time with visual state management.
/// Uses explicit properties with SetProperty for AOT compatibility.
/// </summary>
public sealed partial class Prayer : ObservableObject
{
    #region Core Properties

    private string _id = string.Empty;
    /// <summary>Prayer identifier (e.g., "fajr", "dhuhr", "asr").</summary>
    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    private string _name = string.Empty;
    /// <summary>Localized display name of the prayer.</summary>
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    private string _time = string.Empty;
    /// <summary>Prayer time in HH:mm format.</summary>
    public string Time
    {
        get => _time;
        set => SetProperty(ref _time, value);
    }

    private bool _enabled;
    /// <summary>Whether notifications are enabled for this prayer.</summary>
    public bool Enabled
    {
        get => _enabled;
        set => SetProperty(ref _enabled, value);
    }

    #endregion

    #region Visual State Properties

    private string _state = string.Empty;
    /// <summary>Current temporal state: "Happening", "Passed", or "Waiting".</summary>
    public string State
    {
        get => _state;
        set => SetProperty(ref _state, value);
    }

    private string _stateDescription = string.Empty;
    /// <summary>Human-readable state description.</summary>
    public string StateDescription
    {
        get => _stateDescription;
        set => SetProperty(ref _stateDescription, value);
    }

    private bool _isActive;
    /// <summary>True when this is the current prayer time window.</summary>
    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    private bool _isPast;
    /// <summary>True when this prayer time has already passed today.</summary>
    public bool IsPast
    {
        get => _isPast;
        set => SetProperty(ref _isPast, value);
    }

    private bool _isUpcoming;
    /// <summary>True when this prayer time is still coming today.</summary>
    public bool IsUpcoming
    {
        get => _isUpcoming;
        set => SetProperty(ref _isUpcoming, value);
    }

    private double _opacity = 1.0;
    /// <summary>Visual opacity (0.9 for past, 1.0 for current/upcoming).</summary>
    public double Opacity
    {
        get => _opacity;
        set => SetProperty(ref _opacity, value);
    }

    #endregion

    #region Icon Properties

    private string _iconPath = string.Empty;
    /// <summary>Path to animated weather icon (e.g., "sunrise", "clearday").</summary>
    public string IconPath
    {
        get => _iconPath;
        set => SetProperty(ref _iconPath, value);
    }

    private string _fontAwesomeIcon = string.Empty;
    /// <summary>Fallback FontAwesome icon glyph.</summary>
    public string FontAwesomeIcon
    {
        get => _fontAwesomeIcon;
        set => SetProperty(ref _fontAwesomeIcon, value);
    }

    private string _description = string.Empty;
    /// <summary>Astronomical description of the prayer time.</summary>
    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    #endregion

    /// <summary>Navigation command set by ViewModel for compiled binding.</summary>
    public IRelayCommand? NavigateCommand { get; set; }

    /// <summary>
    /// Updates visual state properties based on the current State value.
    /// Call this after setting State to update IsActive, IsPast, IsUpcoming, and Opacity.
    /// </summary>
    public void UpdateVisualState()
    {
        switch (State?.ToLowerInvariant())
        {
            case "happening" or "current":
                IsActive = true;
                IsPast = false;
                IsUpcoming = false;
                Opacity = 1.0;
                StateDescription = "Current";
                break;

            case "passed" or "completed":
                IsActive = false;
                IsPast = true;
                IsUpcoming = false;
                Opacity = 0.9;
                StateDescription = "Passed";
                break;

            case "waiting" or "upcoming" or "next":
                IsActive = false;
                IsPast = false;
                IsUpcoming = true;
                Opacity = 1.0;
                StateDescription = "Upcoming";
                break;

            default:
                IsActive = false;
                IsPast = false;
                IsUpcoming = false;
                Opacity = 1.0;
                StateDescription = string.Empty;
                break;
        }
    }
}
