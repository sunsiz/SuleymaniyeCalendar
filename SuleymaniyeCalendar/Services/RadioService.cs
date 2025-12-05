using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Core.Primitives;
using SuleymaniyeCalendar.Resources.Strings;

namespace SuleymaniyeCalendar.Services
{
    /// <summary>
    /// Radio service for streaming audio playback.
    /// Note: Loading/buffering indicators are now handled by XAML DataTriggers 
    /// bound directly to MediaElement.CurrentState (Microsoft best practice).
    /// </summary>
    public class RadioService : IRadioService
    {
        private readonly IAudioSessionService _audioSessionService;
        private MediaElement? _mediaElement;
        private bool _isPlaying;
        private string _currentTitle = AppResources.FitratinSesi;
        private const string RadioStreamUrl = "https://www.suleymaniyevakfi.org/radio.mp3";

        public RadioService(IAudioSessionService audioSessionService)
        {
            _audioSessionService = audioSessionService;
        }

        public bool IsPlaying => _isPlaying;
        public bool IsLoading => false; // Loading is now handled by XAML DataTriggers
        public string CurrentTitle => _currentTitle;

        public event EventHandler<bool>? PlaybackStateChanged;
#pragma warning disable CS0067 // Event is never used - kept for interface compatibility
        public event EventHandler<bool>? LoadingStateChanged; // Loading is now handled by XAML DataTriggers
#pragma warning restore CS0067
        public event EventHandler<string>? TitleChanged;

        public void SetMediaElement(MediaElement mediaElement)
        {
            if (_mediaElement != null)
            {
                // Unsubscribe from previous media element events
                _mediaElement.MediaOpened -= OnMediaOpened;
                _mediaElement.MediaFailed -= OnMediaFailed;
                _mediaElement.MediaEnded -= OnMediaEnded;
                _mediaElement.StateChanged -= OnStateChanged;
            }

            _mediaElement = mediaElement;

            if (_mediaElement != null)
            {
                // Subscribe to media element events
                _mediaElement.MediaOpened += OnMediaOpened;
                _mediaElement.MediaFailed += OnMediaFailed;
                _mediaElement.MediaEnded += OnMediaEnded;
                _mediaElement.StateChanged += OnStateChanged;
            }
        }

        public async Task PlayAsync()
        {
            if (_mediaElement == null) return;

            try
            {
                // Initialize audio session (iOS only, no-op elsewhere)
                _audioSessionService.InitializeAudioSession();

                // Create media source with metadata for better media control display
                var mediaSource = MediaSource.FromUri(RadioStreamUrl);
                
                // Set metadata directly on MediaElement for proper media controls
                _mediaElement.MetadataTitle = AppResources.RadyoFitrat; // "Radio Fitrat"
                _mediaElement.MetadataArtist = AppResources.FitratinSesi; // "Radio Fitrat - The Voice of Fitrah"
                _mediaElement.MetadataArtworkUrl = "https://www.fitratradyo.com/img/fitrat_radyo.png";
                
                // Set the source
                _mediaElement.Source = mediaSource;

                _mediaElement.Play();
                System.Diagnostics.Debug.WriteLine("📻 Radio Play() called");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Radio play error: {ex.Message}");
                SetPlaybackState(false);
            }
        }

        public async Task PauseAsync()
        {
            if (_mediaElement == null) return;

            try
            {
                _mediaElement.Pause();
                SetPlaybackState(false);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Radio pause error: {ex.Message}");
            }
        }

        public async Task StopAsync()
        {
            if (_mediaElement == null) return;

            try
            {
                _mediaElement.Stop();
                SetPlaybackState(false);
        
                // iOS: Deactivate audio session
                if (DeviceInfo.Platform == DevicePlatform.iOS)
                {
#if __IOS__
                    Platforms.iOS.AudioSessionManager.DeactivateAudioSession();
#endif
                }

                System.Diagnostics.Debug.WriteLine("✅ Radio stopped");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Radio stop error: {ex.Message}");
            }
        }

        private void OnMediaOpened(object? sender, EventArgs e)
        {
            SetPlaybackState(true);
            System.Diagnostics.Debug.WriteLine("📻 Radio media opened successfully");
        }

        private void OnMediaFailed(object? sender, EventArgs e)
        {
            SetPlaybackState(false);
            System.Diagnostics.Debug.WriteLine("❌ Radio media failed");
        }

        private void OnMediaEnded(object? sender, EventArgs e)
        {
            SetPlaybackState(false);
            System.Diagnostics.Debug.WriteLine("📻 Radio media ended");
        }

        private void OnStateChanged(object? sender, EventArgs e)
        {
            if (_mediaElement == null) return;
            
            var state = _mediaElement.CurrentState.ToString();
            System.Diagnostics.Debug.WriteLine($"📻 Radio state: {state}");
            
            // Only update playback state - loading indicator is now handled by XAML DataTriggers
            if (state == "Playing")
            {
                SetPlaybackState(true);
            }
            else if (state == "Paused" || state == "Stopped")
            {
                SetPlaybackState(false);
            }
        }

        private void SetPlaybackState(bool isPlaying)
        {
            if (_isPlaying != isPlaying)
            {
                _isPlaying = isPlaying;
                System.Diagnostics.Debug.WriteLine($"📻 SetPlaybackState: {isPlaying}");
                PlaybackStateChanged?.Invoke(this, _isPlaying);
            }
        }

        private void SetCurrentTitle(string title)
        {
            if (_currentTitle != title)
            {
                _currentTitle = title;
                TitleChanged?.Invoke(this, _currentTitle);
            }
        }

        public void ShowNotification()
        {
            // Implementation for showing notification (can be platform-specific)
            System.Diagnostics.Debug.WriteLine("Radio notification shown");
        }

        public void HideNotification()
        {
            // Implementation for hiding notification (can be platform-specific)
            System.Diagnostics.Debug.WriteLine("Radio notification hidden");
        }
    }
}
