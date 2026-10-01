using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using QuickClaudeWake.Models;

namespace QuickClaudeWake.Helpers
{
    public static class AgySessionHelper
    {
        public static string BrainRoot => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gemini", "antigravity-cli", "brain");

        public static List<AgySessionInfo> GetRecentSessions(int max = 7, int daysLookback = 5)
        {
            var result = new List<AgySessionInfo>();
            if (!Directory.Exists(BrainRoot)) return result;

            try
            {
                var files = Directory.GetFiles(BrainRoot, "transcript.jsonl", SearchOption.AllDirectories);
                var cutoff = DateTime.Now.AddDays(-daysLookback);

                var fileInfos = files.Select(f =>
                {
                    try { return new FileInfo(f); } catch { return null; }
                })
                .Where(fi => fi != null && fi.LastWriteTime >= cutoff)
                .OrderByDescending(fi => fi!.LastWriteTime);

                foreach (var fi in fileInfos)
                {
                    if (fi == null) continue;
                    try
                    {
                        string conversationId = fi.Directory?.Parent?.Parent?.Name ?? "";
                        if (string.IsNullOrEmpty(conversationId) || conversationId.Equals("brain", StringComparison.OrdinalIgnoreCase))
                        {
                            var parts = fi.FullName.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
                            int brainIdx = Array.FindIndex(parts, p => p.Equals("brain", StringComparison.OrdinalIgnoreCase));
                            if (brainIdx >= 0 && brainIdx + 1 < parts.Length)
                            {
                                conversationId = parts[brainIdx + 1];
                            }
                        }

                        if (string.IsNullOrEmpty(conversationId)) continue;

                        string title = "";
                        string workspace = "";

                        using (var fs = new FileStream(fi.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (var sr = new StreamReader(fs))
                        {
                            string? line;
                            int linesRead = 0;
                            while ((line = sr.ReadLine()) != null && linesRead < 50)
                            {
                                linesRead++;
                                if (line.Contains("\"type\":\"USER_INPUT\"") || line.Contains("USER_REQUEST"))
                                {
                                    int userReqStart = line.IndexOf("<USER_REQUEST>");
                                    if (userReqStart >= 0)
                                    {
                                        int contentStart = userReqStart + "<USER_REQUEST>".Length;
                                        int userReqEnd = line.IndexOf("</USER_REQUEST>", contentStart);
                                        if (userReqEnd > contentStart)
                                        {
                                            title = line.Substring(contentStart, userReqEnd - contentStart).Trim();
                                        }
                                        else
                                        {
                                            title = line.Substring(contentStart, Math.Min(120, line.Length - contentStart)).Trim();
                                        }
                                    }

                                    if (string.IsNullOrEmpty(title))
                                    {
                                        int cIdx = line.IndexOf("\"content\":\"");
                                        if (cIdx >= 0)
                                        {
                                            int start = cIdx + 11;
                                            int end = line.IndexOf("\"", start);
                                            if (end > start)
                                            {
                                                title = line.Substring(start, Math.Min(100, end - start));
                                            }
                                        }
                                    }

                                    var wsMatch = Regex.Match(line, @"([A-Za-z]:\\[^ \r\n\t<""\\]+(?:\\[^ \r\n\t<""\\]+)*)");
                                    if (wsMatch.Success && Directory.Exists(wsMatch.Value))
                                    {
                                        workspace = wsMatch.Value;
                                    }

                                    break;
                                }
                            }
                        }

                        if (string.IsNullOrWhiteSpace(title))
                        {
                            title = $"AGY Session {conversationId.Substring(0, Math.Min(8, conversationId.Length))}";
                        }
                        else
                        {
                            title = title.Replace("\\n", " ").Replace("\\r", " ").Replace("\\\"", "\"").Trim();
                            while (title.Contains("  ")) title = title.Replace("  ", " ");
                        }

                        result.Add(new AgySessionInfo
                        {
                            ConversationId = conversationId,
                            Title = title,
                            WorkspacePath = workspace,
                            LastModified = fi.LastWriteTime
                        });

                        if (result.Count >= max) break;
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogException(ex, "AgySessionHelper.GetRecentSessions");
            }

            return result.OrderByDescending(s => s.LastModified).ToList();
        }

        public static void LaunchAgyTerminal(string? workspacePath, string conversationId)
        {
            string workDir = ClaudeSessionHelper.ResolveWorkingDir(workspacePath);
            string agyCmd = $"agy --conversation {conversationId} --dangerously-skip-permissions";

            if (ClaudeSessionHelper.HasWindowsTerminal())
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "wt.exe",
                        Arguments = $"-d \"{workDir}\" {agyCmd}",
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                    return;
                }
                catch (Exception ex)
                {
                    AppLogger.LogException(ex, "LaunchAgyTerminal.WT");
                }
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/k \"cd /d \"{workDir}\" && {agyCmd}\"",
                    WorkingDirectory = workDir,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                AppLogger.LogException(ex, "LaunchAgyTerminal.Cmd");
            }
        }
    }
}
