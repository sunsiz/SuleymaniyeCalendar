namespace SuleymaniyeCalendar.Models;

/// <summary>
/// Theme preference values for app appearance.
/// </summary>
public enum ThemeMode
{
    /// <summary>Dark theme - dark backgrounds, light text.</summary>
    Dark = 0,
    
    /// <summary>Light theme - light backgrounds, dark text.</summary>
    Light = 1,
    
    /// <summary>System default - follows device setting.</summary>
    System = 2
}

/// <summary>
/// Static helper for managing app theme preference.
/// </summary>
public static class Theme
{
    private const string PreferenceKey = "Tema";
    private const ThemeMode DefaultTheme = ThemeMode.System;

    /// <summary>
    /// Gets or sets the current theme mode.
    /// </summary>
    public static ThemeMode CurrentTheme
    {
        get => (ThemeMode)Preferences.Get(PreferenceKey, (int)DefaultTheme);
        set => Preferences.Set(PreferenceKey, (int)value);
    }

    /// <summary>
    /// Legacy property for backward compatibility.
    /// </summary>
    [Obsolete("Use CurrentTheme instead. This property is kept for backward compatibility.")]
    public static int Tema
    {
        get => (int)CurrentTheme;
        set => CurrentTheme = (ThemeMode)value;
    }
}
