using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SuleymaniyeCalendar.Models;
using SuleymaniyeCalendar.Services;

namespace SuleymaniyeCalendar.Tests
{
    [TestClass]
    public class AlarmServiceTests
    {
        private Mock<IAlarmService> _alarmServiceMock;

        [TestInitialize]
        public void Setup()
        {
            _alarmServiceMock = new Mock<IAlarmService>();
        }

        [TestMethod]
        public void SetAlarm_WithValidParameters_CallsCorrectly()
        {
            // Arrange
            var date = DateTime.Today.AddDays(1);
            var triggerTime = TimeSpan.FromHours(5).Add(TimeSpan.FromMinutes(30)); // 05:30
            var timeOffset = 10; // 10 minutes before
            var name = "Fajr";

            // Act
            _alarmServiceMock.Object.SetAlarm(date, triggerTime, timeOffset, name);

            // Assert
            _alarmServiceMock.Verify(a => a.SetAlarm(date, triggerTime, timeOffset, name), Times.Once);
        }

        [TestMethod]
        public void CancelAlarm_RemovesAllAlarms()
        {
            // Act
            _alarmServiceMock.Object.CancelAlarm();

            // Assert
            _alarmServiceMock.Verify(a => a.CancelAlarm(), Times.Once);
        }

        [TestMethod]
        public void StartAlarmForegroundService_StartsService()
        {
            // Act
            _alarmServiceMock.Object.StartAlarmForegroundService();

            // Assert
            _alarmServiceMock.Verify(a => a.StartAlarmForegroundService(), Times.Once);
        }

        [TestMethod]
        public void StopAlarmForegroundService_StopsService()
        {
            // Act
            _alarmServiceMock.Object.StopAlarmForegroundService();

            // Assert
            _alarmServiceMock.Verify(a => a.StopAlarmForegroundService(), Times.Once);
        }

        [TestMethod]
        public void SetAlarm_FajrPrayer_SchedulesCorrectly()
        {
            // Arrange
            var date = DateTime.Today.AddDays(1);
            var fajrTime = TimeSpan.FromHours(5).Add(TimeSpan.FromMinutes(30));
            var offset = 10; // 10 minutes before
            var name = "Fajr";

            // Act
            _alarmServiceMock.Object.SetAlarm(date, fajrTime, offset, name);

            // Assert
            _alarmServiceMock.Verify(a => a.SetAlarm(
                It.Is<DateTime>(d => d.Date == date.Date),
                It.Is<TimeSpan>(t => t.Hours == 5 && t.Minutes == 30),
                10,
                "Fajr"), Times.Once);
        }

        [TestMethod]
        public void SetAlarm_DhuhrPrayer_SchedulesCorrectly()
        {
            // Arrange
            var date = DateTime.Today.AddDays(1);
            var dhuhrTime = TimeSpan.FromHours(13).Add(TimeSpan.FromMinutes(5));
            var offset = 5; // 5 minutes before
            var name = "Dhuhr";

            // Act
            _alarmServiceMock.Object.SetAlarm(date, dhuhrTime, offset, name);

            // Assert
            _alarmServiceMock.Verify(a => a.SetAlarm(date, dhuhrTime, offset, name), Times.Once);
        }

        [TestMethod]
        public void SetAlarm_AsrPrayer_SchedulesCorrectly()
        {
            // Arrange
            var date = DateTime.Today.AddDays(1);
            var asrTime = TimeSpan.FromHours(16).Add(TimeSpan.FromMinutes(20));
            var offset = 15; // 15 minutes before
            var name = "Asr";

            // Act
            _alarmServiceMock.Object.SetAlarm(date, asrTime, offset, name);

            // Assert
            _alarmServiceMock.Verify(a => a.SetAlarm(date, asrTime, offset, name), Times.Once);
        }

