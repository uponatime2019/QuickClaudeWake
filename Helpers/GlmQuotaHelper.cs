using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using QuickClaudeWake.Models;

namespace QuickClaudeWake.Helpers
{
    public static class GlmQuotaHelper
    {
        private const string QuotaUrl = "https://api.z.ai/api/monitor/usage/quota/limit";
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };

        public static async Task<(int? percentage, TimeSpan? timeToReset, DateTime? nextResetTime)> Get5HourQuotaPercentAsync(string? bearerToken)
        {
            if (string.IsNullOrWhiteSpace(bearerToken))
            {
                return (null, null, null);
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, QuotaUrl);
                request.Headers.Add("Authorization", $"Bearer {bearerToken.Trim()}");
                request.Headers.Add("accept", "application/json, text/plain, */*");

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return (null, null, null);

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("data", out var data) && data.TryGetProperty("limits", out var limits) && limits.ValueKind == JsonValueKind.Array)
                {
                    foreach (var limit in limits.EnumerateArray())
                    {
                        if (limit.TryGetProperty("type", out var typeEl) && typeEl.GetString() == "TOKENS_LIMIT" &&
                            limit.TryGetProperty("unit", out var unitEl) && unitEl.GetInt32() == 3)
                        {
                            if (limit.TryGetProperty("nextResetTime", out var resetEl) &&
                                limit.TryGetProperty("percentage", out var pctEl))
                            {
                                long resetEpoch = resetEl.GetInt64();
                                var resetTime = DateTimeOffset.FromUnixTimeMilliseconds(resetEpoch).LocalDateTime;
                                var timeToReset = resetTime - DateTime.Now;
                                if (timeToReset < TimeSpan.Zero) timeToReset = TimeSpan.Zero;
                                return (pctEl.GetInt32(), timeToReset, resetTime);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogException(ex, "GlmQuotaHelper.Get5HourQuotaPercentAsync");
            }
            return (null, null, null);
        }
    }
}
