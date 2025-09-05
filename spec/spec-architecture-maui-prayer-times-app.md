---
title: SuleymaniyeCalendar App Architecture and Integration Spec
version: 1.0
date_created: 2025-09-04
last_updated: 2025-09-04
owner: Core App (SuleymaniyeCalendar)
tags: [architecture, app, maui, android, ios, windows, rtl, performance]
---

# Introduction

This specification documents the architecture, data flows, interfaces, requirements, and acceptance criteria of the SuleymaniyeCalendar .NET MAUI application. It is designed for AI agents and developers to quickly understand how to safely extend and maintain the app.

## 1. Purpose & Scope

- Define the MAUI app's high-level architecture, responsibilities, and integration points.
- Cover hybrid data fetching (JSON API + XML fallback), caching, widgets, localization, and RTL support.
- Scope includes app runtime behavior across Android, iOS, and Windows, plus Android widget behavior.
- Audience: Contributors, AI coding agents, and maintainers.

## 2. Definitions

- MVVM: Model-View-ViewModel pattern.
- MAUI: .NET Multi-platform App UI.
- RTL: Right-to-left language support.
- JSON API: New REST endpoints at `api.suleymaniyetakvimi.com`.
- XML API: Legacy SOAP-like endpoints at `servis.suleymaniyetakvimi.com/servis.asmx`.
- Calendar model: `SuleymaniyeCalendar.Models.Calendar`, app’s core daily record.

## 3. Requirements, Constraints & Guidelines

- REQ-001: Use hybrid API pattern. Attempt JSON API first, then fallback to XML API if null/fails.
- REQ-002: Use `CultureInfo.InvariantCulture` for numeric parsing/formatting of coordinates and numeric fields.
- REQ-003: Always update ObservableCollections on UI thread. Use `MainThread.InvokeOnMainThreadAsync`.
- REQ-004: Implement delayed loading on MonthPage with staged delays (100ms then 500ms) before background loading.
- REQ-005: Cache monthly JSON data to `%LOCALAPPDATA%/monthlycalendar.json` and XML to `%LOCALAPPDATA%/monthlycalendar.xml`.
- REQ-006: Maintain full RTL support in pages and widget. Respect `IRtlService` when present.
- REQ-007: Ensure widget is theme-aware and language-aware; auto-refresh on theme/language changes.
- REQ-008: Only update font/culture resources when changed. Avoid unnecessary resource churn.
- SEC-001: Do not exfiltrate secrets or make network calls unless required by the feature.
- CON-001: Avoid blocking UI thread; use async/await and background tasks for IO.
- CON-002: Preserve DI registrations; use constructor injection.
- GUD-001: Use DynamicResource for font sizes; never StaticResource for fonts.
- PAT-001: Batch large collection updates in chunks of 10 to reduce UI thrash.

## 4. Interfaces & Data Contracts

- DataService (system hub)
  - GetMonthlyPrayerTimesHybridAsync(Location, bool): ObservableCollection<Calendar>
  - GetDailyPrayerTimesHybridAsync(Location, DateTime?): Calendar
  - GetPrayerTimesHybridAsync(bool): Calendar
  - Responsibilities: location, caching, alarm scheduling, API selection.

- JsonApiService (new API)
  - GET TimeCalculation/TimeCalculate?latitude&longitude&date
  - GET TimeCalculation/TimeCalculateByMonth?latitude&longitude&monthId
  - Response models:
    - JsonPrayerTimeResponse { isSuccess: bool, message: string, data: JsonPrayerTimeData }
    - JsonMonthlyPrayerTimeResponse { isSuccess: bool, message: string, data: JsonPrayerTimeData[] }
    - JsonPrayerTimeData { date, latitude, longitude, altitude, timeZone, dayLightSaving, falseFajr, fajr, sunrise, dhuhr, asr, maghrib, isha, endOfIsha }

- XML API (legacy)
  - VakitHesabiListesi?Enlem&Boylam&Yukseklik&SaatBolgesi&yazSaati&Tarih
  - Parsed to Calendar collection; safe numeric parsing via InvariantCulture.

