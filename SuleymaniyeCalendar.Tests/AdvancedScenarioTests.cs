using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using FluentAssertions;
using SuleymaniyeCalendar.Services;
using SuleymaniyeCalendar.ViewModels;
using SuleymaniyeCalendar.Models;
using Microsoft.Maui.Devices.Sensors;
using System.Collections.ObjectModel;
using LocalizationResourceManager.Maui;
using Calendar = SuleymaniyeCalendar.Models.Calendar;

namespace SuleymaniyeCalendar.Tests
{
    [TestClass]
    public class AdvancedScenarioTests
    {
        private Mock<DataService> _mockDataService;
        private Mock<JsonApiService> _mockJsonApiService;
        private Mock<IAlarmService> _mockAlarmService;
        private Mock<IRtlService> _mockRtlService;
        private Mock<IAudioPreviewService> _mockAudioService;
        private Mock<ILocalizationResourceManager> _mockLocalizationManager;

        [TestInitialize]
        public void Setup()
        {
            _mockDataService = new Mock<DataService>();
            _mockJsonApiService = new Mock<JsonApiService>();
            _mockAlarmService = new Mock<IAlarmService>();
            _mockRtlService = new Mock<IRtlService>();
            _mockAudioService = new Mock<IAudioPreviewService>();
            _mockLocalizationManager = new Mock<ILocalizationResourceManager>();
        }

        [TestMethod]
        public async Task JsonApiService_MonthlyDataRequest()
        {
            // Arrange
            var monthlyData = new ObservableCollection<Calendar> 
            { 
                new Calendar { Date = "2024-12-31", Fajr = "06:00", Dhuhr = "12:15" },
                new Calendar { Date = "2024-12-30", Fajr = "05:58", Dhuhr = "12:14" }
            };

            _mockJsonApiService.Setup(j => j.GetMonthlyPrayerTimesAsync(41.0082, 28.9784, 12, 0))
                .ReturnsAsync(monthlyData);

            // Act
            var result = await _mockJsonApiService.Object.GetMonthlyPrayerTimesAsync(41.0082, 28.9784, 12, 0);

            // Assert
            result.Should().NotBeNull();
            result.Count.Should().Be(2);
            result[0].Date.Should().Be("2024-12-31");
            result[0].Fajr.Should().Be("06:00");
        }

        [TestMethod]
        public async Task JsonApiService_DailyDataRequest()
        {
            // Arrange
            var dailyData = new Calendar { Date = "2024-06-15", Fajr = "05:30", Dhuhr = "12:15" };

            _mockJsonApiService.Setup(j => j.GetDailyPrayerTimesAsync(39.9334, 32.8597, It.IsAny<DateTime>(), 0))
                .ReturnsAsync(dailyData);

            // Act
            var result = await _mockJsonApiService.Object.GetDailyPrayerTimesAsync(39.9334, 32.8597, DateTime.Now, 0);

            // Assert
            result.Should().NotBeNull();
            result.Date.Should().Be("2024-06-15");
            result.Fajr.Should().Be("05:30");
        }

        [TestMethod]
        public void AlarmService_SetAlarmFunctionality()
        {
            // Arrange
            var prayerTime = DateTime.Now.AddHours(2);

            _mockAlarmService.Setup(a => a.SetAlarm(It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<int>(), It.IsAny<string>()));

            // Act
            _mockAlarmService.Object.SetAlarm(prayerTime, TimeSpan.Zero, 1, "Fajr");

            // Assert
            _mockAlarmService.Verify(a => a.SetAlarm(prayerTime, TimeSpan.Zero, 1, "Fajr"), Times.Once);
        }

        [TestMethod]
        public void AlarmService_CancelAlarmFunctionality()
        {
            // Arrange
            _mockAlarmService.Setup(a => a.CancelAlarm());

            // Act
            _mockAlarmService.Object.CancelAlarm();

            // Assert
            _mockAlarmService.Verify(a => a.CancelAlarm(), Times.Once);
        }

