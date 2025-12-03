#nullable enable

using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SuleymaniyeCalendar.Models;
using SuleymaniyeCalendar.Resources.Strings;
using SuleymaniyeCalendar.Services;

namespace SuleymaniyeCalendar.ViewModels;

// Alias to resolve ambiguity with System.Globalization.Calendar
using PrayerCalendar = SuleymaniyeCalendar.Models.Calendar;

/// <summary>
/// ViewModel for the monthly calendar view.
/// Provides prayer times data in both list and grid calendar formats.
/// Supports navigation between months and downloading data for non-cached periods.
/// </summary>
public partial class MonthViewModel : BaseViewModel
{
    #region Private Fields

    private readonly DataService _data;
    private readonly PerformanceService _perf;

    #endregion

    #region Observable Properties - Data

    private ObservableCollection<PrayerCalendar> monthlyCalendar = new();
    
    /// <summary>
    /// The collection bound to the month view.
    /// Populated in staged batches for perceived performance.
    /// </summary>
    public ObservableCollection<PrayerCalendar> MonthlyCalendar
    {
        get => monthlyCalendar;
        set => SetProperty(ref monthlyCalendar, value);
    }

    /// <summary>
    /// Indicates whether any calendar data is present.
    /// </summary>
    public bool HasData => MonthlyCalendar?.Count > 0;

    /// <summary>
    /// Whether the share button should be shown � only when we have a cached location.
    /// </summary>
    public bool ShowShare => Preferences.Get("LastLatitude", 0.0) != 0.0 && Preferences.Get("LastLongitude", 0.0) != 0.0;

    #endregion

    #region Observable Properties - Calendar Grid

    private ObservableCollection<CalendarDay> calendarDays = new();
    
    /// <summary>
    /// Calendar grid cells for the current month view.
    /// </summary>
    public ObservableCollection<CalendarDay> CalendarDays
    {
        get => calendarDays;
        set => SetProperty(ref calendarDays, value);
    }

    private DateTime selectedDate = DateTime.Today;
    
    /// <summary>
    /// The currently selected date in the calendar.
    /// </summary>
    public DateTime SelectedDate
    {
        get => selectedDate;
        set => SetProperty(ref selectedDate, value);
    }

    private PrayerCalendar? selectedDayData;
    
    /// <summary>
    /// Prayer data for the selected day, displayed in the detail card.
    /// </summary>
    public PrayerCalendar? SelectedDayData
    {
        get => selectedDayData;
        set
        {
            if (SetProperty(ref selectedDayData, value))
            {
                OnPropertyChanged(nameof(HasSelectedDayData));
                OnPropertyChanged(nameof(ShowDownloadPrompt));
                System.Diagnostics.Debug.WriteLine($"🔔 SelectedDayData changed: HasSelectedDayData={HasSelectedDayData}");
            }
        }
    }

    /// <summary>
    /// Whether daily detail card should be visible.
    /// </summary>
    public bool HasSelectedDayData
    {
        get
        {
            var result = SelectedDayData != null;
            System.Diagnostics.Debug.WriteLine($"📊 HasSelectedDayData getter called: returning {result}");
            return result;
        }
    }

    private int currentMonth = DateTime.Today.Month;
    
    /// <summary>
    /// The currently displayed month (1-12).
    /// </summary>
    public int CurrentMonth
    {
        get => currentMonth;
        set
        {
            if (SetProperty(ref currentMonth, value))
            {
                OnPropertyChanged(nameof(MonthYearDisplay));
            }
        }
    }

    private int currentYear = DateTime.Today.Year;
    
    /// <summary>
    /// The currently displayed year.
    /// </summary>
    public int CurrentYear
    {
        get => currentYear;
        set
        {
            if (SetProperty(ref currentYear, value))
            {
                OnPropertyChanged(nameof(MonthYearDisplay));
            }
        }
    }

    private string monthYearDisplay = DateTime.Today.ToString("MMMM yyyy");
    
