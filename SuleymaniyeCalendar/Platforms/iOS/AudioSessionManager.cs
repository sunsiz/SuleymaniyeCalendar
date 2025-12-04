using AVFoundation;
using Foundation;
using System.Diagnostics;

namespace SuleymaniyeCalendar.Platforms.iOS;

/// <summary>
/// Manages AVAudioSession for radio streaming and playback.
/// Configures audio category, mode, and options for proper operation.
/// </summary>
public static class AudioSessionManager
{
    /// <summary>
    /// Initializes audio session for streaming radio playback.
    /// Must be called before attempting to play audio.
    /// </summary>
    public static void InitializeAudioSession()
    {
        try
        {
            var audioSession = AVAudioSession.SharedInstance();
            
            // Set category to Playback (allows audio in background)
            audioSession.SetCategory(
                AVAudioSessionCategory.Playback,
                AVAudioSessionCategoryOptions.DuckOthers | 
                AVAudioSessionCategoryOptions.DefaultToSpeaker
            );
            
            // Activate the session
            NSError? error = null;
            // Set mode to Movie Playback (best for streaming)
            audioSession.SetMode(AVAudioSessionMode.MoviePlayback, out error);

            audioSession.SetActive(true, AVAudioSessionSetActiveOptions.NotifyOthersOnDeactivation, out error);

            Debug.WriteLine(error != null
                ? $"❌ Audio session error: {error.LocalizedDescription}"
                : "✅ Audio session initialized for playback");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Failed to initialize audio session: {ex.Message}");
        }
    }

    /// <summary>
    /// Deactivates audio session when radio playback ends.
    /// </summary>
    public static void DeactivateAudioSession()
    {
        try
        {
            var audioSession = AVAudioSession.SharedInstance();
            NSError? error = null;
            audioSession.SetActive(false, out error);
            
            if (error != null)
            {
                Debug.WriteLine($"⚠️ Audio deactivation warning: {error.LocalizedDescription}");
            }
            else
            {
                Debug.WriteLine("✅ Audio session deactivated");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Failed to deactivate audio session: {ex.Message}");
        }
    }
}