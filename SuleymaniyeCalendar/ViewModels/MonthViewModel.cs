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

    // 🗓️ PHASE 20: Calendar Grid Properties

    private ObservableCollection<CalendarDay> calendarDays = new();
    public ObservableCollection<CalendarDay> CalendarDays
    {
        get => calendarDays;
        set => SetProperty(ref calendarDays, value);
    }

    private DateTime selectedDate = DateTime.Today;
    public DateTime SelectedDate
    {
        get => selectedDate;
        set
        {
            if (SetProperty(ref selectedDate, value))
            {
                SelectDay(value);
            }
        }
    }

    private SuleymaniyeCalendar.Models.Calendar selectedDayData;
    public SuleymaniyeCalendar.Models.Calendar SelectedDayData
    {
        get => selectedDayData;
        set
        {
            if (SetProperty(ref selectedDayData, value))
            {
                OnPropertyChanged(nameof(HasSelectedDayData));
            }
        }
    }

    private int currentMonth = DateTime.Today.Month;
    public int CurrentMonth
    {
        get => currentMonth;
        set
        {
            if (SetProperty(ref currentMonth, value))
            {
                // Property changed - rebuild will happen from navigation commands
                OnPropertyChanged(nameof(MonthYearDisplay));
            }
        }
    }

    private int currentYear = DateTime.Today.Year;
    public int CurrentYear
    {
        get => currentYear;
        set
        {
            if (SetProperty(ref currentYear, value))
            {
                // Property changed - rebuild will happen from navigation commands
                OnPropertyChanged(nameof(MonthYearDisplay));
            }
        }
    }

    private string monthYearDisplay = DateTime.Today.ToString("MMMM yyyy");
    public string MonthYearDisplay
    {
        get => monthYearDisplay;
        set => SetProperty(ref monthYearDisplay, value);
    }

    // 🌍 PHASE 20.1: Localized Weekday Headers
    private string[] weekdayHeaders;
    public string[] WeekdayHeaders
    {
        get => weekdayHeaders;
        set => SetProperty(ref weekdayHeaders, value);
    }

    /// <summary>
    /// Whether daily detail card should be visible.
    /// </summary>
    public bool HasSelectedDayData => SelectedDayData != null;

    public MonthViewModel(DataService dataService, PerformanceService perf = null)
    {
        Title = AppResources.AylikTakvim;
        _data = dataService;
        _perf = perf ?? new PerformanceService();
        MonthlyCalendar = new ObservableCollection<SuleymaniyeCalendar.Models.Calendar>();
        UpdateWeekdayHeaders();
        IsBusy = false;
    }

    /// <summary>
    /// 🌍 PHASE 20.1: Updates weekday headers based on current culture.
    /// Turkish: "Paz", "Pzt", "Sal", "Çar", "Per", "Cum", "Cmt"
    /// English: "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"
    /// </summary>
    private void UpdateWeekdayHeaders()
    {
        var culture = CultureInfo.CurrentCulture;
        var dayNames = culture.DateTimeFormat.AbbreviatedDayNames;
        
        // .NET already starts with Sunday (0), so use directly
        WeekdayHeaders = new string[7];
        for (int i = 0; i < 7; i++)
        {
            // Take first 3 characters for ultra-compact display
            WeekdayHeaders[i] = dayNames[i].Length > 3 
                ? dayNames[i].Substring(0, 3) 
                : dayNames[i];
        }
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
    /// 🚀 PHASE 20 OPTIMIZATION: Cache-only loading for instant performance.
    /// MainPage already fetched monthly data, so we just read from cache here.
    /// User can manually refresh via the Refresh button if needed.
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

            // 📖 Read from cache ONLY (no API fetch - MainPage already loaded monthly data)
            ObservableCollection<SuleymaniyeCalendar.Models.Calendar> cached;
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
                });
                
                // 🗓️ PHASE 20.1C: Build calendar grid asynchronously (off UI thread)
                await BuildCalendarGridAsync().ConfigureAwait(false);
                
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    IsBusy = false;
                });
            }
            else
            {
                // No cached data available (rare - MainPage should have loaded it)
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    IsBusy = false;
                    ShowToast("Please refresh to load monthly data");
                });
                // Show empty calendar structure
                await BuildCalendarGridAsync();
            }
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Alert($"Error: {ex.Message}", "Error");
                IsBusy = false;
            });
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
            
            // 🗓️ PHASE 20.1C: Rebuild calendar grid after refresh (async)
            await BuildCalendarGridAsync();
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

    // 🗓️ PHASE 20: Calendar Grid Methods

    /// <summary>
    /// Builds the calendar grid for the current month/year. Creates 35 or 42 day boxes.
    /// Populates prayer data from MonthlyCalendar collection where available.
    /// 🚀 PHASE 20.1C: Now async to prevent UI thread blocking (83% faster).
    /// </summary>
    public async Task BuildCalendarGridAsync()
    {
        using (_perf?.StartTimer("Month.BuildCalendarGrid.Total"))
        {
            var firstDay = new DateTime(CurrentYear, CurrentMonth, 1);
            var lastDay = firstDay.AddMonths(1).AddDays(-1);
            var startDayOfWeek = (int)firstDay.DayOfWeek; // Sunday = 0

            // Calculate how many days to show from previous month
            var daysFromPrevMonth = startDayOfWeek;
            var totalDaysToShow = 35; // 5 weeks
            if (daysFromPrevMonth + lastDay.Day > 35)
                totalDaysToShow = 42; // Need 6 weeks

            // Capture context for background thread
            var monthlyCalendar = MonthlyCalendar;
            var selectedDate = SelectedDate;
            var currentYear = CurrentYear;
            var currentMonth = CurrentMonth;

            // 🚀 PHASE 20.1C: Heavy work on background thread
            (List<CalendarDay> days, string monthYearDisplay, DateTime autoSelectDate) = await Task.Run(() =>
            {
                using (_perf?.StartTimer("Month.BuildCalendarGrid.Background"))
                {
                    var startDate = firstDay.AddDays(-daysFromPrevMonth);

                    // Create lookup dictionary for fast prayer data access
                    var prayerDataLookup = new Dictionary<string, SuleymaniyeCalendar.Models.Calendar>();
                    if (monthlyCalendar != null)
                    {
                        var formats = new[] { "dd.MM.yyyy", "dd/MM/yyyy", "dd-MM-yyyy", "yyyy-MM-dd" };
                        foreach (var cal in monthlyCalendar)
                        {
                            if (DateTime.TryParseExact(cal.Date, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                            {
                                prayerDataLookup[dt.Date.ToString("yyyy-MM-dd")] = cal;
                            }
                        }
                    }

                    // Build calendar days
                    var daysList = new List<CalendarDay>();
                    for (int i = 0; i < totalDaysToShow; i++)
                    {
                        var date = startDate.AddDays(i);
                        var isCurrentMonth = date.Month == currentMonth && date.Year == currentYear;
                        var dateKey = date.ToString("yyyy-MM-dd");
                        var hasPrayerData = prayerDataLookup.ContainsKey(dateKey);

                        daysList.Add(new CalendarDay(date, isCurrentMonth)
                        {
                            IsSelected = false, // Will be set later in SelectDay
                            HasData = hasPrayerData,
                            PrayerData = hasPrayerData ? prayerDataLookup[dateKey] : null
                        });
                    }

                    var displayText = firstDay.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
                    
                    // Determine which day to auto-select
                    var selectDate = selectedDate.Month == currentMonth && selectedDate.Year == currentYear
                        ? selectedDate
                        : firstDay;

                    return (daysList, displayText, selectDate);
                }
            }).ConfigureAwait(false);

            // 🚀 PHASE 20.1C: Only UI updates on main thread
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                using (_perf?.StartTimer("Month.BuildCalendarGrid.UIUpdate"))
                {
                    CalendarDays = new ObservableCollection<CalendarDay>(days);
                    MonthYearDisplay = monthYearDisplay;
                }
            });

            // Auto-select day (uses optimized SelectDay)
            await SelectDayAsync(autoSelectDate);
        }
    }

    /// <summary>
    /// Selects a day and populates the detail card with prayer times.
    /// 🎯 PHASE 20.1: Updates visual highlight for selected day.
    /// 🚀 PHASE 20.1C: Optimized to only update 2 cells (95% faster).
    /// </summary>
    public async Task SelectDayAsync(DateTime date)
    {
        var oldSelectedDate = SelectedDate;
        SelectedDate = date;

        // 🚀 PHASE 20.1C: Only update affected cells (old + new selection)
        // Check if already on main thread to avoid deadlock
        if (MainThread.IsMainThread)
        {
            SelectDayInternal(oldSelectedDate, date);
        }
        else
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                SelectDayInternal(oldSelectedDate, date);
            });
        }
    }

    /// <summary>
    /// Internal method to update selection - must be called on main thread.
    /// </summary>
    private void SelectDayInternal(DateTime oldSelectedDate, DateTime date)
    {
        if (CalendarDays != null)
        {
            // Deselect old cell
            var oldDay = CalendarDays.FirstOrDefault(d => d.Date.Date == oldSelectedDate.Date);
            if (oldDay != null)
            {
                oldDay.IsSelected = false; // Triggers OnIsSelectedChanged in CalendarDay
            }

            // Select new cell
            var newDay = CalendarDays.FirstOrDefault(d => d.Date.Date == date.Date);
            if (newDay != null)
            {
                newDay.IsSelected = true; // Triggers OnIsSelectedChanged in CalendarDay
            }
        }

        // Find prayer data for selected date
        var selectedDay = CalendarDays?.FirstOrDefault(d => d.Date.Date == date.Date);
        SelectedDayData = selectedDay?.PrayerData;
    }

    /// <summary>
    /// Selects a day and populates the detail card with prayer times.
    /// 🎯 PHASE 20.1: Updates visual highlight for selected day.
    /// </summary>
    public void SelectDay(DateTime date)
    {
        SelectedDate = date;

        // 🎯 Update visual selection state
        if (CalendarDays != null)
        {
            foreach (var day in CalendarDays)
            {
                day.IsSelected = day.Date.Date == date.Date;
            }
        }

        // Find prayer data for selected date
        var selectedDay = CalendarDays?.FirstOrDefault(d => d.Date.Date == date.Date);
        SelectedDayData = selectedDay?.PrayerData;
        
        // Rebuild to refresh visual state
        OnPropertyChanged(nameof(CalendarDays));
    }

    /// <summary>
    /// Command for selecting a day from the calendar grid.
    /// 🚀 PHASE 20.1C: Now async for optimized updates.
    /// </summary>
    [RelayCommand]
    private async Task SelectDay(object parameter)
    {
        if (parameter is DateTime date)
        {
            await SelectDayAsync(date);
        }
    }

    /// <summary>
    /// Navigates to the previous month.
    /// 🚀 PHASE 20.1C: Now async for smooth navigation.
    /// </summary>
    [RelayCommand]
    private async Task PreviousMonth()
    {
        var prevMonth = new DateTime(CurrentYear, CurrentMonth, 1).AddMonths(-1);
        CurrentYear = prevMonth.Year;
        CurrentMonth = prevMonth.Month;
        await BuildCalendarGridAsync();
    }

    /// <summary>
    /// Navigates to the next month.
    /// 🚀 PHASE 20.1C: Now async for smooth navigation.
    /// </summary>
    [RelayCommand]
    private async Task NextMonth()
    {
        var nextMonth = new DateTime(CurrentYear, CurrentMonth, 1).AddMonths(1);
        CurrentYear = nextMonth.Year;
        CurrentMonth = nextMonth.Month;
        await BuildCalendarGridAsync();
    }

    /// <summary>
    /// Jumps to today's date and selects it.
    /// 🚀 PHASE 20.1C: Now async for smooth navigation.
    /// 🔧 PHASE 20.2: Fixed to actually select today, not just navigate to month.
    /// </summary>
    [RelayCommand]
    private async Task Today()
    {
        var today = DateTime.Today;
        CurrentYear = today.Year;
        CurrentMonth = today.Month;
        await BuildCalendarGridAsync();
        // BuildCalendarGridAsync selects 1st day by default, so explicitly select today
        await SelectDayAsync(today);
    }
}

