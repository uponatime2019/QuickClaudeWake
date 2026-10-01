using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using QuickClaudeWake.Models;

namespace QuickClaudeWake.Helpers
{
    public static class ClaudeSessionHelper
    {
        public static string HistoryPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "history.jsonl");

        public static string GetProjectsRoot(string? customRoot = null)
        {
            if (!string.IsNullOrWhiteSpace(customRoot) && Directory.Exists(customRoot))
            {
                return customRoot;
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects");
        }

        public static bool HasWindowsTerminal()
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo
                {
                    FileName = "where.exe",
                    Arguments = "wt.exe",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                });
                if (p != null)
                {
                    p.WaitForExit(2000);
                    return p.ExitCode == 0;
                }
            }
            catch { }
            return false;
        }

        public static string ResolveWorkingDir(string? sessionProjectPath, string? customFallback = null)
        {
            if (!string.IsNullOrWhiteSpace(sessionProjectPath) && Directory.Exists(sessionProjectPath))
            {
                return sessionProjectPath;
            }
            if (!string.IsNullOrWhiteSpace(customFallback) && Directory.Exists(customFallback))
            {
                return customFallback;
            }

            string userProjects = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Projects");
            if (Directory.Exists(userProjects)) return userProjects;

            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        public static void LaunchSession(string projectPath, string sessionId, string extraArgs = "--effort max --permission-mode auto", string? prompt = "continue", bool inBackground = true, string? customFallbackDir = null)
        {
            string workDir = ResolveWorkingDir(projectPath, customFallbackDir);
            string promptArg = string.IsNullOrWhiteSpace(prompt) ? "" : $" -p \"{prompt}\"";
            string extra = string.IsNullOrWhiteSpace(extraArgs) ? "" : " " + extraArgs.Trim();
            string command = $"claude --resume {sessionId}{promptArg}{extra}";

            if (inBackground)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c {command}",
                        WorkingDirectory = workDir,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        UseShellExecute = false
                    };
                    Process.Start(psi);
                    return;
                }
                catch (Exception ex)
                {
                    AppLogger.LogException(ex, "LaunchSession.Background");
                }
            }

            // Interactive terminal mode
            string interactiveCmd = $"claude --resume {sessionId}{extra}";
            try
            {
                if (HasWindowsTerminal())
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "wt.exe",
                        UseShellExecute = true
                    };
                    psi.Arguments = string.IsNullOrEmpty(workDir)
                        ? interactiveCmd
                        : $"-d \"{workDir}\" {interactiveCmd}";
                    Process.Start(psi);
                    return;
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogException(ex, "LaunchSession.Terminal");
            }

            // Fallback: cmd window
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    UseShellExecute = true,
                    WorkingDirectory = workDir,
                    Arguments = $"/k {interactiveCmd}"
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                AppLogger.LogException(ex, "LaunchSession.CmdFallback");
            }
        }

        public static bool IsSystemOrSkillText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return true;
            if (text.Contains("- claude-hud:", StringComparison.OrdinalIgnoreCase)) return true;
            if (text.Contains("- dataviz:", StringComparison.OrdinalIgnoreCase)) return true;
            if (text.Contains("Use this skill whenever", StringComparison.OrdinalIgnoreCase)) return true;
            if (text.Contains("Configure HUD display options", StringComparison.OrdinalIgnoreCase)) return true;
            if (text.Contains("Available skills", StringComparison.OrdinalIgnoreCase)) return true;
            if (text.Contains("Skills are folders", StringComparison.OrdinalIgnoreCase)) return true;
            if (text.Contains("<local-command-caveat>", StringComparison.OrdinalIgnoreCase)) return true;
            if (text.Contains("<command-name>", StringComparison.OrdinalIgnoreCase)) return true;
            if (text.Contains("<command-message>", StringComparison.OrdinalIgnoreCase)) return true;
            if (text.Contains("<system_message>", StringComparison.OrdinalIgnoreCase)) return true;
            if (text.Contains("<system-reminder>", StringComparison.OrdinalIgnoreCase)) return true;
            if (text.Contains("<SYSTEM_MESSAGE>", StringComparison.OrdinalIgnoreCase)) return true;
            if (text.Contains("<context_summary>", StringComparison.OrdinalIgnoreCase)) return true;
            if (text.Contains("<identity>", StringComparison.OrdinalIgnoreCase)) return true;
            if (text.Contains("statusline", StringComparison.OrdinalIgnoreCase)) return true;
            if (text.Contains("usline", StringComparison.OrdinalIgnoreCase)) return true;
            if (text.Contains("You are Antigravity", StringComparison.OrdinalIgnoreCase)) return true;
            if (text.Contains("You are Claude", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static string CleanSessionText(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";

            // 1. Remove ANSI escape sequences
            s = Regex.Replace(s, @"(?:\u001b|\\u001[bB]|\\x1[bB]|\\033)\[[0-9;?]*[a-zA-Z]", "");
            s = Regex.Replace(s, @"(?:\u001b|\\u001[bB]|\\x1[bB]|\\033)\][^\u0007\u001b\r\n]*(?:\u0007|\\u0007|\u001b|\\u001[bB]\\]?)", "");
            s = Regex.Replace(s, @"\\[uU]001[bB]", "");
            s = Regex.Replace(s, @"\\[uU]000[0-9a-fA-F]", "");
            s = Regex.Replace(s, @"\[\?[0-9;]+[a-zA-Z]", "");
            s = Regex.Replace(s, @"\[[0-9;]*[mHJK]", "");

            // 2. Remove console paths
            s = Regex.Replace(s, @".*?(?:conhost\.exe|cmd\.exe|powershell\.exe)[^\r\n]*", "", RegexOptions.IgnoreCase);

            // 3. Remove 429 API rate limit error blocks
            s = Regex.Replace(s, @"API Error:\s*Request rejected\s*\(429\)[^\r\n]*", "", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\[\d+\]\[Usage limit reached[^\r\n]*", "", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"Usage limit reached for \d+ hour[^\r\n]*", "", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"Your limit will reset at [0-9\-:\s]+(?:\[[a-zA-Z0-9]+\])?", "", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"limit will reset at [0-9\-:\s]+", "", RegexOptions.IgnoreCase);

            // 4. Standard unescaping
            s = s.Replace("\\n", "\n")
                 .Replace("\\r", "")
                 .Replace("\\t", "  ")
                 .Replace("\\\"", "\"")
                 .Replace("\\\\", "\\");

            // 5. Line-by-line filtering
            var lines = s.Split(new[] { '\r', '\n' }, StringSplitOptions.None)
                         .Select(l => l.Trim())
                         .Where(l => !string.IsNullOrWhiteSpace(l) &&
                                     !l.Contains("conhost.exe", StringComparison.OrdinalIgnoreCase) &&
                                     !l.StartsWith("API Error", StringComparison.OrdinalIgnoreCase) &&
                                     !l.Contains("Usage limit reached", StringComparison.OrdinalIgnoreCase) &&
                                     !l.StartsWith("<local-command-caveat>", StringComparison.OrdinalIgnoreCase) &&
                                     !l.StartsWith("<command-name>", StringComparison.OrdinalIgnoreCase) &&
                                     !l.StartsWith("<command-message>", StringComparison.OrdinalIgnoreCase) &&
                                     !l.StartsWith("<system", StringComparison.OrdinalIgnoreCase) &&
                                     !l.StartsWith("</system", StringComparison.OrdinalIgnoreCase) &&
                                     !l.StartsWith("statusline", StringComparison.OrdinalIgnoreCase) &&
                                     !l.StartsWith("usline", StringComparison.OrdinalIgnoreCase) &&
                                     !l.StartsWith("- claude-hud:", StringComparison.OrdinalIgnoreCase) &&
                                     !l.StartsWith("- dataviz:", StringComparison.OrdinalIgnoreCase) &&
                                     !l.Contains("Use this skill whenever", StringComparison.OrdinalIgnoreCase) &&
                                     !l.Contains("Configure HUD display options", StringComparison.OrdinalIgnoreCase) &&
                                     !l.Contains("Available skills:", StringComparison.OrdinalIgnoreCase) &&
                                     !l.Contains("You are Antigravity", StringComparison.OrdinalIgnoreCase) &&
                                     !l.Contains("You are Claude", StringComparison.OrdinalIgnoreCase))
                         .ToList();

            s = string.Join("\n", lines).Trim();
            while (s.Contains("\n\n\n")) s = s.Replace("\n\n\n", "\n\n");
            return s;
        }

        public static (DateTime? resetTime, string resetTimeString) GetLatestGlobalRateLimit(double lookbackDays = 2.0, string? customProjectsRoot = null)
        {
            string root = GetProjectsRoot(customProjectsRoot);
            if (!Directory.Exists(root)) return (null, "");

            try
            {
                var files = Directory.GetFiles(root, "*.jsonl", SearchOption.AllDirectories);
                var cutoff = DateTime.Now.AddDays(-lookbackDays);
                var now = DateTime.Now;

                var candidateFiles = files.Select(f =>
                {
                    try { return new FileInfo(f); } catch { return null; }
                })
                .Where(fi => fi != null && fi.LastWriteTime >= cutoff)
                .OrderByDescending(fi => fi!.LastWriteTime);

                DateTime? bestReset = null;
                string bestResetStr = "";

                foreach (var fi in candidateFiles)
                {
                    if (fi == null) continue;
                    var info = GetSessionInfoFromFile(fi.FullName);
                    if (info != null && info.HasRateLimit && info.ResetTime.HasValue && info.ResetTime.Value > now)
                    {
                        if (!bestReset.HasValue || info.ResetTime.Value > bestReset.Value)
                        {
                            bestReset = info.ResetTime.Value;
                            bestResetStr = info.ResetTimeString;
                        }
                    }
                }

                return (bestReset, bestResetStr);
            }
            catch { }

            return (null, "");
        }

        public static List<ClaudeSessionInfo> GetSessionsNeedingContinue(double lookbackDays = 1.0, bool onlyInCooldown = false, string? customProjectsRoot = null)
        {
            var result = new List<ClaudeSessionInfo>();
            string root = GetProjectsRoot(customProjectsRoot);
            if (!Directory.Exists(root)) return result;

            try
            {
                var files = Directory.GetFiles(root, "*.jsonl", SearchOption.AllDirectories);
                var cutoff = DateTime.Now.AddDays(-lookbackDays);
                var now = DateTime.Now;

                var fileList = new List<FileInfo>();
                foreach (var file in files)
                {
                    try
                    {
                        var fi = new FileInfo(file);
                        if (fi.LastWriteTime >= cutoff)
                        {
                            fileList.Add(fi);
                        }
                    }
                    catch { }
                }

                var sorted = fileList.OrderByDescending(f => f.LastWriteTime);

                foreach (var fi in sorted)
                {
                    var info = GetSessionInfoFromFile(fi.FullName);
                    if (info == null) continue;

                    if (onlyInCooldown)
                    {
                        if (info.HasRateLimit && info.ResetTime.HasValue && info.ResetTime.Value > now)
                        {
                            result.Add(info);
                        }
                    }
                    else
                    {
                        result.Add(info);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogException(ex, "GetSessionsNeedingContinue");
            }

            return result;
        }

        public static List<ClaudeSessionInfo> GetRecentSessions(int max = 7, int daysLookback = 5, string? customProjectsRoot = null)
        {
            var result = new List<ClaudeSessionInfo>();
            string root = GetProjectsRoot(customProjectsRoot);
            if (!Directory.Exists(root)) return result;

            try
            {
                var files = Directory.GetFiles(root, "*.jsonl", SearchOption.AllDirectories);
                var cutoff = DateTime.Now.AddDays(-daysLookback);

                var fileInfos = files.Select(f =>
                {
                    try { return new FileInfo(f); } catch { return null; }
                })
                .Where(fi => fi != null && fi.LastWriteTime >= cutoff)
                .OrderByDescending(fi => fi!.LastWriteTime)
                .Take(max);

                foreach (var fi in fileInfos)
                {
                    if (fi == null) continue;
                    var info = GetSessionInfoFromFile(fi.FullName);
                    if (info != null)
                    {
                        result.Add(info);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogException(ex, "GetRecentSessions");
            }

            return result;
        }

        public static ClaudeSessionInfo? GetSessionInfoFromFile(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath)) return null;

            try
            {
                string sessionId = Path.GetFileNameWithoutExtension(fullPath);
                string projectPath = "";
                string title = "";
                string lastPrompt = "";
                string lastMessage = "";
                bool hasRateLimit = false;
                DateTime? resetTime = null;
                string resetTimeString = "";
                bool isResumed = false;
                bool isTurnCompleted = false;

                var fi = new FileInfo(fullPath);
                DateTime lastModified = fi.LastWriteTime;

                var tailLines = new List<string>();
                using (var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs))
                {
                    string? line;
                    int linesRead = 0;
                    while ((line = sr.ReadLine()) != null)
                    {
                        linesRead++;
                        if (linesRead < 15)
                        {
                            if (string.IsNullOrEmpty(projectPath))
                            {
                                var mPath = Regex.Match(line, @"[""']projectPath[""']\s*:\s*[""']([^""']+)[""']");
                                if (mPath.Success) projectPath = mPath.Groups[1].Value;
                            }
                            if (string.IsNullOrEmpty(title))
                            {
                                var mTitle = Regex.Match(line, @"[""']aiTitle[""']\s*:\s*[""']([^""']+)[""']");
                                if (mTitle.Success) title = mTitle.Groups[1].Value;
                            }
                        }

                        tailLines.Add(line);
                        if (tailLines.Count > 40)
                        {
                            tailLines.RemoveAt(0);
                        }
                    }
                }

                // Analyze tail lines for stop reasons and rate limit messages
                for (int i = tailLines.Count - 1; i >= 0; i--)
                {
                    string tl = tailLines[i];
                    if (string.IsNullOrWhiteSpace(tl)) continue;

                    if (tl.Contains("\"stop_reason\":\"end_turn\""))
                    {
                        isTurnCompleted = true;
                    }

                    // Look for rate limit markers
                    if (tl.Contains("429") || tl.Contains("Usage limit reached") || tl.Contains("Your limit will reset at"))
                    {
                        hasRateLimit = true;
                        var mReset = Regex.Match(tl, @"Your limit will reset at\s*([0-9]{4}-[0-9]{2}-[0-9]{2}\s+[0-9]{2}:[0-9]{2}:[0-9]{2})");
                        if (!mReset.Success)
                        {
                            mReset = Regex.Match(tl, @"([0-9]{4}-[0-9]{2}-[0-9]{2}\s+[0-9]{2}:[0-9]{2}:[0-9]{2})");
                        }
                        if (mReset.Success)
                        {
                            if (DateTime.TryParse(mReset.Groups[1].Value, out DateTime parsed))
                            {
                                resetTime = parsed;
                                resetTimeString = mReset.Groups[1].Value;
                            }
                        }
                    }

                    // Look for last user prompt
                    if (string.IsNullOrEmpty(lastPrompt))
                    {
                        var mUser = Regex.Match(tl, @"[""']role[""']\s*:\s*[""']user[""']\s*,\s*[""']content[""']\s*:\s*[""']([^""']+)[""']");
                        if (mUser.Success && !IsSystemOrSkillText(mUser.Groups[1].Value))
                        {
                            lastPrompt = CleanSessionText(mUser.Groups[1].Value);
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(title))
                {
                    title = !string.IsNullOrWhiteSpace(lastPrompt) ? lastPrompt : $"Session {sessionId.Substring(0, Math.Min(8, sessionId.Length))}";
                }

                var sessionInfo = new ClaudeSessionInfo
                {
                    SessionId = sessionId,
                    ProjectPath = projectPath,
                    FullPath = fullPath,
                    Title = title,
                    LastPrompt = lastPrompt,
                    LastMessage = lastMessage,
                    LastModified = lastModified,
                    HasRateLimit = hasRateLimit,
                    ResetTime = resetTime,
                    ResetTimeString = resetTimeString,
                    IsTurnCompleted = isTurnCompleted,
                    IsResumed = isResumed
                };

                sessionInfo.UpdateCountdown(DateTime.Now);
                return sessionInfo;
            }
            catch (Exception ex)
            {
                AppLogger.LogException(ex, "GetSessionInfoFromFile");
                return null;
            }
        }
    }
}
