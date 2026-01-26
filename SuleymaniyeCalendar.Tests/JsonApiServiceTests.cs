using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SuleymaniyeCalendar.Services;

namespace SuleymaniyeCalendar.Tests;

[TestClass]
public class JsonApiServiceTests
{
    private Mock<JsonApiService> _mockService = null!;
    private Models.Calendar _testCalendar = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockService = new Mock<JsonApiService> { CallBase = false };

        _testCalendar = new Models.Calendar
        {
            Date = DateTime.Today.ToString("dd/MM/yyyy"),
            Latitude = 41.0082,
            Longitude = 28.9784,
            Altitude = 114.0,
            Fajr = "05:30",
            Sunrise = "07:15",
            Dhuhr = "13:05",
            Asr = "16:20",
            Maghrib = "19:45",
            Isha = "21:30"
        };
    }

    [TestMethod]
    public async Task GetMonthlyPrayerTimesAsync_WhenConfigured_ReturnsData()
    {
        var expectedData = new ObservableCollection<Models.Calendar> { _testCalendar };
        _mockService
            .Setup(s => s.GetMonthlyPrayerTimesAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<int?>()))
            .ReturnsAsync(expectedData);

        var result = await _mockService.Object.GetMonthlyPrayerTimesAsync(41.0082, 28.9784, 1);

        result.Should().NotBeNull();
        result!.Should().HaveCount(1);

        _mockService.Verify(s => s.GetMonthlyPrayerTimesAsync(41.0082, 28.9784, 1, It.IsAny<double>(), null), Times.Once);
    }

    [TestMethod]
    public async Task GetDailyPrayerTimesAsync_WhenConfigured_ReturnsData()
    {
        _mockService
            .Setup(s => s.GetDailyPrayerTimesAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<DateTime>(), It.IsAny<double>()))
            .ReturnsAsync(_testCalendar);

        var result = await _mockService.Object.GetDailyPrayerTimesAsync(41.0082, 28.9784, DateTime.Today, 114.0);

        result.Should().NotBeNull();
        result!.Fajr.Should().Be("05:30");

        _mockService.Verify(s => s.GetDailyPrayerTimesAsync(41.0082, 28.9784, DateTime.Today, 114.0), Times.Once);
    }

    [TestMethod]
    public async Task TestConnectionAsync_WhenConfigured_ReturnsTrue()
    {
        _mockService.Setup(s => s.TestConnectionAsync()).ReturnsAsync(true);

        var result = await _mockService.Object.TestConnectionAsync();

        result.Should().BeTrue();
        _mockService.Verify(s => s.TestConnectionAsync(), Times.Once);
    }

    [TestMethod]
    public void Dispose_WhenCalled_DoesNotThrow()
    {
        using var service = new JsonApiService();
        FluentActions.Invoking(() => service.Dispose()).Should().NotThrow();
    }
}
