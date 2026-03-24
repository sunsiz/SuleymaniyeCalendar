# .NET 10.0 Upgrade Plan — SuleymaniyeCalendar

## Table of Contents

- [1. Executive Summary](#1-executive-summary)
- [2. Migration Strategy](#2-migration-strategy)
- [3. Detailed Dependency Analysis](#3-detailed-dependency-analysis)
- [4. Project-by-Project Plans](#4-project-by-project-plans)
  - [4.1 SuleymaniyeCalendar.csproj](#41-suleymaniyecalendarcsproj)
  - [4.2 SuleymaniyeCalendar.Tests.csproj](#42-suleymaniyecalendartestscsproj)
- [5. Package Update Reference](#5-package-update-reference)
- [6. Breaking Changes Catalog](#6-breaking-changes-catalog)
- [7. Risk Management](#7-risk-management)
- [8. Testing & Validation Strategy](#8-testing--validation-strategy)
- [9. Complexity & Effort Assessment](#9-complexity--effort-assessment)
- [10. Source Control Strategy](#10-source-control-strategy)
- [11. Success Criteria](#11-success-criteria)

---

## 1. Executive Summary

### Selected Strategy

**All-At-Once Strategy** — All projects upgraded simultaneously in a single atomic operation.

**Rationale**:
- 2 projects (small solution well under the 5-project threshold)
- Both currently on .NET 9.0 with 🟢 Low upgrade difficulty
- Simple dependency structure (one app project, one test project)
- Only 1 package requires version update; 12 others are compatible as-is
- No security vulnerabilities detected
- No circular dependencies

### Scope

| Metric | Value |
|:---|:---|
| Total Projects | 2 |
| Total NuGet Packages | 13 (1 needs upgrade) |
| Total Code Files | 89 |
| Total Lines of Code | 18,601 |
| API Issues Detected | 2,881 |
| Security Vulnerabilities | 0 |

### Current → Target State

| Project | Current TFM | Target TFM |
|:---|:---|:---|
| `SuleymaniyeCalendar.csproj` | `net9.0-android;net9.0-ios;net9.0-windows10.0.26100.0` | `net10.0-android;net10.0-ios;net10.0-windows10.0.26100.0` |
| `SuleymaniyeCalendar.Tests.csproj` | `net9.0-windows10.0.26100` | `net10.0-windows10.0.26100` |

### Complexity Classification

**Simple** — Fast batch approach (2–3 detail iterations).

- 2 projects, dependency depth of 1
- No high-risk projects, no security vulnerabilities
- The vast majority of flagged "source incompatible" APIs (2,846 of 2,881 issues) reside in auto-generated `obj/` files produced by MAUI SourceGen and CommunityToolkit generators — these will regenerate automatically after recompilation against .NET 10
- 18 binary incompatible issues concentrated in auto-generated `XamlTypeInfo.g.cs` — also regenerated during build
- 14 behavioral changes require runtime testing but no code modifications

### Critical Observations

- **Auto-generated code dominates issue counts**: ~95% of flagged issues are in `obj/` source-generated files that rebuild automatically. Actual user code changes are expected to be minimal.
- **Conditional PropertyGroups**: The main project file has multiple `Condition`-based `PropertyGroup` elements referencing `net9.0-*` TFMs that must be updated to `net10.0-*`.
- **iOS codesign properties**: A `PropertyGroup` conditioned on `'$(TargetFramework)'=='net9.0-ios'` must be updated to `net10.0-ios`.

## 2. Migration Strategy

### Approach: All-At-Once

Both projects are upgraded simultaneously in a single coordinated operation. No intermediate states — the solution transitions directly from .NET 9 to .NET 10.

### Justification

| Criterion | Assessment | Supports All-At-Once? |
|:---|:---|:---:|
| Project count | 2 (well below 5-project threshold) | ✅ |
| Dependency complexity | Depth 1, no cycles | ✅ |
| Codebase size | 18,601 LOC total | ✅ |
| Difficulty rating | Both 🟢 Low | ✅ |
| Package updates | 1 of 13 packages needs update | ✅ |
| Security vulnerabilities | None | ✅ |
| Test coverage | Dedicated test project with MSTest + Moq + FluentAssertions | ✅ |

### Implementation Timeline

#### Phase 0: Preparation
- Verify .NET 10 SDK is installed ✅ (already confirmed)
- No `global.json` update needed (none present in repo)

#### Phase 1: Atomic Upgrade
**Operations** (performed as a single coordinated batch):
- Update all project files to target `net10.0-*` frameworks
- Update all conditional `PropertyGroup` conditions to reference `net10.0-*`
- Update `Microsoft.Extensions.Logging.Debug` from `9.0.10` to `10.0.5`
- Restore dependencies and build the entire solution
- Fix any compilation errors discovered during build

**Deliverable**: Solution builds with 0 errors

#### Phase 2: Test Validation
**Operations**:
- Execute all tests in `SuleymaniyeCalendar.Tests.csproj`
- Address any test failures caused by the upgrade

**Deliverable**: All tests pass

## 3. Detailed Dependency Analysis

### Dependency Graph

```
SuleymaniyeCalendar.Tests.csproj ──depends on──► SuleymaniyeCalendar.csproj
```

- **Leaf node** (no project dependencies): `SuleymaniyeCalendar.csproj` — the main .NET MAUI application
- **Root node** (no dependants): `SuleymaniyeCalendar.Tests.csproj` — MSTest project referencing the main app

### Migration Order

Since the All-At-Once strategy is selected, both projects are upgraded simultaneously. However, the logical dependency order is:

1. `SuleymaniyeCalendar.csproj` (leaf — must build first)
2. `SuleymaniyeCalendar.Tests.csproj` (depends on #1)

### Critical Path

The critical path is straightforward: main app → test project. No circular dependencies, no complex transitive chains. Both projects share `CommunityToolkit.Mvvm 8.4.0` and `Microsoft.Maui.Controls 9.0.120` — both marked compatible and do not require version changes.

### Project Classification

| Project | Type | Role | Dependencies | Dependants |
|:---|:---|:---|:---:|:---:|
| `SuleymaniyeCalendar.csproj` | .NET MAUI App (Android, iOS, Windows) | Main application | 0 projects | 1 |
| `SuleymaniyeCalendar.Tests.csproj` | MSTest Library (Windows) | Unit tests | 1 project | 0 |

## 4. Project-by-Project Plans

### 4.1 SuleymaniyeCalendar.csproj

#### Current State

- **Target Framework**: `net9.0-android;net9.0-ios;net9.0-windows10.0.26100.0` (conditionally adds Windows on Windows OS)
- **Project Kind**: .NET MAUI Application (Exe)
- **Lines of Code**: 14,009
- **Files**: 84 (62 with flagged issues; most in auto-generated `obj/` files)
- **NuGet Packages**: 6 direct references
- **Dependencies**: 0 project references
- **Dependants**: 1 (`SuleymaniyeCalendar.Tests.csproj`)
- **Risk Level**: 🟢 Low

#### Target State

- **Target Framework**: `net10.0-android;net10.0-ios;net10.0-windows10.0.26100.0`
- **Updated Packages**: 1 (`Microsoft.Extensions.Logging.Debug` → `10.0.5`)

#### Migration Steps

##### 1. Update TargetFramework Properties

Update the primary `TargetFrameworks` element:
```xml
<!-- FROM -->
<TargetFrameworks>net9.0-android;net9.0-ios</TargetFrameworks>

<!-- TO -->
<TargetFrameworks>net10.0-android;net10.0-ios</TargetFrameworks>
```

Update the Windows conditional TFM:
```xml
<!-- FROM -->
<TargetFrameworks Condition="$([MSBuild]::IsOSPlatform('windows'))">$(TargetFrameworks);net9.0-windows10.0.26100.0</TargetFrameworks>

<!-- TO -->
<TargetFrameworks Condition="$([MSBuild]::IsOSPlatform('windows'))">$(TargetFrameworks);net10.0-windows10.0.26100.0</TargetFrameworks>
```

##### 2. Update Conditional PropertyGroup Conditions

Each `PropertyGroup` with a `Condition` referencing `net9.0-*` must be updated to `net10.0-*`:

| Condition (current) | Condition (target) | Properties Affected |
|:---|:---|:---|
| `'Release\|net9.0-android\|AnyCPU'` | `'Release\|net10.0-android\|AnyCPU'` | `PublishTrimmed`, `RunAOTCompilation` |
| `'Release\|net9.0-ios\|AnyCPU'` | `'Release\|net10.0-ios\|AnyCPU'` | `UseInterpreter`, `MtouchInterpreter` |
| `'Debug\|net9.0-android\|AnyCPU'` | `'Debug\|net10.0-android\|AnyCPU'` | `EmbedAssembliesIntoApk`, `AndroidUseAapt2`, etc. |
| `'$(TargetFramework)'=='net9.0-ios'` | `'$(TargetFramework)'=='net10.0-ios'` | `CodesignKey`, `CodesignProvision` |

##### 3. Update Package References

| Package | Current Version | Target Version | Reason |
|:---|:---:|:---:|:---|
| `Microsoft.Extensions.Logging.Debug` | `9.0.10` | `10.0.5` | Framework-aligned package upgrade recommended |

All other packages (`CommunityToolkit.Maui 12.3.0`, `CommunityToolkit.Maui.MediaElement 6.1.2`, `CommunityToolkit.Mvvm 8.4.0`, `LocalizationResourceManager.Maui 1.2.2`, `Microsoft.Maui.Controls 9.0.120`) are compatible and require no version change.

##### 4. Expected Breaking Changes

- **Binary Incompatible (18 instances)**: All 18 are in auto-generated `XamlTypeInfo.g.cs` (15) and source-gen files (3). These regenerate during build — no manual code changes expected.
- **Source Incompatible (2,252 instances)**: Distributed across:
  - Auto-generated source-gen files in `obj/` (~2,100 instances) — regenerated automatically
  - User ViewModel files (~150 instances) — `MainViewModel.cs` (301), `CompassViewModel.cs` (96), `SettingsViewModel.cs` (85), `PrayerDetailViewModel.cs` (66). These are flagged because MAUI types they reference have changed assemblies/namespaces internally. They should resolve on recompilation without code changes.
- **Behavioral Changes (14 instances)**: Runtime behavior differences. Require testing but no code modification.

##### 5. Code Modifications

Most flagged issues are in auto-generated code. Expected user-code changes:

- **`TimeSpan.FromSeconds`**: Calls with integer literals (e.g., `TimeSpan.FromSeconds(1)`) will resolve to new `long` overload in .NET 10. No functional change expected but verify precision.
- **MAUI API surface**: Types like `Animation`, `Preferences`, `Application`, `FlowDirection`, `Location` have internal reorganization. No user-facing API changes expected — recompilation should resolve.
- **Clean `obj/` before rebuild**: Delete `obj/` directories to ensure source generators regenerate cleanly against .NET 10.

##### 6. Validation Checklist

- [ ] All `net9.0-*` references replaced with `net10.0-*` in project file
- [ ] All conditional `PropertyGroup` conditions updated
- [ ] `Microsoft.Extensions.Logging.Debug` updated to `10.0.5`
- [ ] Project builds without errors for all target platforms
- [ ] Project builds without warnings (new)
- [ ] No runtime crashes on startup (manual verification)

---

### 4.2 SuleymaniyeCalendar.Tests.csproj

#### Current State

- **Target Framework**: `net9.0-windows10.0.26100`
- **Project Kind**: MSTest Library
- **Lines of Code**: 4,592
- **Files**: 18 (all flagged with issues; all in auto-generated `obj/` files)
- **NuGet Packages**: 8 direct references
- **Dependencies**: 1 project (`SuleymaniyeCalendar.csproj`)
- **Dependants**: 0
- **Risk Level**: 🟢 Low

#### Target State

- **Target Framework**: `net10.0-windows10.0.26100`
- **Updated Packages**: 0 (all compatible)

#### Migration Steps

##### 1. Update TargetFramework

```xml
<!-- FROM -->
<TargetFramework>net9.0-windows10.0.26100</TargetFramework>

<!-- TO -->
<TargetFramework>net10.0-windows10.0.26100</TargetFramework>
```

##### 2. Package References

No package updates required. All 8 packages are compatible with .NET 10:

| Package | Version | Status |
|:---|:---:|:---|
| `Microsoft.Maui.Controls` | `9.0.120` | ✅ Compatible |
| `Microsoft.Maui.Controls.Compatibility` | `9.0.120` | ✅ Compatible |
| `CommunityToolkit.Mvvm` | `8.4.0` | ✅ Compatible |
| `MSTest.TestAdapter` | `4.0.1` | ✅ Compatible |
| `MSTest.TestFramework` | `4.0.1` | ✅ Compatible |
| `Microsoft.NET.Test.Sdk` | `17.12.0` | ✅ Compatible |
| `Moq` | `4.20.72` | ✅ Compatible |
| `FluentAssertions` | `8.7.1` | ✅ Compatible |
| `coverlet.collector` | `6.0.4` | ✅ Compatible |

##### 3. Expected Breaking Changes

- **Source Incompatible (594 instances)**: All in auto-generated `obj/` files from CommunityToolkit.Maui source generators (TextColorTo extensions). These regenerate automatically during build.
- **Binary Incompatible**: 0
- **Behavioral Changes**: 0

##### 4. Validation Checklist

- [ ] `TargetFramework` updated to `net10.0-windows10.0.26100`
- [ ] Project builds without errors
- [ ] All unit tests pass
- [ ] No new test warnings

## 5. Package Update Reference

### Package Requiring Update (1 package, 1 project)

| Package | Current Version | Target Version | Affected Project | Reason |
|:---|:---:|:---:|:---|:---|
| `Microsoft.Extensions.Logging.Debug` | `9.0.10` | `10.0.5` | `SuleymaniyeCalendar.csproj` | Framework-aligned upgrade recommended |

### Compatible Packages (No Update Required)

| Package | Version | Projects |
|:---|:---:|:---|
| `CommunityToolkit.Maui` | `12.3.0` | SuleymaniyeCalendar |
| `CommunityToolkit.Maui.MediaElement` | `6.1.2` | SuleymaniyeCalendar |
| `CommunityToolkit.Mvvm` | `8.4.0` | SuleymaniyeCalendar, Tests |
| `LocalizationResourceManager.Maui` | `1.2.2` | SuleymaniyeCalendar |
| `Microsoft.Maui.Controls` | `9.0.120` | SuleymaniyeCalendar, Tests |
| `Microsoft.Maui.Controls.Compatibility` | `9.0.120` | Tests |
| `Microsoft.NET.Test.Sdk` | `17.12.0` | Tests |
| `MSTest.TestAdapter` | `4.0.1` | Tests |
| `MSTest.TestFramework` | `4.0.1` | Tests |
| `Moq` | `4.20.72` | Tests |
| `FluentAssertions` | `8.7.1` | Tests |
| `coverlet.collector` | `6.0.4` | Tests |

## 6. Breaking Changes Catalog

### Binary Incompatible (18 instances — Auto-generated Only)

| API | Count | Location | Action |
|:---|:---:|:---|:---|
| Various WinUI XamlTypeInfo entries | 15 | `XamlTypeInfo.g.cs` (auto-generated) | Regenerated during build — no manual action |
| Source-gen MAUI types | 3 | `obj/` source-gen files | Regenerated during build — no manual action |

**Impact**: None on user code. Clean rebuild will regenerate all affected files.

### Source Incompatible (2,846 instances — Predominantly Auto-generated)

| Category | Count | Source | Action |
|:---|:---:|:---|:---|
| CommunityToolkit.Maui TextColorTo generators | ~680 | `obj/` source-gen files | Regenerated during build |
| MAUI BindingSourceGen | ~31 | `obj/` source-gen files | Regenerated during build |
| MAUI CodeBehindGenerator (Styles, Brushes, Colors) | ~12 | `obj/` source-gen files | Regenerated during build |
| MAUI API usage in ViewModels | ~550 | User source files | Recompilation expected to resolve (no API removals) |
| `TimeSpan.FromSeconds` overload | 19 | User source files | New `long` overload in .NET 10 — existing `int` calls will bind to it automatically |
| `Preferences`, `Application`, `Location` types | ~400 | User source files | Internal assembly reorganization — transparent to user code |

**Key user-code APIs flagged** (expected to compile cleanly after TFM update):

| API | Occurrences | Files Affected |
|:---|:---:|:---|
| `Microsoft.Maui.Storage.Preferences` (Get/Set) | 156 | Multiple ViewModels |
| `Microsoft.Maui.Controls.Application.Current` | 62 | ViewModels, Services |
| `Microsoft.Maui.FlowDirection` | 107 | RTL support code |
| `Microsoft.Maui.Devices.Sensors.Location` | 83 | Location services |
| `Microsoft.Maui.Controls.Animation` | 180 | UI animation code |
| `Microsoft.Maui.ApplicationModel.MainThread` | 67 | Async UI updates |
| `System.TimeSpan.FromSeconds` | 19 | Performance logging |

### Behavioral Changes (14 instances)

| API | Count | Impact | Verification |
|:---|:---:|:---|:---|
| Various .NET 10 runtime behaviors | 14 | Runtime behavior may differ | Execute full test suite; manual runtime testing |

⚠️ **Behavioral changes cannot be detected at compile time.** Test execution is mandatory to validate no regressions.

## 8. Testing & Validation Strategy

### Phase 1: Build Validation (After Atomic Upgrade)

- Clean `obj/` and `bin/` directories before build to force source generator regeneration
- Build the entire solution targeting all platforms
- Verify 0 compilation errors
- Review and address any new warnings

### Phase 2: Test Execution

**Test Project**: `SuleymaniyeCalendar.Tests.csproj`
- **Framework**: MSTest with Moq and FluentAssertions
- **Target Platform**: `net10.0-windows10.0.26100`
- **Expected**: All existing tests pass without modification

**Test execution steps**:
1. Build the test project
2. Run all tests via `dotnet test` or Visual Studio Test Explorer
3. If failures occur, categorize as:
   - **Build failure**: Address compilation error first (see §6 Breaking Changes)
   - **Behavioral regression**: Compare against the 14 behavioral changes flagged in assessment
   - **Test framework issue**: Verify MSTest/Moq/FluentAssertions compatibility (all marked compatible)

### Validation Checklist (Entire Solution)

- [ ] All projects target `net10.0-*`
- [ ] Solution builds with 0 errors (all platforms)
- [ ] Solution builds with 0 new warnings
- [ ] All unit tests pass
- [ ] `Microsoft.Extensions.Logging.Debug` is at version `10.0.5`
- [ ] No remaining `net9.0` references in any project file
- [ ] No security vulnerabilities in NuGet packages

## 9. Complexity & Effort Assessment

| Project | Complexity | Dependencies | Risk | Rationale |
|:---|:---:|:---|:---:|:---|
| `SuleymaniyeCalendar.csproj` | Low | 0 projects, 6 packages | 🟢 Low | Multi-platform TFM updates + 1 package update + conditional PropertyGroup updates. All API issues in auto-generated code. |
| `SuleymaniyeCalendar.Tests.csproj` | Low | 1 project, 8 packages | 🟢 Low | Single TFM update. No package changes needed. All issues in auto-generated code. |

### Phase Complexity

| Phase | Complexity | Description |
|:---|:---:|:---|
| Phase 0: Preparation | Low | SDK already verified. No `global.json` present. |
| Phase 1: Atomic Upgrade | Low | 2 project files, 5 conditional PropertyGroups, 1 package version. All straightforward text replacements. |
| Phase 2: Test Validation | Low | Single test project with established test patterns. |

### Resource Requirements

- **Skill Level**: Standard .NET developer familiarity with MAUI project structure
- **Parallel Capacity**: N/A — All-At-Once strategy; single atomic operation

## 10. Source Control Strategy

### Branching

- **Source branch**: `master`
- **Upgrade branch**: `upgrade-to-NET10` (already created and checked out)
- **Approach**: Single commit for the entire atomic upgrade

### Commit Strategy

All project file updates, package updates, and any compilation fixes are committed as a **single commit** on the `upgrade-to-NET10` branch. This reflects the atomic nature of the All-At-Once strategy.

**Recommended commit message**:
```
Upgrade solution from .NET 9 to .NET 10 (LTS)

- Update TargetFrameworks in SuleymaniyeCalendar.csproj (net10.0-android, net10.0-ios, net10.0-windows)
- Update TargetFramework in SuleymaniyeCalendar.Tests.csproj (net10.0-windows)
- Update all conditional PropertyGroup conditions from net9.0-* to net10.0-*
- Update Microsoft.Extensions.Logging.Debug from 9.0.10 to 10.0.5
- Fix compilation errors (if any)
```

### Merge Process

1. Verify all tests pass on `upgrade-to-NET10`
2. Create PR from `upgrade-to-NET10` → `master`
3. Review changes (focus on project file TFM updates and any code modifications)
4. Merge via standard team process

### Rollback

If critical issues are discovered post-merge, revert the merge commit. The single-commit strategy makes rollback clean and simple.

## 11. Success Criteria

### Technical Criteria

- [ ] All projects target their `.NET 10` target frameworks
- [ ] `SuleymaniyeCalendar.csproj` targets `net10.0-android;net10.0-ios;net10.0-windows10.0.26100.0`
- [ ] `SuleymaniyeCalendar.Tests.csproj` targets `net10.0-windows10.0.26100`
- [ ] `Microsoft.Extensions.Logging.Debug` updated to `10.0.5`
- [ ] All conditional `PropertyGroup` conditions reference `net10.0-*`
- [ ] Solution builds with 0 errors across all platforms
- [ ] All unit tests pass
- [ ] No NuGet package dependency conflicts
- [ ] No security vulnerabilities in packages

### Quality Criteria

- [ ] No new compiler warnings introduced
- [ ] Test coverage maintained (no tests removed)
- [ ] All existing functionality preserved

### Process Criteria

- [ ] All-At-Once strategy followed (single atomic operation)
- [ ] Changes committed to `upgrade-to-NET10` branch
- [ ] Single commit for entire upgrade
- [ ] Plan fully executed before merge to `master`
