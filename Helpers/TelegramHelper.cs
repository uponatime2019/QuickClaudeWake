using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using QuickClaudeWake.Models;

namespace QuickClaudeWake.Helpers
{
    public static class TelegramHelper
    {
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        public static async Task<bool> SendAlertAsync(string message, QuickClaudeWakeSettings? settings = null)
        {
            settings ??= QuickClaudeWakeSettings.Load();
            if (string.IsNullOrWhiteSpace(settings.TelegramBotToken) || string.IsNullOrWhiteSpace(settings.TelegramChatId))
            {
                return false;
            }

            try
            {
                string url = $"https://api.telegram.org/bot{settings.TelegramBotToken}/sendMessage";
                var payload = new
                {
                    chat_id = settings.TelegramChatId,
                    text = message,
                    parse_mode = "HTML"
                };

                string json = JsonSerializer.Serialize(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(url, content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                AppLogger.LogException(ex, "TelegramHelper.SendAlertAsync");
                return false;
            }
        }
    }
}
