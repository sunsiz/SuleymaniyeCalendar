using Microsoft.Extensions.Logging;
using Microsoft.Maui.Accessibility;
using SuleymaniyeCalendar.ViewModels;

namespace SuleymaniyeCalendar.Services
{
    /// <summary>
    /// Enhanced accessibility service for prayer times app
    /// </summary>
    public class AccessibilityService
    {
        private readonly ILogger<AccessibilityService> _logger;

        public AccessibilityService(ILogger<AccessibilityService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Announce prayer time changes to screen readers
        /// </summary>
        public void AnnouncePrayerTime(string prayerName, string time, string? timeRemaining = null)
        {
            var announcement = $"{prayerName} prayer time: {time}";
            if (!string.IsNullOrEmpty(timeRemaining))
            {
                announcement += $". Time remaining: {timeRemaining}";
            }

            try
            {
                SemanticScreenReader.Announce(announcement);
                _logger?.LogInformation("Announced prayer time: {Prayer}", prayerName);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to announce prayer time");
            }
        }

        /// <summary>
        /// Announce navigation changes for better screen reader support
        /// </summary>
        public void AnnounceNavigation(string pageName, string? description = null)
        {
            var announcement = $"Navigated to {pageName}";
            if (!string.IsNullOrEmpty(description))
            {
                announcement += $". {description}";
            }

            try
            {
                SemanticScreenReader.Announce(announcement);
                _logger?.LogInformation("Announced navigation to: {Page}", pageName);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to announce navigation");
            }
        }

        /// <summary>
        /// Check if high contrast mode is enabled
        /// </summary>
        public bool IsHighContrastMode()
        {
            try
            {
                // Platform-specific high contrast detection would go here
                return false; // Default implementation
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to detect high contrast mode");
                return false;
            }
        }

        /// <summary>
        /// Get recommended font scaling based on accessibility preferences
        /// </summary>
        public double GetAccessibilityFontScale()
        {
            try
            {
                // Enhanced font scaling for accessibility
                var currentScale = Application.Current?.Resources.TryGetValue("DefaultFontSize", out var fontSize) == true
                    ? Convert.ToDouble(fontSize) : 14.0;

                // If VoiceOver/TalkBack is active, suggest larger scaling
                if (IsScreenReaderActive())
                {
                    return Math.Max(currentScale * 1.2, 16.0); // Minimum 16pt for screen readers
                }

                return currentScale;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to calculate accessibility font scale");
                return 14.0; // Safe default
            }
        }

        /// <summary>
        /// Check if screen reader is currently active
        /// </summary>
        public bool IsScreenReaderActive()
        {
            try
            {
                // Use the existing BaseViewModel method, but with error handling
                return BaseViewModel.IsVoiceOverRunning();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to detect screen reader status");
                return false;
            }
        }

        /// <summary>
        /// Provide haptic feedback for prayer time alerts (accessibility enhancement)
        /// </summary>
        public async Task ProvideHapticFeedbackAsync(string eventType = "notification")
        {
            try
            {
                switch (eventType.ToLower())
                {
                    case "notification":
                        HapticFeedback.Default.Perform(HapticFeedbackType.LongPress);
                        break;
                    case "success":
                        HapticFeedback.Default.Perform(HapticFeedbackType.Click);
                        break;
                    case "error":
                        // Double vibration for errors
                        HapticFeedback.Default.Perform(HapticFeedbackType.LongPress);
                        await Task.Delay(200);
                        HapticFeedback.Default.Perform(HapticFeedbackType.LongPress);
                        break;
                }
                
                _logger?.LogInformation("Provided haptic feedback: {EventType}", eventType);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to provide haptic feedback");
            }
        }
    }
}
