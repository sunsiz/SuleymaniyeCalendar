# AI working guide for SuleymaniyeCalendar

This is a .NET MAUI prayer times app (Android, iOS, Windows) using CommunityToolkit.MVVM, Shell navigation, and localization. Features Material Design 3 UI with dynamic typography, performance-optimized async patterns, and comprehensive RTL support.

## Big picture architecture
- **MVVM pattern**: ViewModels inherit from `BaseViewModel` (ObservableObject), Views in `Views/`, Shell navigation in `AppShell.xaml`
- **Hybrid API system**: `DataService` + `JsonApiService` with fallback: Try new JSON API first, fallback to legacy XML API
- **DI container**: All types registered as singletons in `MauiProgram.CreateMauiApp()` with constructor injection
- **Localization**: Uses `LocalizationResourceManager.Maui` with `AppResources.resx`, XAML binds via `{localization:Translate Key}`
- **Prayer data flow**: JSON API → JSON cache → Calendar models → Prayer ViewModels → UI cards (with XML fallback)
- **Performance layer**: `BackgroundDataPreloader` + `PerformanceService` for proactive caching and monitoring

## Key patterns unique to this codebase

### 1. Culture-safe numeric conversion
**Always use** `CultureInfo.InvariantCulture` for parsing coordinates/numbers from strings:
```csharp
Convert.ToDouble(_calendar.Latitude, CultureInfo.InvariantCulture.NumberFormat)
```
This prevents comma/dot locale issues in coordinate parsing.

### 2. Dynamic font scaling system
Uses `DynamicResource` throughout for app-wide font scaling:
```xaml
<Setter Property="FontSize" Value="{DynamicResource DefaultFontSize}" />
<!-- NEVER use StaticResource for fonts -->
```
BaseViewModel manages font scale: `HeaderFontSize` (1.5x), `SubheaderFontSize` (1.25x), `DefaultFontSize` (1x)

### 3. Delayed loading pattern for instant UI
ViewModels show UI immediately, then load data with delays for smooth UX:
```csharp
public async Task InitializeWithDelayAsync() {
    // Show empty UI first (100ms) → Loading indicator (500ms) → Background data load
    await Task.Delay(100);
    IsBusy = true;
    await Task.Delay(500);
    await LoadMonthlyDataAsync();
}
```
Always use `MainThread.InvokeOnMainThreadAsync()` for UI updates from background tasks.

### 4. Hybrid API pattern (critical for reliability)
All data fetching uses JSON-first with XML fallback:
```csharp
// Strategy 1: Try new JSON API
var jsonResult = await _jsonApiService.GetMonthlyPrayerTimesAsync(lat, lng, month);
if (jsonResult != null) return jsonResult;

// Strategy 2: Fallback to legacy XML API  
return GetMonthlyPrayerTimes(location, forceRefresh);
```
Use `GetMonthlyPrayerTimesHybridAsync()` and `GetDailyPrayerTimesHybridAsync()` methods.

### 4. Prayer state management
Each prayer has temporal states (Past/Current/Future) calculated in `MainViewModel.CheckState()`:
```csharp
asr.State = CheckState(DateTime.Parse(_calendar.Asr), DateTime.Parse(_calendar.Maghrib));
asr.UpdateVisualState(); // Updates colors/icons based on state
```

### 5. Comprehensive RTL support  
All pages support right-to-left languages via `IRtlService`:
```csharp
// XAML: FlowDirection binding for all layouts
FlowDirection="{Binding FlowDirection}"

// C#: Conditional resource updates
if (_rtlService.IsRightToLeft) {
    this.FlowDirection = FlowDirection.RightToLeft;
}
```
Widget layouts include RTL variants: `Widget.axml` and `WidgetRtl.axml`.

### 6. Performance optimizations
- **Batched collection updates**: Use 10-item batches for large collections
- **Conditional font/culture**: Only update when changed to avoid overhead  
- **Background preloading**: `BackgroundDataPreloader` caches data after app launch
- **Safe type conversion**: Always use `Convert.ToDouble(value)` instead of casting

## DataService responsibilities (the system hub)
- **Prayer times**: Fetches from `http://servis.suleymaniyetakvimi.com/servis.asmx` (XML), caches to `%LOCALAPPDATA%/monthlycalendar.xml`
- **JSON API**: Primary data source via `JsonApiService` at `api.suleymaniyetakvimi.com` with local JSON caching
- **Location**: Handles permissions, GPS, geocoding with robust fallbacks
- **Alarms**: Schedules 30 days via `SetMonthlyAlarmsAsync()` using `EnsureDaysRangeAsync()` for month boundary handling
- **Network resilience**: Uses cached data when offline, shows appropriate toasts

