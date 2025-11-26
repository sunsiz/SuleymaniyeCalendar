namespace SuleymaniyeCalendar.Services;

/// <summary>
/// Service for right-to-left (RTL) language support.
/// </summary>
public interface IRtlService
{
    /// <summary>
    /// Gets the appropriate FlowDirection for a language.
    /// </summary>
    FlowDirection GetFlowDirection(string languageCode);

    /// <summary>
    /// Determines if a language code represents an RTL language.
    /// </summary>
    bool IsRtlLanguage(string languageCode);

    /// <summary>
    /// Applies the correct FlowDirection to the app based on language.
    /// </summary>
    void ApplyFlowDirection(string languageCode);

    /// <summary>
    /// Whether the current language is RTL.
    /// </summary>
    bool IsRightToLeft { get; }
}