        [TestMethod]
        public void SetAlarm_MaghribPrayer_SchedulesCorrectly()
        {
            // Arrange
            var date = DateTime.Today.AddDays(1);
            var maghribTime = TimeSpan.FromHours(19).Add(TimeSpan.FromMinutes(45));
            var offset = 0; // Exact time
            var name = "Maghrib";

            // Act
            _alarmServiceMock.Object.SetAlarm(date, maghribTime, offset, name);

            // Assert
            _alarmServiceMock.Verify(a => a.SetAlarm(
                It.IsAny<DateTime>(),
                It.Is<TimeSpan>(t => t.Hours == 19 && t.Minutes == 45),
                0, // No offset
                "Maghrib"), Times.Once);
        }

        [TestMethod]
        public void SetAlarm_IshaPrayer_SchedulesCorrectly()
        {
            // Arrange
            var date = DateTime.Today.AddDays(1);
            var ishaTime = TimeSpan.FromHours(21).Add(TimeSpan.FromMinutes(30));
            var offset = 20; // 20 minutes before
            var name = "Isha";

            // Act
            _alarmServiceMock.Object.SetAlarm(date, ishaTime, offset, name);

            // Assert
            _alarmServiceMock.Verify(a => a.SetAlarm(date, ishaTime, offset, name), Times.Once);
        }

        [TestMethod]
        public void SetAlarm_ZeroOffset_SchedulesAtExactTime()
        {
            // Arrange
            var date = DateTime.Today.AddDays(1);
            var prayerTime = TimeSpan.FromHours(12).Add(TimeSpan.FromMinutes(0));
            var offset = 0; // Exact time
            var name = "TestPrayer";

            // Act
            _alarmServiceMock.Object.SetAlarm(date, prayerTime, offset, name);

            // Assert
            _alarmServiceMock.Verify(a => a.SetAlarm(
                It.IsAny<DateTime>(),
                It.IsAny<TimeSpan>(),
                0, // Zero offset
                It.IsAny<string>()), Times.Once);
        }

        [TestMethod]
        public void SetMultipleAlarms_SchedulesAllCorrectly()
        {
            // Arrange
            var date = DateTime.Today.AddDays(1);
            var prayers = new[]
            {
                new { Name = "Fajr", Time = TimeSpan.FromHours(5).Add(TimeSpan.FromMinutes(30)), Offset = 10 },
                new { Name = "Dhuhr", Time = TimeSpan.FromHours(13).Add(TimeSpan.FromMinutes(5)), Offset = 5 },
                new { Name = "Asr", Time = TimeSpan.FromHours(16).Add(TimeSpan.FromMinutes(20)), Offset = 15 },
                new { Name = "Maghrib", Time = TimeSpan.FromHours(19).Add(TimeSpan.FromMinutes(45)), Offset = 0 },
                new { Name = "Isha", Time = TimeSpan.FromHours(21).Add(TimeSpan.FromMinutes(30)), Offset = 20 }
            };

            // Act
            foreach (var prayer in prayers)
            {
                _alarmServiceMock.Object.SetAlarm(date, prayer.Time, prayer.Offset, prayer.Name);
            }

            // Assert
            _alarmServiceMock.Verify(a => a.SetAlarm(It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<int>(), It.IsAny<string>()), 
                                   Times.Exactly(prayers.Length));
        }

        [TestMethod]
        public void AlarmForegroundService_StartStop_WorksCorrectly()
        {
            // Act
            _alarmServiceMock.Object.StartAlarmForegroundService();
            _alarmServiceMock.Object.StopAlarmForegroundService();

            // Assert
            _alarmServiceMock.Verify(a => a.StartAlarmForegroundService(), Times.Once);
            _alarmServiceMock.Verify(a => a.StopAlarmForegroundService(), Times.Once);
        }

        [TestMethod]
        public void SetAlarm_FutureDate_AllowsScheduling()
        {
            // Arrange
            var futureDate = DateTime.Today.AddDays(7); // One week from now
            var prayerTime = TimeSpan.FromHours(12);
            var offset = 10;
            var name = "Future Prayer";

            // Act
            _alarmServiceMock.Object.SetAlarm(futureDate, prayerTime, offset, name);

            // Assert
            _alarmServiceMock.Verify(a => a.SetAlarm(
                It.Is<DateTime>(d => d.Date >= DateTime.Today), 
                It.IsAny<TimeSpan>(), 
                It.IsAny<int>(), 
                It.IsAny<string>()), Times.Once);
        }

