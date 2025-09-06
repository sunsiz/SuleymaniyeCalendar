using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using SuleymaniyeCalendar.Services;
using SuleymaniyeCalendar.Models;
using System.Threading.Tasks;
using System;

namespace SuleymaniyeCalendar.Tests
{
    [TestClass]
    public class EnhancedServicesTests
    {
        private Mock<ILogger<AccessibilityService>> _accessibilityLoggerMock;
        private AccessibilityService _accessibilityService;

        [TestInitialize]
        public void Setup()
        {
            _accessibilityLoggerMock = new Mock<ILogger<AccessibilityService>>();
            _accessibilityService = new AccessibilityService(_accessibilityLoggerMock.Object);
        }

        #region AccessibilityService Tests - Core Functionality

        [TestMethod]
        public void AccessibilityService_AnnouncePrayerTime_NoException()
        {
            // Act & Assert
            FluentActions.Invoking(() => _accessibilityService.AnnouncePrayerTime("Fajr", "05:30", "30 minutes"))
                          .Should().NotThrow();
        }

        [TestMethod]
        public void AccessibilityService_AnnounceNavigation_NoException()
        {
            // Act & Assert
            FluentActions.Invoking(() => _accessibilityService.AnnounceNavigation("Prayer Times", "Current prayer schedule"))
                          .Should().NotThrow();
        }

        [TestMethod]
        public void AccessibilityService_GetAccessibilityFontScale_ReturnsValidValue()
        {
            // Act
            var fontScale = _accessibilityService.GetAccessibilityFontScale();

            // Assert
            fontScale.Should().BeGreaterThan(0);
            fontScale.Should().BeLessOrEqualTo(32); // Reasonable maximum
        }

        [TestMethod]
        public void AccessibilityService_IsScreenReaderActive_ReturnsBool()
        {
            // Act
            var isActive = _accessibilityService.IsScreenReaderActive();

            // Assert - verify it returns a boolean value
            Assert.IsNotNull(isActive);
            Assert.IsInstanceOfType(isActive, typeof(bool));
        }

        [TestMethod]
        public async Task AccessibilityService_ProvideHapticFeedback_NoException()
        {
            // Act & Assert - test different haptic feedback types
            await FluentActions.Invoking(async () => await _accessibilityService.ProvideHapticFeedbackAsync("notification"))
                               .Should().NotThrowAsync();

            await FluentActions.Invoking(async () => await _accessibilityService.ProvideHapticFeedbackAsync("success"))
                               .Should().NotThrowAsync();

            await FluentActions.Invoking(async () => await _accessibilityService.ProvideHapticFeedbackAsync("error"))
                               .Should().NotThrowAsync();
        }

        [TestMethod]
        public void AccessibilityService_IsHighContrastMode_ReturnsValidResult()
        {
            // Act
            var isHighContrast = _accessibilityService.IsHighContrastMode();

            // Assert
            Assert.IsNotNull(isHighContrast);
            Assert.IsInstanceOfType(isHighContrast, typeof(bool));
        }

        #endregion

        #region AccessibilityService Tests - Edge Cases

        [TestMethod]
        public void AccessibilityService_EmptyPrayerName_HandledGracefully()
        {
            // Act & Assert
            FluentActions.Invoking(() => _accessibilityService.AnnouncePrayerTime("", "05:30", "30 minutes"))
                          .Should().NotThrow();
        }

        [TestMethod]
        public void AccessibilityService_NullTimeRemaining_HandledGracefully()
        {
            // Act & Assert
            FluentActions.Invoking(() => _accessibilityService.AnnouncePrayerTime("Fajr", "05:30", null))
                          .Should().NotThrow();
        }

        [TestMethod]
        public async Task AccessibilityService_InvalidHapticType_HandledGracefully()
        {
            // Act & Assert
            await FluentActions.Invoking(async () => await _accessibilityService.ProvideHapticFeedbackAsync("invalid_type"))
                               .Should().NotThrowAsync();
        }

