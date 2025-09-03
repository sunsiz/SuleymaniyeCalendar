using Microsoft.Maui.Controls;
using System.Collections.Generic;
using System.Linq;

namespace SuleymaniyeCalendar.Services
{
    public class RtlService : IRtlService
    {
        // List of RTL language codes
        private static readonly HashSet<string> RtlLanguages = new HashSet<string>
        {
            "ar",   // Arabic
            "fa",   // Farsi/Persian
            "he",   // Hebrew
            "ku",   // Kurdish
            "ps",   // Pashto
            "sd",   // Sindhi
            "ug",   // Uyghur
            "ur",   // Urdu
            "yi"    // Yiddish
        };

        public bool IsRtlLanguage(string languageCode)
        {
            if (string.IsNullOrEmpty(languageCode))
                return false;
                
            // Handle culture codes like "ar-SA" by taking just the language part
            var langCode = languageCode.Split('-')[0].ToLower();
            return RtlLanguages.Contains(langCode);
        }

        public FlowDirection GetFlowDirection(string languageCode)
        {
            return IsRtlLanguage(languageCode) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        }

        public void ApplyFlowDirection(string languageCode)
        {
            var flowDirection = GetFlowDirection(languageCode);
            
            // Apply to application resources for global effect
            if (Application.Current != null)
            {
                Application.Current.Resources["FlowDirection"] = flowDirection;
                
                // If there's a main window, update it directly as well
                if (Application.Current.Windows?.Count > 0)
                {
                    var mainWindow = Application.Current.Windows[0];
                    if (mainWindow?.Page != null)
                    {
                        mainWindow.Page.FlowDirection = flowDirection;
                    }
                }
            }
        }
    }
}