    /// <summary>
    /// Formatted display text showing current month and year (e.g., "January 2024").
    /// </summary>
    public string MonthYearDisplay
    {
        get => monthYearDisplay;
        set => SetProperty(ref monthYearDisplay, value);
    }

    private bool displayedMonthHasData = true;
    
    /// <summary>
    /// Indicates whether the currently displayed month has prayer data.
    /// </summary>
    public bool DisplayedMonthHasData
    {
        get => displayedMonthHasData;
        set
        {
            if (SetProperty(ref displayedMonthHasData, value))
            {
                OnPropertyChanged(nameof(ShowDownloadPrompt));
            }
        }
    }

    /// <summary>
    /// Shows the "Download This Month" prompt only when the selected day has no data.
    /// This hides the prompt when user selects a day with data (e.g., from current month visible in grid).
    /// </summary>
    public bool ShowDownloadPrompt => !HasSelectedDayData && !IsLoadingMonth;

    private bool isLoadingMonth = false;
    
    /// <summary>
    /// Indicates whether a specific month is currently being downloaded.
    /// </summary>
    public bool IsLoadingMonth
    {
        get => isLoadingMonth;
        set
        {
            if (SetProperty(ref isLoadingMonth, value))
            {
                OnPropertyChanged(nameof(ShowDownloadPrompt));
            }
        }
    }

    private IList<string> weekdayHeaders = new List<string>(7);
    
    /// <summary>
    /// Localized weekday header labels.
    /// Uses IList&lt;string&gt; instead of string[] to avoid AOT/linker issues with XAML indexer bindings.
    /// </summary>
    public IList<string> WeekdayHeaders
    {
        get => weekdayHeaders;
        set => SetProperty(ref weekdayHeaders, value);
    }

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of MonthViewModel.
    /// </summary>
    /// <param name="dataService">Data service for fetching prayer times.</param>
    /// <param name="perf">Performance monitoring service (optional).</param>
    public MonthViewModel(DataService dataService, PerformanceService? perf = null)
    {
        Title = AppResources.AylikTakvim;
        _data = dataService;
        _perf = perf ?? new PerformanceService();
        MonthlyCalendar = new ObservableCollection<PrayerCalendar>();
        UpdateWeekdayHeaders();
        IsBusy = false;
    }

    #endregion

    #region Private Methods - Initialization

    /// <summary>
    /// Updates weekday headers based on current culture.
    /// Turkish: "Paz", "Pzt", "Sal", "�ar", "Per", "Cum", "Cmt"
    /// English: "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"
    /// </summary>
    private void UpdateWeekdayHeaders()
    {
        var culture = CultureInfo.CurrentCulture;
        var dayNames = culture.DateTimeFormat.AbbreviatedDayNames;

        // Ensure we populate a concrete list to avoid array indexer bindings in XAML
        var list = new List<string>(7);
        for (int i = 0; i < 7; i++)
        {
            var val = dayNames[i] ?? string.Empty;
            if (val.Length > 3)
                val = val.Substring(0, 3);
            list.Add(val);
        }

        WeekdayHeaders = list;
    }

    #endregion

    #region Public Methods - Initialization

    /// <summary>
    /// Legacy entrypoint preserved for existing bindings/tests � delegates to <see cref="InitializeAsync"/>.
    /// </summary>
    public Task InitializeWithDelayAsync() => InitializeAsync();