- Android Widget
  - AppWidget + WidgetService build RemoteViews.
  - Theme detection via `IsSystemDarkMode` and `SelectedTheme` preference.
  - RTL through `Widget.axml` and `WidgetRtl.axml`. Unicode refresh icon.

## 5. Acceptance Criteria

- AC-001: Given internet and valid location, when requesting monthly data, the app must first call JSON API and on success cache results to JSON; otherwise fallback to XML and display data.
- AC-002: Given the MonthPage opens, when no data loaded yet, then UI appears instantly, shows busy indicator after ~600ms total, and populates in batches of ~10.
- AC-003: Given a RTL language is selected, when navigating across pages and widget, all text/layout aligns right and widget uses RTL layout.
- AC-004: Given theme changes (system or SelectedTheme), the widget updates background and text colors without user action.
- AC-005: Given high-lat/long locales, numeric parsing uses invariant culture and does not throw due to comma/dot differences.
- AC-006: Given JSON API returns isSuccess=false or schema mismatch, app logs and gracefully falls back to XML.

## 6. Test Automation Strategy

- Test Levels: Unit (models, converters), Integration (DataService hybrid path), UI smoke (MonthPage load pattern).
- Frameworks: MSTest or xUnit; FluentAssertions; Moq for service fakes.
- Test Data: Store minimal JSON/XML samples under a test resources folder; verify parsing and fallback.
- CI/CD: GitHub Actions to build and run tests on push; artifact app packages per platform.
- Coverage: Aim for 70%+ on services/models; critical paths (hybrid, caching) must be covered.
- Performance: Measure MonthPage load times with PerformanceService; assert bounds (<1s to first paint, <2s to data shown on average dev machine).

## 7. Rationale & Context

- JSON-first ensures future-proofing and faster responses; XML fallback preserves reliability.
- Delayed loading improves perceived performance and avoids ANR-like jank.
- Invariant culture avoids numeric parsing bugs when UI culture uses commas.
- Widget uses Unicode icons to avoid font rendering issues on some OEM launchers.

## 8. Dependencies & External Integrations

### External Systems
- EXT-001: Legacy XML API (servis.suleymaniyetakvimi.com/servis.asmx) – monthly and daily prayer times.
- EXT-002: JSON API (api.suleymaniyetakvimi.com) – modern endpoints for daily/monthly times.

### Third-Party Services
- SVC-001: MAUI CommunityToolkit – MVVM, alerts, toasts.

### Infrastructure Dependencies
- INF-001: Local storage for caches in `%LOCALAPPDATA%`.

### Data Dependencies
- DAT-001: Cached JSON file `monthlycalendar.json`, XML file `monthlycalendar.xml`.

### Technology Platform Dependencies
- PLT-001: .NET 9 MAUI; Android/iOS/Windows targets.

### Compliance Dependencies
- COM-001: App respects device language and RTL policies; permissions conform to platform rules.

## 9. Examples & Edge Cases

```code
// JSON fallback example
var data = await _jsonApiService.GetMonthlyPrayerTimesAsync(lat, lng, month);
if (data == null || data.Count == 0) {
    var xml = GetMonthlyPrayerTimes(location, forceRefresh);
}

// Invariant culture
calendar.Latitude = Convert.ToDouble(item.Value, CultureInfo.InvariantCulture.NumberFormat);

// Delayed load
await Task.Delay(100); IsBusy = true; await Task.Delay(500); await LoadMonthlyDataAsync();
```

## 10. Validation Criteria

- Build succeeds on all target frameworks.
- Hybrid methods return non-null objects when at least one API succeeds.
- JSON schema mismatches log errors and do not crash the app.
- Widget theme, RTL, and refresh icon update correctly.

## 11. Related Specifications / Further Reading

- `.github/copilot-instructions.md` – AI working guide
- `SuleymaniyeCalendar/Services/DataService.cs` – core logic
- `SuleymaniyeCalendar/Services/JsonApiService.cs` – JSON API integration
- `SuleymaniyeCalendar/ViewModels/MonthViewModel.cs` – delayed load pattern
- `SuleymaniyeCalendar/Platforms/Android/WidgetService.cs` – widget implementation
