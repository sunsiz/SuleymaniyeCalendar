using System;
using System.Threading.Tasks;

namespace SuleymaniyeCalendar.Services
{
	public class AudioPreviewService : IAudioPreviewService
	{
#if ANDROID
		private Android.Media.MediaPlayer? _player;
#endif

		public bool IsPlaying {
			get {
#if ANDROID
				return _player?.IsPlaying == true;
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
#endif
			return Task.CompletedTask;
		}
	}
}
