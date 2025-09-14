# Android Button CornerRadius Workaround

## Problem
Button and ImageButton `CornerRadius` property does not work correctly on Android platform in .NET MAUI, including version 9.0. This is a confirmed bug tracked by Microsoft.

### Affected Controls
- `Button` - CornerRadius ignored on Android
- `ImageButton` - CornerRadius ignored on Android

### GitHub Issues
- [#23854](https://github.com/dotnet/maui/issues/23854) - ImageButton CornerRadius not being applied on Android
- [#24939](https://github.com/dotnet/maui/issues/24939) - Android ImageButton CornerRadius regression from Xamarin.Forms
- Status: Verified, Triaged, Assigned to .NET 9 Servicing milestone

## Official Microsoft Solution
Microsoft recommends using **Border wrapper** as the official workaround for this Android limitation.

### Implementation Pattern

Instead of:
```xml
<!-- This won't work on Android -->
<Button Text="My Button" 
        CornerRadius="25"
        BackgroundColor="Blue" />
```

Use this pattern:
```xml
<!-- Android-compatible Button with CornerRadius -->
<Border BackgroundColor="Blue"
        Stroke="Transparent"
        StrokeThickness="0"
        Padding="0">
    <Border.StrokeShape>
        <RoundRectangle CornerRadius="25" />
    </Border.StrokeShape>
    <Button Text="My Button"
            BackgroundColor="Transparent"
            TextColor="White" />
</Border>
```

### Key Points
1. **Border provides the corner radius** - Use `RoundRectangle` with desired `CornerRadius`
2. **Border provides the background color** - Set the color on Border, not Button
3. **Button becomes transparent** - Set `BackgroundColor="Transparent"` on Button
4. **Full functionality preserved** - Command binding, text, icons, etc. all work normally
5. **Cross-platform compatible** - Works identically on iOS, Windows, and Android

## Current Implementation
Our `MainPage.xaml` monthly calendar button uses this pattern:

```xml
<!-- Monthly Calendar Button with Border (Android CornerRadius Workaround) -->
<Border 
    BackgroundColor="{AppThemeBinding Light={StaticResource PrimaryColor}, Dark={StaticResource Primary80}}"
    Stroke="Transparent"
    StrokeThickness="0"
    Margin="16,0"
    HorizontalOptions="Center"
    Padding="0">
    <Border.StrokeShape>
        <RoundRectangle CornerRadius="25" />
    </Border.StrokeShape>
    <Button
        Text="{localization:Translate AylikTakvim}"
        Command="{Binding GoToMonthCommand}"
        BackgroundColor="Transparent"
        TextColor="{AppThemeBinding Light={StaticResource OnPrimaryColor}, Dark={StaticResource OnPrimaryColor}}"
        FontFamily="OpenSansRegular"
        FontSize="{DynamicResource BodyFontSize}"
        FontAttributes="Bold"
        Padding="24,16"
        MinimumHeightRequest="56"
        HorizontalOptions="Center"
        IsEnabled="{Binding IsNotBusy}">
        <Button.ImageSource>
            <FontImageSource
                FontFamily="{StaticResource IconFontFamily}"
                Glyph="&#xf073;"
                Size="{DynamicResource IconMediumSize}"
                Color="{AppThemeBinding Light={StaticResource OnPrimaryColor}, Dark={StaticResource OnPrimaryColor}}" />
        </Button.ImageSource>
    </Button>
</Border>
```

## Alternative Solutions
1. **Custom Renderers** - Create platform-specific button renderers (more complex)
2. **Third-party libraries** - Use Syncfusion or Telerik buttons (adds dependencies)
3. **Wait for fix** - Microsoft is working on a fix for .NET 9 Servicing release

## Recommendation
Continue using the Border wrapper pattern as it:
- ✅ Works reliably on all platforms
- ✅ Maintains full Button functionality  
- ✅ Is the officially recommended approach
- ✅ Requires no additional dependencies
- ✅ Will continue working even after Microsoft fixes the underlying issue

## Testing Results
- **iOS**: Border wrapper works perfectly
- **Windows**: Border wrapper works perfectly
- **Android**: Border wrapper works perfectly (solves the CornerRadius issue)
- **Performance**: No measurable impact compared to direct Button styling

---
*Last updated: January 2025*
*Related: .NET MAUI 9.0 Android Button CornerRadius limitations*