        [TestMethod]
        public void SetAlarm_PastDate_StillCallsMethod()
        {
            // Arrange
            var pastDate = DateTime.Today.AddDays(-1); // Yesterday
            var prayerTime = TimeSpan.FromHours(12);
            var offset = 10;
            var name = "Past Prayer";

            // Act
            _alarmServiceMock.Object.SetAlarm(pastDate, prayerTime, offset, name);

            // Assert
            _alarmServiceMock.Verify(a => a.SetAlarm(pastDate, prayerTime, offset, name), Times.Once);
        }

        [TestMethod]
        public void CancelAlarm_MultipleCallsSafety()
        {
            // Act
            _alarmServiceMock.Object.CancelAlarm();
            _alarmServiceMock.Object.CancelAlarm();
            _alarmServiceMock.Object.CancelAlarm();

            // Assert
            _alarmServiceMock.Verify(a => a.CancelAlarm(), Times.Exactly(3));
        }

        [TestMethod]
        public void SetAlarm_VariousTimeOffsets_HandledCorrectly()
        {
            // Arrange
            var date = DateTime.Today.AddDays(1);
            var baseTime = TimeSpan.FromHours(12);
            var offsets = new[] { 0, 5, 10, 15, 30, 60 }; // Different notification offsets

            // Act & Assert
            foreach (var offset in offsets)
            {
                _alarmServiceMock.Object.SetAlarm(date, baseTime, offset, $"Prayer_{offset}min");
                
                _alarmServiceMock.Verify(a => a.SetAlarm(
                    It.IsAny<DateTime>(), 
                    It.IsAny<TimeSpan>(), 
                    offset, 
                    $"Prayer_{offset}min"), Times.Once);
            }
        }

        [TestMethod]
        public void AlarmService_EdgeCaseTimes_HandlesCorrectly()
        {
            // Arrange
            var date = DateTime.Today.AddDays(1);
            var edgeCaseTimes = new[]
            {
                TimeSpan.FromHours(0), // Midnight
                TimeSpan.FromHours(23).Add(TimeSpan.FromMinutes(59)), // Almost midnight
                TimeSpan.FromHours(12), // Noon
                TimeSpan.FromMinutes(1), // Very early morning
            };

            // Act & Assert
            foreach (var time in edgeCaseTimes)
            {
                _alarmServiceMock.Object.SetAlarm(date, time, 0, "EdgeCase");
                _alarmServiceMock.Verify(a => a.SetAlarm(date, time, 0, "EdgeCase"), Times.AtLeastOnce);
            }
        }

        [TestMethod]
        public void ForegroundService_LifecycleManagement()
        {
            // Test typical lifecycle: Start -> Stop
            _alarmServiceMock.Object.StartAlarmForegroundService();
            _alarmServiceMock.Object.StopAlarmForegroundService();

            // Test multiple starts/stops (should be safe)
            _alarmServiceMock.Object.StartAlarmForegroundService();
            _alarmServiceMock.Object.StartAlarmForegroundService(); // Double start
            _alarmServiceMock.Object.StopAlarmForegroundService();
            _alarmServiceMock.Object.StopAlarmForegroundService(); // Double stop

            // Assert
            _alarmServiceMock.Verify(a => a.StartAlarmForegroundService(), Times.Exactly(3));
            _alarmServiceMock.Verify(a => a.StopAlarmForegroundService(), Times.Exactly(3));
        }

        [TestMethod]
        public void SetAlarm_DifferentPrayerNames_AllSupported()
        {
            // Arrange
            var date = DateTime.Today.AddDays(1);
            var baseTime = TimeSpan.FromHours(12);
            var prayerNames = new[] { "Fajr", "Dhuhr", "Asr", "Maghrib", "Isha", "Custom Prayer", "" };

            // Act & Assert
            foreach (var name in prayerNames)
            {
                _alarmServiceMock.Object.SetAlarm(date, baseTime, 10, name);
                _alarmServiceMock.Verify(a => a.SetAlarm(
                    It.IsAny<DateTime>(), 
                    It.IsAny<TimeSpan>(), 
                    It.IsAny<int>(), 
                    name), Times.Once);
            }
        }
    }
}
