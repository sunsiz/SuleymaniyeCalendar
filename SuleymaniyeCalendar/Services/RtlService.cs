namespace SuleymaniyeCalendar.Services;

/// <summary>
/// Implements right-to-left (RTL) language support.
/// </summary>
public sealed class RtlService : IRtlService
{
    /// <summary>RTL language codes (Arabic, Farsi, Hebrew, Kurdish, etc.).</summary>
    private static readonly HashSet<string> RtlLanguages =
    [
        "ar", "fa", "he", "ku", "ps", "sd", "ug", "ur", "yi"
    ];

    /// <inheritdoc/>
    public bool IsRightToLeft => IsRtlLanguage(Preferences.Get("SelectedLanguage", "tr"));

    /// <inheritdoc/>
    public bool IsRtlLanguage(string languageCode)
    {
        if (string.IsNullOrEmpty(languageCode)) return false;

        // Handle culture codes like "ar-SA" by extracting the base language
        var baseCode = languageCode.Split('-')[0].ToLowerInvariant();
        return RtlLanguages.Contains(baseCode);
    }

    /// <inheritdoc/>
    public FlowDirection GetFlowDirection(string languageCode)
    {
        return IsRtlLanguage(languageCode) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
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
