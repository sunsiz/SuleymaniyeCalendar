using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using SuleymaniyeCalendar.Resources.Strings;

namespace SuleymaniyeCalendar.Services
{
    public static class ToastAndDialogService
    {
        // Enhanced severity levels with icons for better user feedback
        public static async Task ShowErrorDialogAsync(string title, string message, string primaryAction = null, string secondaryAction = null)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (!string.IsNullOrEmpty(primaryAction) && !string.IsNullOrEmpty(secondaryAction))
                {
                    // ModernDialogService does not support two-button dialogs yet; fallback to primary only
                    await Services.ModernDialogService.ShowAsync(title, message, primaryAction);
                }
                else
                {
                    await Services.ModernDialogService.ShowAsync(title, message, AppResources.Tamam);
                }
            });
        }

        public static void ShowSuccessToast(string message)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                double fontSize = Preferences.Get("FontSize", 16);
                var toast = Toast.Make($"🌟 {message}", ToastDuration.Short, fontSize); // Gold star for success
                toast.Show(CancellationToken.None);
            });
        }

        public static void ShowWarningToast(string message)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                double fontSize = Preferences.Get("FontSize", 16);
                var toast = Toast.Make($"⚠️ {message}", ToastDuration.Long, fontSize); // Warning icon
                toast.Show(CancellationToken.None);
            });
        }

        public static void ShowErrorToast(string message)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                double fontSize = Preferences.Get("FontSize", 16);
                var toast = Toast.Make($"❌ {message}", ToastDuration.Long, fontSize); // Red cross for error
                toast.Show(CancellationToken.None);
            });
        }

        /// <summary>
        /// Enhanced network connectivity feedback with contextual guidance
        /// </summary>
        public static void ShowNetworkErrorToast(string feature = null)
        {
            var message = string.IsNullOrEmpty(feature) 
                ? AppResources.RadyoIcinInternet 
                : $"{feature} {AppResources.TakvimIcinInternet}";
            MainThread.BeginInvokeOnMainThread(() =>
            {
                double fontSize = Preferences.Get("FontSize", 16);
                var toast = Toast.Make($"📶 {message}", ToastDuration.Long, fontSize); // Network icon
                toast.Show(CancellationToken.None);
            });
        }

        /// <summary>
        /// Location permission guidance with action suggestions
        /// </summary>
        public static async Task ShowLocationPermissionDialogAsync()
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await Services.ModernDialogService.ShowAsync(
                    AppResources.KonumIzniBaslik,
                    $"{AppResources.KonumIzniIcerik} {AppResources.UygulamaAyarlari}",
                    AppResources.Tamam);
            });
        }
    }
}