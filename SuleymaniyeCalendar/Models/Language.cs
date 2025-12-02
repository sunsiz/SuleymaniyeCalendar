using SuleymaniyeCalendar.Helpers;

namespace SuleymaniyeCalendar.Models;

/// <summary>
/// Represents a supported language with RTL detection.
/// </summary>
public sealed class Language
{
    public Language(string name, string cultureCode)
    {
        Name = name;
        CI = cultureCode;
        IsRtl = AppConstants.IsRtlLanguage(cultureCode);
        FlowDirection = IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    }

    /// <summary>Display name of the language (localized).</summary>
    public string Name { get; set; }

    /// <summary>Culture/language code (e.g., "en", "tr", "ar").</summary>
    public string CI { get; set; }

    /// <summary>Whether this language uses right-to-left text direction.</summary>
    public bool IsRtl { get; set; }

    /// <summary>XAML FlowDirection for this language.</summary>
    public FlowDirection FlowDirection { get; set; }

    /// <summary>Enable Picker to display without ItemDisplayBinding.</summary>
    public override string ToString() => Name;
}
