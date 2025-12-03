using System.Linq;
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
            Assert.AreEqual("sunrise", PrayerIconService.GetPrayerIconById("sunrise"));
            Assert.AreEqual("overcastnight", PrayerIconService.GetPrayerIconById("falsefajr"));
            Assert.AreEqual("overcast", PrayerIconService.GetPrayerIconById("fajr"));
            Assert.AreEqual("clearday", PrayerIconService.GetPrayerIconById("dhuhr"));
            Assert.AreEqual("partlycloudyday", PrayerIconService.GetPrayerIconById("asr"));
            Assert.AreEqual("sunset", PrayerIconService.GetPrayerIconById("maghrib"));
            Assert.AreEqual("overcastnight", PrayerIconService.GetPrayerIconById("isha"));
            Assert.AreEqual("starrynight", PrayerIconService.GetPrayerIconById("endofisha"));
            
            // Test default fallback
            Assert.AreEqual("clearday", PrayerIconService.GetPrayerIconById("unknown"));
            Assert.AreEqual("clearday", PrayerIconService.GetPrayerIconById(null));
        }

        [TestMethod]
        public void AssignIconById_ShouldSetIconPathWithoutDescription()
        {
            // Test Fajr prayer
            var fajrPrayer = new Prayer { Id = "fajr", Name = "Fecr-i Sadık" };
            PrayerIconService.AssignIconById(fajrPrayer);
            
            Assert.AreEqual("overcast", fajrPrayer.IconPath);
            Assert.AreEqual(string.Empty, fajrPrayer.Description); // No description
            
            // Test Dhuhr prayer  
            var dhuhrPrayer = new Prayer { Id = "dhuhr", Name = "Öğle" };
            PrayerIconService.AssignIconById(dhuhrPrayer);
            
            Assert.AreEqual("clearday", dhuhrPrayer.IconPath);
            Assert.AreEqual(string.Empty, dhuhrPrayer.Description); // No description
            
            // Test Asr prayer
            var asrPrayer = new Prayer { Id = "asr", Name = "İkindi" };
            PrayerIconService.AssignIconById(asrPrayer);
            
            Assert.AreEqual("partlycloudyday", asrPrayer.IconPath);
            Assert.AreEqual(string.Empty, asrPrayer.Description); // No description
            
            // Test Maghrib prayer
            var maghribPrayer = new Prayer { Id = "maghrib", Name = "Akşam" };
            PrayerIconService.AssignIconById(maghribPrayer);
            
            Assert.AreEqual("sunset", maghribPrayer.IconPath);
            Assert.AreEqual(string.Empty, maghribPrayer.Description); // No description
            
            // Test Isha prayer
            var ishaPrayer = new Prayer { Id = "isha", Name = "Yatsı" };
            PrayerIconService.AssignIconById(ishaPrayer);
            
            Assert.AreEqual("overcastnight", ishaPrayer.IconPath);
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
            
            // Updated to include all 8 prayer times
            // Verify count using collection assertion
#pragma warning disable MSTEST0037 // Use Assert.HasCount - IReadOnlyDictionary doesn't directly support it
            Assert.AreEqual(8, icons.Count, "Expected 8 prayer icon mappings");
#pragma warning restore MSTEST0037
            Assert.IsTrue(icons.ContainsKey("falsefajr"));
            Assert.IsTrue(icons.ContainsKey("fajr"));
            Assert.IsTrue(icons.ContainsKey("sunrise"));
            Assert.IsTrue(icons.ContainsKey("dhuhr"));
            Assert.IsTrue(icons.ContainsKey("asr"));
            Assert.IsTrue(icons.ContainsKey("maghrib"));
            Assert.IsTrue(icons.ContainsKey("isha"));
            Assert.IsTrue(icons.ContainsKey("endofisha"));
            
            // Verify correct icons are mapped
            Assert.AreEqual("overcast", icons["fajr"]);
            Assert.AreEqual("clearday", icons["dhuhr"]);
            Assert.AreEqual("partlycloudyday", icons["asr"]);
            Assert.AreEqual("sunset", icons["maghrib"]);
            Assert.AreEqual("overcastnight", icons["isha"]);
        }
    }
}
