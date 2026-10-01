using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace QuickClaudeWake.Helpers
{
    public static class AppLogger
    {
        private static readonly string LogFilePath;

        static AppLogger()
        {
            try
            {
                string localFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "QuickClaudeWake");
                string logsDir = Path.Combine(localFolder, "logs");
                Directory.CreateDirectory(logsDir);

                long unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                string fileName = $"app_session_{unixTimestamp}.txt";
                LogFilePath = Path.Combine(logsDir, fileName);

                string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] AppLogger initialized at {LogFilePath}";
                File.AppendAllText(LogFilePath, entry + Environment.NewLine);
                Debug.WriteLine(entry);
            }
            catch (Exception ex)
            {
                LogFilePath = string.Empty;
                Debug.WriteLine($"[Logger Error] Failed to initialize logger: {ex.Message}");
            }
        }

        public static void LogAction(string actionName, Dictionary<string, object>? parameters = null)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string entry = $"[{timestamp}] {actionName}";
                if (parameters != null && parameters.Count > 0)
                {
                    entry += " | " + string.Join(", ", parameters);
                }
                if (!string.IsNullOrEmpty(LogFilePath))
                {
                    File.AppendAllText(LogFilePath, entry + Environment.NewLine);
                }
                Debug.WriteLine(entry);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Logger Error] {ex.Message}");
            }
        }

        public static void LogException(Exception ex, string context)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string entry = $"[{timestamp}] EXCEPTION [{context}]: {ex.GetType().FullName}: {ex.Message}{Environment.NewLine}" +
                               $"Stack Trace: {ex.StackTrace}{Environment.NewLine}" +
                               $"Inner Exception: {ex.InnerException?.GetType().FullName}: {ex.InnerException?.Message}{Environment.NewLine}" +
                               $"Inner Stack Trace: {ex.InnerException?.StackTrace}";
                if (!string.IsNullOrEmpty(LogFilePath))
                {
                    File.AppendAllText(LogFilePath, entry + Environment.NewLine);
                }
                Debug.WriteLine($"[AppLogger] Exception in {context}: {ex.Message}");
            }
            catch (Exception logEx)
            {
                Debug.WriteLine($"[Logger Error] Failed to log exception: {logEx.Message}");
            }
        }
    }
}
