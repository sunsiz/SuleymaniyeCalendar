using System.Diagnostics;
using SuleymaniyeCalendar.Helpers;

namespace SuleymaniyeCalendar.Services;

/// <summary>
/// Implements right-to-left (RTL) language support.
/// </summary>
public sealed class RtlService : IRtlService
{
    /// <inheritdoc/>
    public bool IsRightToLeft => AppConstants.IsRtlLanguage(Preferences.Get("SelectedLanguage", "tr"));

    /// <inheritdoc/>
    public bool IsRtlLanguage(string languageCode) => AppConstants.IsRtlLanguage(languageCode);

    /// <inheritdoc/>
    public FlowDirection GetFlowDirection(string languageCode)
    {
        return AppConstants.IsRtlLanguage(languageCode) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    }

    /// <inheritdoc/>
    public void ApplyFlowDirection(string languageCode)
    {
        var flowDirection = GetFlowDirection(languageCode);
        Debug.WriteLine($"🔄 RtlService.ApplyFlowDirection: lang={languageCode}, direction={flowDirection}");

        if (Application.Current is null)
        {
            Debug.WriteLine("🔄 RtlService: Application.Current is null");
            return;
        }

        // Update global resource for XAML DynamicResource bindings
        Application.Current.Resources["FlowDirection"] = flowDirection;
        Debug.WriteLine($"🔄 RtlService: Updated DynamicResource FlowDirection to {flowDirection}");

        // Update the main window's page (Shell) directly
        var mainWindow = Application.Current.Windows?.FirstOrDefault();
        if (mainWindow?.Page is Shell shell)
        {
            // Update Shell FlowDirection
            shell.FlowDirection = flowDirection;
            Debug.WriteLine($"🔄 RtlService: Set Shell.FlowDirection to {flowDirection}");
            
            // Update the current page if available
            if (shell.CurrentPage is not null)
            {
                var currentPageName = shell.CurrentPage.GetType().Name;
                Debug.WriteLine($"🔄 RtlService: CurrentPage is {currentPageName}, FlowDirection was {shell.CurrentPage.FlowDirection}");
                shell.CurrentPage.FlowDirection = flowDirection;
                Debug.WriteLine($"🔄 RtlService: Set {currentPageName}.FlowDirection to {flowDirection}");
                
                // Also set Content FlowDirection if it's a ContentPage
                if (shell.CurrentPage is ContentPage contentPage && contentPage.Content is VisualElement content)
                {
                    content.FlowDirection = flowDirection;
                    Debug.WriteLine($"🔄 RtlService: Set {currentPageName}.Content.FlowDirection to {flowDirection}");
                }
            }
        }
        else if (mainWindow?.Page is not null)
        {
            // Non-Shell page
            mainWindow.Page.FlowDirection = flowDirection;
            Debug.WriteLine($"🔄 RtlService: Set mainWindow.Page.FlowDirection to {flowDirection}");
        }
    }
}
