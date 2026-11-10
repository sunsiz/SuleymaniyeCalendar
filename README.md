# Süleymaniye Calendar

Süleymaniye Calendar is a .NET MAUI application for viewing prayer times and related calendar information. It includes location-aware daily and monthly calendars, prayer reminders, a Qibla compass, radio playback, localization, and an Android home-screen widget.

## Supported platforms

The app project targets Android and iOS, and adds a Windows target when built on Windows. Platform-specific capabilities differ:

| Platform | App | Prayer reminders | Home-screen widget |
| --- | --- | --- | --- |
| Android | Yes | Native alarms and notifications | Yes |
| iOS | Yes | Local notifications, subject to iOS limits | No |
| Windows | Windows target is added on Windows | No native alarm implementation is registered | No |

The repository contains additional platform folders, but those folders alone do not mean a platform is currently configured as a build target. iOS builds require macOS and Xcode.

## Features

- **Daily prayer times:** Displays the day's times for the selected/current location, with current, past, and upcoming prayer states.
- **Monthly calendar:** Browse prayer times across a month. The app can show previously cached data while it refreshes data for the selected month.
- **Location:** Uses device location with permission handling and a saved-location path. Users can request a fresh fix, and the setting to always renew location opts into refreshing on app use.
- **Prayer reminders:** Configure reminders by prayer, including enablement, sound, and notification timing. Android requires exact-alarm access for enabled reminders; Android 13 and later also require notification permission. The app does not silently substitute inexact alarms if exact-alarm access is unavailable.
- **Android alarm recovery:** Rebuilds scheduled alarms after relevant system events and uses periodic background renewal. The optional foreground notification is separate from the prayer reminders themselves.
- **Qibla compass:** Uses location and compass sensor readings to show the direction of Qibla. Sensor use is limited to the compass screen's lifecycle.
- **Radio:** Provides radio playback through the app's radio service.
- **Android widget:** Shows prayer-time information on the home screen and has theme- and right-to-left-aware layouts.
- **Languages:** English (base resources), Turkish, Arabic, German, French, Russian, Chinese, Farsi, Uyghur, Uzbek, and Azerbaijani.
- **Right-to-left layout:** Arabic, Farsi, and Uyghur use right-to-left direction handling; the Android widget includes a corresponding RTL layout.
- **Appearance and accessibility:** Light, dark, and system themes; scalable typography; and accessible labels and controls.

## How data and reminders work

### Prayer-time data

1. The location service supplies a saved or freshly acquired location.
2. The repository checks the local prayer cache for the requested location and date range.
3. If network data is needed, `JsonApiService` requests daily or monthly data from `https://api.suleymaniyetakvimi.com/api/TimeCalculation/`.
4. The response is mapped to the app's calendar model and persisted in a location- and year-based cache.
5. View models present the data to the calendar pages and the Android widget.

The older XML API integration was removed in the March 2026 .NET 10 update. The current network source is the JSON API; the local cache provides reuse and offline continuity when matching data is already available. It is not an XML or second-server fallback. When data is neither cached nor available from the API, callers must handle an unavailable result.

Cache access is synchronized across service instances. Updates are written to a temporary file before replacing the stored cache, so concurrent reads and interrupted writes are less likely to expose partial data.

### Prayer reminders

Reminder settings are stored per prayer. The notification scheduler validates prayer times and the date range before scheduling and tracks coverage only after scheduling succeeds. It maintains a rolling schedule of up to 30 days, fetching data across month boundaries as necessary.

On Android, the platform alarm service handles exact alarms, notification channels, and recovery after events such as reboot and time changes. The foreground notification is an optional status feature, not a prerequisite for reminders. On iOS, reminders use local notifications and are subject to operating-system scheduling limits. Windows currently uses a no-op alarm service.

## Architecture and design decisions

The project follows MVVM and separates UI state from platform and data concerns:

- **Views and Shell:** XAML pages define the UI; Shell provides the tab navigation for the main calendar, compass, radio, and about pages.
- **View models:** Own page state and user actions. `BaseViewModel` centralizes shared view-model behavior, including typography-related state.
- **`DataService`:** A small facade used by view models. It delegates location, prayer-time retrieval, caching, and alarm scheduling to specialized services rather than concentrating those responsibilities in page code.
- **`PrayerTimesRepository`:** Coordinates cache reads and JSON API requests, and assembles date ranges used by the calendar and scheduler.
- **`JsonApiService`:** Owns the JSON API integration and maps API responses to app models.
- **`PrayerCacheService`:** Owns the local location/year cache and its synchronized file updates.
- **`NotificationSchedulerService`:** Applies shared reminder policy and date-range validation, while `IAlarmService` allows Android and iOS to use native implementations and unsupported targets to use a no-op implementation.
- **Dependency injection:** `MauiProgram` registers services and view models. Platform-specific implementations are selected at compile time, keeping native alarm/widget/audio-session code out of shared UI logic.
- **Localization and resources:** `LocalizationResourceManager.Maui` loads localized `.resx` resources; the RTL service and app constants apply directionality where needed.

