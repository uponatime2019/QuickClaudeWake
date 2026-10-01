using Microsoft.Win32;
using System;
using System.IO;

namespace QuickClaudeWake.Services
{
    /// <summary>
    /// Registry-based Run-on-Startup via HKCU ...\Run.
    /// </summary>
    public class StartupManager
    {
        private const string RunKeyName = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private readonly string _appName;

        public StartupManager(string appName = "QuickClaudeWake")
        {
            _appName = appName;
        }

        public string AppName => _appName;

        public bool IsAutoStartEnabled
        {
            get
            {
                try
                {
                    using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyName, false);
                    if (key != null)
                    {
                        string? value = key.GetValue(_appName) as string;
                        return !string.IsNullOrEmpty(value);
                    }
                }
                catch (Exception) { }
                return false;
            }
        }

        public void SetAutoStart(bool enable)
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyName, true);
                if (key != null)
                {
                    if (enable)
                    {
                        string exePath = GetExecutablePath();
                        if (!string.IsNullOrEmpty(exePath))
                        {
                            key.SetValue(_appName, $"\"{exePath}\" --startup");
                        }
                    }
                    else
                    {
                        key.DeleteValue(_appName, false);
                    }
                }
            }
            catch (Exception) { }
        }

        private string GetExecutablePath()
        {
            try
            {
                string? exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath))
                    return System.Reflection.Assembly.GetExecutingAssembly().Location;
                return Path.GetFullPath(exePath);
            }
            catch (Exception) { }
            return string.Empty;
        }
    }
}
