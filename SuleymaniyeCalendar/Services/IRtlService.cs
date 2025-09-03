using Microsoft.Maui.Controls;

namespace SuleymaniyeCalendar.Services
{
    public interface IRtlService
    {
        FlowDirection GetFlowDirection(string languageCode);
        bool IsRtlLanguage(string languageCode);
        void ApplyFlowDirection(string languageCode);
    }
}
