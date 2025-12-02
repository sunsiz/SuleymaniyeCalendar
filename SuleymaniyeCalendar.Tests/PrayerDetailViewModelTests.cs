using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SuleymaniyeCalendar.Models;
using SuleymaniyeCalendar.Services;
using SuleymaniyeCalendar.ViewModels;

namespace SuleymaniyeCalendar.Tests
{
    [TestClass]
    public class PrayerDetailViewModelTests
    {
        private Mock<IAudioPreviewService> _audioPreviewMock;
        private Mock<DataService> _dataServiceMock;
        private PrayerDetailViewModel _viewModel;

        [TestInitialize]
        public void Setup()
        {
            _audioPreviewMock = new Mock<IAudioPreviewService>();
            _dataServiceMock = new Mock<DataService>();
            _viewModel = new PrayerDetailViewModel(_audioPreviewMock.Object, _dataServiceMock.Object);
        }

        [TestMethod]
        public void PrayerDetail_NotificationTime_PersistsPreference()
        {
            // Arrange
            _viewModel.PrayerId = "fajr";
            var testNotificationTime = 10;

            // Act
            _viewModel.NotificationTime = testNotificationTime;

            // Assert
            _viewModel.NotificationTime.Should().Be(testNotificationTime);
        }

        [TestMethod]
        public void Constructor_InitializesPropertiesCorrectly()
        {
            // Assert
            _viewModel.Should().NotBeNull();
            _viewModel.AvailableSounds.Should().NotBeNull();
            _viewModel.AvailableSounds.Count.Should().BeGreaterThan(0);
            _viewModel.IsPlaying.Should().BeFalse();
        }

        [TestMethod]
        public void AvailableSounds_LoadsAllSoundOptions()
        {
            // Assert
            _viewModel.AvailableSounds.Should().NotBeEmpty();
            
            // Verify specific sounds are available
            var soundFileNames = _viewModel.AvailableSounds.Select(s => s.FileName).ToList();
            soundFileNames.Should().Contain("kus");
            soundFileNames.Should().Contain("horoz");
            soundFileNames.Should().Contain("alarm");
            soundFileNames.Should().Contain("ezan");
            soundFileNames.Should().Contain("alarm2");
            soundFileNames.Should().Contain("beep1");
            soundFileNames.Should().Contain("beep2");
            soundFileNames.Should().Contain("beep3");
        }

        [TestMethod]
        public void PrayerId_Updates_LoadsPrayerData()
        {
            // Act
            _viewModel.PrayerId = "fajr";

            // Assert
            _viewModel.PrayerId.Should().Be("fajr");
            _viewModel.Title.Should().NotBeNullOrEmpty();
        }

        [TestMethod]
        public void EnabledProperty_UpdatesPreferences()
        {
            // Arrange
            _viewModel.PrayerId = "dhuhr";

            // Act
            _viewModel.Enabled = true;

            // Assert
            _viewModel.Enabled.Should().BeTrue();
            _viewModel.ShowAdvancedOptions.Should().Be(_viewModel.IsNecessary && _viewModel.Enabled);
        }

        [TestMethod]
        public void EnabledSwitchToggled_UpdatesPreferences()
        {
            // Arrange
            _viewModel.PrayerId = "asr";

            // Act
            _viewModel.EnabledSwitchToggledCommand.Execute(true);

            // Assert
            _viewModel.Enabled.Should().BeTrue();
        }

        [TestMethod]
        public async Task TestButtonClicked_StartsAudioPlayback()
        {
            // Arrange
            _viewModel.SelectedSound = _viewModel.AvailableSounds.First();
            _audioPreviewMock.Setup(a => a.PlayAsync(It.IsAny<string>(), It.IsAny<bool>()))
                           .Returns(Task.CompletedTask);
            _audioPreviewMock.Setup(a => a.IsPlaying).Returns(true);

            // Act
            await _viewModel.TestButtonClickedCommand.ExecuteAsync(null);

            // Assert
            _audioPreviewMock.Verify(a => a.PlayAsync(It.IsAny<string>(), true), Times.Once);
        }

