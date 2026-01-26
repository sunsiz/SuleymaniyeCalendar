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
                System.Diagnostics.Debug.WriteLine("📻 PlayAsync: Starting...");

                // Pre-fetch metadata strings BEFORE going to main thread to avoid blocking
                // AppResources lookups can trigger expensive operations when accessed on main thread
                var title = AppResources.RadyoFitrat;
                var artist = AppResources.FitratinSesi;
                const string artworkUrl = "https://www.fitratradyo.com/img/fitrat_radyo.png";

                // Initialize audio session on background thread
                await Task.Run(() => 
                {
                    _audioSessionService.InitializeAudioSession();
                }).ConfigureAwait(false);

                System.Diagnostics.Debug.WriteLine("📻 PlayAsync: Audio session initialized");

                // Set source on main thread, but DON'T call Play() yet
                // MediaElement will begin loading asynchronously
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    try
                    {
                        // Set pre-fetched metadata (no expensive lookups on main thread)
                        _mediaElement.MetadataTitle = title;
                        _mediaElement.MetadataArtist = artist;
                        _mediaElement.MetadataArtworkUrl = artworkUrl;
                        
                        // Set source - MediaElement begins async loading immediately
                        _mediaElement.Source = MediaSource.FromUri(RadioStreamUrl);
                        
                        System.Diagnostics.Debug.WriteLine("📻 PlayAsync: Source set (async loading started)");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ MediaElement setup failed: {ex.Message}");
                        SetPlaybackState(false);
                        throw;
                    }
                });

                // Schedule Play() call on background thread to avoid blocking main thread
                // The MediaElement will call Play() after it has buffered enough data
                _ = Task.Run(async () =>
                {
                    // Wait briefly for MediaElement to start loading
                    await Task.Delay(100).ConfigureAwait(false);
                    
                    // Now trigger playback on main thread
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        try
                        {
                            _mediaElement.Play();
                            System.Diagnostics.Debug.WriteLine("📻 PlayAsync: Play() called (buffering in background)");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"❌ Play() failed: {ex.Message}");
                            SetPlaybackState(false);
                        }
                    });
                }).ConfigureAwait(false);

                System.Diagnostics.Debug.WriteLine("📻 PlayAsync: Completed (buffering continues in background)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Radio play error: {ex.Message}\nStack: {ex.StackTrace}");
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
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    _mediaElement.Stop();
                    SetPlaybackState(false);
                });
        
                // iOS: Deactivate audio session on background thread (prevent main thread blocking)
                if (DeviceInfo.Platform == DevicePlatform.iOS)
                {
#if __IOS__
                    await Task.Run(() => 
                    {
                        Platforms.iOS.AudioSessionManager.DeactivateAudioSession();
                    }).ConfigureAwait(false);
#endif
                }

                System.Diagnostics.Debug.WriteLine("✅ Radio stopped");
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
