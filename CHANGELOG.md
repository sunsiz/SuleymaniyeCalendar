# Changelog

## Unreleased

### Alarm reliability

- Require exact alarm access before saving enabled Android reminders; do not fall back to inexact alarms.
- Validate prayer times and the requested date range before replacing scheduled alarms. Record coverage only after scheduling succeeds.
- Serialize reminder settings changes with alarm scheduling, restore previous settings after a failed save, and warn when previous alarms cannot be restored.
- Request Android alarm recovery independently of the optional foreground notification, including after reboot, time changes, app updates, and exact-alarm permission grants. Add periodic background renewal.
- Await iOS local notification scheduling and report scheduling failures.
- Use prayer IDs to select localized Android alarm titles.

### Other changes

- Reuse the Android foreground notification channel and retry loading the current day's data after a failed date refresh, while retaining the 30-second refresh interval and separate sound channels.
- Serialize cache reads and updates across service instances, and replace cache files only after the new data is fully written.
- Refresh the Android widget with today's data rather than reusing a previous day's calendar, and process widget requests on the service worker thread.
- Add localized guidance for alarm permission and save failures.
- Align test dependencies with the app and add prayer-time validation and alarm synchronization tests.
- Remove the Android `BATTERY_STATS` permission and stop logging location coordinates.

## Project history

These are selected development milestones from Git history, not formal versioned releases.

### 2026-11-04 to 2026-11-06

- Harden Android alarm and notification recovery, including boot/time-change handling and periodic renewal; improve save synchronization and failure reporting for reminder settings.
- Serialize prayer-cache access and replace cache files only after a complete write.
- Correct Android widget refresh data and move widget work onto the service worker thread.
- Add Android crash guards, improve location error handling, and update localized UI and alarm guidance.

### 2026-03-24 to 2026-03-28

- Upgrade the app from .NET 9 to .NET 10.
- Remove the legacy XML API integration and streamline prayer-time retrieval around the JSON API and cache.
- Improve Android alarm reliability and notification threading, and add the `beep4` alarm sound.

### 2026-01-07 to 2026-01-26

- Refactor app code for maintainability and address iOS local-notification behavior, badge handling, and font scaling.
- Add widget-related tests and continue test-suite improvements.

### 2025-12-03 to 2025-12-19

- Add background data preloading and optimize prayer-time fetching.
- Improve Android and iOS notification permissions, alarm sound delivery, and alarm settings behavior.
- Expand right-to-left and Uyghur language support, add Material 3 styling and dynamic font scaling, and improve location display synchronization.

### 2025-09-01 to 2025-09-25

- Introduce location refresh preferences, JSON-first prayer-time retrieval with caching, and performance instrumentation.
- Add radio services and broaden right-to-left support; improve month-page loading and widget behavior.

### 2025-08-21

- Establish the initial .NET MAUI app project and repository structure.
