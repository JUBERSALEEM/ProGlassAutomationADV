using System;
using System.Linq;
using System.Windows;

namespace ProGlassAutomation.Services
{
    public enum AppTheme
    {
        Light,
        Dark
    }

    public static class ThemeManager
    {
        private static AppTheme _currentTheme = AppTheme.Light;

        public static AppTheme CurrentTheme => _currentTheme;

        public static event EventHandler ThemeChanged;

        public static void ApplyTheme(AppTheme theme)
        {
            _currentTheme = theme;

            string themeUri = theme == AppTheme.Dark
                ? "pack://application:,,,/Themes/DarkTheme.xaml"
                : "pack://application:,,,/Themes/LightTheme.xaml";

            var newDict = new ResourceDictionary
            {
                Source = new Uri(themeUri, UriKind.Absolute)
            };

            var appDicts = Application.Current.Resources.MergedDictionaries;

            // Remove existing theme dictionaries
            var existing = appDicts.FirstOrDefault(d =>
                d.Source != null &&
                (d.Source.OriginalString.Contains("LightTheme.xaml") ||
                 d.Source.OriginalString.Contains("DarkTheme.xaml")));

            if (existing != null)
                appDicts.Remove(existing);

            appDicts.Add(newDict);

            ThemeChanged?.Invoke(null, EventArgs.Empty);
        }

        public static void ToggleTheme()
        {
            ApplyTheme(_currentTheme == AppTheme.Light ? AppTheme.Dark : AppTheme.Light);
        }
    }
}