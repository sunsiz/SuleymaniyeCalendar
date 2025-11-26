namespace SuleymaniyeCalendar.Services;

/// <summary>
/// Service for previewing alarm sounds in the settings.
/// </summary>
public interface IAudioPreviewService
{
    /// <summary>
    /// Plays a sound file for preview.
    /// </summary>
    /// <param name="fileNameWithoutExtension">Sound file name (without .mp3/.wav extension).</param>
    /// <param name="loop">Whether to loop the sound continuously.</param>
    Task PlayAsync(string fileNameWithoutExtension, bool loop = true);

    /// <summary>
    /// Stops the currently playing preview sound.
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Whether a sound is currently playing.
    /// </summary>
    bool IsPlaying { get; }
}
