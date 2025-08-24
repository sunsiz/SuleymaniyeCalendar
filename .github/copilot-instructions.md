# AI working guide for SuleymaniyeCalendar

This is a .NET MAUI app (Android, iOS, Windows conditional) using CommunityToolkit.MVVM, MAUI Toolkit, and a localization resource manager with Shell navigation. Use these repo-specific practices.

## Big picture
- MVVM: ViewModels under `ViewModels/` derive from `BaseViewModel`; Views in `Views/`; XAML Shell in `AppShell.xaml`.
- DI and app wiring live in `MauiProgram.CreateMauiApp()`. Most types are registered as singletons and resolved via constructor injection.
- Core logic hub: `Services/DataService.cs` (location, network, XML parsing, caching, alarms).

## DI + MVVM patterns
- Attributes from CommunityToolkit.MVVM:
  - `[ObservableProperty]` creates bindable properties and optional partial setters to persist `Preferences` (see `BaseViewModel`, `SettingsViewModel`).
  - `[RelayCommand]` exposes commands (e.g., `MainViewModel.RefreshLocation`).
- Register new Pages, ViewModels, and Services in `MauiProgram`; add Shell routes for navigable pages.
- On non-Android, if you need alarms, register `NullAlarmService` behind `#else` for `IAlarmService`.

## Shell navigation
- Tabs are declared in `AppShell.xaml`; extra routes registered in `AppShell.xaml.cs` via page class names (e.g., `nameof(SettingsPage)`).
- Navigate with `Shell.Current.GoToAsync(nameof(Page), ...);` and pass parameters using a dictionary keyed by target VM property names.
- XAML uses localization markup `{localization:Translate Key}` for titles and text.

## DataService responsibilities (key behaviors)
- Fetches prayer times from `http://servis.suleymaniyetakvimi.com/servis.asmx` (XML) and caches monthly data to `%LOCALAPPDATA%/monthlycalendar.xml`.
- Networking gated by `Connectivity`. On no internet, uses cache when valid; otherwise returns `null` and shows toasts.
- Location flow: checks/requests `Permissions.LocationWhenInUse`; WinUI treated as granted. Uses `Geolocation` and `Geocoding`.
- Culture safety: numeric parsing uses `CultureInfo.InvariantCulture` to avoid comma/dot issues—preserve this for all numeric conversions.
- Alarms: `SetWeeklyAlarmsAsync()` schedules up to 15 days via `IAlarmService` using user offsets from `Preferences` (e.g., `fajrNotificationTime`).

## Android alarm foreground service
- `Platforms/Android/AlarmForegroundService.cs` implements `IAlarmService` using `AlarmManager`; distinct `PendingIntent` request codes per prayer/day.
- Foreground notification updates every 30s with localized content; toggled by `Preferences["ForegroundServiceEnabled"]`.
- Non-Android platforms rely on `NullAlarmService` if used.

## Preferences and state
- Location/time cache: `LastLatitude`, `LastLongitude`, `LastAltitude`, `LocationSaved`, `LastAlarmDate`.
- Prayer times and toggles use ids: `<prayerId>` and `<prayerId>Enabled`, `<prayerId>NotificationTime` (e.g., `fajr`, `fajrEnabled`).
- App options: `FontSize`, `AlwaysRenewLocationEnabled`, `NotificationPrayerTimesEnabled`, `ForegroundServiceEnabled`, `SelectedLanguage`.
- Follow existing key names and minimal types when adding settings.

## Localization
- Resource file: `Resources/Strings/AppResources.resx` with generated designer; use `AppResources.*` in C#.
- In `SettingsViewModel`, `ILocalizationResourceManager` updates `CurrentCulture`; selection stored in `Preferences["SelectedLanguage"]`.

## Build & run
- Solution: `SuleymaniyeCalendar.sln`; project: `SuleymaniyeCalendar/SuleymaniyeCalendar.csproj`.
- Target frameworks: `net9.0-android; net9.0-ios`; Windows adds `net9.0-windows10.0.26100.0` on Windows hosts.
- Packages include `Microsoft.Maui.Controls 9.0.100`, `CommunityToolkit.Maui`, `CommunityToolkit.Mvvm`, `LocalizationResourceManager.Maui`.
- Use VS or `dotnet` to deploy. Delete `%LOCALAPPDATA%/monthlycalendar.xml` to force a fresh monthly fetch.
- Optional Tizen workload installer: `workload-install.ps1` (not required unless targeting Tizen).

## When adding/changing features
- Use DI; don’t new-up services inside ViewModels. Register in `MauiProgram` and add Shell route when needed.
- Keep parsing/formatting culture-safe; keep UI interactions on the main thread (`MainThread.BeginInvokeOnMainThread`).
- Keep Android-specific code under `#if ANDROID`; don’t leak platform types into shared code.

Quick pointers: `MauiProgram.cs`, `AppShell.xaml/_.cs`, `Services/DataService.cs`, `ViewModels/MainViewModel.cs`, `ViewModels/SettingsViewModel.cs`, `Platforms/Android/AlarmForegroundService.cs`.