## 30-day alarm scheduling architecture
Critical pattern for scheduling alarms across month boundaries:
```csharp
// EnsureDaysRangeAsync collects exactly 30 days by progressive month fetching
var daysToSchedule = await EnsureDaysRangeAsync(startDate, 30);

// Loop through collected days without break conditions
foreach (var day in daysToSchedule) {
    try {
        // Schedule each prayer with defensive error handling
        await _alarmService.SetAlarm(prayerTime, requestCode, notificationSettings);
    } catch (Exception ex) {
        Debug.WriteLine($"❌ Alarm failed for {prayerName}: {ex.Message}");
        // Continue scheduling remaining alarms even if one fails
    }
}
```
**Key principles:**
- Use `while (collectedDays.Count < daysNeeded)` not `while (cursor <= endDate)` to ensure exact count
- Filter month data before adding: `daysInRange.Where(day => dayDate >= fromDate && dayDate <= toDate)`
- Never break loop early with `if (dayCounter >= 30) break;` patterns
- Each alarm gets defensive try-catch to prevent cascade failures
- AlarmForegroundService auto-reschedules, so 30 days is optimal for performance vs coverage

## JsonApiService integration
- **Endpoint pattern**: `https://api.suleymaniyetakvimi.com/api/TimeCalculation/TimeCalculate[ByMonth]`
- **Response models**: `TimeCalcDto` (internal DTO) → `Calendar` (app model) via `ConvertJsonDataToCalendar()`
- **Error handling**: Returns `null` on failure; no `IsSuccess` wrapper in current API version
- **Fallback strategy**: XML API used when JSON returns `null` or throws exception
- **DTO flexibility**: Uses `[JsonPropertyName]` for multiple field variants (e.g., `fajrBeginTime` vs `fajr`)

## Navigation patterns
- Tab navigation declared in `AppShell.xaml`, extra routes registered in `AppShell.xaml.cs`
- Navigation: `await Shell.Current.GoToAsync(nameof(Page))` 
- Parameter passing: `new Dictionary<string, object> { { "PropertyName", value } }`
- Back navigation: `await Shell.Current.GoToAsync("..")`

## Material Design 3 card system
```xaml
<Border Style="{StaticResource Card}">            <!-- Standard card -->
<Border Style="{StaticResource CurrentPrayerCard}"> <!-- Enhanced emphasis -->
<Border Style="{StaticResource AnimatedPrayerCard}"> <!-- Prayer state animations -->
<Border Style="{StaticResource SettingsCard}">      <!-- Settings with hover -->
<Border Style="{StaticResource MediaCard}">         <!-- Radio/media controls -->
```
Cards use `SurfaceVariantColor` backgrounds, `OutlineColor` borders, automatic light/dark theming.

## Android widget system
- **Theme awareness**: Automatically detects light/dark mode via `IsSystemDarkMode()`
- **RTL support**: Dual layouts `Widget.axml` and `WidgetRtl.axml` with automatic selection
- **Color management**: `ApplyThemeColors()` updates all widget elements including refresh icon
- **Background updates**: `WidgetService` refreshes on theme/language changes without user action

## Preferences storage patterns
- Prayer toggles: `{prayerId}Enabled` (e.g., "fajrEnabled")
- Notification timing: `{prayerId}NotificationTime` 
- Location cache: `LastLatitude`, `LastLongitude`, `LastAltitude`
- App settings: `FontSize`, `SelectedLanguage`, `ForegroundServiceEnabled`

## Build & debug commands
```bash
dotnet build                     # Build solution
dotnet run --framework net9.0-android  # Run on Android
```
- Delete `%LOCALAPPDATA%/monthlycalendar.xml` to force fresh prayer time fetch
- Use VS Code or VS for debugging with hot reload

## Android Java interop patterns (critical for alarm scheduling)
**PendingIntent creation for AlarmManager:**
```csharp
// ❌ WRONG: Causes "Type is not derived from a java type" error
var showIntent = new Intent(Application.Context, typeof(NotificationChannelManager));

// ✅ CORRECT: Use PackageManager to get launch intent
var showIntent = Application.Context.PackageManager?.GetLaunchIntentForPackage(Application.Context.PackageName);

// Create PendingIntent with Android 12+ Immutable flag
var pendingShowIntent = PendingIntent.GetActivity(
    Application.Context, 
    requestCode, 
    showIntent, 
    PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent
);
```
**Key rules:**
- Only Activities, Services, or BroadcastReceivers can be used in `Intent(context, typeof(T))`
- For AlarmClockInfo showIntent, use `PackageManager.GetLaunchIntentForPackage()` to get app's main activity
- Android 12+ requires `PendingIntentFlags.Immutable` for security (apps targeting API 31+)
- Always combine with `UpdateCurrent` to refresh pending intents: `Immutable | UpdateCurrent`

