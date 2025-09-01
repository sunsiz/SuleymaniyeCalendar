# AI working guide for SuleymaniyeCalendar

This is a .NET MAUI prayer times app (Android, iOS, Windows) using CommunityToolkit.MVVM, Shell navigation, and localization. Features Material Design 3 UI with dynamic typography and performance-optimized async patterns.

## Big picture architecture
- **MVVM pattern**: ViewModels inherit from `BaseViewModel` (ObservableObject), Views in `Views/`, Shell navigation in `AppShell.xaml`
- **Central service**: `DataService` handles location, prayer time API calls, caching, and alarm scheduling
- **DI container**: All types registered as singletons in `MauiProgram.CreateMauiApp()` with constructor injection
- **Localization**: Uses `LocalizationResourceManager.Maui` with `AppResources.resx`, XAML binds via `{localization:Translate Key}`
- **Prayer data flow**: API → XML cache → Calendar models → Prayer ViewModels → UI cards

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

### 3. UI-first async loading pattern
ViewModels show UI immediately, then load data async:
```csharp
public MonthViewModel(DataService dataService) {
    MonthlyCalendar = new ObservableCollection<Calendar>();
    _ = LoadMonthlyDataAsync(); // Fire-and-forget
}
```
Always use `MainThread.InvokeOnMainThreadAsync()` for UI updates from background tasks.

### 4. Prayer state management
Each prayer has temporal states (Past/Current/Future) calculated in `MainViewModel.CheckState()`:
```csharp
asr.State = CheckState(DateTime.Parse(_calendar.Asr), DateTime.Parse(_calendar.Maghrib));
asr.UpdateVisualState(); // Updates colors/icons based on state
```

### 5. Platform-specific service registration
```csharp
#if ANDROID
    builder.Services.AddSingleton<IAlarmService, AlarmForegroundService>();
#else
    builder.Services.AddSingleton<IAlarmService, NullAlarmService>();
#endif
```

## DataService responsibilities (the system hub)
- **Prayer times**: Fetches from `http://servis.suleymaniyetakvimi.com/servis.asmx` (XML), caches to `%LOCALAPPDATA%/monthlycalendar.xml`
- **Location**: Handles permissions, GPS, geocoding with robust fallbacks
- **Alarms**: Schedules up to 15 days via `IAlarmService` using user notification offsets
- **Network resilience**: Uses cached data when offline, shows appropriate toasts

## Navigation patterns
- Tab navigation declared in `AppShell.xaml`, extra routes registered in `AppShell.xaml.cs`
- Navigation: `await Shell.Current.GoToAsync(nameof(Page))` 
- Parameter passing: `new Dictionary<string, object> { { "PropertyName", value } }`
- Back navigation: `await Shell.Current.GoToAsync("..")`

## Material Design 3 card system
```xaml
<Border Style="{StaticResource Card}">            <!-- Standard card -->
<Border Style="{StaticResource CurrentPrayerCard}"> <!-- Enhanced emphasis -->
```
Cards use `SurfaceVariantColor` backgrounds, `OutlineColor` borders, automatic light/dark theming.

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

Quick file pointers: `MauiProgram.cs` (DI), `DataService.cs` (core logic), `BaseViewModel.cs` (MVVM foundation), `AppShell.xaml` (navigation), `Resources/Styles/` (Material Design 3 theming).
