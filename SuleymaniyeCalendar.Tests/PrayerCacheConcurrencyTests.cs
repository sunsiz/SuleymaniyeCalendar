using System.Globalization;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SuleymaniyeCalendar.Services;
using Calendar = SuleymaniyeCalendar.Models.Calendar;

namespace SuleymaniyeCalendar.Tests;

[TestClass]
public class PrayerCacheConcurrencyTests
{
    private const int Year = 2097;
    private Location _location = null!;
    private string _path = null!;
    private bool _canCleanup;

    [TestInitialize]
    public void Setup()
    {
        var latitude = Math.Round(Random.Shared.NextDouble() * 160 - 80, 4);
        var longitude = Math.Round(Random.Shared.NextDouble() * 340 - 170, 4);
        _location = new Location(latitude, longitude);
        var lat = latitude.ToString(CultureInfo.InvariantCulture);
        var lon = longitude.ToString(CultureInfo.InvariantCulture);
        _path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            $"prayercache_{lat}_{lon}_{Year}.json");
        Assert.IsFalse(File.Exists(_path), "The test cache path must not already exist.");
        _canCleanup = true;
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (_canCleanup && File.Exists(_path))
            File.Delete(_path);
    }

    [TestMethod]
    public async Task SaveToUnifiedCacheAsync_ConcurrentInstances_PreservesAllDays()
    {
        var services = Enumerable.Range(0, 4).Select(_ => new PrayerCacheService()).ToArray();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var days = Enumerable.Range(0, 60)
            .Select(i => new Calendar { Date = new DateTime(Year, 1, 1).AddDays(i).ToString("yyyy-MM-dd") })
            .ToArray();

        var writes = days.Select((day, i) => Task.Run(async () =>
        {
            await start.Task;
            await services[i % services.Length].SaveToUnifiedCacheAsync(_location, [day]);
        })).ToArray();
        start.SetResult();
        await Task.WhenAll(writes);

        var saved = await services[0].LoadYearCacheAsync(_location, Year);
        Assert.IsNotNull(saved);
        CollectionAssert.AreEquivalent(days.Select(day => day.Date).ToArray(), saved.Select(day => day.Date).ToArray());
    }

    [TestMethod]
    public async Task SaveToUnifiedCacheAsync_ExistingDay_ReplacesThatDayAndPreservesOthers()
    {
        var cache = new PrayerCacheService();
        await cache.SaveToUnifiedCacheAsync(_location,
        [
            new Calendar { Date = "2097-01-01", Fajr = "05:00" },
            new Calendar { Date = "2097-01-02", Fajr = "05:01" }
        ]);
        await cache.SaveToUnifiedCacheAsync(_location,
        [
            new Calendar { Date = "01/01/2097", Fajr = "05:30" }
        ]);

        var saved = await cache.LoadYearCacheAsync(_location, Year);
        Assert.IsNotNull(saved);
        Assert.AreEqual(2, saved.Count);
        Assert.AreEqual("05:30", saved[0].Fajr);
        Assert.AreEqual("05:01", saved[1].Fajr);
    }

    [TestMethod]
    public async Task LoadYearCacheAsync_DuringConcurrentWrites_AlwaysReadsValidCache()
    {
        var writer = new PrayerCacheService();
        var reader = new PrayerCacheService();
        await writer.SaveToUnifiedCacheAsync(_location, [new Calendar { Date = "2097-01-01" }]);
        var writes = Task.Run(async () =>
        {
            for (var i = 0; i < 40; i++)
                await writer.SaveToUnifiedCacheAsync(_location,
                    [new Calendar { Date = new DateTime(Year, 1, 2).AddDays(i).ToString("yyyy-MM-dd") }]);
        });
        var reads = Task.Run(async () =>
        {
            for (var i = 0; i < 40; i++)
            {
                var cached = await reader.LoadYearCacheAsync(_location, Year);
                Assert.IsNotNull(cached);
                Assert.IsTrue(cached.Any(day => day.Date == "2097-01-01"));
            }
        });
        await Task.WhenAll(writes, reads);
    }
}
