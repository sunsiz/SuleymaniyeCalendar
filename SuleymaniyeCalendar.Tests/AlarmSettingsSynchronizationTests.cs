using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SuleymaniyeCalendar.Services;

namespace SuleymaniyeCalendar.Tests;

[TestClass]
[DoNotParallelize]
public class AlarmSettingsSynchronizationTests
{
    private static readonly string[] PrayerIds =
        ["falsefajr", "fajr", "sunrise", "dhuhr", "asr", "maghrib", "isha", "endofisha"];
    private readonly Dictionary<string, bool?> _originalEnabled = new();
    private int? _originalTime;
    private string? _originalSound;
    private string? _originalCoverage;
    private string? _originalCooldown;
    private Mock<IAlarmService> _alarm = null!;
    private NotificationSchedulerService _scheduler = null!;

    [TestInitialize]
    public void Setup()
    {
        foreach (var id in PrayerIds)
        {
            var key = id + "Enabled";
            _originalEnabled[key] = Preferences.ContainsKey(key) ? Preferences.Get(key, false) : null;
            Preferences.Set(key, false);
        }
        _originalTime = Preferences.ContainsKey("fajrNotificationTime") ? Preferences.Get("fajrNotificationTime", 0) : null;
        _originalSound = Preferences.ContainsKey("fajrAlarmSound") ? Preferences.Get("fajrAlarmSound", "kus") : null;
        _originalCoverage = Preferences.ContainsKey("LastAlarmDate") ? Preferences.Get("LastAlarmDate", "") : null;
        _originalCooldown = Preferences.ContainsKey("LastAutoRescheduleUtc") ? Preferences.Get("LastAutoRescheduleUtc", "") : null;
        _alarm = new Mock<IAlarmService>();
        var performance = new PerformanceService();
        _scheduler = new NotificationSchedulerService(
            _alarm.Object, new PrayerTimesRepository(null!, null!, performance), performance);
    }

    [TestCleanup]
    public void Cleanup()
    {
        foreach (var (key, value) in _originalEnabled)
        {
            if (value.HasValue) Preferences.Set(key, value.Value);
            else Preferences.Remove(key);
        }
        if (_originalTime.HasValue) Preferences.Set("fajrNotificationTime", _originalTime.Value);
        else Preferences.Remove("fajrNotificationTime");
        if (_originalSound is not null) Preferences.Set("fajrAlarmSound", _originalSound);
        else Preferences.Remove("fajrAlarmSound");
        if (_originalCoverage is not null) Preferences.Set("LastAlarmDate", _originalCoverage);
        else Preferences.Remove("LastAlarmDate");
        if (_originalCooldown is not null) Preferences.Set("LastAutoRescheduleUtc", _originalCooldown);
        else Preferences.Remove("LastAutoRescheduleUtc");
    }

    [TestMethod]
    public async Task SetMonthlyAlarmsAsync_AllRemindersDisabled_CancelsDespiteFutureCoverage()
    {
        Preferences.Set("LastAlarmDate", DateTime.Today.AddDays(20).ToString("O"));
        Preferences.Set("LastAutoRescheduleUtc", DateTime.UtcNow.ToString("O"));

        await _scheduler.SetMonthlyAlarmsAsync(new Location(41, 29));

        _alarm.Verify(service => service.CancelAllAlarms(), Times.Once);
        Assert.IsFalse(Preferences.ContainsKey("LastAlarmDate"));
        Assert.IsFalse(Preferences.ContainsKey("LastAutoRescheduleUtc"));
    }

    [TestMethod]
    public async Task SavePrayerSettingsAsync_LocationFailure_RollsBackWithoutCancellingExistingAlarms()
    {
        var exception = await Assert.ThrowsAsync<AlarmSettingsSaveException>(
            () => _scheduler.SavePrayerSettingsAsync("fajr", true, 10, "new",
                () => Task.FromResult<Location?>(null)));

        Assert.IsFalse(exception.RestoreFailed);
        Assert.IsFalse(Preferences.Get("fajrEnabled", false));
        _alarm.Verify(service => service.CancelAllAlarms(), Times.Never);
    }

    [TestMethod]
    public async Task SavePrayerSettingsAsync_BackgroundCancellation_WaitsUntilRollbackCompletes()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var save = _scheduler.SavePrayerSettingsAsync("fajr", true, 10, "new", async () =>
        {
            entered.SetResult();
            await release.Task;
            return null;
        });

        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var cancel = _scheduler.CancelAllAlarmsAsync();
            Assert.IsFalse(cancel.IsCompleted);
            _alarm.Verify(service => service.CancelAllAlarms(), Times.Never);
            release.TrySetResult();
            await Assert.ThrowsAsync<AlarmSettingsSaveException>(() => save);
            await cancel.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsFalse(Preferences.Get("fajrEnabled", false));
            _alarm.Verify(service => service.CancelAllAlarms(), Times.Once);
        }
        finally
        {
            release.TrySetResult();
        }
    }
}
