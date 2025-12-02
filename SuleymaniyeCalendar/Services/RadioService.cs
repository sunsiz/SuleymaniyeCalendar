using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Core.Primitives;
using SuleymaniyeCalendar.Resources.Strings;

namespace SuleymaniyeCalendar.Services
{
    public class RadioService : IRadioService
    {
        private MediaElement? _mediaElement;
        private bool _isPlaying;
        private bool _isLoading;
        private string _currentTitle = AppResources.FitratinSesi;
        private const string RadioStreamUrl = "https://www.suleymaniyevakfi.org/radio.mp3";

        public bool IsPlaying => _isPlaying;
        public bool IsLoading => _isLoading;
        public string CurrentTitle => _currentTitle;

        public event EventHandler<bool>? PlaybackStateChanged;
        public event EventHandler<bool>? LoadingStateChanged;
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
                // iOS: Initialize audio session before playing
                if (DeviceInfo.Platform == DevicePlatform.iOS)
                {
#if __IOS__
                    Platforms.iOS.AudioSessionManager.InitializeAudioSession();
#endif
                }

                SetLoadingState(true);
                
                // Create media source with metadata for better media control display
                var mediaSource = MediaSource.FromUri(RadioStreamUrl);
                
                // Set metadata directly on MediaElement for proper media controls
                _mediaElement.MetadataTitle = AppResources.RadyoFitrat; // "Radio Fitrat"
                _mediaElement.MetadataArtist = AppResources.FitratinSesi; // "Radio Fitrat - The Voice of Fitrah"
                _mediaElement.MetadataArtworkUrl = "https://www.kurandersi.com/resimler/fitrat-radyo-logo-alt.png"; // No artwork for radio stream
                
                // Set the source
                _mediaElement.Source = mediaSource;

                _mediaElement.Play();
                System.Diagnostics.Debug.WriteLine("📻 Radio playback started");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Radio play error: {ex.Message}");
                SetLoadingState(false);
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
                SetLoadingState(false);
        
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
            SetLoadingState(false);
            SetPlaybackState(true);
            System.Diagnostics.Debug.WriteLine("Radio media opened successfully");
        }

        private void OnMediaFailed(object? sender, EventArgs e)
        {
            SetLoadingState(false);
            SetPlaybackState(false);
            System.Diagnostics.Debug.WriteLine($"Radio media failed");
        }

        private void OnMediaEnded(object? sender, EventArgs e)
        {
            SetPlaybackState(false);
            System.Diagnostics.Debug.WriteLine("Radio media ended");
        }

        private void OnStateChanged(object? sender, EventArgs e)
        {
            if (_mediaElement == null) return;
            
            System.Diagnostics.Debug.WriteLine($"Radio state changed: {_mediaElement.CurrentState}");
            
            // Simple state handling without complex enum checks
            if (_mediaElement.CurrentState.ToString().Contains("Playing"))
            {
                SetLoadingState(false);
                SetPlaybackState(true);
            }
            else if (_mediaElement.CurrentState.ToString().Contains("Paused") || 
                     _mediaElement.CurrentState.ToString().Contains("Stopped"))
            {
                SetPlaybackState(false);
            }
            else if (_mediaElement.CurrentState.ToString().Contains("Buffering"))
            {
                SetLoadingState(true);
            }
            else if (_mediaElement.CurrentState.ToString().Contains("Failed"))
            {
                SetLoadingState(false);
                SetPlaybackState(false);
            }
        }

        private void SetPlaybackState(bool isPlaying)
        {
            if (_isPlaying != isPlaying)
            {
                _isPlaying = isPlaying;
                PlaybackStateChanged?.Invoke(this, _isPlaying);
            }
        }

        private void SetLoadingState(bool isLoading)
        {
            if (_isLoading != isLoading)
            {
                _isLoading = isLoading;
                LoadingStateChanged?.Invoke(this, _isLoading);
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
