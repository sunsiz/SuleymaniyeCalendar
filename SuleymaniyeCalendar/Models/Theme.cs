namespace SuleymaniyeCalendar.Models;

/// <summary>
/// Static helper for managing app theme preference.
/// </summary>
/// <remarks>
/// Theme values: 0 = Dark, 1 = Light, 2 = System (default).
/// </remarks>
public static class Theme
{
    private const string PreferenceKey = nameof(Tema);
    private const int DefaultTheme = 2; // System theme

    /// <summary>
    /// Gets or sets the current theme.
    /// 0 = Dark, 1 = Light, 2 = System (follows device setting).
    /// </summary>
    public static int Tema
    {
        get => Preferences.Get(PreferenceKey, DefaultTheme);
        set => Preferences.Set(PreferenceKey, value);
    }
}
