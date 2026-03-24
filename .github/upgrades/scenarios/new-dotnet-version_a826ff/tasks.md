# SuleymaniyeCalendar .NET 10.0 Upgrade Tasks

## Overview

This document tracks the execution of the SuleymaniyeCalendar project upgrade from .NET 9 to .NET 10 (LTS). Both projects will be upgraded simultaneously in a single atomic operation, followed by testing and validation.

**Progress**: 2/4 tasks complete (50%) ![0%](https://progress-bar.xyz/50)

---

## Tasks

### [✓] TASK-001: Verify prerequisites *(Completed: 2026-03-24 11:21)*
**References**: Plan §Phase 0

- [✓] (1) Verify .NET 10 SDK is installed
- [✓] (2) SDK version meets minimum requirements (**Verify**)

---

### [✓] TASK-002: Atomic framework and dependency upgrade with compilation fixes *(Completed: 2026-03-24 11:25)*
**References**: Plan §Phase 1, Plan §4.1, Plan §4.2, Plan §5, Plan §6

- [✓] (1) Update `TargetFrameworks` in `SuleymaniyeCalendar.csproj` from `net9.0-android;net9.0-ios` to `net10.0-android;net10.0-ios` per Plan §4.1
- [✓] (2) Update Windows conditional `TargetFrameworks` from `net9.0-windows10.0.26100.0` to `net10.0-windows10.0.26100.0` per Plan §4.1
- [✓] (3) Update all conditional `PropertyGroup` conditions from `net9.0-*` to `net10.0-*` (5 conditions total) per Plan §4.1
- [✓] (4) All project file TFM references updated to `net10.0-*` (**Verify**)
- [✓] (5) Update `TargetFramework` in `SuleymaniyeCalendar.Tests.csproj` from `net9.0-windows10.0.26100` to `net10.0-windows10.0.26100` per Plan §4.2
- [✓] (6) Test project TFM updated to `net10.0-windows10.0.26100` (**Verify**)
- [✓] (7) Update `Microsoft.Extensions.Logging.Debug` from `9.0.10` to `10.0.5` in `SuleymaniyeCalendar.csproj` per Plan §5
- [✓] (8) Package reference updated to version `10.0.5` (**Verify**)
- [✓] (9) Clean `obj/` and `bin/` directories to force source generator regeneration
- [✓] (10) Restore all dependencies across both projects
- [✓] (11) All dependencies restored successfully (**Verify**)
- [✓] (12) Build entire solution (all target platforms: Android, iOS, Windows)
- [✓] (13) Fix all compilation errors per Plan §6 Breaking Changes Catalog (focus: auto-generated code will regenerate; user code changes minimal)
- [✓] (14) Rebuild solution to verify fixes
- [✓] (15) Solution builds with 0 errors across all platforms (**Verify**)

---

### [⊘] TASK-003: Run full test suite and validate upgrade
**References**: Plan §Phase 2, Plan §8

- [✓] (1) Build test project `SuleymaniyeCalendar.Tests.csproj`
- [✓] (2) Test project builds successfully (**Verify**)
- [✓] (3) Run all tests in `SuleymaniyeCalendar.Tests.csproj`
- [⊘] (4) Fix any test failures (reference Plan §6 Behavioral Changes for runtime behavior differences)
- [⊘] (5) Re-run tests after fixes
- [⊘] (6) All tests pass with 0 failures (**Verify**)

---

### [▶] TASK-004: Final commit
**References**: Plan §10 Source Control Strategy

- [ ] (1) Commit all changes with message: "Upgrade solution from .NET 9 to .NET 10 (LTS) - Update all project files, packages, and pass all tests"

---






