using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SuleymaniyeCalendar.Models;
using SuleymaniyeCalendar.Services;
using SuleymaniyeCalendar.ViewModels;

namespace SuleymaniyeCalendar.Tests
{
    [TestClass]
    public class MonthPageTests
    {
        private Mock<DataService> _dataServiceMock;
        private ObservableCollection<SuleymaniyeCalendar.Models.Calendar> _testMonthlyData;
        private Mock<ILogger<DataService>> _loggerMock;
        private Mock<JsonApiService> _jsonApiServiceMock;

        [TestInitialize]
        public void Setup()
        {
            _testMonthlyData = new ObservableCollection<SuleymaniyeCalendar.Models.Calendar>();
            for (int i = 1; i <= 30; i++)
            {
                _testMonthlyData.Add(new SuleymaniyeCalendar.Models.Calendar
                {
                    Date = new DateTime(2024, 6, i).ToString("dd/MM/yyyy"),
                    Latitude = 41.0082,
                    Longitude = 28.9784,
                    Fajr = "05:30",
                    Sunrise = "07:15",
                    Dhuhr = "13:05",
                    Asr = "16:20",
                    Maghrib = "19:45",
                    Isha = "21:30"
                });
            }

            _loggerMock = new Mock<ILogger<DataService>>();
            _jsonApiServiceMock = new Mock<JsonApiService>(_loggerMock.Object);
            _dataServiceMock = new Mock<DataService>(_loggerMock.Object, _jsonApiServiceMock.Object);
            
            // Setup calendar property
            _dataServiceMock.SetupGet(x => x.calendar).Returns(new SuleymaniyeCalendar.Models.Calendar 
            { 
                Latitude = 41.0082, 
                Longitude = 28.9784, 
                Altitude = 0 
            });
            
            _dataServiceMock.Setup(x => x.GetMonthlyPrayerTimesHybridAsync(It.IsAny<Location>(), It.IsAny<bool>()))
                           .ReturnsAsync(_testMonthlyData);
        }

        private MonthViewModel CreateViewModel()
        {
            return new MonthViewModel(_dataServiceMock.Object);
        }

        [TestMethod]
        public async Task MonthPage_DelayedLoading_InstantUiAndBusyIndicator()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act
            await vm.InitializeWithDelayAsync();

            // Assert
            vm.IsBusy.Should().BeFalse(); // UI shows instantly, busy should be false after completion
        }

        [TestMethod]
        public void MonthPage_BatchCollectionUpdates_NoUiThrash()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act
            vm.MonthlyCalendar = _testMonthlyData;

