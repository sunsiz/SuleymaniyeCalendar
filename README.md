# Süleymaniye Calendar

A .NET MAUI prayer times application for Android, iOS, and Windows.

## Features

- **Prayer Times**: Displays daily prayer times calculated based on your location
- **Alarm Notifications**: Schedule notifications for each prayer time (Android)
- **Monthly Calendar**: View prayer times for the entire month
- **Qibla Compass**: Points to the direction of Qibla using device compass
- **Radio Streaming**: Listen to Süleymaniye radio station
- **Multi-language Support**: Turkish, English, Arabic, German, French, Russian, Chinese, Farsi, Uyghur, Uzbek, Azerbaijani
- **RTL Support**: Full right-to-left layout support for Arabic, Farsi, and Uyghur
- **Themes**: Light, Dark, and System theme options
- **Accessibility**: Screen reader support and dynamic font scaling
- **Android Widget**: Home screen prayer times widget with theme and RTL support

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