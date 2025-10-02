using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SuleymaniyeCalendar.Models;
using SuleymaniyeCalendar.Resources.Strings;
using SuleymaniyeCalendar.Services;

namespace SuleymaniyeCalendar.ViewModels;

public partial class MonthViewModel : BaseViewModel
{
    private readonly DataService _data;
    private readonly PerformanceService _perf;

    private ObservableCollection<SuleymaniyeCalendar.Models.Calendar> monthlyCalendar = new();
    /// <summary>
    /// The collection bound to the month view. Populated in staged batches for perceived performance.
    /// </summary>
    public ObservableCollection<SuleymaniyeCalendar.Models.Calendar> MonthlyCalendar
    {
        get => monthlyCalendar;
        set => SetProperty(ref monthlyCalendar, value);
    }

    /// <summary>
    /// Indicates whether any calendar data is present.
    /// </summary>
    public bool HasData => MonthlyCalendar?.Count > 0;

    /// <summary>
    /// Whether the share button should be shown – only when we have a cached location.
    /// </summary>
    public bool ShowShare => Preferences.Get("LastLatitude", 0.0) != 0.0 && Preferences.Get("LastLongitude", 0.0) != 0.0;

    public MonthViewModel(DataService dataService, PerformanceService perf = null)
    {
        Title = AppResources.AylikTakvim;
        _data = dataService;
        _perf = perf ?? new PerformanceService();
    MonthlyCalendar = new ObservableCollection<SuleymaniyeCalendar.Models.Calendar>();
        IsBusy = false;
    }

    /// <summary>
    /// Legacy entrypoint preserved for existing bindings/tests – delegates to <see cref="InitializeAsync"/>.
    /// </summary>
    public Task InitializeWithDelayAsync() => InitializeAsync();