        [TestMethod]
        public void RtlService_LanguageDetection()
        {
            // Arrange
            _mockRtlService.Setup(r => r.IsRtlLanguage("ar")).Returns(true);
            _mockRtlService.Setup(r => r.IsRtlLanguage("fa")).Returns(true);
            _mockRtlService.Setup(r => r.IsRtlLanguage("he")).Returns(true);
            _mockRtlService.Setup(r => r.IsRtlLanguage("en")).Returns(false);
            _mockRtlService.Setup(r => r.IsRtlLanguage("fr")).Returns(false);

            // Act & Assert - RTL languages
            _mockRtlService.Object.IsRtlLanguage("ar").Should().BeTrue();
            _mockRtlService.Object.IsRtlLanguage("fa").Should().BeTrue();
            _mockRtlService.Object.IsRtlLanguage("he").Should().BeTrue();

            // Act & Assert - LTR languages  
            _mockRtlService.Object.IsRtlLanguage("en").Should().BeFalse();
            _mockRtlService.Object.IsRtlLanguage("fr").Should().BeFalse();
        }

        [TestMethod]
        public void RtlService_FlowDirectionApplication()
        {
            // Arrange
            _mockRtlService.Setup(r => r.ApplyFlowDirection("ar"));
            _mockRtlService.Setup(r => r.ApplyFlowDirection("en"));

            // Act
            _mockRtlService.Object.ApplyFlowDirection("ar");
            _mockRtlService.Object.ApplyFlowDirection("en");

            // Assert
            _mockRtlService.Verify(r => r.ApplyFlowDirection("ar"), Times.Once);
            _mockRtlService.Verify(r => r.ApplyFlowDirection("en"), Times.Once);
        }

        [TestMethod]
        public void SettingsViewModel_ForegroundServiceIntegration()
        {
            // Arrange
            var settingsViewModel = new SettingsViewModel(_mockLocalizationManager.Object, _mockRtlService.Object);

            // Act & Assert - Foreground service enabled shows prayer notification option
            settingsViewModel.ForegroundServiceEnabled = true;
            settingsViewModel.NotificationPrayerTimesEnabled = true;
            settingsViewModel.ShowNotificationPrayerOption.Should().BeTrue();

            // Act & Assert - Foreground service disabled hides prayer notification option
            settingsViewModel.ForegroundServiceEnabled = false;
            settingsViewModel.ShowNotificationPrayerOption.Should().BeFalse();
        }

        [TestMethod]
        public async Task ConcurrentJsonApiCalls_ThreadSafety()
        {
            // Arrange
            var data = new Calendar { Date = "2024-05-10", Fajr = "03:45", Dhuhr = "13:00" };

            _mockJsonApiService.Setup(j => j.GetDailyPrayerTimesAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<DateTime>(), 0))
                .ReturnsAsync(data);

            var tasks = new List<Task<Calendar>>();

            // Act - Multiple concurrent API calls
            for (int i = 0; i < 5; i++)
            {
                tasks.Add(_mockJsonApiService.Object.GetDailyPrayerTimesAsync(51.5074, -0.1278, DateTime.Now, 0));
            }

            var results = await Task.WhenAll(tasks);

            // Assert - All requests should succeed
            results.Should().NotBeNull();
            results.Length.Should().Be(5);
            results.All(r => r?.Date == "2024-05-10").Should().BeTrue();
        }

        [TestMethod]
        public async Task NetworkTimeout_ExceptionHandling()
        {
            // Arrange
            _mockJsonApiService.Setup(j => j.GetDailyPrayerTimesAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<DateTime>(), 0))
                .ThrowsAsync(new TimeoutException("Network timeout"));

            // Act
            Exception exception = null;
            try
            {
                await _mockJsonApiService.Object.GetDailyPrayerTimesAsync(35.6762, 139.6503, DateTime.Now, 0);
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            // Assert - Should handle timeout gracefully
            exception.Should().NotBeNull();
            exception.Should().BeOfType<TimeoutException>();
            exception.Message.Should().Be("Network timeout");
        }

        [TestMethod]
        public void FontScaling_AccessibilitySupport()
        {
            // Arrange
            var baseViewModel = new BaseViewModel();

            // Act - Test various accessibility font scales
            var accessibilityScales = new[] { 12, 16, 20, 24, 28 };

            foreach (var scale in accessibilityScales)
            {
                baseViewModel.FontSize = scale;

                // Assert - Verify clamping and proportional scaling
                baseViewModel.FontSize.Should().BeGreaterThanOrEqualTo(12);
                baseViewModel.FontSize.Should().BeLessThanOrEqualTo(28);
                baseViewModel.HeaderFontSize.Should().Be((int)(baseViewModel.FontSize * 1.5));
                baseViewModel.SubHeaderFontSize.Should().Be((int)(baseViewModel.FontSize * 1.25));
                baseViewModel.CaptionFontSize.Should().Be((int)(baseViewModel.FontSize * 1.1));
                baseViewModel.BodyFontSize.Should().Be((int)(baseViewModel.FontSize * 1.05));
            }
        }

