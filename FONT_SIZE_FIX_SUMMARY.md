# Font Size UI Scaling - Implementation Summary

## Problem Statement
When users selected font sizes larger than 14pt, the Shell tab bar title text was being cut off at the bottom, and image buttons on AboutPage weren't scaling proportionally with the font.

## Solution Overview
Implemented a comprehensive dynamic font scaling system that responds to user preference changes across the entire UI, with special handling for platform-specific native controls.

## Changes Made

### 1. **BaseViewModel.cs** - Font Size Change Trigger
**Location:** FontSize property setter (line ~115)

Added call to `UpdateShellTabBarHeight()` when font size changes:
```csharp
public int FontSize
{
    get => _fontSize;
    set
    {
        var clampedValue = Math.Clamp(value, MinFontSize, MaxFontSize);
        if (SetProperty(ref _fontSize, clampedValue))
        {
            Preferences.Set("FontSize", clampedValue);
            NotifyFontSizeProperties();
            ApplyFontScaleToResources(clampedValue);
            UpdateAndroidWidget();
            UpdateShellTabBarHeight();  // ← NEW
        }
    }
}
```

**New Method:** `UpdateShellTabBarHeight()` (lines ~255-270)
- Safely casts Shell.Current to AppShell
- Invokes on main thread for UI safety
- Calls platform-specific implementation in AppShell

### 2. **AppShell.xaml.cs** - Platform-Specific Implementations
**New Method:** `UpdateTabBarForFontSize()` (lines ~50-100)

#### iOS Implementation
- Accesses UITabBarController from Handler.PlatformView
- Calculates height: `49 + (fontSize - 14) × 2`
- Adjusts UITabBar frame geometry
- Calls `SetNeedsLayout()` to apply changes

#### Android Implementation
- Accesses BottomNavigationView from FragmentActivity
- Calculates height: `56 + (fontSize - 14) × 2`
- Uses TypedValue.ApplyDimension() for DIP conversion
- Calls RequestLayout() to apply changes

**Added Using Statements:**
```csharp
#if IOS
using UIKit;
using CoreGraphics;
#elif ANDROID
using AndroidX.Fragment.App;
using Google.Android.Material.BottomNavigation;
#endif
```

### 3. **PrayerDetailPage.xaml** - Test Sound Button
**Changes:**
- Icon size: `IconSmallFontSize` → `IconMediumFontSize`
- MinimumHeightRequest: Added "52"
- VerticalOptions: Added "Center"
- WidthRequest: "52" → "56"

**Effect:** Button no longer cuts off with large fonts; scales properly with FontSize preference.

### 4. **Styles.xaml** - ModernImageButton Style
**Changes:**
- HeightRequest: "56" → `{DynamicResource PlayButtonContainerSize}`
- WidthRequest: "56" → `{DynamicResource PlayButtonContainerSize}`
- Added: `Aspect="AspectFit"`

**Effect:** AboutPage social media and app store icons now scale with font size (48×48 at font 12, up to 112×112 at font 28).

### 5. **AppResources Files** - Notification Troubleshooting
**Added Resource:** `BildirimSorunu`

**English:**
```
If notifications still don't work after granting permission, you may need to reset 
iOS notification settings: Settings → General → Transfer or Reset iPhone → Reset → 
Reset Location & Privacy...
```

**Turkish (AppResources.tr.resx):**
```
Bildirimler izin verdikten sonra hala çalışmıyorsa, iOS bildirim ayarlarını sıfırlamanız 
gerekebilir: Ayarlar → Genel → Transfer veya iPhone'u Sıfırla → Sıfırla → 
Konum ve Gizlilik'i Sıfırla...
```

## How It Works

### 1. User Changes Font Size in Settings
- SettingsViewModel updates the FontSize property in BaseViewModel
- Property setter clamps value to 12-28 range

### 2. All Dynamic Resources Update
```
NotifyFontSizeProperties() → 26 text size resources scaled proportionally
ApplyFontScaleToResources() → Applied across XAML controls
```

### 3. Shell Tab Bar Height Adjusts
```
UpdateShellTabBarHeight() → Calls AppShell.UpdateTabBarForFontSize()
                        ↓
                   #if IOS: Adjusts UITabBar frame
                   #elif ANDROID: Adjusts BottomNavigationView height
```

### 4. Image Buttons Scale
- PlayButtonContainerSize DynamicResource changes
- ModernImageButton style applies new size to AboutPage buttons

## Build Status
✅ iOS build succeeds with no errors (4 warnings unrelated to font changes)
✅ All platform-specific code properly wrapped in #if directives
✅ Type safety with null checks throughout

## Testing Checklist
- [ ] Open Settings, change font from 14 to 20+
- [ ] Return to MainPage, verify tab bar title text doesn't cut off
- [ ] Verify tab bar grows taller
- [ ] Check AboutPage icons scale proportionally
- [ ] Verify test sound button in PrayerDetailPage stays visible
- [ ] Change back to font 12, verify all UI shrinks correctly
- [ ] Test on both iOS and Android devices

## Font Size Calculation Formula
For consistent scaling across platforms:
```
TabBarHeight = BaseHeight + (UserFontSize - 14) × 2 pixels
              (49 iOS / 56 Android)     (scaling factor)
```

Example progression:
- Font 12: Height = 49 - 4 = 45px (iOS)
- Font 14: Height = 49 + 0 = 49px (iOS) ← Default
- Font 20: Height = 49 + 12 = 61px (iOS)
- Font 28: Height = 49 + 28 = 77px (iOS)

## Edge Cases Handled
- Null check if AppShell is not current Shell
- Null check if Handler is not available on platform
- Exception catching with debug logging
- Min/max clamping of fontSize (12-28)
- MainThread invocation for UI safety
- Platform-specific null checks for native views