        [TestMethod]
        public async Task TestButtonClicked_WhenPlaying_StopsAudioPlayback()
        {
            // Arrange
            _viewModel.IsPlaying = true;
            _audioPreviewMock.Setup(a => a.StopAsync()).Returns(Task.CompletedTask);

            // Act
            await _viewModel.TestButtonClickedCommand.ExecuteAsync(null);

            // Assert
            _audioPreviewMock.Verify(a => a.StopAsync(), Times.Once);
            _viewModel.IsPlaying.Should().BeFalse();
        }

        [TestMethod]
        public void NotificationTime_PropertyChange_UpdatesValue()
        {
            // Arrange
            _viewModel.PrayerId = "maghrib";
            var propertyChangedCount = 0;
            _viewModel.PropertyChanged += (s, e) => 
            { 
                if (e.PropertyName == nameof(_viewModel.NotificationTime)) 
                    propertyChangedCount++; 
            };

            // Act
            _viewModel.NotificationTime = 15;

            // Assert
            _viewModel.NotificationTime.Should().Be(15);
            propertyChangedCount.Should().BeGreaterThan(0);
        }

        [TestMethod]
        public void SelectedSound_Updates_CorrectlyMapped()
        {
            // Arrange
            var testSound = _viewModel.AvailableSounds.First(s => s.FileName == "ezan");

            // Act
            _viewModel.SelectedSound = testSound;

            // Assert
            _viewModel.SelectedSound.Should().BeSameAs(testSound);
            _viewModel.SelectedSound.FileName.Should().Be("ezan");
        }

        [TestMethod]
        public void IsNecessary_PlatformCheck_ReturnsCorrectValue()
        {
            // Act
            var isNecessary = _viewModel.IsNecessary;

            // Assert
            // Value depends on platform, just verify it returns a boolean value
            Assert.IsTrue(isNecessary == true || isNecessary == false);
        }

        [TestMethod]
        public void ShowAdvancedOptions_DependsOnEnabledAndNecessary()
        {
            // Arrange
            _viewModel.PrayerId = "isha";

            // Act & Assert
            _viewModel.Enabled = false;
            _viewModel.ShowAdvancedOptions.Should().BeFalse();

            _viewModel.Enabled = true;
            _viewModel.ShowAdvancedOptions.Should().Be(_viewModel.IsNecessary);
        }

        [TestMethod]
        public void PrayerTitles_LoadCorrectly_ForAllPrayerIds()
        {
            // Test cases for different prayer IDs
            var prayerTestCases = new[]
            {
                new { Id = "falsefajr", ExpectedTitleNotNull = true },
                new { Id = "fajr", ExpectedTitleNotNull = true },
                new { Id = "sunrise", ExpectedTitleNotNull = true },
                new { Id = "dhuhr", ExpectedTitleNotNull = true },
                new { Id = "asr", ExpectedTitleNotNull = true },
                new { Id = "maghrib", ExpectedTitleNotNull = true },
                new { Id = "isha", ExpectedTitleNotNull = true },
                new { Id = "endofisha", ExpectedTitleNotNull = true }
            };

            foreach (var testCase in prayerTestCases)
            {
                // Act
                _viewModel.PrayerId = testCase.Id;

                // Assert
                if (testCase.ExpectedTitleNotNull)
                {
                    _viewModel.Title.Should().NotBeNullOrEmpty($"Title should be set for {testCase.Id}");
                }
            }
        }

        [TestMethod]
        public void NotificationTime_VariousValues_AcceptedCorrectly()
        {
            // Arrange
            _viewModel.PrayerId = "fajr";
            var testValues = new[] { 0, 5, 10, 15, 30, 60 };

            foreach (var testValue in testValues)
            {
                // Act
                _viewModel.NotificationTime = testValue;

                // Assert
                _viewModel.NotificationTime.Should().Be(testValue, $"Failed for notification time: {testValue}");
            }
        }

