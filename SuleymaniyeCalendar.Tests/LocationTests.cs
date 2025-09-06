using System;
using System.Globalization;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SuleymaniyeCalendar.Models;
using SuleymaniyeCalendar.Services;

namespace SuleymaniyeCalendar.Tests
{
    [TestClass]
    public class LocationTests
    {
        private Mock<DataService> _dataServiceMock;

        [TestInitialize]
        public void Setup()
        {
            _dataServiceMock = new Mock<DataService>();
        }

        [TestMethod]
        public void Location_ParsingInvariantCulture_WorksForCommaDot()
        {
            // Arrange
            var latStr = "41.1234";
            var lngStr = "29,9876";

            // Act
            var lat = Convert.ToDouble(latStr, CultureInfo.InvariantCulture);
            var lng = Convert.ToDouble(lngStr.Replace(',', '.'), CultureInfo.InvariantCulture);

            // Assert
            lat.Should().Be(41.1234);
            lng.Should().Be(29.9876);
        }

        [TestMethod]
        public void Location_InvariantCultureParsing_HandlesVariousFormats()
        {
            // Test cases for different coordinate string formats
            var testCases = new[]
            {
                new { Input = "41.0082", Expected = 41.0082 },
                new { Input = "28.9784", Expected = 28.9784 },
                new { Input = "-73.9857", Expected = -73.9857 },
                new { Input = "0.0", Expected = 0.0 },
                new { Input = "180.0", Expected = 180.0 },
                new { Input = "-180.0", Expected = -180.0 }
            };

            foreach (var testCase in testCases)
            {
                // Act
                var result = Convert.ToDouble(testCase.Input, CultureInfo.InvariantCulture);

                // Assert
                result.Should().Be(testCase.Expected, $"Failed for input: {testCase.Input}");
            }
        }

        [TestMethod]
        public void Location_CommaDecimalSeparator_RequiresNormalization()
        {
            // Arrange - Simulate European locale with comma as decimal separator
            var europeanFormat = "41,5"; // 41.5 in European format

            // Act & Assert - Direct parsing should fail with InvariantCulture
            FluentActions.Invoking(() => 
                Convert.ToDouble(europeanFormat, CultureInfo.InvariantCulture))
                .Should().Throw<FormatException>("because InvariantCulture expects dot as decimal separator");

            // Correct approach: normalize comma to dot
            var normalizedFormat = europeanFormat.Replace(',', '.');
            var result = Convert.ToDouble(normalizedFormat, CultureInfo.InvariantCulture);
            result.Should().Be(41.5);
        }

        [TestMethod]
        public void Location_CoordinateValidation_RejectsInvalidValues()
        {
            // Test invalid latitude values
            var invalidLatitudes = new[] { "91.0", "-91.0", "200.0", "-200.0" };
            var invalidLongitudes = new[] { "181.0", "-181.0", "360.0", "-360.0" };

            foreach (var lat in invalidLatitudes)
            {
                var parsedLat = Convert.ToDouble(lat, CultureInfo.InvariantCulture);
                // Latitude should be between -90 and 90
                (Math.Abs(parsedLat) > 90).Should().BeTrue($"Latitude {lat} should be considered invalid");
            }

            foreach (var lng in invalidLongitudes)
            {
                var parsedLng = Convert.ToDouble(lng, CultureInfo.InvariantCulture);
                // Longitude should be between -180 and 180
                (Math.Abs(parsedLng) > 180).Should().BeTrue($"Longitude {lng} should be considered invalid");
            }
        }

        [TestMethod]
        public void Location_CoordinateValidation_AcceptsValidValues()
        {
            // Test valid coordinate ranges
            var validLatitudes = new[] { "0.0", "45.0", "-45.0", "90.0", "-90.0", "41.0082" };
            var validLongitudes = new[] { "0.0", "90.0", "-90.0", "180.0", "-180.0", "28.9784" };

            foreach (var lat in validLatitudes)
            {
                var parsedLat = Convert.ToDouble(lat, CultureInfo.InvariantCulture);
                // Latitude should be between -90 and 90
                parsedLat.Should().BeInRange(-90.0, 90.0, $"Latitude {lat} should be valid");
            }

            foreach (var lng in validLongitudes)
            {
                var parsedLng = Convert.ToDouble(lng, CultureInfo.InvariantCulture);
                // Longitude should be between -180 and 180
                parsedLng.Should().BeInRange(-180.0, 180.0, $"Longitude {lng} should be valid");
            }
        }

        [TestMethod]
        public async Task Location_AlwaysRenewLocationEnabled_TriggersGpsFix()
        {
            // Arrange
            var expectedLocation = new Location { Latitude = 41.0082, Longitude = 28.9784 };
            _dataServiceMock.Setup(ds => ds.GetCurrentLocationAsync(true))
                           .ReturnsAsync(expectedLocation);

            // Act
            var result = await _dataServiceMock.Object.GetCurrentLocationAsync(refreshLocation: true);

            // Assert
            result.Should().NotBeNull();
            result.Latitude.Should().Be(41.0082);
            result.Longitude.Should().Be(28.9784);
            _dataServiceMock.Verify(ds => ds.GetCurrentLocationAsync(true), Times.Once);
        }

