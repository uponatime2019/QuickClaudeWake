using Microsoft.UI.Xaml;
using System;
using System.IO;
using Windows.UI.ViewManagement;

namespace QuickClaudeWake.Services
{
    public static class ThemeHelper
    {
        private static string ThemeFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QuickClaudeWake",
            "theme.txt");

        public static ElementTheme GetSystemTheme()
        {
            try
            {
                var app = Application.Current;
                if (app != null)
                {
                    if (app.RequestedTheme == ApplicationTheme.Light)
                    {
                        return ElementTheme.Light;
                    }
                    if (app.RequestedTheme == ApplicationTheme.Dark)
                    {
                        return ElementTheme.Dark;
                    }
                }
            }
            catch { }

            try
            {
                var uiSettings = new UISettings();
                var bg = uiSettings.GetColorValue(UIColorType.Background);
                var luminance = 0.299 * bg.R + 0.587 * bg.G + 0.114 * bg.B;
                return luminance < 128 ? ElementTheme.Dark : ElementTheme.Light;
            }
            catch { }

            return ElementTheme.Dark;
        }

        public static ElementTheme LoadSavedTheme()
        {
            try
            {
                string path = ThemeFilePath;
                if (File.Exists(path))
                {
                    string s = File.ReadAllText(path).Trim();
                    if (string.Equals(s, "Light", StringComparison.OrdinalIgnoreCase))
                    {
                        return ElementTheme.Light;
                    }
                    if (string.Equals(s, "Dark", StringComparison.OrdinalIgnoreCase))
                    {
                        return ElementTheme.Dark;
                    }
                }
            }
            catch { }
            return ElementTheme.Default;
        }

        public static void SaveTheme(ElementTheme theme)
        {
            try
            {
                string path = ThemeFilePath;
                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                string val = theme switch
                {
                    ElementTheme.Light => "Light",
                    ElementTheme.Dark => "Dark",
                    _ => "System",
                };
                File.WriteAllText(path, val);
            }
            catch { }
        }
    }
}
