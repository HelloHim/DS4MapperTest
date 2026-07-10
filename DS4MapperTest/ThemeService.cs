using System;
using System.Linq;
using System.Windows;

namespace DS4MapperTest
{
    public enum ThemeMode
    {
        Dark,
        Light
    }

    public static class ThemeService
    {
        public const string DEFAULT_THEME_MODE = "Dark";

        private const string DarkColorsSource = "Views/Styles/JsmccThemeDark.xaml";
        private const string LightColorsSource = "Views/Styles/JsmccThemeLight.xaml";
        private const string DarkSkinSource = "pack://application:,,,/HandyControl;component/Themes/SkinDark.xaml";
        private const string LightSkinSource = "pack://application:,,,/HandyControl;component/Themes/SkinDefault.xaml";

        public static ThemeMode CurrentTheme { get; private set; } = ThemeMode.Dark;

        public static event EventHandler<ThemeMode> ThemeChanged;

        public static void Initialize(AppGlobalData appGlobal)
        {
            ThemeMode startupTheme = ParseThemeMode(appGlobal.appSettings?.ThemeMode);
            ApplyTheme(startupTheme, appGlobal, persist: false);
        }

        public static void ToggleTheme(AppGlobalData appGlobal)
        {
            ThemeMode next = CurrentTheme == ThemeMode.Dark ? ThemeMode.Light : ThemeMode.Dark;
            ApplyTheme(next, appGlobal, persist: true);
        }

        public static void ApplyTheme(ThemeMode mode, AppGlobalData appGlobal, bool persist)
        {
            string colorSource = mode == ThemeMode.Light ? LightColorsSource : DarkColorsSource;
            string skinSource = mode == ThemeMode.Light ? LightSkinSource : DarkSkinSource;

            var dictionaries = Application.Current.Resources.MergedDictionaries;
            var existingSkin = dictionaries.FirstOrDefault(d =>
                d.Source != null &&
                (d.Source.OriginalString.EndsWith("/Themes/SkinDark.xaml", StringComparison.OrdinalIgnoreCase) ||
                 d.Source.OriginalString.EndsWith("/Themes/SkinDefault.xaml", StringComparison.OrdinalIgnoreCase) ||
                 d.Source.OriginalString.EndsWith(";component/Themes/SkinDark.xaml", StringComparison.OrdinalIgnoreCase) ||
                 d.Source.OriginalString.EndsWith(";component/Themes/SkinDefault.xaml", StringComparison.OrdinalIgnoreCase)));

            var existing = dictionaries.FirstOrDefault(d =>
                d.Source != null &&
                (d.Source.OriginalString.EndsWith(DarkColorsSource, StringComparison.OrdinalIgnoreCase) ||
                 d.Source.OriginalString.EndsWith(LightColorsSource, StringComparison.OrdinalIgnoreCase)));

            var newSkinDictionary = new ResourceDictionary { Source = new Uri(skinSource, UriKind.Absolute) };
            var newDictionary = new ResourceDictionary { Source = new Uri(colorSource, UriKind.Relative) };

            if (existingSkin != null)
            {
                int index = dictionaries.IndexOf(existingSkin);
                dictionaries[index] = newSkinDictionary;
            }
            else
            {
                dictionaries.Insert(0, newSkinDictionary);
            }

            if (existing != null)
            {
                int index = dictionaries.IndexOf(existing);
                dictionaries[index] = newDictionary;
            }
            else
            {
                dictionaries.Insert(0, newDictionary);
            }

            CurrentTheme = mode;

            if (persist && appGlobal?.appSettings != null)
            {
                appGlobal.appSettings.ThemeMode = mode.ToString();
                appGlobal.SaveAppSettings();
            }

            ThemeChanged?.Invoke(null, mode);
        }

        private static ThemeMode ParseThemeMode(string value)
        {
            return Enum.TryParse(value, true, out ThemeMode parsed) ? parsed : ThemeMode.Dark;
        }
    }
}
