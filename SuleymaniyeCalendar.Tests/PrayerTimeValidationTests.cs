using Microsoft.VisualStudio.TestTools.UnitTesting;
using SuleymaniyeCalendar.Helpers;

namespace SuleymaniyeCalendar.Tests;

[TestClass]
public class PrayerTimeValidationTests
{
    [DataTestMethod]
    [DataRow("00:00", 0)]
    [DataRow("05:30", 330)]
    [DataRow("5:30", 330)]
    [DataRow("23:59", 1439)]
    public void TryParsePrayerTime_ValidTime_ReturnsTime(string value, int minutes)
    {
        Assert.IsTrue(AppConstants.TryParsePrayerTime(value, out var time));
        Assert.AreEqual(TimeSpan.FromMinutes(minutes), time);
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("invalid")]
    [DataRow("0")]
    [DataRow("1.02:03")]
    [DataRow("24:00")]
    [DataRow("-01:00")]
    public void TryParsePrayerTime_InvalidTime_ReturnsFalse(string value)
    {
        Assert.IsFalse(AppConstants.TryParsePrayerTime(value, out _));
    }
}
