using SuleymaniyeCalendar.Services;

namespace SuleymaniyeCalendar.Platforms.iOS;

/// <summary>
/// iOS implementation of IAudioSessionService using AVAudioSession.
/// Wraps AudioSessionManager for dependency injection.
/// </summary>
public class iOSAudioSessionService : IAudioSessionService
{
    public void InitializeAudioSession()
    {
        AudioSessionManager.InitializeAudioSession();
    }

    public void DeactivateAudioSession()
    {
        AudioSessionManager.DeactivateAudioSession();
    }
}
