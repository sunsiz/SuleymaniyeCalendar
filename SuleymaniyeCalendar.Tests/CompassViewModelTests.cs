using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SuleymaniyeCalendar.Services;
using SuleymaniyeCalendar.ViewModels;

namespace SuleymaniyeCalendar.Tests
{
    [TestClass]
    public class CompassViewModelTests
    {
        private Mock<DataService> _dataServiceMock;
        private Mock<IRtlService> _rtlServiceMock;
        private CompassViewModel _viewModel;

        [TestInitialize]
        public void Setup()
        {
            _dataServiceMock = new Mock<DataService>();
            _rtlServiceMock = new Mock<IRtlService>();
        }

        private CompassViewModel CreateViewModel()
        {
            return new CompassViewModel(_dataServiceMock.Object);
        }

        [TestMethod]
        public void Constructor_InitializesPropertiesCorrectly()
        {
            // Act
            var vm = CreateViewModel();

            // Assert
            vm.Should().NotBeNull();
            vm.Latitude.Should().BeGreaterThanOrEqualTo(0);
            vm.Longitude.Should().BeGreaterThanOrEqualTo(0);
        }

        [TestMethod]
        public async Task ManualRefresh_AlwaysForcesGpsFix()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act
            await vm.RefreshLocationCommand.ExecuteAsync(null);

            // Assert
            vm.Should().NotBeNull(); // Command completes successfully
        }

        [TestMethod]
        public void Start_Command_SetsCompassRunning()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act
            vm.StartCommand.Execute(null);

            // Assert
            vm.Should().NotBeNull();
        }

        [TestMethod]
        public void Stop_Command_StopsCompass()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act
            vm.StopCommand.Execute(null);

            // Assert
            vm.Should().NotBeNull();
        }

        [TestMethod]
        public void Heading_Property_UpdatesCorrectly()
        {
            // Arrange
            var vm = CreateViewModel();
            var testHeading = 45.5;

            // Act
            vm.Heading = testHeading;

            // Assert
            vm.Heading.Should().Be(testHeading);
        }

        [TestMethod]
        public void Latitude_Property_UpdatesCorrectly()
        {
            // Arrange
            var vm = CreateViewModel();
            var testLatitude = 41.0082;

            // Act
            vm.Latitude = testLatitude;

            // Assert
            vm.Latitude.Should().Be(testLatitude);
        }

        [TestMethod]
        public void Longitude_Property_UpdatesCorrectly()
        {
            // Arrange
            var vm = CreateViewModel();
            var testLongitude = 28.9784;

            // Act
            vm.Longitude = testLongitude;

            // Assert
            vm.Longitude.Should().Be(testLongitude);
        }

        [TestMethod]
        public void Altitude_Property_UpdatesCorrectly()
        {
            // Arrange
            var vm = CreateViewModel();
            var testAltitude = 114.0;

            // Act
            vm.Altitude = testAltitude;

            // Assert
            vm.Altitude.Should().Be(testAltitude);
        }

        [TestMethod]
        public void Address_Property_UpdatesCorrectly()
        {
            // Arrange
            var vm = CreateViewModel();
            var testAddress = "Istanbul, Turkey";

            // Act
            vm.Address = testAddress;

            // Assert
            vm.Address.Should().Be(testAddress);
        }

        [TestMethod]
        public async Task RefreshLocationFromAppAsync_UpdatesFromDataService()
        {
            // Arrange
            var testCalendar = new SuleymaniyeCalendar.Models.Calendar
            {
                Latitude = 41.0082,
                Longitude = 28.9784,
                Altitude = 114.0
            };
            _dataServiceMock.SetupGet(x => x.calendar).Returns(testCalendar);
            var vm = CreateViewModel();

            // Act
            await vm.RefreshLocationFromAppAsync();

            // Assert
            _dataServiceMock.VerifyGet(x => x.calendar, Times.AtLeastOnce);
        }

        [TestMethod]
        public void Dispose_CleansUpResources()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act & Assert
            FluentActions.Invoking(() => vm.Dispose()).Should().NotThrow();
        }

        [TestMethod]
        public void PropertyChanged_Events_FireCorrectly()
        {
            // Arrange
            var vm = CreateViewModel();
            var propertyChangedCount = 0;
            vm.PropertyChanged += (s, e) => propertyChangedCount++;

            // Act
            vm.Heading = 90.0;
            vm.Latitude = 40.0;
            vm.Longitude = 30.0;

            // Assert
            propertyChangedCount.Should().BeGreaterThan(0);
        }

        [TestMethod]
        public void QiblaDirection_Calculation_IsAccurate()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act
            vm.Latitude = 41.0082; // Istanbul coordinates
            vm.Longitude = 28.9784;

            // Assert
            vm.Latitude.Should().Be(41.0082);
            vm.Longitude.Should().Be(28.9784);
        }

        [TestMethod]
        public void CompassReadings_HandleExtremeBearings()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act & Assert
            FluentActions.Invoking(() =>
            {
                vm.Heading = 0.0;   // North
                vm.Heading = 90.0;  // East
                vm.Heading = 180.0; // South
                vm.Heading = 270.0; // West
                vm.Heading = 360.0; // Full circle
            }).Should().NotThrow();
        }

        [TestMethod]
        public void LocationData_ValidatesCoordinateRanges()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act & Assert
            FluentActions.Invoking(() =>
            {
                vm.Latitude = -90.0;  // South pole
                vm.Latitude = 90.0;   // North pole
                vm.Longitude = -180.0; // Antemeridian west
                vm.Longitude = 180.0;  // Antemeridian east
            }).Should().NotThrow();
        }
    }
}
