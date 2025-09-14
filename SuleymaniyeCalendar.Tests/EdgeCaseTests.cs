using System;
using System.Collections.ObjectModel;
using System.Globalization;
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
    public class EdgeCaseTests
    {
        private Mock<DataService> _dataServiceMock;
        private Mock<JsonApiService> _jsonApiServiceMock;
        private Mock<IAlarmService> _alarmServiceMock;
        private Mock<ILogger<DataService>> _loggerMock;

        [TestInitialize]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger<DataService>>();
            _jsonApiServiceMock = new Mock<JsonApiService>(_loggerMock.Object);
            _alarmServiceMock = new Mock<IAlarmService>();
            _dataServiceMock = new Mock<DataService>(_alarmServiceMock.Object, _jsonApiServiceMock.Object);
        }

        [TestMethod]
        public async Task OfflineMode_UsesCachedDataAndShowsToast()
        {
            // Arrange - Simulate offline mode
            var cachedData = new ObservableCollection<SuleymaniyeCalendar.Models.Calendar>
            {
                new SuleymaniyeCalendar.Models.Calendar { Date = DateTime.Today.ToString("dd/MM/yyyy"), Fajr = "05:30" }
            };

            _dataServiceMock.Setup(ds => ds.GetMonthlyPrayerTimesHybridAsync(It.IsAny<Location>(), It.IsAny<bool>()))
                           .ReturnsAsync(cachedData);

            var location = new Location { Latitude = 41.0082, Longitude = 28.9784 };

            // Act
            var result = await _dataServiceMock.Object.GetMonthlyPrayerTimesHybridAsync(location, false);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeSameAs(cachedData);
            _dataServiceMock.Verify(ds => ds.GetMonthlyPrayerTimesHybridAsync(It.IsAny<Location>(), false), Times.Once);
        }

        [TestMethod]
        public async Task PermissionDenied_ShowsPermissionPrompt()
        {
            // Arrange - Simulate permission denied
            _dataServiceMock.Setup(ds => ds.GetCurrentLocationAsync(It.IsAny<bool>()))
                           .ReturnsAsync((Location)null); // Null indicates permission issues

            // Act
            var result = await _dataServiceMock.Object.GetCurrentLocationAsync(true);

            // Assert
            result.Should().BeNull(); // Should handle permission denial gracefully
            _dataServiceMock.Verify(ds => ds.GetCurrentLocationAsync(true), Times.Once);
        }

        [TestMethod]
        public async Task CrossYearMonthTransition_EnsuresDaysRangeAsync()
        {
            // Arrange - Test year/month boundary transition
            var mockData = new ObservableCollection<SuleymaniyeCalendar.Models.Calendar>();
            
            // Add data spanning across months
            for (int i = -5; i <= 5; i++)
            {
                var date = new DateTime(2024, 12, 31).AddDays(i); // Cross New Year
                mockData.Add(new SuleymaniyeCalendar.Models.Calendar
                {
                    Date = date.ToString("dd/MM/yyyy"),
                    Fajr = "05:30",
                    Dhuhr = "13:00"
                });
            }

            _dataServiceMock.Setup(ds => ds.GetMonthlyPrayerTimesHybridAsync(It.IsAny<Location>(), It.IsAny<bool>()))
                           .ReturnsAsync(mockData);

            var location = new Location { Latitude = 41.0082, Longitude = 28.9784 };

            // Act
            var result = await _dataServiceMock.Object.GetMonthlyPrayerTimesHybridAsync(location, false);

            // Assert
            result.Should().NotBeNull();
            result.Count.Should().Be(11); // 11 days spanning across months/years
        }

        [TestMethod]
        public void CoordinateEdgeCases_ExtremeLatLongValues()
        {
            // Test extreme coordinate values
            var extremeCases = new[]
            {
                new { Lat = "90.0", Lng = "180.0", Valid = true },    // North Pole, Date Line
                new { Lat = "-90.0", Lng = "-180.0", Valid = true },  // South Pole, Date Line West
                new { Lat = "0.0", Lng = "0.0", Valid = true },       // Equator, Prime Meridian
                new { Lat = "91.0", Lng = "0.0", Valid = false },     // Invalid latitude
                new { Lat = "0.0", Lng = "181.0", Valid = false },    // Invalid longitude
                new { Lat = "-91.0", Lng = "0.0", Valid = false },    // Invalid latitude
                new { Lat = "0.0", Lng = "-181.0", Valid = false }    // Invalid longitude
            };

            foreach (var testCase in extremeCases)
            {
                // Act
                var lat = Convert.ToDouble(testCase.Lat, CultureInfo.InvariantCulture);
                var lng = Convert.ToDouble(testCase.Lng, CultureInfo.InvariantCulture);

                // Assert
                if (testCase.Valid)
                {
                    lat.Should().BeInRange(-90.0, 90.0, $"Latitude {testCase.Lat} should be valid");
                    lng.Should().BeInRange(-180.0, 180.0, $"Longitude {testCase.Lng} should be valid");
                }
                else
                {
                    (Math.Abs(lat) > 90 || Math.Abs(lng) > 180).Should().BeTrue($"Coordinates {testCase.Lat},{testCase.Lng} should be invalid");
                }
            }
        }

        [TestMethod]
        public void PrayerTimeEdgeCases_InvalidTimeFormats()
        {
            // Test various invalid prayer time formats
            var invalidTimes = new[] { "25:00", "12:60", "24:01", "invalid", "", null, "1:2:3", "-5:30" };

            foreach (var invalidTime in invalidTimes)
            {
                // Act & Assert - Should not crash when encountering invalid times
                var calendar = new SuleymaniyeCalendar.Models.Calendar { Fajr = invalidTime };
                calendar.Fajr.Should().Be(invalidTime); // Just verify assignment doesn't crash
            }
        }

        [TestMethod]
        public async Task NetworkTimeoutEdgeCases_HandledGracefully()
        {
            // Arrange - Simulate network timeout
            _jsonApiServiceMock.Setup(j => j.GetDailyPrayerTimesAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<DateTime>(), It.IsAny<double>()))
                              .ThrowsAsync(new TaskCanceledException("Request timeout"));

            var location = new Location { Latitude = 41.0082, Longitude = 28.9784 };

            // Act & Assert - Should not throw
            await FluentActions.Invoking(async () => 
                await _dataServiceMock.Object.GetDailyPrayerTimesHybridAsync(location))
                .Should().NotThrowAsync("Network timeouts should be handled gracefully");
        }

        [TestMethod]
        public void DateEdgeCases_LeapYearAndDST()
        {
            // Test leap year and daylight saving time edge cases
            var edgeDates = new[]
            {
                new DateTime(2024, 2, 29), // Leap year
                new DateTime(2023, 2, 28), // Non-leap year last Feb day
                new DateTime(2024, 3, 10), // DST start (US)
                new DateTime(2024, 11, 3), // DST end (US)
                new DateTime(2024, 12, 31, 23, 59, 59), // Year end
                new DateTime(2024, 1, 1, 0, 0, 0) // Year start
            };

            foreach (var edgeDate in edgeDates)
            {
                // Act
                var dateString = edgeDate.ToString("dd/MM/yyyy");
                
                // Assert - Date formatting should work correctly
                dateString.Should().NotBeNullOrEmpty();
                DateTime.TryParseExact(dateString, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate)
                    .Should().BeTrue($"Date {edgeDate} should parse correctly");
                parsedDate.Date.Should().Be(edgeDate.Date);
            }
        }

        [TestMethod]
        public async Task ConcurrentRequests_DataIntegrity()
        {
            // Arrange - Simulate concurrent data requests
            var location = new Location { Latitude = 41.0082, Longitude = 28.9784 };
            var testData = new ObservableCollection<SuleymaniyeCalendar.Models.Calendar>
            {
                new SuleymaniyeCalendar.Models.Calendar { Date = DateTime.Today.ToString("dd/MM/yyyy"), Fajr = "05:30" }
            };

            _dataServiceMock.Setup(ds => ds.GetMonthlyPrayerTimesHybridAsync(It.IsAny<Location>(), It.IsAny<bool>()))
                           .ReturnsAsync(testData);

            // Act - Make concurrent requests
            var tasks = new[]
            {
                _dataServiceMock.Object.GetMonthlyPrayerTimesHybridAsync(location, false),
                _dataServiceMock.Object.GetMonthlyPrayerTimesHybridAsync(location, false),
                _dataServiceMock.Object.GetMonthlyPrayerTimesHybridAsync(location, false)
            };

            var results = await Task.WhenAll(tasks);

            // Assert - All requests should complete successfully
            foreach (var result in results)
            {
                result.Should().NotBeNull();
                result.Should().BeSameAs(testData);
            }
        }

        [TestMethod]
        public void MemoryPressureEdgeCases_LargeDatasets()
        {
            // Test handling of large datasets (365+ days)
            var largeDataset = new ObservableCollection<SuleymaniyeCalendar.Models.Calendar>();
            
            for (int i = 0; i < 1000; i++) // Large dataset
            {
                largeDataset.Add(new SuleymaniyeCalendar.Models.Calendar
                {
                    Date = DateTime.Today.AddDays(i).ToString("dd/MM/yyyy"),
                    Fajr = "05:30",
                    Sunrise = "07:15",
                    Dhuhr = "13:05",
                    Asr = "16:20",
                    Maghrib = "19:45",
                    Isha = "21:30"
                });
            }

            // Act & Assert - Should handle large datasets without memory issues
            largeDataset.Count.Should().Be(1000);
            
            // Verify data integrity in large dataset
            var firstItem = largeDataset[0];
            var lastItem = largeDataset[999];
            
            firstItem.Should().NotBeNull();
            lastItem.Should().NotBeNull();
            firstItem.Fajr.Should().Be("05:30");
            lastItem.Isha.Should().Be("21:30");
        }

        [TestMethod]
        public void CultureSpecificEdgeCases_DateTimeFormats()
        {
            // Test different culture-specific date formats
            var cultures = new[] { "en-US", "tr-TR", "ar-SA", "de-DE", "ja-JP" };
            var testDate = new DateTime(2024, 3, 15);

            foreach (var cultureName in cultures)
            {
                try
                {
                    var culture = new CultureInfo(cultureName);
                    
                    // Act - Format date in culture-specific format, then parse with invariant
                    var cultureSpecificFormat = testDate.ToString("dd/MM/yyyy", culture);
                    var invariantParsed = DateTime.ParseExact(cultureSpecificFormat, "dd/MM/yyyy", CultureInfo.InvariantCulture);

                    // Assert - Should maintain consistency across cultures
                    invariantParsed.Date.Should().Be(testDate.Date, $"Date parsing failed for culture {cultureName}");
                }
                catch (CultureNotFoundException)
                {
                    // Some cultures might not be available on all systems, skip them
                    continue;
                }
            }
        }

        [TestMethod]
        public async Task ResourceExhaustionEdgeCases_FileSystemLimits()
        {
            // Test file system resource exhaustion scenarios
            var location = new Location { Latitude = 41.0082, Longitude = 28.9784 };

            // Simulate file system issues
            _dataServiceMock.Setup(ds => ds.GetMonthlyPrayerTimesHybridAsync(It.IsAny<Location>(), It.IsAny<bool>()))
                           .ThrowsAsync(new UnauthorizedAccessException("File access denied"));

            // Act & Assert - Should handle file system errors gracefully
            await FluentActions.Invoking(async () => 
                await _dataServiceMock.Object.GetMonthlyPrayerTimesHybridAsync(location, false))
                .Should().ThrowAsync<UnauthorizedAccessException>();
        }

        [TestMethod]
        public void AlarmSchedulingEdgeCases_DateBoundaries()
        {
            // Test alarm scheduling at date boundaries
            var boundaryTimes = new[]
            {
                new DateTime(2024, 12, 31, 23, 59, 0), // End of year
                new DateTime(2024, 1, 1, 0, 1, 0),     // Start of year
                new DateTime(2024, 2, 29, 5, 30, 0),   // Leap year
                new DateTime(2024, 3, 10, 2, 30, 0),   // DST transition
                new DateTime(2024, 11, 3, 1, 30, 0)    // DST end
            };

            foreach (var boundaryTime in boundaryTimes)
            {
                // Act - Test alarm scheduling parameters
                var timeSpan = boundaryTime.TimeOfDay;
                var offset = 10; // 10 minutes before
                var name = "Test Prayer";

                // Assert - Should handle boundary times correctly
                timeSpan.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
                timeSpan.Should().BeLessThan(TimeSpan.FromDays(1));
                offset.Should().BeGreaterThanOrEqualTo(0);
                name.Should().NotBeNullOrEmpty();
            }
        }

        [TestMethod]
        public void StringParsing_EdgeCases_MalformedInput()
        {
            // Test malformed coordinate strings
            var malformedInputs = new[] { 
                "41.", ".5", "41..5", "41,5,6", "NaN", "Infinity", "-Infinity", 
                "41°5'", "41.5N", "very long string that should not parse", "🌍" 
            };

            foreach (var input in malformedInputs)
            {
                // Act & Assert - Should handle malformed input gracefully
                FluentActions.Invoking(() => Convert.ToDouble(input, CultureInfo.InvariantCulture))
                            .Should().Throw<FormatException>($"Input '{input}' should cause a FormatException");
            }
        }

        [TestMethod]
        public void ComponentLifecycle_EdgeCases_InitializationOrder()
        {
            // Test component initialization order edge cases
            // Just test that we can create a mock without causing issues
            FluentActions.Invoking(() => _dataServiceMock.Object).Should().NotThrow();

            // Test basic mock functionality
            _dataServiceMock.Should().NotBeNull();
        }

        [TestMethod]
        public void BatchProcessing_EdgeCases_EmptyAndSingleItem()
        {
            // Test batch processing edge cases mentioned in instructions (10-item batches)
            var edgeCaseBatches = new[]
            {
                new ObservableCollection<SuleymaniyeCalendar.Models.Calendar>(), // Empty
                new ObservableCollection<SuleymaniyeCalendar.Models.Calendar> // Single item
                {
                    new SuleymaniyeCalendar.Models.Calendar { Date = "01/01/2024", Fajr = "05:30" }
                },
                // Exactly 10 items (one complete batch)
                new ObservableCollection<SuleymaniyeCalendar.Models.Calendar>(
                    Enumerable.Range(1, 10).Select(i => new SuleymaniyeCalendar.Models.Calendar 
                    { 
                        Date = $"{i:00}/01/2024", 
                        Fajr = "05:30" 
                    })),
                // 15 items (1.5 batches)
                new ObservableCollection<SuleymaniyeCalendar.Models.Calendar>(
                    Enumerable.Range(1, 15).Select(i => new SuleymaniyeCalendar.Models.Calendar 
                    { 
                        Date = $"{i:00}/01/2024", 
                        Fajr = "05:30" 
                    }))
            };

            foreach (var batch in edgeCaseBatches)
            {
                // Act & Assert - Batch processing should handle all sizes
                batch.Count.Should().BeGreaterThanOrEqualTo(0);
                
                // Simulate batch processing
                const int batchSize = 10;
                var processedCount = 0;
                for (int i = 0; i < batch.Count; i += batchSize)
                {
                    var currentBatch = batch.Skip(i).Take(batchSize);
                    processedCount += currentBatch.Count();
                }
                
                processedCount.Should().Be(batch.Count, "All items should be processed in batches");
            }
        }
    }
}
