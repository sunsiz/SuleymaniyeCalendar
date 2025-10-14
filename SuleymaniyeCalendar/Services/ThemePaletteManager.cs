using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace SuleymaniyeCalendar.Services
{
    /// <summary>
    /// Simple runtime palette switcher. It swaps between default Colors.xaml and Colors.MTB.xaml
    /// while preserving existing resource keys, so the UI updates without code changes.
    /// </summary>
    public static class ThemePaletteManager
    {
        private const string PrefKey = "UseMtbPalette";

        public static bool UseMtbPalette
        {
            get => Preferences.Get(PrefKey, false);
            set
            {
                Preferences.Set(PrefKey, value);
                ApplyCurrentPalette();
            }
        }

        public static void ApplyCurrentPalette()
        {
            var app = Application.Current;
            if (app?.Resources is not ResourceDictionary root)
                return;

            // Find the merged dictionaries block in App.xaml
            // We replace or inject the MTB dictionary after the base Colors.xaml
            // so it can override selected color keys.
            const string mtbPath = "Resources/Styles/Colors.MTB.xaml";

            // Remove existing MTB dict if present
            ResourceDictionary existingMtb = null;
            foreach (var dict in root.MergedDictionaries)
            {
                if (dict.Source?.OriginalString?.EndsWith("Colors.MTB.xaml", StringComparison.OrdinalIgnoreCase) == true)
                {
                    existingMtb = dict;
                    break;
                }
            }
            if (existingMtb != null)
            {
                root.MergedDictionaries.Remove(existingMtb);
            }

            if (UseMtbPalette)
            {
                try
                {
                    var mtbDict = new ResourceDictionary { Source = new Uri(mtbPath, UriKind.Relative) };
                    // Ensure it comes right after base Colors.xaml if possible
                    root.MergedDictionaries.Add(mtbDict);
                }
                catch (Exception)
                {
                    // If loading fails, silently continue with base palette
                }
            }
        }
    }
}