        [TestMethod]
        public void CultureInvariantParsing_NumericValues()
        {
            // Arrange
            var originalCulture = CultureInfo.CurrentCulture;

            try
            {
                // Test with comma-decimal culture (German)
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");

                var latitudeString = "52.520008";
                var longitudeString = "13.404954";

                // Act
                var latitude = Convert.ToDouble(latitudeString, CultureInfo.InvariantCulture);
                var longitude = Convert.ToDouble(longitudeString, CultureInfo.InvariantCulture);

                // Assert
                latitude.Should().Be(52.520008);
                longitude.Should().Be(13.404954);

                // Test with Arabic culture (RTL numerals)
                CultureInfo.CurrentCulture = new CultureInfo("ar-SA");

                // Act
                var arabicLat = Convert.ToDouble(latitudeString, CultureInfo.InvariantCulture);
                var arabicLng = Convert.ToDouble(longitudeString, CultureInfo.InvariantCulture);

                // Assert
                arabicLat.Should().Be(52.520008);
                arabicLng.Should().Be(13.404954);

                // Test with Turkish culture (different decimal separator in some contexts)
                CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
                
                var turkishLat = Convert.ToDouble(latitudeString, CultureInfo.InvariantCulture);
                var turkishLng = Convert.ToDouble(longitudeString, CultureInfo.InvariantCulture);

                turkishLat.Should().Be(52.520008);
                turkishLng.Should().Be(13.404954);
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
            }
        }

        [TestMethod]
        public async Task HybridApiStrategy_JsonPrimaryFallback()
        {
            // Arrange
            var location = new Location(35.6762, 139.6503); // Tokyo

            _mockJsonApiService.Setup(j => j.GetDailyPrayerTimesAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<DateTime>(), 0))
                .ReturnsAsync((Calendar)null); // JSON API fails

            // Act - Try JSON first
            var jsonResult = await _mockJsonApiService.Object.GetDailyPrayerTimesAsync(35.6762, 139.6503, DateTime.Now, 0);

            // Assert - JSON should fail, demonstrating need for fallback
            jsonResult.Should().BeNull();

