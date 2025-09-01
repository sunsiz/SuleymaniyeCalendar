using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using SuleymaniyeCalendar.Resources.Strings;

namespace SuleymaniyeCalendar.Services
{
    public static class ToastAndDialogService
    {
        public static async Task ShowErrorDialogAsync(string title, string message, string primaryAction = null, string secondaryAction = null)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (!string.IsNullOrEmpty(primaryAction) && !string.IsNullOrEmpty(secondaryAction))
                {
                    var result = await Shell.Current.DisplayAlert(title, message, primaryAction, secondaryAction);
                    // Handle result if needed
                }
                else
                {
                    await Shell.Current.DisplayAlert(title, message, AppResources.Tamam);
                }
            });
        }

        public static void ShowSuccessToast(string message)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var toast = Toast.Make($"✅ {message}", ToastDuration.Short, 16);
                toast.Show(CancellationToken.None);
            });
        }

        public static void ShowWarningToast(string message)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var toast = Toast.Make($"⚠️ {message}", ToastDuration.Long, 16);
                toast.Show(CancellationToken.None);
            });
        }

        public static void ShowErrorToast(string message)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var toast = Toast.Make($"❌ {message}", ToastDuration.Long, 16);
                toast.Show(CancellationToken.None);
            });
        }
    }
}