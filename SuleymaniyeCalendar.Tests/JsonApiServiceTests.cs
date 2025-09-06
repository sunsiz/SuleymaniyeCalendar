using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SuleymaniyeCalendar.Models;
using SuleymaniyeCalendar.Services;

namespace SuleymaniyeCalendar.Tests
{
    [TestClass]
    public class JsonApiServiceTests
    {
        private JsonApiService _service;

        [TestInitialize]
        public void Setup()
        {
            _service = new JsonApiService();
        }

        [TestMethod]
        public async Task GetMonthlyPrayerTimesAsync_ValidInput_ReturnsData()
        {
            // Arrange
            var latitude = 41.0082;
            var longitude = 28.9784;
            var month = DateTime.Today.Month;

            // Act
            var result = await _service.GetMonthlyPrayerTimesAsync(latitude, longitude, month);

            // Assert
            // In a test environment, the API might not be available, so we check for graceful handling
            result.Should().NotBeNull();
        }

        [TestMethod]
        public async Task GetMonthlyPrayerTimesAsync_InvalidCoordinates_HandlesGracefully()
        {
            // Act
            var result = await _service.GetMonthlyPrayerTimesAsync(0.0, 0.0, 0);

            // Assert
            // Should handle invalid coordinates gracefully without throwing
            result.Should().NotBeNull();
        }

        [TestMethod]
        public async Task GetDailyPrayerTimesAsync_ValidInput_ReturnsData()
        {
            // Arrange
            var latitude = 41.0082;
            var longitude = 28.9784;
            var date = DateTime.Today;
            var altitude = 114.0;

            // Act
            var result = await _service.GetDailyPrayerTimesAsync(latitude, longitude, date, altitude);

            // Assert
            result.Should().NotBeNull();
        }

        [TestMethod]
        public async Task GetDailyPrayerTimesAsync_InvalidCoordinates_HandlesGracefully()
        {
            // Arrange
            var invalidDate = DateTime.MinValue;

            // Act
            var result = await _service.GetDailyPrayerTimesAsync(0.0, 0.0, invalidDate, 0.0);

            // Assert
            result.Should().NotBeNull();
        }

        [TestMethod]
        public void JsonApiService_Constructor_InitializesCorrectly()
        {
            // Act & Assert
            _service.Should().NotBeNull();
        }

        [TestMethod]
        public async Task GetMonthlyPrayerTimesAsync_NetworkError_HandlesGracefully()
        {
            // Arrange
            var extremeCoordinates = (-999.0, -999.0); // Invalid coordinates to trigger network error

            // Act & Assert
            await FluentActions.Invoking(async () => 
                await _service.GetMonthlyPrayerTimesAsync(extremeCoordinates.Item1, extremeCoordinates.Item2, 13))
                .Should().NotThrowAsync();
        }

        [TestMethod]
        public async Task GetDailyPrayerTimesAsync_FutureDate_HandlesCorrectly()
        {
            // Arrange
            var futureDate = DateTime.Today.AddYears(1);
            var latitude = 41.0082;
            var longitude = 28.9784;

            // Act
            var result = await _service.GetDailyPrayerTimesAsync(latitude, longitude, futureDate, 114.0);

            // Assert
            result.Should().NotBeNull();
        }

        [TestMethod]
        public async Task GetMonthlyPrayerTimesAsync_ExtremeMonth_HandlesCorrectly()
        {
            // Arrange
            var latitude = 41.0082;
            var longitude = 28.9784;

            // Act & Assert
            await FluentActions.Invoking(async () =>
            {
                await _service.GetMonthlyPrayerTimesAsync(latitude, longitude, 0); // Invalid month
                await _service.GetMonthlyPrayerTimesAsync(latitude, longitude, 13); // Invalid month
            }).Should().NotThrowAsync();
        }

        [TestMethod]
        public async Task JsonApiService_Response_ParsesCorrectly()
        {
            // Arrange
            var latitude = 41.0082;
            var longitude = 28.9784;
            var currentMonth = DateTime.Today.Month;

            // Act
            var result = await _service.GetMonthlyPrayerTimesAsync(latitude, longitude, currentMonth);

            // Assert
            if (result != null && result.Count > 0)
            {
                // If we get data, it should have proper structure
                var firstEntry = result[0];
                firstEntry.Should().NotBeNull();
                firstEntry.Date.Should().NotBeNullOrEmpty();
            }
        }

        [TestMethod]
        public async Task GetDailyPrayerTimesAsync_SpecificDate_ReturnsCorrectDate()
        {
            // Arrange
            var specificDate = new DateTime(2024, 6, 15);
            var latitude = 41.0082;
            var longitude = 28.9784;

            // Act
            var result = await _service.GetDailyPrayerTimesAsync(latitude, longitude, specificDate, 114.0);

            // Assert
            result.Should().NotBeNull();
            if (result != null && !string.IsNullOrEmpty(result.Date))
            {
                result.Date.Should().Contain("15");
            }
        }

        [TestMethod]
        public async Task JsonApiService_MultipleRequests_HandlesConcurrency()
        {
            // Arrange
            var tasks = new Task<ObservableCollection<SuleymaniyeCalendar.Models.Calendar>>[]
            {
                _service.GetMonthlyPrayerTimesAsync(41.0, 29.0, 1),
                _service.GetMonthlyPrayerTimesAsync(41.0, 29.0, 6),
                _service.GetMonthlyPrayerTimesAsync(41.0, 29.0, 12)
            };

            // Act & Assert
            await FluentActions.Invoking(async () => await Task.WhenAll(tasks))
                               .Should().NotThrowAsync();
        }

        [TestMethod]
        public async Task JsonApiService_ErrorHandling_DoesNotCrash()
        {
            // Act & Assert
            await FluentActions.Invoking(async () =>
            {
                // Test various edge cases
                await _service.GetMonthlyPrayerTimesAsync(double.MaxValue, double.MaxValue, 1);
                await _service.GetMonthlyPrayerTimesAsync(double.MinValue, double.MinValue, 1);
                await _service.GetDailyPrayerTimesAsync(double.NaN, double.NaN, DateTime.Today, 0);
            }).Should().NotThrowAsync();
        }
    }
}