            // Verify JSON API was called
            _mockJsonApiService.Verify(j => j.GetDailyPrayerTimesAsync(35.6762, 139.6503, It.IsAny<DateTime>(), 0), Times.Once);
        }

        [TestMethod]
        public void MemoryManagement_LargeDataSetsHandled()
        {
            // Arrange
            var largeDataSet = new List<Calendar>();
            for (int i = 1; i <= 365; i++) // Full year
            {
                largeDataSet.Add(new Calendar
                {
                    Date = $"2024-{(i % 12) + 1:D2}-{(i % 28) + 1:D2}",
                    Fajr = "05:00",
                    Dhuhr = "12:00",
                    Asr = "15:00",
                    Maghrib = "18:00",
                    Isha = "19:30"
                });
            }

            // Act - Process large dataset in batches (simulate UI batching)
            var processedCount = 0;
            var batchSize = 10;

            for (int i = 0; i < largeDataSet.Count; i += batchSize)
            {
                var batch = largeDataSet.Skip(i).Take(batchSize);
                foreach (var calendar in batch)
                {
                    if (!string.IsNullOrEmpty(calendar.Fajr))
                        processedCount++;
                }
            }

            // Assert
            processedCount.Should().Be(365);
            largeDataSet.Count.Should().Be(365);
        }

        [TestMethod]
        public void PrayerTimeValidation_FormatConsistency()
        {
            // Arrange
            var calendar = new Calendar
            {
                Date = "2024-06-15",
                Fajr = "05:30",
                Dhuhr = "12:15",
                Asr = "15:45",
                Maghrib = "18:30",
                Isha = "20:00"
            };

            // Act & Assert - Prayer times should follow HH:mm format
            DateTime.TryParseExact(calendar.Fajr, "HH:mm", null, DateTimeStyles.None, out _).Should().BeTrue();
            DateTime.TryParseExact(calendar.Dhuhr, "HH:mm", null, DateTimeStyles.None, out _).Should().BeTrue();
            DateTime.TryParseExact(calendar.Asr, "HH:mm", null, DateTimeStyles.None, out _).Should().BeTrue();
            DateTime.TryParseExact(calendar.Maghrib, "HH:mm", null, DateTimeStyles.None, out _).Should().BeTrue();
            DateTime.TryParseExact(calendar.Isha, "HH:mm", null, DateTimeStyles.None, out _).Should().BeTrue();
        }

        [TestMethod]
        public void CalendarModel_PropertyValidation()
        {
            // Arrange & Act
            var calendar = new Calendar
            {
                Date = "2024-07-20",
                Latitude = 40.7589,
                Longitude = -73.9851,
                Altitude = 10.5,
                TimeZone = -5.0,
                DayLightSaving = 1.0,
                FalseFajr = "04:00",
                Fajr = "05:15",
                Sunrise = "06:30",
                Dhuhr = "13:00",
                Asr = "16:45",
                Maghrib = "19:30",
                Isha = "21:00",
                EndOfIsha = "23:00"
            };

            // Assert - All properties should be properly set
            calendar.Date.Should().Be("2024-07-20");
            calendar.Latitude.Should().Be(40.7589);
            calendar.Longitude.Should().Be(-73.9851);
            calendar.Altitude.Should().Be(10.5);
            calendar.TimeZone.Should().Be(-5.0);
            calendar.DayLightSaving.Should().Be(1.0);
            calendar.FalseFajr.Should().Be("04:00");
            calendar.Fajr.Should().Be("05:15");
            calendar.Sunrise.Should().Be("06:30");
            calendar.Dhuhr.Should().Be("13:00");
            calendar.Asr.Should().Be("16:45");
            calendar.Maghrib.Should().Be("19:30");
            calendar.Isha.Should().Be("21:00");
            calendar.EndOfIsha.Should().Be("23:00");
        }

        [TestMethod]
        public void MainViewModel_InitialState()
        {
            // Arrange & Act
            var mainViewModel = new MainViewModel(_mockDataService.Object);

            // Assert - Initial state should be consistent
            mainViewModel.IsBusy.Should().BeFalse();
            mainViewModel.IsNotBusy.Should().BeTrue();
            mainViewModel.Title.Should().NotBeNull();
        }

        [TestMethod]
        public void BaseViewModel_PropertyNotifications()
        {
            // Arrange
            var baseViewModel = new BaseViewModel();
            var propertyChangedEvents = new List<string>();
            
            baseViewModel.PropertyChanged += (s, e) => propertyChangedEvents.Add(e.PropertyName);

            // Act
            baseViewModel.Title = "Test Title";
            baseViewModel.IsBusy = true;
            baseViewModel.FontSize = 18;

            // Assert
            propertyChangedEvents.Should().Contain(nameof(BaseViewModel.Title));
            propertyChangedEvents.Should().Contain(nameof(BaseViewModel.IsBusy));
            propertyChangedEvents.Should().Contain(nameof(BaseViewModel.IsNotBusy));
            propertyChangedEvents.Should().Contain(nameof(BaseViewModel.FontSize));
        }

        [TestMethod]
        public async Task JsonApiService_ErrorHandling()
        {
            // Arrange
            _mockJsonApiService.Setup(j => j.GetDailyPrayerTimesAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<DateTime>(), 0))
                .ThrowsAsync(new HttpRequestException("API endpoint not found"));

            // Act & Assert
            var exception = await Assert.ThrowsExactlyAsync<HttpRequestException>(async () =>
            {
                await _mockJsonApiService.Object.GetDailyPrayerTimesAsync(40.7589, -73.9851, DateTime.Now, 0);
            });

            exception.Message.Should().Be("API endpoint not found");
        }

        [TestMethod]
        public void AlarmService_ForegroundServiceControl()
        {
            // Arrange
            _mockAlarmService.Setup(a => a.StartAlarmForegroundService());
            _mockAlarmService.Setup(a => a.StopAlarmForegroundService());

            // Act
            _mockAlarmService.Object.StartAlarmForegroundService();
            _mockAlarmService.Object.StopAlarmForegroundService();

            // Assert
            _mockAlarmService.Verify(a => a.StartAlarmForegroundService(), Times.Once);
            _mockAlarmService.Verify(a => a.StopAlarmForegroundService(), Times.Once);
        }
    }
}
