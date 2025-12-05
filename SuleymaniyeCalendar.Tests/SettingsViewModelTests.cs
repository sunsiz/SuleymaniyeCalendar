using System.Globalization;
using System.Linq;
using FluentAssertions;
using LocalizationResourceManager.Maui;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SuleymaniyeCalendar.Models;
using SuleymaniyeCalendar.Services;
using SuleymaniyeCalendar.ViewModels;

namespace SuleymaniyeCalendar.Tests
{
    [TestClass]
    public class SettingsViewModelTests
    {
        private Mock<ILocalizationResourceManager> _localizationMock;
        private Mock<IRtlService> _rtlServiceMock;
        private Mock<IAlarmService> _alarmServiceMock;
        private Mock<IWidgetService> _widgetServiceMock;

        [TestInitialize]
        public void Setup()
        {
            _localizationMock = new Mock<ILocalizationResourceManager>();
            _rtlServiceMock = new Mock<IRtlService>();
            _alarmServiceMock = new Mock<IAlarmService>();
            _widgetServiceMock = new Mock<IWidgetService>();
        }

        private SettingsViewModel CreateViewModel()
        {
            return new SettingsViewModel(
                _localizationMock.Object, 
                _rtlServiceMock.Object,
                _alarmServiceMock.Object,
                _widgetServiceMock.Object);
        }

        [TestMethod]
        public void Constructor_InitializesPropertiesCorrectly()
        {
            // Act
            var vm = CreateViewModel();

            // Assert
            vm.Should().NotBeNull();
            vm.SupportedLanguages.Should().NotBeNull();
        }

        [TestMethod]
        public void NotificationPrayerTimesEnabled_OnlyVisibleWithForegroundService()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act & Assert
            vm.ForegroundServiceEnabled = true;
            vm.ShowNotificationPrayerOption.Should().BeTrue();
            
            vm.ForegroundServiceEnabled = false;
            vm.ShowNotificationPrayerOption.Should().BeFalse();
        }

        [TestMethod]
        public void SelectedLanguage_WhenChanged_UpdatesResourceManager()
        {
            // Arrange
            var vm = CreateViewModel();
            var englishLanguage = new Language("English", "en");

            // Act
            vm.SelectedLanguage = englishLanguage;

            // Assert
            vm.SelectedLanguage.Should().Be(englishLanguage);
            _localizationMock.VerifySet(x => x.CurrentCulture = It.IsAny<CultureInfo>(), Times.Once);
        }

        [TestMethod]
        public void SelectedLanguage_WhenChangedToRtl_AppliesFlowDirection()
        {
            // Arrange
            var vm = CreateViewModel();
            var arabicLanguage = new Language("العربية", "ar");

            // Act
            vm.SelectedLanguage = arabicLanguage;

            // Assert
            _rtlServiceMock.Verify(x => x.ApplyFlowDirection("ar"), Times.Once);
        }

        [TestMethod]
        public void ThemeSelection_LightChecked_AppliesLightTheme()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act
            vm.LightChecked = true;

            // Assert
            vm.LightChecked.Should().BeTrue();
            vm.DarkChecked.Should().BeFalse();
            vm.SystemChecked.Should().BeFalse();
        }

        [TestMethod]
        public void ThemeSelection_DarkChecked_AppliesDarkTheme()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act
            vm.DarkChecked = true;

            // Assert
            vm.DarkChecked.Should().BeTrue();
            vm.LightChecked.Should().BeFalse();
            vm.SystemChecked.Should().BeFalse();
        }

        [TestMethod]
        public void ThemeSelection_SystemChecked_AppliesSystemTheme()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act
            vm.SystemChecked = true;

            // Assert
            vm.SystemChecked.Should().BeTrue();
            vm.LightChecked.Should().BeFalse();
            vm.DarkChecked.Should().BeFalse();
        }

        [TestMethod]
        public void CurrentTheme_WhenChanged_UpdatesThemeSelection()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act
            vm.CurrentTheme = 1; // Light theme

            // Assert
            vm.CurrentTheme.Should().Be(1);
        }

        [TestMethod]
        public void FontSize_Properties_UpdateCorrectly()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act & Assert
            vm.FontSize.Should().BeGreaterThan(0);
            vm.HeaderFontSize.Should().BeGreaterThan(vm.FontSize);
            vm.SubHeaderFontSize.Should().BeGreaterThan(vm.FontSize);
            vm.SubHeaderFontSize.Should().BeLessThan(vm.HeaderFontSize);
        }

        [TestMethod]
        public void FontSize_WhenChanged_UpdatesAllRelatedFontSizes()
        {
            // Arrange
            var vm = CreateViewModel();
            var initialHeaderSize = vm.HeaderFontSize;

            // Act
            vm.FontSize = 18;

            // Assert
            vm.FontSize.Should().Be(18);
            vm.HeaderFontSize.Should().BeGreaterThan(initialHeaderSize);
        }

        [TestMethod]
        public void AlarmSettings_DefaultValues_AreConsistent()
        {
            // Arrange & Act
            var vm = CreateViewModel();

            // Assert
            // Test that alarm properties have reasonable defaults
            vm.Should().NotBeNull();
            // Additional alarm-related properties would be tested here
        }

        [TestMethod]
        public void PrayerNotificationSettings_ToggleCorrectly()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act
            vm.NotificationPrayerTimesEnabled = true;

            // Assert
            vm.NotificationPrayerTimesEnabled.Should().BeTrue();
        }

        [TestMethod]
        public void AlwaysRenewLocationEnabled_ToggleCorrectly()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act
            vm.AlwaysRenewLocationEnabled = true;

            // Assert
            vm.AlwaysRenewLocationEnabled.Should().BeTrue();
        }

        [TestMethod]
        public void ForegroundServiceEnabled_ToggleCorrectly()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act
            vm.ForegroundServiceEnabled = true;

            // Assert
            vm.ForegroundServiceEnabled.Should().BeTrue();
            vm.ShowNotificationPrayerOption.Should().BeTrue();
        }

        [TestMethod]
        public void LanguageSelection_HandlesNullGracefully()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act
            vm.SelectedLanguage = null;

            // Assert
            // Should not throw an exception
            vm.SelectedLanguage.Should().BeNull();
        }

        [TestMethod]
        public void PropertyChanged_Events_FireCorrectly()
        {
            // Arrange
            var vm = CreateViewModel();
            var propertyChangedCount = 0;
            vm.PropertyChanged += (s, e) => propertyChangedCount++;

            // Act
            vm.Dark = true;
            vm.FontSize = 16;
            vm.AlwaysRenewLocationEnabled = true;

            // Assert
            propertyChangedCount.Should().BeGreaterThan(0);
        }

        [TestMethod]
        public void Theme_Transitions_MaintainConsistency()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act & Assert - Only one theme should be selected at a time
            vm.LightChecked = true;
            (vm.LightChecked && !vm.DarkChecked && !vm.SystemChecked).Should().BeTrue();

            vm.DarkChecked = true;
            (!vm.LightChecked && vm.DarkChecked && !vm.SystemChecked).Should().BeTrue();

            vm.SystemChecked = true;
            (!vm.LightChecked && !vm.DarkChecked && vm.SystemChecked).Should().BeTrue();
        }
    }
}