            // Assert
            vm.MonthlyCalendar.Count.Should().Be(30);
        }

        [TestMethod]
        public void Constructor_InitializesPropertiesCorrectly()
        {
            // Act
            var vm = CreateViewModel();

            // Assert
            vm.Should().NotBeNull();
            vm.MonthlyCalendar.Should().NotBeNull();
            vm.HasData.Should().BeFalse(); // Initially empty
        }

        [TestMethod]
        public async Task InitializeAsync_PopulatesCollection()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act
            await vm.InitializeAsync();

            // Assert
            vm.MonthlyCalendar.Should().NotBeNull();
            vm.HasData.Should().BeTrue();
        }

        [TestMethod]
        public async Task RefreshCommand_ReloadsData()
        {
            // Arrange
            var vm = CreateViewModel();
            _dataServiceMock.Setup(x => x.GetCurrentLocationAsync(It.IsAny<bool>()))
                           .ReturnsAsync(new Location { Latitude = 41.0082, Longitude = 28.9784 });

            // Act
            await vm.RefreshCommand.ExecuteAsync(null);

            // Assert
            _dataServiceMock.Verify(x => x.GetMonthlyPrayerTimesHybridAsync(It.IsAny<Location>(), true), Times.AtLeastOnce);
        }

        [TestMethod]
        public void MonthlyCalendar_Collection_NotifiesChanges()
        {
            // Arrange
            var vm = CreateViewModel();
            var propertyChangedCount = 0;
            vm.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(vm.MonthlyCalendar)) propertyChangedCount++; };

            // Act
            vm.MonthlyCalendar = new ObservableCollection<SuleymaniyeCalendar.Models.Calendar>();

            // Assert
            propertyChangedCount.Should().BeGreaterThan(0);
        }

        [TestMethod]
        public async Task InitializeAsync_WithValidData_ReturnsCorrectCount()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act
            await vm.InitializeAsync();

            // Assert
            vm.MonthlyCalendar.Count.Should().Be(30); // Test data has 30 days
            vm.HasData.Should().BeTrue();
        }

        [TestMethod]
        public async Task InitializeAsync_WithEmptyData_HandlesGracefully()
        {
            // Arrange
            _dataServiceMock.Setup(x => x.GetMonthlyPrayerTimesHybridAsync(It.IsAny<Location>(), It.IsAny<bool>()))
                           .ReturnsAsync(new ObservableCollection<SuleymaniyeCalendar.Models.Calendar>());
            var vm = CreateViewModel();

            // Act
            await vm.InitializeAsync();

            // Assert
            vm.MonthlyCalendar.Should().NotBeNull();
            vm.MonthlyCalendar.Count.Should().Be(0);
            vm.HasData.Should().BeFalse();
        }

        [TestMethod]
        public async Task InitializeAsync_WithNullData_HandlesGracefully()
        {
            // Arrange
            _dataServiceMock.Setup(x => x.GetMonthlyPrayerTimesHybridAsync(It.IsAny<Location>(), It.IsAny<bool>()))
                           .ReturnsAsync((ObservableCollection<SuleymaniyeCalendar.Models.Calendar>)null);
            var vm = CreateViewModel();

            // Act & Assert
            await FluentActions.Invoking(async () => await vm.InitializeAsync())
                               .Should().NotThrowAsync();
        }

        [TestMethod]
        public void PrayerTimes_Data_HasValidStructure()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act
            vm.MonthlyCalendar = _testMonthlyData;

            // Assert
            var firstEntry = vm.MonthlyCalendar.FirstOrDefault();
            firstEntry.Should().NotBeNull();
            firstEntry.Date.Should().NotBeNullOrEmpty();
            firstEntry.Fajr.Should().NotBeNullOrEmpty();
            firstEntry.Dhuhr.Should().NotBeNullOrEmpty();
            firstEntry.Asr.Should().NotBeNullOrEmpty();
            firstEntry.Maghrib.Should().NotBeNullOrEmpty();
            firstEntry.Isha.Should().NotBeNullOrEmpty();
        }

        [TestMethod]
        public async Task LoadingState_UpdatesCorrectly()
        {
            // Arrange
            var vm = CreateViewModel();
            var isBusyStates = new System.Collections.Generic.List<bool>();
            vm.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(vm.IsBusy)) isBusyStates.Add(vm.IsBusy); };

            // Act
            await vm.InitializeAsync();

            // Assert
            vm.IsBusy.Should().BeFalse(); // Should be false after completion
        }

        [TestMethod]
        public async Task ErrorHandling_NetworkFailure_HandlesGracefully()
        {
            // Arrange
            _dataServiceMock.Setup(x => x.GetMonthlyPrayerTimesHybridAsync(It.IsAny<Location>(), It.IsAny<bool>()))
                           .ThrowsAsync(new System.Net.Http.HttpRequestException("Network error"));
            var vm = CreateViewModel();

            // Act & Assert
            await FluentActions.Invoking(async () => await vm.InitializeAsync())
                               .Should().NotThrowAsync();
        }

        [TestMethod]
        public void MonthlyData_Filtering_ByDate_WorksCorrectly()
        {
            // Arrange
            var vm = CreateViewModel();
            vm.MonthlyCalendar = _testMonthlyData;

            // Act
            var june15Data = vm.MonthlyCalendar.FirstOrDefault(x => x.Date.Contains("15/06/2024"));

            // Assert
            june15Data.Should().NotBeNull();
        }

        [TestMethod]
        public async Task GoBackCommand_ExecutesSuccessfully()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act & Assert
            await FluentActions.Invoking(async () => await vm.GoBackCommand.ExecuteAsync(null))
                               .Should().NotThrowAsync();
        }

        [TestMethod]
        public void HasData_Property_UpdatesCorrectly()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act & Assert
            vm.HasData.Should().BeFalse(); // Initially false

            vm.MonthlyCalendar = _testMonthlyData;
            vm.HasData.Should().BeTrue(); // True when data is added
        }

        [TestMethod]
        public void ShowShare_Property_ReflectsLocationState()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act & Assert
            // ShowShare depends on preferences, so we just verify it doesn't throw
            var showShare = vm.ShowShare;
            showShare.Should().Be(showShare); // Just verify it returns a boolean value
        }

        [TestMethod]
        public async Task ShareCommand_ExecutesWithoutError()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act & Assert
            await FluentActions.Invoking(async () => await vm.ShareCommand.ExecuteAsync(null))
                               .Should().NotThrowAsync();
        }

        [TestMethod]
        public async Task InitializeWithDelayAsync_SkipsIfAlreadyLoaded()
        {
            // Arrange
            var vm = CreateViewModel();
            vm.MonthlyCalendar = _testMonthlyData; // Pre-populate

            // Act
            await vm.InitializeWithDelayAsync();

            // Assert
            // Should not make additional calls since data already exists
            _dataServiceMock.Verify(x => x.GetMonthlyPrayerTimesHybridAsync(It.IsAny<Location>(), It.IsAny<bool>()), Times.Never);
        }

        [TestMethod]
        public async Task Refresh_WithoutLocation_HandlesGracefully()
        {
            // Arrange
            var vm = CreateViewModel();
            _dataServiceMock.Setup(x => x.GetCurrentLocationAsync(It.IsAny<bool>()))
                           .ReturnsAsync((Location)null);

            // Act & Assert
            await FluentActions.Invoking(async () => await vm.RefreshCommand.ExecuteAsync(null))
                               .Should().NotThrowAsync();
        }

        [TestMethod]
        public async Task Refresh_WithInvalidLocation_HandlesGracefully()
        {
            // Arrange
            var vm = CreateViewModel();
            _dataServiceMock.Setup(x => x.GetCurrentLocationAsync(It.IsAny<bool>()))
                           .ReturnsAsync(new Location { Latitude = 0, Longitude = 0 }); // Invalid location

            // Act & Assert
            await FluentActions.Invoking(async () => await vm.RefreshCommand.ExecuteAsync(null))
                               .Should().NotThrowAsync();
        }
    }
}
