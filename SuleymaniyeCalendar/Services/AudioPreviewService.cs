using System;
using System.Threading.Tasks;

namespace SuleymaniyeCalendar.Services
{
	public class AudioPreviewService : IAudioPreviewService
	{
#if ANDROID
		private Android.Media.MediaPlayer? _player;
#elif IOS || MACCATALYST
		private AVFoundation.AVAudioPlayer? _player;
#endif

		public bool IsPlaying {
			get {
#if ANDROID
				return _player?.IsPlaying == true;
#elif IOS || MACCATALYST
				return _player?.Playing == true;
#else
				return false;
#endif
			}
		}

	public async Task PlayAsync(string fileNameWithoutExtension, bool loop = true)
		{
			await StopAsync();
#if ANDROID
			try
			{
				var context = Android.App.Application.Context;
				int resId = context.Resources?.GetIdentifier(fileNameWithoutExtension, "raw", context.PackageName) ?? 0;
				if (resId == 0)
					return;

				_player = Android.Media.MediaPlayer.Create(context, resId);
				if (_player != null)
				{
					_player.Looping = loop;
					_player.Start();
				}
			}
			catch { /* ignore */ }
#elif IOS || MACCATALYST
			try
			{
				// Find the sound file in bundle (raw resources are embedded as bundle resources)
				var bundlePath = Foundation.NSBundle.MainBundle.PathForResource(fileNameWithoutExtension, "mp3");
				if (string.IsNullOrEmpty(bundlePath))
					bundlePath = Foundation.NSBundle.MainBundle.PathForResource(fileNameWithoutExtension, "wav");
				if (string.IsNullOrEmpty(bundlePath))
					bundlePath = Foundation.NSBundle.MainBundle.PathForResource(fileNameWithoutExtension, "m4a");
				
				if (!string.IsNullOrEmpty(bundlePath))
				{
					var url = Foundation.NSUrl.FromFilename(bundlePath);
					_player = AVFoundation.AVAudioPlayer.FromUrl(url, out var error);
					if (_player != null && error == null)
					{
						_player.NumberOfLoops = loop ? -1 : 0; // -1 = infinite loop
						_player.PrepareToPlay();
						_player.Play();
					}
				}
			}
			catch { /* ignore */ }
#else
			await Task.CompletedTask;
#endif
		}

		public Task StopAsync()
		{
#if ANDROID
			try
			{
				if (_player != null)
				{
					if (_player.IsPlaying) _player.Stop();
					_player.Reset();
					_player.Release();
					_player.Dispose();
					_player = null;
				}
			}
			catch { /* ignore */ }
#elif IOS || MACCATALYST
			try
			{
				if (_player != null)
				{
					if (_player.Playing) _player.Stop();
					_player.Dispose();
					_player = null;
				}
			}
			catch { /* ignore */ }
#endif
			return Task.CompletedTask;
		}
	}
}
