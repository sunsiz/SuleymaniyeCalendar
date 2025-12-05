namespace SuleymaniyeCalendar.Services;

/// <summary>
/// Interface for platform-specific audio session management.
/// iOS uses AVAudioSession to configure audio playback modes.
/// Other platforms use no-op implementation.
/// </summary>
public interface IAudioSessionService
{
    /// <summary>
    /// Initializes audio session for streaming playback.
    /// On iOS, sets AVAudioSession category and activates session.
    /// </summary>
    void InitializeAudioSession();

    /// <summary>
    /// Deactivates audio session when playback ends.
    /// </summary>
    void DeactivateAudioSession();
}