Important choices and why they were made:

| Decision | Reason |
| --- | --- |
| JSON API plus a local cache, rather than retaining the legacy XML service | Keeps the data integration aligned with the current API while allowing repeated reads and cached/offline use without maintaining two parsers and fallback paths. |
| A cache partitioned by location and year | Reuses data for month views and alarm date ranges while keeping different locations and calendar years separate. |
| Separate repository, cache, location, and scheduling services behind a facade | Gives each service a clear responsibility and keeps view models independent of HTTP, file I/O, and native alarm APIs. |
| Exact Android reminders with explicit permission handling | Prayer reminders are time-sensitive; using an inexact fallback would claim success while weakening the behavior users requested. |
| Platform-specific alarm adapters | Android and iOS expose different notification APIs and constraints, so native behavior is kept behind a shared interface. Unsupported Windows alarms are explicit rather than simulated. |
| A rolling alarm window rather than scheduling an entire year | Limits scheduling work and resource use while background recovery renews future coverage. |
| Invariant-culture parsing for numeric data | Coordinates and numeric API values must not change meaning with the user's decimal separator. |
| Resource-based localization and explicit RTL handling | Keeps translated text separate from layout logic and supports languages whose reading direction differs from the default. |
| A delayed/async loading approach for larger views and background preloading | Lets the interface render promptly and moves data preparation away from UI-blocking work. |

## Repository layout

```text
SuleymaniyeCalendar/
├── Models/          # Calendar, prayer, theme, language, and related data
├── Views/           # XAML pages and view code-behind
├── ViewModels/      # MVVM page and shared UI state
├── Services/        # Data, location, cache, API, alarm, radio, and support services
├── Helpers/         # Shared constants and parsing helpers
├── Resources/       # Images, fonts, styles, and localized strings
└── Platforms/       # Android, iOS, and platform-specific code

SuleymaniyeCalendar.Tests/  # MSTest service, view-model, alarm, cache, and widget tests
docs/                       # Focused implementation notes
spec/                       # Architecture specification
CHANGELOG.md                # Unreleased changes and selected project milestones
```

## Build and test

### Requirements

- .NET 10 SDK and the MAUI workloads required for the target platform.
- Android SDK for Android builds.
- macOS with Xcode for iOS builds.
- Windows and the Windows App SDK tooling for Windows builds.

### Commands

Run commands from the repository root:

```bash
# Restore the solution; build all configured targets when their workloads are available
dotnet restore SuleymaniyeCalendar.sln
dotnet build SuleymaniyeCalendar.sln

# Build an individual app target
dotnet build SuleymaniyeCalendar/SuleymaniyeCalendar.csproj -f net10.0-android
dotnet build SuleymaniyeCalendar/SuleymaniyeCalendar.csproj -f net10.0-ios
dotnet build SuleymaniyeCalendar/SuleymaniyeCalendar.csproj -f net10.0-windows10.0.26100.0

# Run the automated tests
dotnet test SuleymaniyeCalendar.Tests/SuleymaniyeCalendar.Tests.csproj
```

The Windows app target is available on Windows and targets `net10.0-windows10.0.26100.0`; run that command on Windows. The iOS target requires macOS and Xcode. Deploy and launch the app using Visual Studio or the .NET MAUI tooling for the chosen device or simulator.

Tests use MSTest, Moq, and FluentAssertions. They cover service and view-model behavior, JSON mapping, location handling, alarm scheduling and settings synchronization, cache concurrency, and widget logic. Automated tests do not replace device-level checks for permissions, native notifications, sensors, or widget rendering.

## Further reading

- [CHANGELOG.md](CHANGELOG.md) — current work and selected milestones from Git history.
- [spec/spec-architecture-maui-prayer-times-app.md](spec/spec-architecture-maui-prayer-times-app.md) — architecture and integration specification. Some historical requirements in this document predate the JSON-only API design; use the current code and this README for current behavior.
- [docs/android-button-cornerradius-workaround.md](docs/android-button-cornerradius-workaround.md) — Android UI implementation note.
- [LICENSE.txt](LICENSE.txt) — license details.
