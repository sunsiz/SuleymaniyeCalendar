# Süleymaniye Calendar

A .NET MAUI prayer times application for Android, iOS, and Windows.

## Features

- **Prayer Times**: Displays daily prayer times calculated based on your location
- **Alarm Notifications**: Configure prayer reminders with a choice of bundled sounds and notification timing
- **Monthly Calendar**: View prayer times for the entire month
- **Qibla Compass**: Points to the direction of Qibla using device compass
- **Radio Streaming**: Listen to Süleymaniye radio station
- **Multi-language Support**: Turkish, English, Arabic, German, French, Russian, Chinese, Farsi, Uyghur, Uzbek, Azerbaijani
- **RTL Support**: Full right-to-left layout support for Arabic, Farsi, and Uyghur
- **Themes**: Light, Dark, and System theme options
- **Accessibility**: Screen reader support and dynamic font scaling
- **Android Widget**: Home screen prayer times widget with theme and RTL support

## Prayer reminders

- On Android 12 and later, exact alarm access is required when enabling a reminder. If access is not granted, the changes are not saved and no inexact alarm is substituted. Android 13 and later also require notification permission.
- The optional Android foreground notification displays prayer-time status and refreshes approximately every 30 seconds. Turning it off does not disable prayer reminders.
- Android uses separate notification channels for the bundled alarm sounds. Alarm recovery is requested after events such as reboot and time-zone changes, and a periodic background job helps renew scheduled coverage.
- On iOS, reminders use local notifications and are subject to iOS notification limits.

See [CHANGELOG.md](CHANGELOG.md) for recent changes.

## Architecture

- **Pattern**: MVVM with CommunityToolkit.MVVM
- **Navigation**: Shell-based navigation
- **DI**: Microsoft.Extensions.DependencyInjection
- **Localization**: LocalizationResourceManager.Maui
- **API**: JSON API (`api.suleymaniyetakvimi.com`) with local caching via `PrayerCacheService`

## Project Structure

```
SuleymaniyeCalendar/
├── Models/          # Data models (Calendar, Prayer, etc.)
├── Views/           # XAML pages and views
├── ViewModels/      # MVVM ViewModels
├── Services/        # Business logic and API services
├── Resources/       # Fonts, images, strings, styles
└── Platforms/       # Platform-specific code
```

## Building

```bash
# Build all platforms
dotnet build

# Build for Android
dotnet build -f net10.0-android

# Run on Android device
dotnet run -f net10.0-android
```

## Requirements

- .NET 10.0 SDK
- Visual Studio 2022 (17.14+) or VS Code with MAUI extension
- Android SDK (for Android development)
- Xcode (for iOS development, macOS only)

## License

See [LICENSE.txt](LICENSE.txt) for details.