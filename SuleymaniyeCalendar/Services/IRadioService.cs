namespace SuleymaniyeCalendar.Services;

/// <summary>
/// Service for streaming the Suleymaniye radio station.
/// </summary>
public interface IRadioService
{
    /// <summary>Whether audio is currently playing.</summary>
    bool IsPlaying { get; }

    /// <summary>Whether the stream is currently buffering/loading.</summary>
    bool IsLoading { get; }

    /// <summary>Current track or stream title (from metadata).</summary>
    string CurrentTitle { get; }

    /// <summary>Raised when playback starts or stops.</summary>
    event EventHandler<bool> PlaybackStateChanged;

    /// <summary>Raised when loading/buffering state changes.</summary>
    event EventHandler<bool> LoadingStateChanged;

    /// <summary>Raised when track metadata changes.</summary>
    event EventHandler<string> TitleChanged;

    /// <summary>Start playing the radio stream.</summary>
    Task PlayAsync();

    /// <summary>Pause the radio stream.</summary>
    Task PauseAsync();

    /// <summary>Stop the radio stream and release resources.</summary>
    Task StopAsync();

    /// <summary>Show the media notification (for background playback).</summary>
    void ShowNotification();

    /// <summary>Hide the media notification.</summary>
    void HideNotification();
}
