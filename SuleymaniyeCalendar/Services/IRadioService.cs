namespace SuleymaniyeCalendar.Services
{
    public interface IRadioService
    {
        bool IsPlaying { get; }
        bool IsLoading { get; }
        string CurrentTitle { get; }
        
        event EventHandler<bool> PlaybackStateChanged;
        event EventHandler<bool> LoadingStateChanged;
        event EventHandler<string> TitleChanged;
        
        Task PlayAsync();
        Task PauseAsync();
        Task StopAsync();
        void ShowNotification();
        void HideNotification();
    }
}
