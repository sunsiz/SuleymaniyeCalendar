namespace SuleymaniyeCalendar.Services;

/// <summary>
/// Service for updating platform-specific widgets (e.g. Android Home Screen Widget).
/// </summary>
public interface IWidgetService
{
    /// <summary>
    /// Triggers an update of the widget content.
    /// </summary>
    void UpdateWidget();
}
