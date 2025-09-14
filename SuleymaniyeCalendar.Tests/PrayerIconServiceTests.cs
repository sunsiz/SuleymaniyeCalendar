using Microsoft.VisualStudio.TestTools.UnitTesting;
using SuleymaniyeCalendar.Models;
using SuleymaniyeCalendar.Services;

namespace SuleymaniyeCalendar.Tests
{
    [TestClass]
    public class PrayerIconServiceTests
    {
        [TestMethod]
        public void GetPrayerIconById_ShouldReturnCorrectIcons()
        {
            // Test main prayer icons by ID
            Assert.AreEqual("sunrise.svg", PrayerIconService.GetPrayerIconById("fajr"));
            Assert.AreEqual("sunrise.svg", PrayerIconService.GetPrayerIconById("falsefajr"));
            Assert.AreEqual("clearday.svg", PrayerIconService.GetPrayerIconById("dhuhr"));
            Assert.AreEqual("partlycloudyday.svg", PrayerIconService.GetPrayerIconById("asr"));
            Assert.AreEqual("sunset.svg", PrayerIconService.GetPrayerIconById("maghrib"));
            Assert.AreEqual("starrynight.svg", PrayerIconService.GetPrayerIconById("isha"));
            Assert.AreEqual("starrynight.svg", PrayerIconService.GetPrayerIconById("endofisha"));
            Assert.AreEqual("sunrise.svg", PrayerIconService.GetPrayerIconById("sunrise"));
            
            // Test default fallback
            Assert.AreEqual("clearday.svg", PrayerIconService.GetPrayerIconById("unknown"));
            Assert.AreEqual("clearday.svg", PrayerIconService.GetPrayerIconById(null));
        }

        [TestMethod]
        public void AssignIconById_ShouldSetIconPathWithoutDescription()
        {
            // Test Fajr prayer
            var fajrPrayer = new Prayer { Id = "fajr", Name = "Fecr-i Sadık" };
            PrayerIconService.AssignIconById(fajrPrayer);
            
            Assert.AreEqual("sunrise.svg", fajrPrayer.IconPath);
            Assert.AreEqual(string.Empty, fajrPrayer.Description); // No description
            
            // Test Dhuhr prayer  
            var dhuhrPrayer = new Prayer { Id = "dhuhr", Name = "Öğle" };
            PrayerIconService.AssignIconById(dhuhrPrayer);
            
            Assert.AreEqual("clearday.svg", dhuhrPrayer.IconPath);
            Assert.AreEqual(string.Empty, dhuhrPrayer.Description); // No description
            
            // Test Asr prayer
            var asrPrayer = new Prayer { Id = "asr", Name = "İkindi" };
            PrayerIconService.AssignIconById(asrPrayer);
            
            Assert.AreEqual("partlycloudyday.svg", asrPrayer.IconPath);
            Assert.AreEqual(string.Empty, asrPrayer.Description); // No description
            
            // Test Maghrib prayer
            var maghribPrayer = new Prayer { Id = "maghrib", Name = "Akşam" };
            PrayerIconService.AssignIconById(maghribPrayer);
            
            Assert.AreEqual("sunset.svg", maghribPrayer.IconPath);
            Assert.AreEqual(string.Empty, maghribPrayer.Description); // No description
            
            // Test Isha prayer
            var ishaPrayer = new Prayer { Id = "isha", Name = "Yatsı" };
            PrayerIconService.AssignIconById(ishaPrayer);
            
            Assert.AreEqual("starrynight.svg", ishaPrayer.IconPath);
            Assert.AreEqual(string.Empty, ishaPrayer.Description); // No description
        }

        [TestMethod]
        public void AssignIconById_ShouldHandleNullPrayer()
        {
            // Should not throw exception
            PrayerIconService.AssignIconById(null);
        }

        [TestMethod]
        public void AssignIcon_ShouldHandleNullPrayer()
        {
            // Should not throw exception
            PrayerIconService.AssignIcon(null);
        }

        [TestMethod]
        public void GetAllPrayerIcons_ShouldReturnAllMappings()
        {
            var icons = PrayerIconService.GetAllPrayerIcons();
            
            Assert.AreEqual(5, icons.Count);
            Assert.IsTrue(icons.ContainsKey("fajr"));
            Assert.IsTrue(icons.ContainsKey("dhuhr"));
            Assert.IsTrue(icons.ContainsKey("asr"));
            Assert.IsTrue(icons.ContainsKey("maghrib"));
            Assert.IsTrue(icons.ContainsKey("isha"));
            
            // Verify correct icons are mapped
            Assert.AreEqual("sunrise.svg", icons["fajr"]);
            Assert.AreEqual("clearday.svg", icons["dhuhr"]);
            Assert.AreEqual("partlycloudyday.svg", icons["asr"]);
            Assert.AreEqual("sunset.svg", icons["maghrib"]);
            Assert.AreEqual("starrynight.svg", icons["isha"]);
        }
    }
}
