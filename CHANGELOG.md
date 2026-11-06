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
- Add localized guidance for alarm permission and save failures.
- Align test dependencies with the app and add prayer-time validation and alarm synchronization tests.
- Remove the Android `BATTERY_STATS` permission and stop logging location coordinates.
