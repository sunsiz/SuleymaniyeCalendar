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
- **Alarms**: Schedules up to 15 days via `IAlarmService` using user notification offsets
- **Network resilience**: Uses cached data when offline, shows appropriate toasts

## JsonApiService integration
- **Endpoint pattern**: `https://api.suleymaniyetakvimi.com/api/TimeCalculation/TimeCalculate[ByMonth]`
- **Response models**: `JsonPrayerTimeResponse` (single day), `JsonMonthlyPrayerTimeResponse` (full month)
- **Error handling**: Always check `IsSuccess` property before using `Data`
- **Fallback strategy**: XML API used when JSON fails or returns null

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

## Common gotchas
- Android 10+ disables alarm flags in constructor (legacy workaround)
- WinUI location permissions treated as always granted
- Compass disposal requires explicit cleanup in `CompassViewModel.Dispose()`
- FontAwesome icons use Unicode glyphs: `FontFamily="{StaticResource IconFontFamily}"`
- JSON deserialization: Check `JsonResponse.IsSuccess` before accessing `Data` property
- Widget icons: Use Unicode symbols instead of FontAwesome for better compatibility

Quick file pointers: `MauiProgram.cs` (DI), `DataService.cs` (core logic), `BaseViewModel.cs` (MVVM foundation), `AppShell.xaml` (navigation), `Resources/Styles/` (Material Design 3 theming).
