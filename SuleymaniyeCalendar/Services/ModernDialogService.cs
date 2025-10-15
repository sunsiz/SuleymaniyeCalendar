using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using SuleymaniyeCalendar.Views;

namespace SuleymaniyeCalendar.Services
{
    public static class ModernDialogService
    {
        private static ModernDialog _dialogInstance;

        public static void Register(ModernDialog dialog)
        {
            _dialogInstance = dialog;
        }

        public static async Task<bool> ShowAsync(string title, string message, string primaryText = "OK", string secondaryText = null)
        {
            if (_dialogInstance == null)
                return false;
            return await _dialogInstance.ShowAsync(title, message, primaryText, secondaryText);
        }
    }
}