    /// <summary>
    /// Initializes the month view if not already loaded. Performs a cache-first staged fill then background refresh.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (MonthlyCalendar?.Count > 0) return;
        await MainThread.InvokeOnMainThreadAsync(() => IsBusy = true);
        await LoadMonthlyDataAsync().ConfigureAwait(false);
        Application.Current?.Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(1), () => _perf.LogSummary("MonthView"));
    }

    /// <summary>
    /// Core monthly loading logic. Uses cached JSON first (if available) for instant UI, then refreshes from hybrid API.
    /// UI population is staged in 10 + 10 + remainder batches to keep main thread responsive.
    /// </summary>
    private async Task LoadMonthlyDataAsync()
    {
        try
        {
            var place = _data.calendar;
            var location = new Location { Latitude = place.Latitude, Longitude = place.Longitude, Altitude = place.Altitude };
            if (location.Latitude == 0 || location.Longitude == 0)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    ShowToast(AppResources.KonumIzniIcerik);
                    IsBusy = false;
                });
                return;
            }

            // Cached first (shows full spinner until assignment completes)
            ObservableCollection<SuleymaniyeCalendar.Models.Calendar> cached = null;
            using (_perf.StartTimer("Month.ReadCache"))
            {
                cached = await _data.GetMonthlyFromCacheOrEmptyAsync(location).ConfigureAwait(false);
            }
            if (cached != null && cached.Count > 0)
            {
                var normalizedCache = DeduplicateAndSort(cached);
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    using (_perf.StartTimer("Month.UI.Assign.CacheFull"))
                    {
                        MonthlyCalendar = new ObservableCollection<SuleymaniyeCalendar.Models.Calendar>(normalizedCache);
                        OnPropertyChanged(nameof(HasData));
                    }
                    IsBusy = false; // hide spinner once full list visible
                });
            }

            // Fresh replacement (background). Spinner remains hidden; result just swaps silently unless larger.
            _ = Task.Run(async () =>
            {
                try
                {
                    ObservableCollection<SuleymaniyeCalendar.Models.Calendar> fresh;
                    using (_perf.StartTimer("Month.HybridMonthly"))
                    {
                        fresh = await _data.GetMonthlyPrayerTimesHybridAsync(location, forceRefresh: false).ConfigureAwait(false);
                    }
                    if (fresh == null || fresh.Count == 0) return;
                    var normalizedFresh = DeduplicateAndSort(fresh);
                    // Only update if content differs (count or any date/time mismatch)
                    bool differs = ShouldReplace(normalizedFresh, MonthlyCalendar?.ToList());
                    if (!differs) return;
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        using (_perf.StartTimer("Month.UI.Assign.FreshFull"))
                        {
                            MonthlyCalendar = new ObservableCollection<SuleymaniyeCalendar.Models.Calendar>(normalizedFresh);
                            OnPropertyChanged(nameof(HasData));
                        }
                    });
                }
                catch { /* ignore background errors */ }
            });
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Alert($"Error: {ex.Message}", "Error");
                IsBusy = false;
            });
        }
        finally
        {
            if (MonthlyCalendar.Count == 0)
            {
                // No data at all (cache empty & network likely offline) – ensure spinner off
                await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
            }
        }
    }

    /// <summary>
    /// Pull-to-refresh command: forces a fresh hybrid fetch and replaces the entire collection (no staging).
    /// </summary>
    [RelayCommand]
    private async Task Refresh()
    {
        await MainThread.InvokeOnMainThreadAsync(() => IsBusy = true);
        try
        {
            var location = await _data.GetCurrentLocationAsync(false).ConfigureAwait(false);
            if (location == null || location.Latitude == 0 || location.Longitude == 0)
            {
                await MainThread.InvokeOnMainThreadAsync(() => ShowToast(AppResources.KonumIzniIcerik));
                return;
            }
            var fresh = await _data.GetMonthlyPrayerTimesHybridAsync(location, true).ConfigureAwait(false);
            if (fresh == null)
            {
                await MainThread.InvokeOnMainThreadAsync(() => Alert(AppResources.TakvimIcinInternet, AppResources.TakvimIcinInternetBaslik));
                return;
            }
            var normalizedFresh = DeduplicateAndSort(fresh.ToList());
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                using (_perf.StartTimer("Month.UI.AssignItemsSource.RefreshFull"))
                {
                    MonthlyCalendar = new ObservableCollection<SuleymaniyeCalendar.Models.Calendar>(normalizedFresh);
                    OnPropertyChanged(nameof(HasData));
                }
                ShowToast(AppResources.AylikTakvimYenilendi);
            });
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
        }
    }

    /// <summary>
    /// Removes duplicate calendar entries by date and returns a chronologically sorted distinct list.
    /// </summary>
    private List<SuleymaniyeCalendar.Models.Calendar> DeduplicateAndSort(IEnumerable<SuleymaniyeCalendar.Models.Calendar> source)
    {
    if (source == null) return new List<SuleymaniyeCalendar.Models.Calendar>();
        var formats = new[] { "dd.MM.yyyy", "dd/MM/yyyy", "dd-MM-yyyy", "yyyy-MM-dd" };
        DateTime Parse(string s)
        {
            if (DateTime.TryParseExact(s, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)) return dt.Date;
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)) return dt.Date;
            return DateTime.MinValue;
        }
    var map = new Dictionary<DateTime, SuleymaniyeCalendar.Models.Calendar>();
        foreach (var cal in source)
        {
            var key = Parse(cal.Date);
            if (key == DateTime.MinValue) continue;
            // Prefer later occurrence (assume newer/fresher data overwrites older duplicate)
            map[key] = cal;
        }
        return map.OrderBy(kv => kv.Key).Select(kv => kv.Value).ToList();
    }

    /// <summary>
    /// Determines if new list differs from currently displayed list (count or any field time differences per date).
    /// </summary>
    private bool ShouldReplace(List<SuleymaniyeCalendar.Models.Calendar> incoming, List<SuleymaniyeCalendar.Models.Calendar> existing)
    {
        if (existing == null) return true;
        if (incoming.Count != existing.Count) return true;
        for (int i = 0; i < incoming.Count; i++)
        {
            var a = incoming[i];
            var b = existing[i];
            if (!string.Equals(a.Date, b.Date, StringComparison.Ordinal)) return true;
            if (!string.Equals(a.Fajr, b.Fajr, StringComparison.Ordinal) ||
                !string.Equals(a.Sunrise, b.Sunrise, StringComparison.Ordinal) ||
                !string.Equals(a.Dhuhr, b.Dhuhr, StringComparison.Ordinal) ||
                !string.Equals(a.Asr, b.Asr, StringComparison.Ordinal) ||
                !string.Equals(a.Maghrib, b.Maghrib, StringComparison.Ordinal) ||
                !string.Equals(a.Isha, b.Isha, StringComparison.Ordinal) ||
                !string.Equals(a.EndOfIsha, b.EndOfIsha, StringComparison.Ordinal) ||
                !string.Equals(a.FalseFajr, b.FalseFajr, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Navigates back in the shell hierarchy.
    /// </summary>
    [RelayCommand]
    private async Task GoBack() => await Shell.Current.GoToAsync("..").ConfigureAwait(false);

    /// <summary>
    /// Opens the Suleymaniye Takvimi monthly calendar share URL using the last known location.
    /// </summary>
    [RelayCommand]
    private async Task Share()
    {
        var latitude = Preferences.Get("LastLatitude", 0.0);
        var longitude = Preferences.Get("LastLongitude", 0.0);
        var url = $"https://www.suleymaniyetakvimi.com/monthlyCalendar.html?latitude={latitude}&longitude={longitude}&monthId={DateTime.Today.Month}";
        await Launcher.OpenAsync(url).ConfigureAwait(false);
    }
}