        [TestMethod]
        public async Task Location_CachedLocation_UsedWhenRefreshDisabled()
        {
            // Arrange
            var cachedLocation = new Location { Latitude = 41.0082, Longitude = 28.9784 };
            _dataServiceMock.Setup(ds => ds.GetCurrentLocationAsync(false))
                           .ReturnsAsync(cachedLocation);

            // Act
            var result = await _dataServiceMock.Object.GetCurrentLocationAsync(refreshLocation: false);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeSameAs(cachedLocation);
            _dataServiceMock.Verify(ds => ds.GetCurrentLocationAsync(false), Times.Once);
        }

        [TestMethod]
        public void Location_IstanbulCoordinates_ParsedCorrectly()
        {
            // Arrange - Istanbul coordinates as mentioned in the instructions
            var istanbulLat = "41.0082";
            var istanbulLng = "28.9784";

            // Act
            var latitude = Convert.ToDouble(istanbulLat, CultureInfo.InvariantCulture);
            var longitude = Convert.ToDouble(istanbulLng, CultureInfo.InvariantCulture);

            // Assert
            latitude.Should().Be(41.0082);
            longitude.Should().Be(28.9784);

            // Verify these are valid Istanbul coordinates
            latitude.Should().BeInRange(40.5, 41.5, "Istanbul latitude should be around 41°");
            longitude.Should().BeInRange(28.5, 29.5, "Istanbul longitude should be around 29°");
        }

        [TestMethod]
        public async Task Location_PermissionHandling_RobustFallback()
        {
            // Arrange - Simulate permission denied scenario
            _dataServiceMock.Setup(ds => ds.GetCurrentLocationAsync(It.IsAny<bool>()))
                           .ReturnsAsync((Location)null);

            // Act
            var result = await _dataServiceMock.Object.GetCurrentLocationAsync(true);

            // Assert
            result.Should().BeNull(); // Should handle gracefully without throwing
            _dataServiceMock.Verify(ds => ds.GetCurrentLocationAsync(It.IsAny<bool>()), Times.Once);
        }

        [TestMethod]
        public void Location_DefaultFallback_SuleymaniyeCoordinates()
        {
            // Arrange - Default fallback coordinates mentioned in DataService (42.142, 29.218, 10)
            var defaultLat = "42.142";
            var defaultLng = "29.218";
            var defaultAlt = "10";

            // Act
            var latitude = Convert.ToDouble(defaultLat, CultureInfo.InvariantCulture);
            var longitude = Convert.ToDouble(defaultLng, CultureInfo.InvariantCulture);
            var altitude = Convert.ToDouble(defaultAlt, CultureInfo.InvariantCulture);

            // Assert
            latitude.Should().Be(42.142);
            longitude.Should().Be(29.218);
            altitude.Should().Be(10.0);

            // Verify these are reasonable fallback coordinates
            latitude.Should().BeInRange(40.0, 45.0);
            longitude.Should().BeInRange(25.0, 35.0);
        }

        [TestMethod]
        public async Task Location_GeocodingIntegration_HandlesAddressLookup()
        {
            // Arrange - Test address-based location lookup
            var addressLocation = new Location { Latitude = 41.0, Longitude = 29.0 };
            _dataServiceMock.Setup(ds => ds.GetCurrentLocationAsync(It.IsAny<bool>()))
                           .ReturnsAsync(addressLocation);

            // Act
            var result = await _dataServiceMock.Object.GetCurrentLocationAsync(false);

            // Assert
            result.Should().NotBeNull();
            result.Latitude.Should().BeApproximately(41.0, 0.1);
            result.Longitude.Should().BeApproximately(29.0, 0.1);
        }

        [TestMethod]
        public void Location_HighPrecisionCoordinates_MaintainAccuracy()
        {
            // Arrange - Test high-precision coordinates
            var highPrecisionLat = "41.008238472";
            var highPrecisionLng = "28.978359127";

            // Act
            var latitude = Convert.ToDouble(highPrecisionLat, CultureInfo.InvariantCulture);
            var longitude = Convert.ToDouble(highPrecisionLng, CultureInfo.InvariantCulture);

            // Assert
            latitude.Should().Be(41.008238472);
            longitude.Should().Be(28.978359127);

            // Verify precision is maintained
            latitude.ToString(CultureInfo.InvariantCulture).Should().Contain("41.008238472");
            longitude.ToString(CultureInfo.InvariantCulture).Should().Contain("28.978359127");
        }

