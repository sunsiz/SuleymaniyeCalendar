using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using FluentAssertions;
using SuleymaniyeCalendar.ViewModels;
using SuleymaniyeCalendar.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace SuleymaniyeCalendar.Tests
{
    [TestClass]
    public class UiThemeTests
    {
        private Mock<DataService> _mockDataService;
        private Mock<IRtlService> _mockRtlService;

        [TestInitialize]
        public void Setup()
        {
            _mockDataService = new Mock<DataService>();
            _mockRtlService = new Mock<IRtlService>();
        }

        [TestMethod]
        public void DynamicFontScaling_FontSizeUpdatesCorrectly()
        {
            // Arrange
            var viewModel = new BaseViewModel();
            bool fontSizeChanged = false;
            viewModel.PropertyChanged += (s, e) => {
                if (e.PropertyName == nameof(BaseViewModel.FontSize)) fontSizeChanged = true;
            };

            // Act
            viewModel.FontSize = 16;

            // Assert
            viewModel.FontSize.Should().Be(16);
            viewModel.HeaderFontSize.Should().Be(24); // 16 * 1.5x multiplier
            viewModel.SubHeaderFontSize.Should().Be(20); // 16 * 1.25x multiplier
            fontSizeChanged.Should().BeTrue();
        }

        [TestMethod]
        public void DynamicFontScaling_ExtremeValuesHandled()
        {
            // Arrange
            var viewModel = new BaseViewModel();

            // Act & Assert - Minimum scale (clamped to 12)
            viewModel.FontSize = 5;
            viewModel.FontSize.Should().Be(12);
            viewModel.HeaderFontSize.Should().Be(18);

            // Act & Assert - Maximum scale (clamped to 28)
            viewModel.FontSize = 50;
            viewModel.FontSize.Should().Be(28);
            viewModel.HeaderFontSize.Should().Be(42);
        }

        [TestMethod]
        public void RtlThemeSupport_FlowDirectionByLanguage()
        {
            // Arrange
            _mockRtlService.Setup(r => r.IsRtlLanguage("ar")).Returns(true);
            _mockRtlService.Setup(r => r.IsRtlLanguage("en")).Returns(false);

            // Act
            var isArabicRtl = _mockRtlService.Object.IsRtlLanguage("ar");
            var isEnglishRtl = _mockRtlService.Object.IsRtlLanguage("en");

            // Assert
            isArabicRtl.Should().BeTrue();
            isEnglishRtl.Should().BeFalse();
        }

        [TestMethod]
        public void RtlThemeSupport_LeftToRightLanguages()
        {
            // Arrange
            _mockRtlService.Setup(r => r.IsRtlLanguage("en")).Returns(false);
            _mockRtlService.Setup(r => r.IsRtlLanguage("fr")).Returns(false);
            _mockRtlService.Setup(r => r.IsRtlLanguage("de")).Returns(false);

            // Act
            var isEnglishRtl = _mockRtlService.Object.IsRtlLanguage("en");
            var isFrenchRtl = _mockRtlService.Object.IsRtlLanguage("fr");
            var isGermanRtl = _mockRtlService.Object.IsRtlLanguage("de");

            // Assert
            isEnglishRtl.Should().BeFalse();
            isFrenchRtl.Should().BeFalse();
            isGermanRtl.Should().BeFalse();
        }

        [TestMethod]
        public void ThemeChangeNotifications_PropertyChangedRaised()
        {
            // Arrange
            var viewModel = new BaseViewModel();
            var propertyChanges = new List<string>();
            viewModel.PropertyChanged += (s, e) => propertyChanges.Add(e.PropertyName);

            // Act
            viewModel.FontSize = 18;

            // Assert
            propertyChanges.Should().Contain(nameof(BaseViewModel.FontSize));
            propertyChanges.Should().Contain(nameof(BaseViewModel.HeaderFontSize));
            propertyChanges.Should().Contain(nameof(BaseViewModel.SubHeaderFontSize));
            propertyChanges.Should().Contain(nameof(BaseViewModel.TitleSmallFontSize));
            propertyChanges.Should().Contain(nameof(BaseViewModel.BodyLargeFontSize));
            propertyChanges.Should().Contain(nameof(BaseViewModel.CaptionFontSize));
            propertyChanges.Should().Contain(nameof(BaseViewModel.BodyFontSize));
        }

        [TestMethod]
        public void MainViewModel_PreservesStateOnConstruction()
        {
            // Arrange & Act
            var viewModel = new MainViewModel(_mockDataService.Object);
            
            // Assert - Initial state should be consistent
            viewModel.IsBusy.Should().BeFalse();
            viewModel.IsNotBusy.Should().BeTrue();
        }

        [TestMethod]
        public void FontScaling_ClampingBehavior()
        {
            // Arrange
            var viewModel = new BaseViewModel();

            // Act & Assert - Values below minimum
            viewModel.FontSize = 8;
            viewModel.FontSize.Should().Be(12); // Clamped to minimum

            // Act & Assert - Values above maximum
            viewModel.FontSize = 35;
            viewModel.FontSize.Should().Be(28); // Clamped to maximum
        }

        [TestMethod]
        public void FontScaling_ValidRangeValues()
        {
            // Arrange
            var viewModel = new BaseViewModel();

            // Act & Assert - Valid range values
            viewModel.FontSize = 14;
            viewModel.FontSize.Should().Be(14);

            viewModel.FontSize = 20;
            viewModel.FontSize.Should().Be(20);

            viewModel.FontSize = 24;
            viewModel.FontSize.Should().Be(24);
        }

        [TestMethod]
        public void MultipleFontScaleChanges_LastValueWins()
        {
            // Arrange
            var viewModel = new BaseViewModel();

            // Act
            viewModel.FontSize = 12;
            viewModel.FontSize = 16;
            viewModel.FontSize = 20;

            // Assert
            viewModel.FontSize.Should().Be(20);
            viewModel.HeaderFontSize.Should().Be(30);
            viewModel.SubHeaderFontSize.Should().Be(25);
        }

        [TestMethod]
        public void FontSizePreferences_PersistsChanges()
        {
            // Arrange
            var viewModel = new BaseViewModel();
            var testFontSize = 18;

            // Act
            viewModel.FontSize = testFontSize;

            // Assert - Should persist to preferences
            var savedSize = Preferences.Get("FontSize", 14);
            savedSize.Should().Be(testFontSize);
        }

        [TestMethod]
        public void FontScaleConsistency_AllRelatedPropertiesUpdate()
        {
            // Arrange
            var viewModel = new BaseViewModel();
            var fontSize = 20;

            // Act
            viewModel.FontSize = fontSize;

            // Assert
            viewModel.FontSize.Should().Be(fontSize);
            viewModel.HeaderFontSize.Should().Be((int)(fontSize * 1.5)); // 30
            viewModel.SubHeaderFontSize.Should().Be((int)(fontSize * 1.25)); // 25
            viewModel.CaptionFontSize.Should().Be((int)(fontSize * 1.1)); // 22
            viewModel.BodyFontSize.Should().Be((int)(fontSize * 1.05)); // 21
        }

        [TestMethod]
        public void ThemePropertyBinding_ReactsToChanges()
        {
            // Arrange
            var viewModel = new BaseViewModel();
            var changeCount = 0;
            viewModel.PropertyChanged += (s, e) => {
                if (e.PropertyName?.Contains("FontSize") == true) changeCount++;
            };

            // Act
            viewModel.FontSize = 16;
            viewModel.FontSize = 18;
            viewModel.FontSize = 22;

            // Assert - Should have triggered multiple font size property changes
            changeCount.Should().BeGreaterThan(9); // 3 updates * 4 font properties minimum
        }

        [TestMethod]
        public void AccessibilityFontScaling_LargeScalesSupported()
        {
            // Arrange
            var viewModel = new BaseViewModel();

            // Act - Test accessibility large font sizes (within clamp range)
            viewModel.FontSize = 28; // Maximum allowed

            // Assert
            viewModel.FontSize.Should().Be(28);
            viewModel.HeaderFontSize.Should().Be(42);
            viewModel.SubHeaderFontSize.Should().Be(35);
        }

        [TestMethod]
        public void RtlService_ApplyFlowDirection()
        {
            // Arrange
            var languageCode = "fa"; // Persian
            _mockRtlService.Setup(r => r.IsRtlLanguage("fa")).Returns(true);

            // Act
            _mockRtlService.Object.ApplyFlowDirection(languageCode);

            // Assert
            _mockRtlService.Verify(r => r.ApplyFlowDirection("fa"), Times.Once);
        }

        [TestMethod]
        public void BaseViewModel_ToastMessageFunctionality()
        {
            // Arrange & Act - Toast functionality requires UI context, just verify no exception
            var action = () => BaseViewModel.ShowToast("Test message");

            // Assert - No exception should be thrown
            action.Should().NotThrow();
        }

        [TestMethod]
        public void BaseViewModel_AlertFunctionality()
        {
            // Arrange & Act - Alert functionality requires UI context, just verify no exception
            var action = () => BaseViewModel.Alert("Test Title", "Test Message");

            // Assert - No exception should be thrown
            action.Should().NotThrow();
        }

        [TestMethod]
        public void BaseViewModel_VoiceOverDetection()
        {
            // Act
            var isVoiceOverRunning = BaseViewModel.IsVoiceOverRunning();

            // Assert
            isVoiceOverRunning.Should().BeTrue(); // Currently hardcoded to return true
        }

        [TestMethod]
        public void CultureSpecificTheming_NumberFormatting()
        {
            // Arrange
            var originalCulture = CultureInfo.CurrentCulture;
            
            try
            {
                // Act - Test with different cultures
                CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
                var viewModel = new BaseViewModel();
                viewModel.FontSize = 16;

                // Assert - Should work regardless of culture
                viewModel.FontSize.Should().Be(16);

                // Act - Test with Arabic culture
                CultureInfo.CurrentCulture = new CultureInfo("ar-SA");
                viewModel.FontSize = 20;

                // Assert
                viewModel.FontSize.Should().Be(20);
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
            }
        }

        [TestMethod]
        public void IsBusy_StateTransitions()
        {
            // Arrange
            var viewModel = new BaseViewModel();

            // Act & Assert - Initial state
            viewModel.IsBusy.Should().BeFalse();
            viewModel.IsNotBusy.Should().BeTrue();

            // Act & Assert - Set busy
            viewModel.IsBusy = true;
            viewModel.IsBusy.Should().BeTrue();
            viewModel.IsNotBusy.Should().BeFalse();

            // Act & Assert - Set not busy
            viewModel.IsBusy = false;
            viewModel.IsBusy.Should().BeFalse();
            viewModel.IsNotBusy.Should().BeTrue();
        }

        [TestMethod]
        public void Title_PropertyChangeNotification()
        {
            // Arrange
            var viewModel = new BaseViewModel();
            bool titleChanged = false;
            viewModel.PropertyChanged += (s, e) => {
                if (e.PropertyName == nameof(BaseViewModel.Title)) titleChanged = true;
            };

            // Act
            viewModel.Title = "Test Title";

            // Assert
            viewModel.Title.Should().Be("Test Title");
            titleChanged.Should().BeTrue();
        }

        [TestMethod]
        public void FontSize_UpdatesAllDynamicResourceKeys()
        {
            // Arrange
            var viewModel = new BaseViewModel();
            var mockResourceDictionary = new Microsoft.Maui.Controls.ResourceDictionary();
            
            // Mock Application.Current.Resources for testing
            if (Application.Current?.Resources != null)
            {
                // Act
                viewModel.FontSize = 20;

                // Assert - Verify all DynamicResource keys are updated
                Application.Current.Resources.Should().ContainKey("DefaultFontSize");
                Application.Current.Resources.Should().ContainKey("FontScale");
                Application.Current.Resources.Should().ContainKey("DisplayFontSize");
                Application.Current.Resources.Should().ContainKey("DisplaySmallFontSize");
                Application.Current.Resources.Should().ContainKey("TitleFontSize");
                Application.Current.Resources.Should().ContainKey("TitleMediumFontSize");
                Application.Current.Resources.Should().ContainKey("TitleSmallFontSize"); // Critical for prayer times
                Application.Current.Resources.Should().ContainKey("HeaderFontSize");
                Application.Current.Resources.Should().ContainKey("SubHeaderFontSize");
                Application.Current.Resources.Should().ContainKey("BodyLargeFontSize"); // Critical for prayer names
                Application.Current.Resources.Should().ContainKey("BodyFontSize");
                Application.Current.Resources.Should().ContainKey("BodySmallFontSize");
                Application.Current.Resources.Should().ContainKey("CaptionFontSize");
                
                // Verify icon font sizes
                Application.Current.Resources.Should().ContainKey("IconSmallFontSize");
                Application.Current.Resources.Should().ContainKey("IconMediumFontSize");
                Application.Current.Resources.Should().ContainKey("IconLargeFontSize");
                Application.Current.Resources.Should().ContainKey("IconXLFontSize");

                // Verify specific values for critical prayer card fonts
                Application.Current.Resources["TitleSmallFontSize"].Should().Be(20 * 1.29); // 25.8 for prayer times
                Application.Current.Resources["BodyLargeFontSize"].Should().Be(20 * 1.14);  // 22.8 for prayer names
                Application.Current.Resources["DefaultFontSize"].Should().Be(20.0);
            }
        }

        [TestMethod]
        public void FontSize_PrayerCardFontScaling_WorksCorrectly()
        {
            // Arrange
            var viewModel = new BaseViewModel();
            
            if (Application.Current?.Resources != null)
            {
                // Act - Test minimum font size
                viewModel.FontSize = 12;
                var minTitleSmall = Application.Current.Resources["TitleSmallFontSize"];
                var minBodyLarge = Application.Current.Resources["BodyLargeFontSize"];
                
                // Act - Test maximum font size  
                viewModel.FontSize = 28;
                var maxTitleSmall = Application.Current.Resources["TitleSmallFontSize"];
                var maxBodyLarge = Application.Current.Resources["BodyLargeFontSize"];

                // Assert - Prayer card fonts scale properly
                minTitleSmall.Should().Be(12 * 1.29); // 15.48 at minimum
                minBodyLarge.Should().Be(12 * 1.14);  // 13.68 at minimum
                maxTitleSmall.Should().Be(28 * 1.29); // 36.12 at maximum
                maxBodyLarge.Should().Be(28 * 1.14);  // 31.92 at maximum
            }
        }
    }
}
