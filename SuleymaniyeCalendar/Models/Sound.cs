namespace SuleymaniyeCalendar.Models;

/// <summary>
/// Represents an alarm/notification sound option.
/// </summary>
public sealed class Sound
{
    /// <summary>The sound file name (without path/extension).</summary>
    public string FileName { get; }

    /// <summary>Display name shown in the UI picker.</summary>
    public string Name { get; }

    public Sound(string fileName, string name)
    {
        FileName = fileName;
        Name = name;
    }

    /// <summary>
    /// Allows Picker to display item text without ItemDisplayBinding.
    /// </summary>
    public override string ToString() => Name ?? base.ToString();
}
