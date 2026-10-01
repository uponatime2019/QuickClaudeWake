using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using QuickClaudeWake.Helpers;

namespace QuickClaudeWake.Models
{
    public class QuickClaudeWakeSettings
    {
        public double LookbackDays { get; set; } = 1.0;

        public bool ContinueLoopByDefault { get; set; } = true;

        public bool AutoStartScheduler { get; set; } = false;

        public bool IsRunAtStartup { get; set; } = false;

        public string GlmBearerToken { get; set; } = "";

        public string TelegramBotToken { get; set; } = "";

        public string TelegramChatId { get; set; } = "";

        public string CustomProjectsRoot { get; set; } = "";

        public string CustomWorkingDir { get; set; } = "";

        public List<string> SkippedSessionIds { get; set; } = new List<string>();

        public int WindowWidth { get; set; } = 940;

        public int WindowHeight { get; set; } = 720;

        private static string GetSettingsFilePath()
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "QuickClaudeWake");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "settings.json");
        }

        public static QuickClaudeWakeSettings Load()
        {
            try
            {
                string path = GetSettingsFilePath();
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var settings = JsonSerializer.Deserialize<QuickClaudeWakeSettings>(json);
                    if (settings != null)
                    {
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogException(ex, "QuickClaudeWakeSettings.Load");
            }
            return new QuickClaudeWakeSettings();
        }

        public static Task<QuickClaudeWakeSettings> LoadAsync()
        {
            return Task.FromResult(Load());
        }

        public void Save()
        {
            try
            {
                string path = GetSettingsFilePath();
                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                AppLogger.LogException(ex, "QuickClaudeWakeSettings.Save");
            }
        }

        public Task SaveAsync()
        {
            Save();
            return Task.CompletedTask;
        }
    }
}