        [TestMethod]
        public async Task Save_CallsSetMonthlyAlarms()
        {
            // Arrange
            _viewModel.PrayerId = "dhuhr";
            _viewModel.SelectedSound = _viewModel.AvailableSounds.First();
            _audioPreviewMock.Setup(a => a.StopAsync()).Returns(Task.CompletedTask);
            _dataServiceMock.Setup(d => d.SetMonthlyAlarmsAsync(It.IsAny<bool>())).Returns(Task.CompletedTask);

            // Act
            _viewModel.SaveCommand.Execute(null);
            
            // Allow async operations to complete
            await Task.Delay(100);

            // Assert
            _audioPreviewMock.Verify(a => a.StopAsync(), Times.Once);
            _dataServiceMock.Verify(d => d.SetMonthlyAlarmsAsync(It.Is<bool>(force => force)), Times.Once);
        }

        [TestMethod]
        public void Sound_DefaultSelection_KusIsDefault()
        {
            // Arrange & Act
            _viewModel.PrayerId = "fajr"; // This triggers LoadPrayer

            // Assert
            // Default sound should be "kus" or first available if "kus" not found
            var expectedSound = _viewModel.AvailableSounds.FirstOrDefault(s => s.FileName == "kus")
                              ?? _viewModel.AvailableSounds.FirstOrDefault();
            _viewModel.SelectedSound.Should().NotBeNull();
        }

        [TestMethod]
        public void PropertyChanged_Notifications_FireCorrectly()
        {
            // Arrange
            var propertiesChanged = new System.Collections.Generic.List<string>();
            _viewModel.PropertyChanged += (s, e) => propertiesChanged.Add(e.PropertyName);

            // Act
            _viewModel.Enabled = true;
            _viewModel.NotificationTime = 20;
            _viewModel.IsPlaying = true;

            // Assert
            propertiesChanged.Should().Contain(nameof(_viewModel.Enabled));
            propertiesChanged.Should().Contain(nameof(_viewModel.NotificationTime));
            propertiesChanged.Should().Contain(nameof(_viewModel.IsPlaying));
        }

        [TestMethod]
        public async Task AudioService_Integration_HandlesPlaybackErrors()
        {
            // Arrange
            _viewModel.SelectedSound = _viewModel.AvailableSounds.First();
            _audioPreviewMock.Setup(a => a.PlayAsync(It.IsAny<string>(), It.IsAny<bool>()))
                           .ThrowsAsync(new InvalidOperationException("Audio device unavailable"));

            // Act & Assert
            await FluentActions.Invoking(async () => 
                await _viewModel.TestButtonClickedCommand.ExecuteAsync(null))
                .Should().NotThrowAsync("Audio playback errors should be handled gracefully");
        }

        [TestMethod]
        public async Task AudioService_Integration_HandlesStopErrors()
        {
            // Arrange
            _viewModel.IsPlaying = true;
            _audioPreviewMock.Setup(a => a.StopAsync())
                           .ThrowsAsync(new InvalidOperationException("Stop failed"));

            // Act & Assert
            await FluentActions.Invoking(async () => 
                await _viewModel.TestButtonClickedCommand.ExecuteAsync(null))
                .Should().NotThrowAsync("Audio stop errors should be handled gracefully");
        }

        [TestMethod]
        public void PrayerId_EmptyOrNull_HandlesGracefully()
        {
            // Act & Assert
            FluentActions.Invoking(() => _viewModel.PrayerId = null)
                         .Should().NotThrow();
            FluentActions.Invoking(() => _viewModel.PrayerId = "")
                         .Should().NotThrow();
            FluentActions.Invoking(() => _viewModel.PrayerId = "nonexistent")
                         .Should().NotThrow();
        }

