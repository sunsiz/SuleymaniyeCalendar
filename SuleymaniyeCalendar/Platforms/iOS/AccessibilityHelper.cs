using UIKit;
using System.Diagnostics;

namespace SuleymaniyeCalendar.Platforms.iOS;

/// <summary>
/// Helper to detect active accessibility features on iOS.
/// Includes VoiceOver, Switch Control, and other assistive technologies.
/// </summary>
public static class AccessibilityHelper
{
    /// <summary>
    /// Checks if VoiceOver is currently active.
    /// </summary>
    public static bool IsVoiceOverRunning()
    {
        try
        {
            var isEnabled = UIAccessibility.IsVoiceOverRunning;
            Debug.WriteLine($"VoiceOver running: {isEnabled}");
            return isEnabled;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ VoiceOver detection failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Checks if Switch Control is active.
    /// </summary>
    public static bool IsSwitchControlRunning()
    {
        try
        {
            var isEnabled = UIAccessibility.IsSwitchControlRunning;
            Debug.WriteLine($"Switch Control running: {isEnabled}");
            return isEnabled;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Switch Control detection failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Checks if Zoom is active.
    /// </summary>
    //public static bool IsZoomRunning()
    //{
    //    try
    //    {
    //        var isEnabled = UIAccessibility.IsZoomRunning;
    //        Debug.WriteLine($"Zoom running: {isEnabled}");
    //        return isEnabled;
    //    }
    //    catch (Exception ex)
    //    {
    //        Debug.WriteLine($"❌ Zoom detection failed: {ex.Message}");
    //        return false;
    //    }
    //}

    /// <summary>
    /// Checks if any accessibility feature is active.
    /// </summary>
    public static bool IsAnyAccessibilityFeatureRunning()
    {
        return IsVoiceOverRunning() ||
               IsSwitchControlRunning(); // || 
        //IsZoomRunning();
    }

    /// <summary>
    /// Gets a summary of active accessibility features.
    /// </summary>
    public static string GetActiveAccessibilityFeatures()
    {
        var features = new List<string>();
        
        if (IsVoiceOverRunning())
            features.Add("VoiceOver");
        if (IsSwitchControlRunning())
            features.Add("Switch Control");
        //if (IsZoomRunning())
        //    features.Add("Zoom");

        return features.Count > 0 
            ? string.Join(", ", features) 
            : "None";
    }
}