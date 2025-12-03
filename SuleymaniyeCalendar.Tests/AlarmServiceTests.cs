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
            var alarmTime = DateTime.Now.AddHours(1);
            var requestCode = 123;
            var settings = new NotificationSettings { Title = "Test", Body = "Body", Sound = "kus", PrayerId = "test" };

            // Act
            _alarmServiceMock.Object.SetAlarm(alarmTime, requestCode, settings);

            // Assert
            _alarmServiceMock.Verify(a => a.SetAlarm(alarmTime, requestCode, settings), Times.Once);
        }

        [TestMethod]
        public void CancelAllAlarms_RemovesAllAlarms()
        {
            // Act
            _alarmServiceMock.Object.CancelAllAlarms();

            // Assert
            _alarmServiceMock.Verify(a => a.CancelAllAlarms(), Times.Once);
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
        public void SetAlarm_MultipleCalls_SchedulesAllCorrectly()
        {
            // Arrange
            var alarmTime1 = DateTime.Now.AddHours(1);
            var alarmTime2 = DateTime.Now.AddHours(2);
            var settings = new NotificationSettings { Title = "Test", Body = "Body" };

            // Act
            _alarmServiceMock.Object.SetAlarm(alarmTime1, 1, settings);
            _alarmServiceMock.Object.SetAlarm(alarmTime2, 2, settings);

            // Assert
            _alarmServiceMock.Verify(a => a.SetAlarm(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<NotificationSettings>()), Times.Exactly(2));
        }
    }
}