## Foreground service architecture (Android)
**AlarmForegroundService patterns:**
```csharp
public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId) {
    // 1. Call StartForeground IMMEDIATELY (within 5 seconds or ANR)
    StartForeground(NotificationId, CreateNotification());
    
    // 2. Move heavy work to background thread using Task.Run
    _ = Task.Run(async () => {
        try {
            await data.SetMonthlyAlarmsAsync().ConfigureAwait(false);
        } catch (Exception ex) {
            Debug.WriteLine($"Background scheduling failed: {ex.Message}");
        }
    });
    
    return StartCommandResult.Sticky;
}
```
**Handler-based periodic updates:**
```csharp
private readonly Handler _handler = new Handler(Looper.MainLooper);
private readonly Action _runnable;

_runnable = new Action(() => {
    // ❌ WRONG: Synchronous work on main thread causes ANR
    SetNotification();
    
    // ✅ CORRECT: Background thread for notification updates
    Task.Run(() => {
        try {
            SetNotification();
        } catch (Exception ex) {
            Debug.WriteLine($"SetNotification failed: {ex.Message}");
        }
    });
    
    _handler.PostDelayed(_runnable, 30000); // 30 second interval
});
```
**ANR prevention checklist:**
- Call `StartForeground()` within 5 seconds of `startForegroundService()`
- Move `SetMonthlyAlarmsAsync()` to `Task.Run()` background thread
- Never block main thread with synchronous I/O, database, or network calls
- Use `ConfigureAwait(false)` for all `await` calls in background tasks
- Handler callbacks should dispatch heavy work to `Task.Run()`

## Background task patterns
**Prefer `Task.Run()` over `new Task()`:**
```csharp
// ❌ WRONG: new Task requires manual Start() and doesn't use thread pool
var task = new Task(async () => await DoWorkAsync());
task.Start();

// ✅ CORRECT: Task.Run automatically queues on thread pool
_ = Task.Run(async () => await DoWorkAsync().ConfigureAwait(false));

// For fire-and-forget with explicit discard
_ = Task.Run(async () => {
    try {
        await DoWorkAsync().ConfigureAwait(false);
    } catch (Exception ex) {
        Debug.WriteLine($"Background task failed: {ex.Message}");
    }
});
```
**When to use each pattern:**
- `Task.Run(() => SyncWork())`: CPU-bound synchronous work
- `Task.Run(async () => await AsyncWork())`: Async work on background thread
- `await Task.Run(() => DoWork())`: When you need the result back
- `_ = Task.Run(() => DoWork())`: Fire-and-forget background work

## Compass sensor lifecycle (MAUI)
**Critical initialization order in CompassViewModel:**
```csharp
public CompassViewModel(DataService dataService) {
    _dataService = dataService;
    UpdateLocationFromPreferences(); // Load cached location first
    
    try {
        if (!Compass.IsMonitoring) {
            // ✅ MUST subscribe BEFORE Compass.Start() or readings are lost
            Compass.ReadingChanged += Compass_ReadingChanged;
            Compass.Start(SensorSpeed.UI, applyLowPassFilter: true);
        }
    } catch (FeatureNotSupportedException ex) {
        // Handle devices without compass hardware
    }
}
```
**Proper disposal pattern:**
```csharp
public void Dispose() {
    try {
        if (Compass.IsMonitoring) {
            Compass.ReadingChanged -= Compass_ReadingChanged; // Unsubscribe first
            Compass.Stop(); // Then stop sensor
        }
    } catch (Exception ex) {
        Debug.WriteLine($"Compass disposal error: {ex.Message}");
    }
}
```
**Event handler pattern:**
```csharp
private void Compass_ReadingChanged(object sender, CompassChangedEventArgs e) {
    // Calculate Qibla direction relative to magnetic north
    var qiblaLocation = new Location(_qiblaLatitude, _qiblaLongitude);
    var currentPosition = new Location(_currentLatitude, _currentLongitude);
    var targetHeading = (360 - DistanceCalculator.Bearing(currentPosition, qiblaLocation)) % 360;
    
    var currentHeading = 360 - e.Reading.HeadingMagneticNorth;
    Heading = currentHeading - targetHeading; // Relative rotation for UI
}
```

## Common gotchas
- Android 10+ disables alarm flags in constructor (legacy workaround)
- WinUI location permissions treated as always granted
- Compass disposal requires explicit cleanup in `CompassViewModel.Dispose()` - MUST unsubscribe before Stop()
- FontAwesome icons use Unicode glyphs: `FontFamily="{StaticResource IconFontFamily}"`
- JSON API returns bare objects/arrays, not wrapped in `{isSuccess, data}` structure
- Widget icons: Use Unicode symbols instead of FontAwesome for better compatibility
- PendingIntent requires `PendingIntentFlags.Immutable` on Android 12+ or scheduling fails
- AlarmForegroundService must call `StartForeground()` within 5 seconds or ANR warning appears
- Compass.ReadingChanged subscription MUST happen before Compass.Start() or no updates occur

Quick file pointers: `MauiProgram.cs` (DI), `DataService.cs` (core logic), `BaseViewModel.cs` (MVVM foundation), `AppShell.xaml` (navigation), `Resources/Styles/` (Material Design 3 theming), `AlarmForegroundService.cs` (Android alarm lifecycle).