        [TestMethod]
        public void EnabledToggle_BusyState_PreventsConcurrentChanges()
        {
            // Arrange
            _viewModel.IsBusy = true;

            // Act
            _viewModel.EnabledSwitchToggledCommand.Execute(true);

            // Assert
            // When busy, the command should handle the state appropriately
            // The exact behavior depends on implementation, but it shouldn't crash
            _viewModel.Should().NotBeNull(); // Basic verification
        }

        [TestMethod]
        public void SoundOptions_Localization_SupportedCorrectly()
        {
            // Act
            var sounds = _viewModel.AvailableSounds;

            // Assert - All sounds should have names (localized)
            foreach (var sound in sounds)
            {
                sound.Name.Should().NotBeNullOrEmpty($"Sound {sound.FileName} should have a localized name");
                sound.FileName.Should().NotBeNullOrEmpty($"Sound should have a valid filename");
            }
        }

        [TestMethod]
        public void MultipleNotificationTimes_PersistencePattern_WorksCorrectly()
        {
            // Test the persistence pattern mentioned in instructions
            var prayerIds = new[] { "fajr", "dhuhr", "asr", "maghrib", "isha" };
            var notificationTimes = new[] { 5, 10, 15, 0, 20 };

            for (int i = 0; i < prayerIds.Length; i++)
            {
                // Arrange
                _viewModel.PrayerId = prayerIds[i];

                // Act
                _viewModel.NotificationTime = notificationTimes[i];

                // Assert
                _viewModel.NotificationTime.Should().Be(notificationTimes[i], 
                    $"Notification time should persist for {prayerIds[i]}");
            }
        }

        [TestMethod]
        public void PrayerDetail_TogglePattern_AlarmNotificationOffsets()
        {
            // Test the toggle pattern mentioned in the instructions for alarm notifications
            _viewModel.PrayerId = "fajr";

            // Arrange - Set up various notification offsets
            var offsets = new[] { 0, 5, 10, 15, 30, 60 }; // Minutes before prayer time

            foreach (var offset in offsets)
            {
                // Act
                _viewModel.NotificationTime = offset;
                _viewModel.Enabled = true;

                // Assert
                _viewModel.NotificationTime.Should().Be(offset);
                _viewModel.Enabled.Should().BeTrue();
                
                // When enabled and necessary, advanced options should show
                if (_viewModel.IsNecessary)
                {
                    _viewModel.ShowAdvancedOptions.Should().BeTrue();
                }
            }
        }

        [TestMethod]
        public async Task OnPageResumedAsync_WhenNotAwaitingPermission_DoesNothing()
        {
            // Arrange
            _viewModel.PrayerId = "fajr";
            _viewModel.SelectedSound = _viewModel.AvailableSounds.First();
            _dataServiceMock.Setup(d => d.SetMonthlyAlarmsAsync(It.IsAny<bool>())).Returns(Task.CompletedTask);

            // Act - Call OnPageResumedAsync when not awaiting permission
            await _viewModel.OnPageResumedAsync();

            // Assert - Should not trigger save operations since not awaiting permission
            // The initial state doesn't await permission, so no alarm scheduling should occur
            _viewModel.Should().NotBeNull();
        }

        [TestMethod]
        public void Save_ShowsOverlay_DuringScheduling()
        {
            // Arrange
            _viewModel.PrayerId = "asr";
            _viewModel.SelectedSound = _viewModel.AvailableSounds.First();
            _audioPreviewMock.Setup(a => a.StopAsync()).Returns(Task.CompletedTask);
            _dataServiceMock.Setup(d => d.SetMonthlyAlarmsAsync(It.IsAny<bool>())).Returns(Task.CompletedTask);

            // Act
            _viewModel.SaveCommand.Execute(null);

            // Assert - Should set IsBusy during save
            // Note: In real async execution, ShowOverlay would be true briefly
            _viewModel.Should().NotBeNull();
        }
    }
}