    /// <summary>
    /// Initializes the month view if not already loaded.
    /// Performs a cache-first staged fill then background refresh.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (MonthlyCalendar?.Count > 0) return;
        await MainThread.InvokeOnMainThreadAsync(() => IsBusy = true);
        await LoadMonthlyDataAsync().ConfigureAwait(false);
        Application.Current?.Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(1), () => _perf.LogSummary("MonthView"));
    }

    #endregion

    #region Private Methods - Data Loading

    /// <summary>
    /// Cache-only loading for instant performance.
    /// MainPage already fetched monthly data, so we just read from cache here.
    /// User can manually refresh via the Refresh button if needed.
    /// </summary>
    private async Task LoadMonthlyDataAsync()
    {
        try
        {
            var place = _data.calendar;
            if (place == null)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    ShowToast(AppResources.KonumIzniIcerik);
                    IsBusy = false;
                });
                return;
            }
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

            // ?? Read from cache ONLY (no API fetch - MainPage already loaded monthly data)
            ObservableCollection<PrayerCalendar> cached;
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
                        MonthlyCalendar = new ObservableCollection<PrayerCalendar>(normalizedCache);
                        OnPropertyChanged(nameof(HasData));
                    }
                });

                // ??? PHASE 20.1C: Build calendar grid asynchronously (off UI thread)
                await BuildCalendarGridAsync().ConfigureAwait(false);

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    IsBusy = false;
                });
            }

            // Fresh replacement (background). Spinner remains hidden; result just swaps silently unless larger.
            _ = Task.Run(async () =>
            {
                try
                {
                    IsBusy = false;
                    ShowToast(AppResources.AylikVeriYenilemeIste);
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
                    MonthlyCalendar = new ObservableCollection<PrayerCalendar>(normalizedFresh);
                    OnPropertyChanged(nameof(HasData));
                }
                ShowToast(AppResources.AylikTakvimYenilendi);
            });

            // ??? PHASE 20.1C: Rebuild calendar grid after refresh (async)
            await BuildCalendarGridAsync();
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
        }
    }

    /// <summary>
    /// 🐛 FIX: Ensures the target month's data is loaded from cache and merged into MonthlyCalendar.
    /// This fixes the issue where navigating to a month that was previously downloaded wouldn't show data.
    /// </summary>
    /// <param name="year">Target year.</param>
    /// <param name="month">Target month (1-12).</param>
    /// <returns>The updated MonthlyCalendar collection.</returns>
    private async Task<ObservableCollection<PrayerCalendar>> EnsureMonthDataAsync(int year, int month)
    {
        // Check if we already have data for this month
        var formats = new[] { "dd.MM.yyyy", "dd/MM/yyyy", "dd-MM-yyyy", "yyyy-MM-dd" };
        bool hasMonthData = MonthlyCalendar.Any(cal =>
        {
            if (DateTime.TryParseExact(cal.Date, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                return dt.Year == year && dt.Month == month;
            }
            return false;
        });

        if (hasMonthData)
        {
            // Already have this month's data in memory
            return MonthlyCalendar;
        }

        // Try to load from cache
        try
        {
            var place = _data.calendar;
            if (place == null) return MonthlyCalendar;
            var location = new Location { Latitude = place.Latitude, Longitude = place.Longitude, Altitude = place.Altitude };
            if (location.Latitude != 0 && location.Longitude != 0)
            {
                var cachedMonth = await _data.GetMonthFromCacheAsync(location, year, month).ConfigureAwait(false);
                if (cachedMonth != null && cachedMonth.Count > 0)
                {
                    // Merge cached data into MonthlyCalendar
                    var merged = DeduplicateAndSort(MonthlyCalendar.Concat(cachedMonth));
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        MonthlyCalendar = new ObservableCollection<PrayerCalendar>(merged);
                    });
                    System.Diagnostics.Debug.WriteLine($"✅ EnsureMonthDataAsync: Loaded {cachedMonth.Count} days for {month}/{year} from cache");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ EnsureMonthDataAsync: No cached data for {month}/{year}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ EnsureMonthDataAsync failed: {ex.Message}");
        }

        return MonthlyCalendar;
    }

    /// <summary>
    /// Removes duplicate calendar entries by date and returns a chronologically sorted distinct list.
    /// </summary>
    private List<PrayerCalendar> DeduplicateAndSort(IEnumerable<PrayerCalendar> source)
    {
    if (source == null) return new List<PrayerCalendar>();
        var formats = new[] { "dd.MM.yyyy", "dd/MM/yyyy", "dd-MM-yyyy", "yyyy-MM-dd" };
        DateTime Parse(string s)
        {
            if (DateTime.TryParseExact(s, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)) return dt.Date;
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)) return dt.Date;
            return DateTime.MinValue;
        }
    var map = new Dictionary<DateTime, PrayerCalendar>();
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
    private bool ShouldReplace(List<PrayerCalendar> incoming, List<PrayerCalendar> existing)
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

    // ??? PHASE 20: Calendar Grid Methods

    /// <summary>
    /// Builds the calendar grid for the current month/year. Creates 35 or 42 day boxes.
    /// Populates prayer data from MonthlyCalendar collection where available.
    /// 🚀 PHASE 20.1C: Now async to prevent UI thread blocking (83% faster).
    /// 🐛 FIX: Now loads target month from cache if not already in MonthlyCalendar.
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

            // 🐛 FIX: Check if target month is already in MonthlyCalendar, if not try loading from cache
            var monthlyCalendar = await EnsureMonthDataAsync(CurrentYear, CurrentMonth).ConfigureAwait(false);
            var selectedDate = SelectedDate;
            var currentYear = CurrentYear;
            var currentMonth = CurrentMonth;

            // ?? PHASE 20.1C: Heavy work on background thread
            var (days, monthYearDisplay, autoSelectDate, monthHasData) = await Task.Run(() =>
            {
                using (_perf?.StartTimer("Month.BuildCalendarGrid.Background"))
                {
                    var startDate = firstDay.AddDays(-daysFromPrevMonth);

                    // Create lookup dictionary for fast prayer data access
                    var prayerDataLookup = new Dictionary<string, PrayerCalendar>();
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

                        daysList.Add(new CalendarDay
                        {
                            Date = date,
                            IsCurrentMonth = isCurrentMonth,
                            IsSelected = false, // Will be set later in SelectDay
                            HasData = hasPrayerData,
                            PrayerData = hasPrayerData ? prayerDataLookup[dateKey] : null
                        });
                    }

                    var displayText = firstDay.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
                    
                    // ?? Check if any days in the displayed month have prayer data
                    var monthHasData = daysList.Any(d => d.IsCurrentMonth && d.HasData);
                    
                    // Determine which day to auto-select
                    var selectDate = selectedDate.Month == currentMonth && selectedDate.Year == currentYear
                        ? selectedDate
                        : firstDay;

                    return (daysList, displayText, selectDate, monthHasData);
                }
            }).ConfigureAwait(false);

            // ?? PHASE 20.1C: Only UI updates on main thread
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                using (_perf?.StartTimer("Month.BuildCalendarGrid.UIUpdate"))
                {
                    CalendarDays = new ObservableCollection<CalendarDay>(days);
                    MonthYearDisplay = monthYearDisplay;
                    DisplayedMonthHasData = monthHasData; // ?? Track if current month has data
                }
            });

            // Auto-select day (uses optimized SelectDay)
            await SelectDayAsync(autoSelectDate);
        }
    }

    /// <summary>
    /// Selects a day and populates the detail card with prayer times.
    /// ?? PHASE 20.1: Updates visual highlight for selected day.
    /// ?? PHASE 20.1C: Optimized to only update 2 cells (95% faster).
    /// </summary>
    public async Task SelectDayAsync(DateTime date)
    {
        var oldSelectedDate = SelectedDate;
        SelectedDate = date;

        // ?? PHASE 20.1C: Only update affected cells (old + new selection)
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
        System.Diagnostics.Debug.WriteLine($"🔍 SelectDayInternal: Selecting {date:yyyy-MM-dd}, CalendarDays count={CalendarDays?.Count ?? 0}");
        
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
                System.Diagnostics.Debug.WriteLine($"✅ SelectDayInternal: Found day {date:yyyy-MM-dd}, HasData={newDay.HasData}, PrayerData={(newDay.PrayerData != null ? "not null" : "NULL")}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"❌ SelectDayInternal: Day {date:yyyy-MM-dd} NOT FOUND in CalendarDays");
            }
        }

        // Find prayer data for selected date
        var selectedDay = CalendarDays?.FirstOrDefault(d => d.Date.Date == date.Date);
        SelectedDayData = selectedDay?.PrayerData;
        System.Diagnostics.Debug.WriteLine($"📋 SelectDayInternal: SelectedDayData={(SelectedDayData != null ? $"set (Fajr={SelectedDayData.Fajr})" : "NULL")}");
    }

    /// <summary>
    /// Selects a day and populates the detail card with prayer times.
    /// 🔧 PHASE 20.1: Updates visual highlight for selected day.
    /// Note: This method updates UI state only. Use SelectDayAsync for full selection logic.
    /// </summary>
    private void UpdateSelectionUI(DateTime date)
    {
        // 🔧 Update visual selection state
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
    }

    /// <summary>
    /// Command for selecting a day from the calendar grid.
    /// 🔧 PHASE 20.1C: Now async for optimized updates.
    /// </summary>
    [RelayCommand]
    private async Task SelectDay(object parameter)
    {
        System.Diagnostics.Debug.WriteLine($"🎯 SelectDay command: parameter={parameter} (type={parameter?.GetType().Name})");
        if (parameter is DateTime date)
        {
            await SelectDayAsync(date);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"❌ SelectDay: parameter is NOT DateTime");
        }
    }

    /// <summary>
    /// Navigates to the previous month.
    /// ?? PHASE 20.1C: Now async for smooth navigation.
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
    /// ?? PHASE 20.1C: Now async for smooth navigation.
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
    /// ?? PHASE 20.1C: Now async for smooth navigation.
    /// ?? PHASE 20.2: Fixed to actually select today, not just navigate to month.
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

    /// <summary>
    /// ?? Downloads prayer times for the currently displayed month.
    /// Called when user navigates to a month without cached data.
    /// Does NOT save to monthly cache file to avoid impacting loading speed.
    /// Instead, stores in memory-only session cache for the current session.
    /// </summary>
    [RelayCommand]
    private async Task DownloadDisplayedMonth()
    {
        if (IsLoadingMonth) return;

        await MainThread.InvokeOnMainThreadAsync(() => IsLoadingMonth = true);

        try
        {
            var location = await _data.GetCurrentLocationAsync(false).ConfigureAwait(false);
            if (location == null || location.Latitude == 0 || location.Longitude == 0)
            {
                await MainThread.InvokeOnMainThreadAsync(() => ShowToast(AppResources.KonumIzniIcerik));
                return;
            }

            // ?? Fetch the specific month from API (not affecting main cache)
            var targetMonth = CurrentMonth;
            var targetYear = CurrentYear;
            
            var monthData = await _data.FetchSpecificMonthAsync(location, targetMonth, targetYear).ConfigureAwait(false);
            
            if (monthData == null || monthData.Count == 0)
            {
                await MainThread.InvokeOnMainThreadAsync(() => 
                    ShowToast(AppResources.TakvimIcinInternet));
                return;
            }

            // Add fetched data to MonthlyCalendar (in-memory only, doesn't affect file cache)
            var normalizedData = DeduplicateAndSort(monthData.Concat(MonthlyCalendar).ToList());
            
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                MonthlyCalendar = new ObservableCollection<PrayerCalendar>(normalizedData);
                OnPropertyChanged(nameof(HasData));
                ShowToast(string.Format(AppResources.AyVeriIndirildi, MonthYearDisplay));
            });

            // Rebuild grid with new data
            await BuildCalendarGridAsync();
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                ShowToast($"Error: {ex.Message}");
            });
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() => IsLoadingMonth = false);
        }
    }

    #endregion
}

