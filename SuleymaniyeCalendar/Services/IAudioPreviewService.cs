namespace SuleymaniyeCalendar.Services
{
	public interface IAudioPreviewService
	{
		Task PlayAsync(string fileNameWithoutExtension);
		Task StopAsync();
		bool IsPlaying { get; }
	}
}
