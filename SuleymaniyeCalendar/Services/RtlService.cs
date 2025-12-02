using SuleymaniyeCalendar.Helpers;

namespace SuleymaniyeCalendar.Services;

/// <summary>
/// Implements right-to-left (RTL) language support.
/// </summary>
public sealed class RtlService : IRtlService
{
    /// <inheritdoc/>
    public bool IsRightToLeft => AppConstants.IsRtlLanguage(Preferences.Get("SelectedLanguage", "tr"));

    /// <inheritdoc/>
    public bool IsRtlLanguage(string languageCode) => AppConstants.IsRtlLanguage(languageCode);

    /// <inheritdoc/>
    public FlowDirection GetFlowDirection(string languageCode)
    {
        return AppConstants.IsRtlLanguage(languageCode) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    }

    /// <inheritdoc/>
    public void ApplyFlowDirection(string languageCode)
    {
        var flowDirection = GetFlowDirection(languageCode);

        if (Application.Current is null) return;

        // Update global resource for XAML bindings
        Application.Current.Resources["FlowDirection"] = flowDirection;

        // Update active window directly
        var mainWindow = Application.Current.Windows?.FirstOrDefault();
        if (mainWindow?.Page is not null)
        {
            mainWindow.Page.FlowDirection = flowDirection;
        }
    }
}
