using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SuleymaniyeCalendar.Resources.Strings;
using SuleymaniyeCalendar.Services;
using SuleymaniyeCalendar.Views;

namespace SuleymaniyeCalendar.ViewModels;

/// <summary>
/// ViewModel for the About page showing app info and links.
/// </summary>
public sealed partial class AboutViewModel : BaseViewModel
{
    private readonly PerformanceService _perf = new();
    private string _versionNumber = string.Empty;

    /// <summary>App version string displayed on the About page.</summary>
    public string VersionNumber
    {
        get => _versionNumber;
        set => SetProperty(ref _versionNumber, value);
    }

    /// <summary>Whether to show accessibility buttons (iOS only).</summary>
    public bool ShowButtons => DeviceInfo.Platform == DevicePlatform.iOS && IsAnyAccessibilityFeatureRunning();

    private bool _showDesignShowcase;

    /// <summary>Whether to show the design showcase section.</summary>
    public bool ShowDesignShowcase
    {
        get => _showDesignShowcase;
        set => SetProperty(ref _showDesignShowcase, value);
    }

    public AboutViewModel()
    {
        using (_perf.StartTimer("About.Constructor"))
        {
            Title = AppResources.SuleymaniyeVakfi;
            VersionNumber = $" v{AppInfo.VersionString} ";
        }
        
        // Log summary after page settles (wrapped in try-catch for safety when running without debugger)
        try
        {
            Application.Current?.Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(1), () =>
            {
                try { _perf.LogSummary("AboutView"); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"AboutView perf log failed: {ex.Message}"); }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AboutView DispatchDelayed setup failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ToggleShowcase() => ShowDesignShowcase = !ShowDesignShowcase;

    [RelayCommand]
    private async Task LinkButtonClicked(string url)
    {
        try
        {
            await Launcher.OpenAsync(url).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // iOS Simulator can't open App Store URLs - show toast instead
            System.Diagnostics.Debug.WriteLine($"Launcher.OpenAsync failed: {ex.Message}");
            
            // Copy URL to clipboard as fallback
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await Clipboard.SetTextAsync(url);
                await Toast.Make("📋 URL copied to clipboard", ToastDuration.Short).Show();
            });
        }
    }

    [RelayCommand]
    private async Task Settings()
    {
        IsBusy = true;
        await Shell.Current.GoToAsync(nameof(SettingsPage)).ConfigureAwait(false);
        IsBusy = false;
    }
}
