# 🎨 Phase 15: Golden Component System - Quick Reference

## Component Styles Cheat Sheet

### 1. Switches 🔘

```xaml
<!-- Standard golden switch -->
<Switch Style="{StaticResource GoldenSwitch}"
        IsToggled="{Binding IsEnabled}" />

<!-- Premium golden switch (enhanced) -->
<Switch Style="{StaticResource PremiumGoldenSwitch}"
        IsToggled="{Binding IsPremium}" />
```

**Colors:**
- **On:** Golden track (GoldPure / GoldOrange) + White thumb
- **Off:** Gray track + Gray thumb

---

### 2. Sliders 📏

```xaml
<!-- Standard golden slider -->
<Slider Style="{StaticResource GoldenSlider}"
        Minimum="0" Maximum="100"
        Value="{Binding Volume}" />

<!-- Premium golden slider (enhanced) -->
<Slider Style="{StaticResource PremiumGoldenSlider}"
        Minimum="12" Maximum="24"
        Value="{Binding FontSize}" />
```

**Colors:**
- **Minimum Track:** Golden (GoldPure / GoldOrange)
- **Maximum Track:** Gray (#E0E0E0 / #424242)
- **Thumb:** Golden (GoldPure / GoldLight)

---

### 3. Text Entries ✏️

```xaml
<!-- Standard text input -->
<Entry Style="{StaticResource GoldenEntry}"
       Placeholder="Enter text..."
       Text="{Binding UserName}" />

<!-- Outlined entry (wrapped in Border) -->
<Border Style="{StaticResource OutlinedGoldenEntryBorder}">
    <Entry Placeholder="Enter text..."
           Text="{Binding UserName}"
           BackgroundColor="Transparent" />
</Border>
```

**Behavior:**
- **Normal:** Standard surface background
- **Focused:** Golden cream background (#FFFFFBF5)
- **Outlined Hover:** Golden border (GoldPure) + 2px thickness

---

### 4. Multi-line Text 📝

```xaml
<!-- Multi-line text editor -->
<Editor Style="{StaticResource GoldenEditor}"
        Placeholder="Enter description..."
        Text="{Binding Description}"
        HeightRequest="150" />
```

**Features:**
- Auto-grows with text (AutoSize="TextChanges")
- Minimum 120px height
- Golden cream background on focus

---

### 5. Pickers (Dropdowns) 📋

```xaml
<!-- Dropdown selector -->
<Picker Style="{StaticResource GoldenPicker}"
        Title="Select Language"
        ItemsSource="{Binding Languages}"
        SelectedItem="{Binding SelectedLanguage}" />
```

**Colors:**
- **Title:** Golden (GoldPure) - stands out!
- **Text:** OnSurfaceColor (adaptive)

---

### 6. Steppers ➕➖

```xaml
<!-- Numeric increment/decrement -->
<Stepper Style="{StaticResource GoldenStepper}"
         Minimum="0" Maximum="10"
         Value="{Binding Quantity}"
         Increment="1" />
```

---

### 7. CheckBoxes ☑️

```xaml
<!-- Checkbox with golden check -->
<CheckBox Style="{StaticResource GoldenCheckBox}"
          IsChecked="{Binding AcceptTerms}" />

<!-- With label -->
<HorizontalStackLayout Spacing="8">
    <CheckBox Style="{StaticResource GoldenCheckBox}"
              IsChecked="{Binding AcceptTerms}" />
    <Label Text="I accept the terms" />
</HorizontalStackLayout>
```

**Colors:**
- **Checked:** Golden checkmark (GoldPure)
- **Unchecked:** Gray outline

---

### 8. RadioButtons ⚪

```xaml
<!-- Radio button group -->
<VerticalStackLayout Spacing="8">
    <RadioButton Style="{StaticResource GoldenRadioButton}"
                 GroupName="ThemeGroup"
                 Content="Light Theme"
                 Value="1"
                 IsChecked="{Binding LightChecked}" />
    
    <RadioButton Style="{StaticResource GoldenRadioButton}"
                 GroupName="ThemeGroup"
                 Content="Dark Theme"
                 Value="0"
                 IsChecked="{Binding DarkChecked}" />
    
    <RadioButton Style="{StaticResource GoldenRadioButton}"
                 GroupName="ThemeGroup"
                 Content="System"
                 Value="2"
                 IsChecked="{Binding SystemChecked}" />
</VerticalStackLayout>
```

**Colors:**
- **Checked:** Golden text (GoldPure)
- **Unchecked:** OnSurfaceColor text

---

### 9. SearchBars 🔍

```xaml
<!-- Search input -->
<SearchBar Style="{StaticResource GoldenSearchBar}"
           Placeholder="Search..."
           Text="{Binding SearchQuery}"
           SearchCommand="{Binding SearchCommand}" />
```

**Colors:**
- **Cancel Button:** Golden (GoldPure)
- **Text:** OnSurfaceColor

---

### 10. Date & Time Pickers 📅⏰

```xaml
<!-- Date picker -->
<DatePicker Style="{StaticResource GoldenDatePicker}"
            Date="{Binding SelectedDate}"
            MinimumDate="2024-01-01"
            MaximumDate="2025-12-31" />

<!-- Time picker -->
<TimePicker Style="{StaticResource GoldenTimePicker}"
            Time="{Binding SelectedTime}" />
```

---

### 11. Progress Indicators ⏳

```xaml
<!-- Linear progress bar -->
<ProgressBar Style="{StaticResource GoldenProgressBar}"
             Progress="{Binding DownloadProgress}" />

<!-- Circular loading spinner -->
<ActivityIndicator Style="{StaticResource GoldenActivityIndicator}"
                   IsRunning="{Binding IsLoading}" />
```

**Colors:**
- **Progress/Spinner:** Golden (GoldPure)

---

## Card Hierarchy (Phase 14)

### Quick Card Selection Guide

```xaml
<!-- Past/Background content (minimal weight) -->
<Border Style="{StaticResource FlatContentCard}">
    <Label Text="Past prayer or background info" />
</Border>

<!-- Regular content (standard weight) -->
<Border Style="{StaticResource StandardCard}">
    <Label Text="Standard content card" />
</Border>

<!-- Important content (elevated weight) -->
<Border Style="{StaticResource ElevatedPrimaryCard}">
    <Label Text="Featured setting or important section" />
</Border>

<!-- Critical content (intense weight) -->
<Border Style="{StaticResource IntensePrimaryCard}">
    <Label Text="Banner, alert, or critical info" />
</Border>

<!-- Hero content (maximum weight) ⭐ -->
<Border Style="{StaticResource HeroPrimaryCard}">
    <Label Text="Current prayer or focal point - USE SPARINGLY!" />
</Border>
```

---

## Complete Example: Settings Section

```xaml
<VerticalStackLayout Spacing="16">
    
    <!-- Featured Setting (Elevated) -->
    <Border Style="{StaticResource ElevatedPrimaryCard}" Padding="16,12">
        <Grid ColumnDefinitions="Auto,*,Auto" ColumnSpacing="12">
            <Label Grid.Column="0"
                   FontFamily="{StaticResource IconFontFamily}"
                   Text="&#xf0ac;"
                   FontSize="26"
                   TextColor="{StaticResource GoldMedium}" />
            
            <Label Grid.Column="1"
                   Text="Language"
                   Style="{StaticResource HeadlineMediumStyle}" />
            
            <Label Grid.Column="2"
                   Text="›"
                   FontSize="24"
                   TextColor="{StaticResource OnSurfaceVariantLight}" />
        </Grid>
        <Border.GestureRecognizers>
            <TapGestureRecognizer Command="{Binding SelectLanguageCommand}" />
        </Border.GestureRecognizers>
    </Border>
    
    <!-- Standard Setting (Switch) -->
    <Border Style="{StaticResource StandardCard}" Padding="16,12">
        <Grid ColumnDefinitions="Auto,*,Auto" ColumnSpacing="12">
            <Label Grid.Column="0"
                   FontFamily="{StaticResource IconFontFamily}"
                   Text="&#xf0f3;"
                   FontSize="26"
                   TextColor="{StaticResource GoldMedium}" />
            
            <Label Grid.Column="1"
                   Text="Notifications"
                   Style="{StaticResource HeadlineMediumStyle}" />
            
            <Switch Grid.Column="2"
                    Style="{StaticResource GoldenSwitch}"
                    IsToggled="{Binding NotificationsEnabled}" />
        </Grid>
    </Border>
    
    <!-- Standard Setting (Slider) -->
    <Border Style="{StaticResource StandardCard}" Padding="16,12">
        <VerticalStackLayout Spacing="12">
            <Grid ColumnDefinitions="Auto,*,Auto" ColumnSpacing="12">
                <Label Grid.Column="0"
                       FontFamily="{StaticResource IconFontFamily}"
                       Text="&#xf031;"
                       FontSize="26"
                       TextColor="{StaticResource GoldMedium}" />
                
                <Label Grid.Column="1"
                       Text="Font Size"
                       Style="{StaticResource HeadlineMediumStyle}" />
                
                <Border Grid.Column="2"
                        Background="{StaticResource UpcomingPrayerBrush}"
                        StrokeShape="RoundRectangle 10"
                        Padding="10,4">
                    <Label Text="{Binding FontSize, StringFormat='{0:F0}'}"
                           TextColor="{StaticResource GoldDeep}"
                           FontAttributes="Bold" />
                </Border>
            </Grid>
            
            <Slider Style="{StaticResource PremiumGoldenSlider}"
                    Minimum="12" Maximum="24"
                    Value="{Binding FontSize}" />
        </VerticalStackLayout>
    </Border>
    
</VerticalStackLayout>
```

---

## Design Principles

### 1. Golden Theme Rules ✨
- **Always:** Use golden colors for active/selected states
- **Never:** Use golden for disabled states (use opacity instead)
- **Consistency:** Golden = GoldPure (#FFD700) or GoldOrange

### 2. Touch Target Sizes 👆
- **Minimum:** 44px height for all interactive components
- **Comfortable:** 48px for primary controls
- **Padding:** Adequate spacing around touch areas

### 3. Font Sizing 📏
- **Always:** Use DynamicResource for font sizes
- **Never:** Hardcode font sizes in px
- **Scale:** Components adapt to user's font preference

### 4. Light/Dark Mode 🌓
- **Always:** Use AppThemeBinding for colors
- **Never:** Hardcode colors for specific themes
- **Adapt:** Components look great in both modes

### 5. Visual Hierarchy 📊
```
FlatContent   ░       Past/Background
Standard      ░░      Regular content
Elevated      ░░░     Important content
Intense       ░░░░    Critical content
Hero          ░░░░░   Focal point (1 max!) ⭐
```

---

## Common Patterns

### Settings Row with Switch
```xaml
<Border Style="{StaticResource StandardCard}" Padding="16,12">
    <Grid ColumnDefinitions="Auto,*,Auto" ColumnSpacing="12">
        <Label Grid.Column="0" ... /> <!-- Icon -->
        <Label Grid.Column="1" ... /> <!-- Title -->
        <Switch Grid.Column="2" Style="{StaticResource GoldenSwitch}" ... />
    </Grid>
</Border>
```

### Settings Row with Slider
```xaml
<Border Style="{StaticResource StandardCard}" Padding="16,12">
    <VerticalStackLayout Spacing="12">
        <Grid ColumnDefinitions="Auto,*,Auto" ColumnSpacing="12">
            <Label Grid.Column="0" ... /> <!-- Icon -->
            <Label Grid.Column="1" ... /> <!-- Title -->
            <Label Grid.Column="2" ... /> <!-- Value badge -->
        </Grid>
        <Slider Style="{StaticResource PremiumGoldenSlider}" ... />
    </VerticalStackLayout>
</Border>
```

### Form with Entries
```xaml
<Border Style="{StaticResource StandardCard}" Padding="16,12">
    <VerticalStackLayout Spacing="12">
        <Label Text="Login Form" Style="{StaticResource TitleMediumStyle}" />
        
        <Entry Style="{StaticResource GoldenEntry}"
               Placeholder="Username"
               Text="{Binding UserName}" />
        
        <Entry Style="{StaticResource GoldenEntry}"
               Placeholder="Password"
               IsPassword="True"
               Text="{Binding Password}" />
        
        <Button Text="Login"
                Style="{StaticResource GlassButtonPrimary}"
                Command="{Binding LoginCommand}" />
    </VerticalStackLayout>
</Border>
```

---

## Component Comparison

| Component | Standard Style | Premium/Enhanced Style |
|-----------|---------------|------------------------|
| Switch | GoldenSwitch | PremiumGoldenSwitch |
| Slider | GoldenSlider | PremiumGoldenSlider |
| Entry | GoldenEntry | OutlinedGoldenEntryBorder |
| Card | StandardCard | ElevatedPrimaryCard |
| Button | GlassButtonSecondary | GlassButtonPrimary |

---

## Quick Tips 💡

1. **Use ElevatedPrimaryCard for featured settings**
   - Language, Theme, Font Size

2. **Use StandardCard for regular settings**
   - Location, Notifications, Service options

3. **Use PremiumGoldenSlider for important ranges**
   - Font size, volume controls

4. **Use GoldenSwitch for all toggles**
   - Consistent golden theme

5. **Use HeroPrimaryCard sparingly**
   - Maximum 1 per screen (focal point only)

6. **Wrap Entry in OutlinedGoldenEntryBorder for emphasis**
   - Important forms, search fields

7. **Always set MinimumHeightRequest="44"**
   - Touch-friendly mobile controls

8. **Use GoldenActivityIndicator for loading**
   - Consistent with golden theme

---

## Resources

- **Full Guide:** `PHASE_15_IMPLEMENTATION_COMPLETE.md`
- **Card Reference:** `PHASE_14_COMPREHENSIVE_CARD_SYSTEM.md`
- **Button Reference:** `PHASE_13_ENHANCED_QUICK_REFERENCE.md`
- **Live Showcase:** AboutPage in app

---

**Phase 15 Golden Component System - Ready to Use!** 🎨✨