        [TestMethod]
        public void AccessibilityService_VeryLongPrayerName_HandledGracefully()
        {
            // Arrange
            var longPrayerName = new string('A', 1000); // Very long prayer name
            
            // Act & Assert
            FluentActions.Invoking(() => _accessibilityService.AnnouncePrayerTime(longPrayerName, "05:30", "30 minutes"))
                          .Should().NotThrow("Should handle very long prayer names gracefully");
        }

        [TestMethod]
        public void AccessibilityService_SpecialCharacters_HandledGracefully()
        {
            // Act & Assert
            FluentActions.Invoking(() => _accessibilityService.AnnouncePrayerTime("Fajr🌅", "05:30⏰", "30 minutes⏳"))
                          .Should().NotThrow("Should handle special characters and emojis");
        }

        [TestMethod]
        public void AccessibilityService_WhitespaceInputs_HandledGracefully()
        {
            // Act & Assert
            FluentActions.Invoking(() => _accessibilityService.AnnouncePrayerTime("   ", "   ", "   "))
                          .Should().NotThrow("Should handle whitespace inputs");

            FluentActions.Invoking(() => _accessibilityService.AnnounceNavigation("", null))
                          .Should().NotThrow("Should handle empty/null navigation inputs");
        }

        #endregion

        #region Integration & Performance Tests

        [TestMethod]
        public void IntegrationTest_AccessibilityService_AllFeaturesWorkTogether()
        {
            // Test all accessibility features work together without throwing
            FluentActions.Invoking(() =>
            {
                // Test announcements
                _accessibilityService.AnnouncePrayerTime("Fajr", "05:30", "30 minutes");
                _accessibilityService.AnnounceNavigation("Main Page", "Prayer times for today");

                // Test property checks
                var fontScale = _accessibilityService.GetAccessibilityFontScale();
                var isScreenReader = _accessibilityService.IsScreenReaderActive();
                var isHighContrast = _accessibilityService.IsHighContrastMode();

                // Validate results are within expected ranges
                fontScale.Should().BeGreaterThan(0);
                Assert.IsInstanceOfType(isScreenReader, typeof(bool));
                Assert.IsInstanceOfType(isHighContrast, typeof(bool));

            }).Should().NotThrow();
        }

        [TestMethod]
        public async Task IntegrationTest_AccessibilityService_HapticFeedbackSequence()
        {
            // Test all haptic feedback types work in sequence
            await FluentActions.Invoking(async () =>
            {
                await _accessibilityService.ProvideHapticFeedbackAsync("notification");
                await Task.Delay(50); // Small delay between feedback
                
                await _accessibilityService.ProvideHapticFeedbackAsync("success");
                await Task.Delay(50);
                
                await _accessibilityService.ProvideHapticFeedbackAsync("error");
                await Task.Delay(300); // Error feedback has 200ms internal delay
                
            }).Should().NotThrowAsync();
        }

        [TestMethod]
        public void PerformanceTest_AccessibilityService_FontScaleConsistency()
        {
            // Test font scale is consistent and performs well
            var fontScale1 = _accessibilityService.GetAccessibilityFontScale();
            var fontScale2 = _accessibilityService.GetAccessibilityFontScale();
            var fontScale3 = _accessibilityService.GetAccessibilityFontScale();

            // Assert - should be consistent across multiple calls
            fontScale1.Should().Be(fontScale2);
            fontScale2.Should().Be(fontScale3);
        }

        [TestMethod]
        public void ReliabilityTest_AccessibilityService_MultipleAnnouncements()
        {
            // Test making multiple announcements in quick succession
            FluentActions.Invoking(() =>
            {
                for (int i = 0; i < 10; i++)
                {
                    _accessibilityService.AnnouncePrayerTime($"Prayer{i}", $"0{i}:30", $"{i * 5} minutes");
                }
                
                for (int i = 0; i < 5; i++)
                {
                    _accessibilityService.AnnounceNavigation($"Page{i}", $"Description {i}");
                }
                
            }).Should().NotThrow("Multiple rapid announcements should be handled gracefully");
        }

        #endregion
    }
}
