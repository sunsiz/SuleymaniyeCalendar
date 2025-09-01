namespace SuleymaniyeCalendar.Services
{
	public interface IAudioPreviewService
	{
	Task PlayAsync(string fileNameWithoutExtension, bool loop = true);
		Task StopAsync();
		bool IsPlaying { get; }
	}
}
