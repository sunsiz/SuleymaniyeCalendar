namespace SuleymaniyeCalendar.Services;

/// <summary>
/// No-op implementation of IAudioSessionService for platforms without audio session requirements.
/// Used on Android and Windows where audio session management is handled automatically.
/// </summary>
public sealed class NullAudioSessionService : IAudioSessionService
{
    public void InitializeAudioSession() { }
    public void DeactivateAudioSession() { }
}