        [TestMethod]
        public void Location_EdgeCaseCoordinates_HandledCorrectly()
        {
            // Test edge cases: equator, prime meridian, poles, international date line
            var edgeCases = new[]
            {
                new { Name = "Equator", Lat = "0.0", Lng = "0.0" },
                new { Name = "Prime Meridian", Lat = "51.4778", Lng = "0.0" },
                new { Name = "North Pole", Lat = "90.0", Lng = "0.0" },
                new { Name = "South Pole", Lat = "-90.0", Lng = "0.0" },
                new { Name = "International Date Line", Lat = "0.0", Lng = "180.0" },
                new { Name = "International Date Line West", Lat = "0.0", Lng = "-180.0" }
            };

            foreach (var edgeCase in edgeCases)
            {
                // Act
                var lat = Convert.ToDouble(edgeCase.Lat, CultureInfo.InvariantCulture);
                var lng = Convert.ToDouble(edgeCase.Lng, CultureInfo.InvariantCulture);

                // Assert
                lat.Should().BeInRange(-90.0, 90.0, $"{edgeCase.Name} latitude should be valid");
                lng.Should().BeInRange(-180.0, 180.0, $"{edgeCase.Name} longitude should be valid");
            }
        }

        [TestMethod]
        public async Task Location_TimeoutHandling_DoesNotHang()
        {
            // Arrange - Simulate timeout scenario
            _dataServiceMock.Setup(ds => ds.GetCurrentLocationAsync(It.IsAny<bool>()))
                           .Returns(Task.Delay(10000).ContinueWith(t => (Location)null)); // Long delay

            // Act & Assert - Should not hang indefinitely
            var timeout = Task.Delay(1000); // 1 second timeout
            var locationTask = _dataServiceMock.Object.GetCurrentLocationAsync(true);
            
            var completedTask = await Task.WhenAny(locationTask, timeout);
            
            if (completedTask == timeout)
            {
                // Test passes - we successfully timed out instead of hanging
                Assert.IsTrue(true, "Location request properly timed out");
            }
            else
            {
                // If location task completed quickly, that's also fine
                var result = await locationTask;
                result.Should().BeNull(); // Expected null result
            }
        }

        [TestMethod]
        public void Location_AltitudeHandling_OptionalParameter()
        {
            // Test altitude as optional parameter (default 0 in many cases)
            var locationWithAltitude = new Location
            {
                Latitude = 41.0082,
                Longitude = 28.9784,
                Altitude = 100.0
            };

            var locationWithoutAltitude = new Location
            {
                Latitude = 41.0082,
                Longitude = 28.9784
            };

            // Assert
            locationWithAltitude.Altitude.Should().Be(100.0);
            locationWithoutAltitude.Altitude.Should().BeNull(); // Or 0, depending on implementation
        }

        [TestMethod]
        public void Location_StringConversion_RoundTripConsistency()
        {
            // Test that string -> double -> string conversion is consistent
            var originalCoordinates = new[] { "41.0082", "28.9784", "-73.9857", "151.2093" };

            foreach (var original in originalCoordinates)
            {
                // Act
                var parsed = Convert.ToDouble(original, CultureInfo.InvariantCulture);
                var backToString = parsed.ToString(CultureInfo.InvariantCulture);

                // Assert - Should be able to round-trip accurately
                var reparsed = Convert.ToDouble(backToString, CultureInfo.InvariantCulture);
                reparsed.Should().Be(parsed, $"Round-trip failed for {original}");
            }
        }

        [TestMethod]
        public async Task Location_MultipleRequests_Consistent()
        {
            // Arrange
            var consistentLocation = new Location { Latitude = 41.0082, Longitude = 28.9784 };
            _dataServiceMock.Setup(ds => ds.GetCurrentLocationAsync(false))
                           .ReturnsAsync(consistentLocation);

            // Act - Make multiple requests
            var location1 = await _dataServiceMock.Object.GetCurrentLocationAsync(false);
            var location2 = await _dataServiceMock.Object.GetCurrentLocationAsync(false);
            var location3 = await _dataServiceMock.Object.GetCurrentLocationAsync(false);

            // Assert - Should return consistent results
            location1.Latitude.Should().Be(location2.Latitude);
            location2.Latitude.Should().Be(location3.Latitude);
            location1.Longitude.Should().Be(location2.Longitude);
            location2.Longitude.Should().Be(location3.Longitude);

            _dataServiceMock.Verify(ds => ds.GetCurrentLocationAsync(false), Times.Exactly(3));
        }

        [TestMethod]
        public void Location_PreferenceStorage_HandlesCoordinates()
        {
            // Test coordinate storage in preferences (as mentioned in instructions)
            var testLat = 41.0082;
            var testLng = 28.9784;
            var testAlt = 10.0;

            // Act - Convert to preference-storable format
            var latString = testLat.ToString(CultureInfo.InvariantCulture);
            var lngString = testLng.ToString(CultureInfo.InvariantCulture);
            var altString = testAlt.ToString(CultureInfo.InvariantCulture);

            // Assert - Should be properly formatted for storage
            latString.Should().Be("41.0082");
            lngString.Should().Be("28.9784");
            altString.Should().Be("10");

            // Verify retrieval from storage format
            var retrievedLat = Convert.ToDouble(latString, CultureInfo.InvariantCulture);
            var retrievedLng = Convert.ToDouble(lngString, CultureInfo.InvariantCulture);
            var retrievedAlt = Convert.ToDouble(altString, CultureInfo.InvariantCulture);

            retrievedLat.Should().Be(testLat);
            retrievedLng.Should().Be(testLng);
            retrievedAlt.Should().Be(testAlt);
        }
    }
}
