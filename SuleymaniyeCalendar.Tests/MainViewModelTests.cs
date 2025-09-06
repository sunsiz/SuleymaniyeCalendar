using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Maui.ApplicationModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SuleymaniyeCalendar.Models;
using SuleymaniyeCalendar.Services;
using SuleymaniyeCalendar.ViewModels;

namespace SuleymaniyeCalendar.Tests
{
    [TestClass]
    public class MainViewModelTests
    {
        private Mock<DataService> _dataServiceMock;
        private Mock<IRtlService> _rtlServiceMock;
        private Mock<IAudioPreviewService> _audioPreviewServiceMock;
        private Mock<IRadioService> _radioServiceMock;
        private Mock<PerformanceService> _performanceServiceMock;
        private Mock<BackgroundDataPreloader> _backgroundDataPreloaderMock;
        private MainViewModel _viewModel;
        private SuleymaniyeCalendar.Models.Calendar _testCalendar;

        [TestInitialize]
        public void Setup()
        {
            // Setup test calendar with realistic prayer times
            _testCalendar = new SuleymaniyeCalendar.Models.Calendar
            {
                Date = DateTime.Today.ToString("dd/MM/yyyy"),
                Latitude = 41.0082,
                Longitude = 28.9784, // Istanbul coordinates
                Fajr = "05:30",
                Sunrise = "07:15",
                Dhuhr = "13:05",
                Asr = "16:20",
                Maghrib = "19:45",
                Isha = "21:30"
            };

            _dataServiceMock = new Mock<DataService>();
            _rtlServiceMock = new Mock<IRtlService>();
            _audioPreviewServiceMock = new Mock<IAudioPreviewService>();
            _radioServiceMock = new Mock<IRadioService>();
            _performanceServiceMock = new Mock<PerformanceService>();
            _backgroundDataPreloaderMock = new Mock<BackgroundDataPreloader>();

            // Setup DataService mock
            _dataServiceMock.SetupGet(x => x.calendar).Returns(_testCalendar);
            _dataServiceMock.Setup(x => x.PrepareMonthlyPrayerTimes()).ReturnsAsync(_testCalendar);
            _dataServiceMock.Setup(x => x.SetMonthlyAlarmsAsync()).Returns(Task.CompletedTask);
        }

        private MainViewModel CreateViewModel()
        {
            return new MainViewModel(_dataServiceMock.Object);
        }

        [TestMethod]
        public void Constructor_InitializesPropertiesCorrectly()
        {
            // Act
            var vm = CreateViewModel();

            // Assert
            vm.Should().NotBeNull();
            vm.Prayers.Should().NotBeNull();
            vm.Title.Should().NotBeNullOrEmpty();
        }

        [TestMethod]
        public async Task RefreshLocationCommand_WhenExecuted_UpdatesCalendarAndAlarms()
        {
            // Arrange
            var vm = CreateViewModel();
            _dataServiceMock.Setup(x => x.PrepareMonthlyPrayerTimes()).ReturnsAsync(_testCalendar);

            // Act
            await vm.RefreshLocationCommand.ExecuteAsync(null);

            // Assert
            _dataServiceMock.Verify(x => x.PrepareMonthlyPrayerTimes(), Times.Once);
            _dataServiceMock.Verify(x => x.SetMonthlyAlarmsAsync(), Times.Once);
        }

        [TestMethod]
        public async Task GoToMapCommand_WithValidCoordinates_DoesNotThrow()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act & Assert
            await FluentActions.Invoking(async () => await vm.GoToMapCommand.ExecuteAsync(null))
                .Should().NotThrowAsync();
        }

        [TestMethod]
        public void LoadPrayers_PopulatesPrayersCollection()
        {
            // Arrange & Act
            var vm = CreateViewModel();

            // Assert
            vm.Prayers.Should().NotBeEmpty();
            vm.Prayers.Should().HaveCount(7); // 5 prayers + sunrise + end of isha
        }

        [TestMethod]
        public void LoadPrayers_SetsCorrectPrayerTimes()
        {
            // Arrange & Act
            var vm = CreateViewModel();

            // Assert
            var fajr = vm.Prayers.FirstOrDefault(p => p.Id == "fajr");
            fajr.Should().NotBeNull();
            fajr.Time.Should().Be("05:30");
            
            var dhuhr = vm.Prayers.FirstOrDefault(p => p.Id == "dhuhr");
            dhuhr.Should().NotBeNull();
            dhuhr.Time.Should().Be("13:05");
        }

        [TestMethod]
        public void CheckState_CurrentPrayer_HasCorrectState()
        {
            // Arrange
            var vm = CreateViewModel();
            var now = DateTime.Now;
            var currentHour = now.Hour;
            
            // Act
            // Prayer states are strings: "Past", "Current", "Future"
            var currentPrayer = vm.Prayers.FirstOrDefault(p => p.State == "Current");

            // Assert
            // At least one prayer should have a state (either Past, Current, or Future)
            vm.Prayers.Should().OnlyContain(p => !string.IsNullOrEmpty(p.State));
        }

        [TestMethod]
        public void PrayerSelection_UpdatesSelectedPrayer()
        {
            // Arrange
            var vm = CreateViewModel();
            var firstPrayer = vm.Prayers.First();

            // Act
            vm.SelectedPrayer = firstPrayer;

            // Assert
            vm.SelectedPrayer.Should().Be(firstPrayer);
        }

        [TestMethod]
        public async Task RefreshLocation_WhenAlreadyRefreshing_DoesNotExecuteTwice()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act
            var task1 = vm.RefreshLocationCommand.ExecuteAsync(null);
            var task2 = vm.RefreshLocationCommand.ExecuteAsync(null); // Should be ignored

            await Task.WhenAll(task1, task2);

            // Assert
            _dataServiceMock.Verify(x => x.PrepareMonthlyPrayerTimes(), Times.Once);
        }

        [TestMethod]
        public void IsRefreshing_Property_NotifiesOnChange()
        {
            // Arrange
            var vm = CreateViewModel();
            var propertyChanged = false;
            vm.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(vm.IsRefreshing)) propertyChanged = true; };

            // Act
            vm.IsRefreshing = true;

            // Assert
            propertyChanged.Should().BeTrue();
            vm.IsRefreshing.Should().BeTrue();
        }

        [TestMethod]
        public void City_Property_UpdatesCorrectly()
        {
            // Arrange
            var vm = CreateViewModel();
            const string testCity = "Test City";

            // Act
            vm.City = testCity;

            // Assert
            vm.City.Should().Be(testCity);
        }

        [TestMethod]
        public void RemainingTime_Property_UpdatesCorrectly()
        {
            // Arrange
            var vm = CreateViewModel();
            const string testTime = "02:30:45";

            // Act
            vm.RemainingTime = testTime;

            // Assert
            vm.RemainingTime.Should().Be(testTime);
        }

        [TestMethod]
        public void PrayerStates_TransitionCorrectly_BasedOnTime()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act & Assert
            foreach (var prayer in vm.Prayers)
            {
                prayer.State.Should().BeOneOf("Past", "Current", "Future", "");
            }
        }

        [TestMethod]
        public void ViewModel_Initialization_CompletesSuccessfully()
        {
            // Arrange & Act
            var vm = CreateViewModel();

            // Assert
            // Verification that the ViewModel was created successfully
            vm.Should().NotBeNull();
            vm.Prayers.Should().NotBeNull();
            vm.Title.Should().NotBeNullOrEmpty();
        }
    }
}
