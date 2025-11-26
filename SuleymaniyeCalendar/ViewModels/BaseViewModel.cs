using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using SuleymaniyeCalendar.Resources.Strings;

namespace SuleymaniyeCalendar.ViewModels;

/// <summary>
/// Base ViewModel providing common functionality for all ViewModels.
/// Includes busy state, font scaling, toast/alert helpers, and accessibility support.
/// </summary>
public partial class BaseViewModel : ObservableObject
{
    #region Constants

    private const int DefaultFontSize = 14;
    private const int MinFontSize = 12;
    private const int MaxFontSize = 28;

    /// <summary>Material Design 3 typography scale multipliers.</summary>
    private static readonly Dictionary<string, double> FontScaleMultipliers = new()
    {
        ["DisplayFontSize"] = 2.0,
        ["DisplaySmallFontSize"] = 1.7,
        ["TitleFontSize"] = 1.57,
        ["TitleMediumFontSize"] = 1.43,
        ["TitleSmallFontSize"] = 1.29,
        ["HeaderFontSize"] = 1.35,
        ["SubHeaderFontSize"] = 1.2,
        ["BodyLargeFontSize"] = 1.14,
        ["BodyFontSize"] = 1.05,
        ["BodySmallFontSize"] = 1.0,
        ["CaptionFontSize"] = 0.86,
        ["IconSmallFontSize"] = 1.1,
        ["IconMediumFontSize"] = 1.25,
        ["IconLargeFontSize"] = 1.6,
        ["IconLargerFontSize"] = 2.6,
        ["IconXLFontSize"] = 3.6,
        ["PlayButtonContainerSize"] = 4.0,
        ["PlayButtonCornerRadius"] = 2.0
    };

    #endregion

    #region Core Properties

    private string _title = string.Empty;
    /// <summary>Page title for Shell navigation.</summary>
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    private bool _isBusy;
    /// <summary>Indicates background work is in progress.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
                OnPropertyChanged(nameof(IsNotBusy));
        }
    }

    /// <summary>Inverse of IsBusy for convenient binding.</summary>
    public bool IsNotBusy => !IsBusy;

    private bool _showOverlay;
    /// <summary>Shows a modal overlay for long-running operations.</summary>
    public bool ShowOverlay
    {
        get => _showOverlay;
        set => SetProperty(ref _showOverlay, value);
    }

    private string _overlayMessage;
    /// <summary>Message displayed on the overlay.</summary>
    public string OverlayMessage
    {
        get => _overlayMessage;
        set => SetProperty(ref _overlayMessage, value);
    }

    #endregion

    #region Font Scaling

    private int _fontSize = DefaultFontSize;

    /// <summary>
    /// Base font size for the app. Triggers Material Design 3 typography scale update.
    /// </summary>
    public int FontSize
    {
        get => _fontSize;
        set
        {
            var clampedValue = Math.Clamp(value, MinFontSize, MaxFontSize);
            if (SetProperty(ref _fontSize, clampedValue))
            {
                Preferences.Set("FontSize", clampedValue);
                NotifyFontSizeProperties();
                ApplyFontScaleToResources(clampedValue);
            }
        }
    }

    // Computed font size properties for data binding
    public int HeaderFontSize => (int)(FontSize * 1.35);
    public int SubHeaderFontSize => (int)(FontSize * 1.2);
    public int TitleSmallFontSize => (int)(FontSize * 1.29);
    public int BodyLargeFontSize => (int)(FontSize * 1.14);
    public int CaptionFontSize => (int)(FontSize * 0.86);
    public int BodyFontSize => (int)(FontSize * 1.05);

    private void NotifyFontSizeProperties()
    {
        OnPropertyChanged(nameof(HeaderFontSize));
        OnPropertyChanged(nameof(SubHeaderFontSize));
        OnPropertyChanged(nameof(TitleSmallFontSize));
        OnPropertyChanged(nameof(BodyLargeFontSize));
        OnPropertyChanged(nameof(CaptionFontSize));
        OnPropertyChanged(nameof(BodyFontSize));
    }

    /// <summary>
    /// Applies the font scale to application resources for DynamicResource bindings.
    /// </summary>
    private static void ApplyFontScaleToResources(int baseFontSize)
    {
        var resources = Application.Current?.Resources;
        if (resources is null) return;

        resources["DefaultFontSize"] = (double)baseFontSize;
        resources["FontScale"] = baseFontSize / (double)DefaultFontSize;

        foreach (var (key, multiplier) in FontScaleMultipliers)
        {
            resources[key] = Math.Round(baseFontSize * multiplier);
        }
    }

    /// <summary>
    /// Initializes font scaling from saved preferences. Call at app startup.
    /// </summary>
    public static void InitializeFontSize()
    {
        var savedFontSize = Preferences.Get("FontSize", DefaultFontSize);
        var clampedValue = Math.Clamp(savedFontSize, MinFontSize, MaxFontSize);
        ApplyFontScaleToResources(clampedValue);
    }

    #endregion

    #region Static Helpers

    /// <summary>
    /// Shows a toast notification.
    /// </summary>
    public static void ShowToast(string message)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            using var cts = new CancellationTokenSource();
            var toast = Toast.Make(message, ToastDuration.Long, DefaultFontSize);
            await toast.Show(cts.Token);
        });
    }

    /// <summary>
    /// Shows an alert dialog.
    /// </summary>
    public static void Alert(string title, string message)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Shell.Current.DisplayAlert(title, message, AppResources.Tamam);
        });
    }

    /// <summary>
    /// Checks if VoiceOver (iOS) or TalkBack (Android) is running.
    /// </summary>
    public static bool IsVoiceOverRunning()
    {
#if __IOS__
        if (DeviceInfo.Platform == DevicePlatform.iOS)
            return Platforms.iOS.AccessibilityHelper.IsVoiceOverRunning();
#endif
        return false;
    }

    /// <summary>
    /// Checks if any accessibility feature is active (iOS only).
    /// </summary>
    public static bool IsAnyAccessibilityFeatureRunning()
    {
#if __IOS__
        if (DeviceInfo.Platform == DevicePlatform.iOS)
            return Platforms.iOS.AccessibilityHelper.IsAnyAccessibilityFeatureRunning();
#endif
        return false;
    }

    #endregion

    public BaseViewModel()
    {
        // Load saved font size
        var savedFontSize = Preferences.Get("FontSize", DefaultFontSize);
        _fontSize = Math.Clamp(savedFontSize, MinFontSize, MaxFontSize);
    }
}
