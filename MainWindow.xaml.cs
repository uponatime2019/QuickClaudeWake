using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using QuickClaudeWake.Helpers;
using QuickClaudeWake.Models;
using QuickClaudeWake.Services;

namespace QuickClaudeWake
{
    public partial class MainWindow : Window
    {
        private static readonly HashSet<string> _removedSessionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _scheduledSessionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private ObservableCollection<ClaudeSessionInfo> _sessions = new ObservableCollection<ClaudeSessionInfo>();
        private ObservableCollection<ClaudeSessionInfo> _recentSessions = new ObservableCollection<ClaudeSessionInfo>();

        private DispatcherTimer? _ticker;
        private bool _isSchedulerRunning = false;
        private bool _hasInitialScanned = false;
        private DateTime? _activeGlobalResetTime = null;
        private string _activeGlobalResetTimeStr = "";
        private int? _glmQuotaPercentage = null;
        private DateTime? _glmNextResetTime = null;
        private QuickClaudeWakeSettings _settings;
        private int _tickCounter = 0;

        public MainWindow()
        {
            InitializeComponent();
            _settings = QuickClaudeWakeSettings.Load();
            WindowHelper.Configure(this, "Quick Claude Wake", _settings.WindowWidth, _settings.WindowHeight);
            Closed += MainWindow_Closed;
            RootGrid.Loaded += MainWindow_Loaded;

            LoadSettingsIntoUI();

            AppLogger.LogAction("[WINDOW_INIT] Quick Claude Wake window opened.");
            InitTimerAndScan();
        }

        private void LoadSettingsIntoUI()
        {
            try
            {
                chkRunAtStartup.IsChecked = new StartupManager().IsAutoStartEnabled;
                chkLoopDefault.IsChecked = _settings.ContinueLoopByDefault;
                chkContinueLoop.IsChecked = _settings.ContinueLoopByDefault;
                txtCustomWorkingDir.Text = _settings.CustomWorkingDir;
                txtGlmBearerToken.Password = _settings.GlmBearerToken;
                txtTelegramBotToken.Password = _settings.TelegramBotToken;
                txtTelegramChatId.Text = _settings.TelegramChatId;
            }
            catch (Exception ex)
            {
                AppLogger.LogException(ex, "LoadSettingsIntoUI");
            }
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _settings.ContinueLoopByDefault = chkLoopDefault.IsChecked == true;
                _settings.CustomWorkingDir = txtCustomWorkingDir.Text.Trim();
                _settings.GlmBearerToken = txtGlmBearerToken.Password.Trim();
                _settings.TelegramBotToken = txtTelegramBotToken.Password.Trim();
                _settings.TelegramChatId = txtTelegramChatId.Text.Trim();
                _settings.IsRunAtStartup = chkRunAtStartup.IsChecked == true;

                new StartupManager().SetAutoStart(_settings.IsRunAtStartup);
                _settings.Save();

                txtSaveStatus.Text = "✅ Settings saved successfully!";
                _ = FetchLiveGlmQuotaAsync();
            }
            catch (Exception ex)
            {
                txtSaveStatus.Text = "❌ Error: " + ex.Message;
                AppLogger.LogException(ex, "SaveSettings_Click");
            }
        }

        private async Task FetchLiveGlmQuotaAsync()
        {
            if (string.IsNullOrWhiteSpace(_settings.GlmBearerToken))
            {
                _glmQuotaPercentage = null;
                _glmNextResetTime = null;
                return;
            }

            try
            {
                var (percentage, _, nextResetTime) = await GlmQuotaHelper.Get5HourQuotaPercentAsync(_settings.GlmBearerToken);
                if (percentage.HasValue)
                {
                    _glmQuotaPercentage = percentage.Value;
                    if (nextResetTime.HasValue && nextResetTime.Value > DateTime.Now)
                    {
                        _glmNextResetTime = nextResetTime.Value;
                        _activeGlobalResetTime = nextResetTime.Value;
                        _activeGlobalResetTimeStr = nextResetTime.Value.ToString("yyyy-MM-dd HH:mm:ss");
                    }
                    else if (percentage.Value == 0)
                    {
                        _glmNextResetTime = null;
                        _activeGlobalResetTime = null;
                        _activeGlobalResetTimeStr = "";
                    }
                }
            }
            catch { }
        }

        private void InitTimerAndScan()
        {
            try
            {
                if (_ticker == null)
                {
                    _ticker = new DispatcherTimer();
                    _ticker.Interval = TimeSpan.FromSeconds(1);
                    _ticker.Tick += Ticker_Tick;
                    _ticker.Start();
                }

                _ = FetchLiveGlmQuotaAsync();
                ScanSessions();
                ScanRecentSessions();

                if (_settings.AutoStartScheduler)
                {
                    StartScheduler();
                }
            }
            catch (Exception ex)
            {
                txtStatus.Text = "Error scanning sessions: " + ex.Message;
                AppLogger.LogException(ex, "InitTimerAndScan");
            }
        }

        private void MainWindow_Closed(object sender, WindowEventArgs e)
        {
            if (_ticker != null)
            {
                _ticker.Stop();
                _ticker = null;
            }
            _isSchedulerRunning = false;
            AppLogger.LogAction("[WINDOW_CLOSE] Quick Claude Wake window closed.");
        }

        private double GetLookbackDays()
        {
            if (cmbLookbackInterval?.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                if (double.TryParse(item.Tag.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double val))
                {
                    return val;
                }
            }
            return 1.0;
        }

        private void CmbLookbackInterval_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_hasInitialScanned)
            {
                AppLogger.LogAction($"[CONFIG] Lookback interval changed to {GetLookbackDays()} day(s). SchedulerRunning={_isSchedulerRunning}");
                ScanSessions();
                ScanRecentSessions();
            }
        }

        private void ScanSessions()
        {
            try
            {
                double lookback = GetLookbackDays();
                var list = ClaudeSessionHelper.GetSessionsNeedingContinue(lookbackDays: lookback, onlyInCooldown: false, customProjectsRoot: _settings.CustomProjectsRoot);
                list = list.Where(s => s.HasRateLimit && !s.IsResumed).ToList();

                // Filter out sessions that were removed by the user in memory
                list = list.Where(s => !_removedSessionIds.Contains(s.SessionId) &&
                                       (string.IsNullOrEmpty(s.FullPath) || !_removedSessionIds.Contains(s.FullPath))).ToList();

                var now = DateTime.Now;
                Dictionary<string, ClaudeSessionInfo>? oldById = null;
                DateTime? oldGlobalReset = _activeGlobalResetTime;

                if (_isSchedulerRunning && _sessions.Count > 0)
                {
                    oldById = _sessions
                        .Where(s => !string.IsNullOrEmpty(s.SessionId))
                        .GroupBy(s => s.SessionId, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
                }

                if (oldById != null)
                {
                    foreach (var fresh in list)
                    {
                        if (!string.IsNullOrEmpty(fresh.SessionId) && oldById.TryGetValue(fresh.SessionId, out var old))
                        {
                            fresh.IsResumed = old.IsResumed;
                            fresh.WaitingForNextReset = old.WaitingForNextReset;
                            fresh.LastWokenTime = old.LastWokenTime;
                            fresh.LastWokenResetTimeString = old.LastWokenResetTimeString;

                            if (old.WaitingForNextReset && !old.IsResumed)
                            {
                                if (!fresh.HasRateLimit || !fresh.ResetTime.HasValue || fresh.ResetTime.Value <= now)
                                {
                                    fresh.IsResumed = false;
                                    fresh.HasRateLimit = false;
                                    fresh.ResetTime = null;
                                    fresh.ResetTimeString = "";
                                    fresh.WaitingForNextReset = true;
                                }
                            }
                            if (old.IsResumed && !fresh.HasRateLimit)
                            {
                                fresh.IsResumed = true;
                            }
                        }
                    }

                    foreach (var scheduledId in _scheduledSessionIds.ToList())
                    {
                        bool stillListed = list.Any(s => string.Equals(s.SessionId, scheduledId, StringComparison.OrdinalIgnoreCase));
                        if (!stillListed && oldById.TryGetValue(scheduledId, out var oldScheduled))
                        {
                            if (!oldScheduled.IsResumed || oldScheduled.WaitingForNextReset)
                            {
                                list.Add(oldScheduled);
                            }
                        }
                    }
                }

                var activeLimitSession = list.Where(s => s.ResetTime.HasValue && s.ResetTime.Value > now)
                                             .OrderByDescending(s => s.ResetTime!.Value)
                                             .FirstOrDefault();

                if (activeLimitSession?.ResetTime != null)
                {
                    var candidateReset = activeLimitSession.ResetTime.Value;
                    var candidateResetStr = activeLimitSession.ResetTimeString;
                    if (_isSchedulerRunning && oldGlobalReset.HasValue && oldGlobalReset.Value > now)
                    {
                        if (candidateReset <= oldGlobalReset.Value)
                        {
                            candidateReset = oldGlobalReset.Value;
                            candidateResetStr = _activeGlobalResetTimeStr;
                        }
                    }
                    _activeGlobalResetTime = candidateReset;
                    _activeGlobalResetTimeStr = candidateResetStr;

                    foreach (var s in list)
                    {
                        if (s.WaitingForNextReset && !s.IsResumed) continue;
                        if (!s.ResetTime.HasValue || s.ResetTime.Value <= now)
                        {
                            s.ResetTime = candidateReset;
                            s.ResetTimeString = candidateResetStr;
                            s.HasRateLimit = true;
                        }
                    }
                }
                else
                {
                    var (globalReset, globalResetStr) = ClaudeSessionHelper.GetLatestGlobalRateLimit(lookbackDays: Math.Max(lookback, 2.0), customProjectsRoot: _settings.CustomProjectsRoot);
                    if (globalReset.HasValue && globalReset.Value > now)
                    {
                        if (!(_isSchedulerRunning && oldGlobalReset.HasValue && oldGlobalReset.Value > now && globalReset.Value <= oldGlobalReset.Value))
                        {
                            _activeGlobalResetTime = globalReset.Value;
                            _activeGlobalResetTimeStr = globalResetStr;
                        }
                    }
                }

                if (_isSchedulerRunning && oldById != null)
                {
                    var freshById = list
                        .Where(s => !string.IsNullOrEmpty(s.SessionId))
                        .GroupBy(s => s.SessionId, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                    foreach (var existing in _sessions.ToList())
                    {
                        if (string.IsNullOrEmpty(existing.SessionId)) continue;
                        if (!freshById.ContainsKey(existing.SessionId) && !_scheduledSessionIds.Contains(existing.SessionId))
                        {
                            _sessions.Remove(existing);
                        }
                    }

                    foreach (var item in list)
                    {
                        if (!string.IsNullOrEmpty(item.SessionId) && oldById.TryGetValue(item.SessionId, out var oldObj))
                        {
                            if (ReferenceEquals(item, oldObj))
                            {
                                if (!_sessions.Contains(oldObj)) _sessions.Add(oldObj);
                                continue;
                            }
                        }

                        var match = _sessions.FirstOrDefault(s => string.Equals(s.SessionId, item.SessionId, StringComparison.OrdinalIgnoreCase));
                        if (match == null)
                        {
                            _sessions.Add(item);
                        }
                        else if (!ReferenceEquals(match, item))
                        {
                            int idx = _sessions.IndexOf(match);
                            _sessions[idx] = item;
                        }
                    }
                }
                else
                {
                    _sessions.Clear();
                    foreach (var item in list)
                    {
                        _sessions.Add(item);
                    }
                }

                lvSessions.ItemsSource = _sessions;
                _hasInitialScanned = true;

                int readyCount = _sessions.Count(s => !s.ResetTime.HasValue || now >= s.ResetTime.Value);
                int cooldownCount = _sessions.Count - readyCount;

                if (_sessions.Count > 0)
                {
                    if (cooldownCount > 0 && _activeGlobalResetTime.HasValue && _activeGlobalResetTime.Value > now)
                    {
                        txtSubStatus.Text = $"Found {_sessions.Count} session(s). Global limit active until {_activeGlobalResetTime.Value:HH:mm:ss} ({cooldownCount} waiting).";
                    }
                    else
                    {
                        txtSubStatus.Text = $"Found {_sessions.Count} session(s) ({readyCount} ready to continue).";
                    }
                }
                else
                {
                    if (_activeGlobalResetTime.HasValue && _activeGlobalResetTime.Value > now)
                    {
                        txtSubStatus.Text = $"No sessions queued. Global limit in cooldown until {_activeGlobalResetTime.Value:HH:mm:ss}.";
                    }
                    else
                    {
                        txtSubStatus.Text = "No Claude Code sessions currently stopped by limit needing continue.";
                    }
                }

                UpdateGlobalCountdown(now, _activeGlobalResetTime ?? default);
                AppLogger.LogAction($"[SCAN] Scanned {_sessions.Count} session(s) (Ready: {readyCount}, Cooldown: {cooldownCount})");
            }
            catch (Exception ex)
            {
                txtSubStatus.Text = "Scan error: " + ex.Message;
                AppLogger.LogException(ex, "ScanSessions");
            }
        }

        private async void ScanRecentSessions()
        {
            try
            {
                var list = await Task.Run(() => ClaudeSessionHelper.GetRecentSessions(max: 7, daysLookback: 5, customProjectsRoot: _settings.CustomProjectsRoot));
                _recentSessions.Clear();
                foreach (var item in list)
                {
                    _recentSessions.Add(item);
                }

                lvRecentSessions.ItemsSource = _recentSessions;
                txtRecentSubStatus.Text = _recentSessions.Count > 0
                    ? $"Found {_recentSessions.Count} recent Claude Code session(s) (latest 5 days, max 7)."
                    : "No recent Claude Code sessions found in the past 5 days.";
            }
            catch (Exception ex)
            {
                txtRecentSubStatus.Text = "Scan error: " + ex.Message;
                AppLogger.LogException(ex, "ScanRecentSessions");
            }
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await Task.Delay(800);
            await EnsureAppScreenshotAsync();
        }

        public async Task EnsureAppScreenshotAsync(bool force = false)
        {
            try
            {
                string[] candidateDirs = new[]
                {
                    Path.Combine(AppContext.BaseDirectory, "Assets"),
                    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Assets"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets")
                };

                foreach (var dir in candidateDirs)
                {
                    try
                    {
                        var fullDir = Path.GetFullPath(dir);
                        if (Directory.Exists(fullDir))
                        {
                            string targetFile = Path.Combine(fullDir, "screenshot.png");
                            if (force || !File.Exists(targetFile))
                            {
                                await CaptureScreenshotToFileAsync(targetFile);
                            }
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogException(ex, "EnsureAppScreenshotAsync");
            }
        }

        public async Task CaptureScreenshotToFileAsync(string outputPath)
        {
            try
            {
                if (this.Content is UIElement rootElement)
                {
                    var rtb = new RenderTargetBitmap();
                    await rtb.RenderAsync(rootElement);
                    var pixelBuffer = await rtb.GetPixelsAsync();
                    byte[] pixels = pixelBuffer.ToArray();

                    using var memStream = new InMemoryRandomAccessStream();
                    var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, memStream);
                    encoder.SetPixelData(
                        BitmapPixelFormat.Bgra8,
                        BitmapAlphaMode.Premultiplied,
                        (uint)rtb.PixelWidth,
                        (uint)rtb.PixelHeight,
                        96, 96,
                        pixels);
                    await encoder.FlushAsync();

                    memStream.Seek(0);
                    using var fileStream = File.Create(outputPath);
                    await memStream.AsStreamForRead().CopyToAsync(fileStream);
                    AppLogger.LogAction($"[SCREENSHOT] Saved app screenshot to: {outputPath} ({rtb.PixelWidth}x{rtb.PixelHeight})");
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogException(ex, "CaptureScreenshotToFileAsync");
            }
        }

        private void Pivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (mainPivot == null) return;

            if (mainPivot.SelectedIndex == 0)
            {
                if (!_hasInitialScanned || _sessions.Count == 0)
                {
                    ScanSessions();
                }
            }
            else if (mainPivot.SelectedIndex == 1)
            {
                ScanRecentSessions();
            }
        }

        private void RefreshRecent_Click(object sender, RoutedEventArgs e) => ScanRecentSessions();
        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            ScanSessions();
            if (_isSchedulerRunning) UpdateBottomScheduleStatus();
        }

        private void ContinueNow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ClaudeSessionInfo session)
            {
                AppLogger.LogAction($"[USER_ACTION] Continue Now clicked: {session.ShortId}");
                WakeSession(session, "Manual Continue Button", force: true);
            }
        }

        private void ContinueAllReady_Click(object sender, RoutedEventArgs e)
        {
            var targetSessions = _sessions.ToList();
            if (targetSessions.Count == 0)
            {
                txtStatus.Text = "No sessions to continue.";
                return;
            }

            int count = 0;
            foreach (var session in targetSessions)
            {
                WakeSession(session, "Batch Continue All", force: true);
                count++;
            }
            txtStatus.Text = $"⚡ Triggered continue for {count} session(s).";
        }

        private void Ticker_Tick(object? sender, object e)
        {
            var now = DateTime.Now;
            _tickCounter++;

            var sessionLimit = _sessions.Where(s => !s.IsResumed && !s.WaitingForNextReset && s.ResetTime.HasValue && s.ResetTime.Value > now)
                                       .OrderByDescending(s => s.ResetTime!.Value)
                                       .Select(s => s.ResetTime!.Value)
                                       .FirstOrDefault();
            var activeLimit = sessionLimit;

            if (_tickCounter % 10 == 0)
            {
                _ = FetchLiveGlmQuotaAsync();
            }

            if (_glmNextResetTime.HasValue && _glmNextResetTime.Value > now)
            {
                if (activeLimit == default || _glmNextResetTime.Value > activeLimit)
                {
                    activeLimit = _glmNextResetTime.Value;
                }
            }

            if (_tickCounter % 5 == 0)
            {
                var (fileReset, _) = ClaudeSessionHelper.GetLatestGlobalRateLimit(lookbackDays: Math.Max(GetLookbackDays(), 2.0), customProjectsRoot: _settings.CustomProjectsRoot);
                if (fileReset.HasValue && fileReset.Value > now)
                {
                    if (activeLimit == default || fileReset.Value > activeLimit)
                    {
                        activeLimit = fileReset.Value;
                    }
                }
            }

            if (activeLimit != default && activeLimit > now)
            {
                _activeGlobalResetTime = activeLimit;
                _activeGlobalResetTimeStr = activeLimit.ToString("yyyy-MM-dd HH:mm:ss");

                string globalResetStr = _activeGlobalResetTimeStr;
                foreach (var session in _sessions)
                {
                    if (!session.IsResumed && !session.WaitingForNextReset)
                    {
                        session.ResetTime = activeLimit;
                        session.ResetTimeString = globalResetStr;
                        session.HasRateLimit = true;
                    }
                }
            }
            else
            {
                activeLimit = default;
                _activeGlobalResetTime = null;
                _activeGlobalResetTimeStr = "";
            }

            foreach (var session in _sessions)
            {
                session.UpdateCountdown(now);
            }

            UpdateGlobalCountdown(now, activeLimit);

            if (_isSchedulerRunning)
            {
                bool continueLoop = chkContinueLoop?.IsChecked == true;
                var targetSessions = _sessions.Where(s => _scheduledSessionIds.Contains(s.SessionId)).ToList();
                var nonResumed = targetSessions.Where(s => !s.IsResumed).ToList();

                if (activeLimit == default)
                {
                    foreach (var session in nonResumed)
                    {
                        if (string.IsNullOrEmpty(session.LastWokenResetTimeString) ||
                            !string.Equals(session.ResetTimeString, session.LastWokenResetTimeString, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!session.WaitingForNextReset)
                            {
                                WakeSession(session, "Auto-Wake on Limit Expiry / Ready");
                            }
                        }
                    }
                }
                else
                {
                    foreach (var session in nonResumed)
                    {
                        if (session.ResetTime.HasValue)
                        {
                            if (now >= session.ResetTime.Value)
                            {
                                if (string.IsNullOrEmpty(session.LastWokenResetTimeString) ||
                                    !string.Equals(session.ResetTimeString, session.LastWokenResetTimeString, StringComparison.OrdinalIgnoreCase))
                                {
                                    if (!session.WaitingForNextReset)
                                    {
                                        WakeSession(session, "Auto-Wake on Limit Expiry");
                                    }
                                }
                            }
                        }
                    }
                }

                if (continueLoop && _tickCounter % 5 == 0)
                {
                    RescanLoopSessionState();
                }

                int activeRemaining = targetSessions.Count(s => !s.IsResumed);
                int workingCount = targetSessions.Count(s => !s.IsResumed && s.WaitingForNextReset);
                int cooldownCount = targetSessions.Count(s => !s.IsResumed && s.ResetTime.HasValue && s.ResetTime.Value > now);

                if (activeRemaining > 0)
                {
                    string loopTag = continueLoop ? " (Loop Mode)" : "";
                    if (cooldownCount > 0)
                    {
                        txtStatus.Text = $"🟢 Scheduler Active{loopTag} · {cooldownCount} in cooldown, {workingCount} working, {activeRemaining} remaining...";
                    }
                    else if (workingCount > 0)
                    {
                        txtStatus.Text = $"🟢 Scheduler Active{loopTag} · {workingCount} session(s) working in background...";
                    }
                    else
                    {
                        txtStatus.Text = $"🟢 Scheduler Active{loopTag} · Watching {activeRemaining} active session(s)...";
                    }
                    UpdateBottomScheduleStatus();
                }
                else
                {
                    StopScheduler("✅ All scheduled sessions completed.");
                }
            }
        }

        private void UpdateGlobalCountdown(DateTime now, DateTime activeLimit)
        {
            if (txtGlobalCountdown == null || txtGlobalCountdownSub == null) return;

            string glmUsageTag = _glmQuotaPercentage.HasValue ? $"GLM {_glmQuotaPercentage.Value}% · " : "";

            if (activeLimit != default && activeLimit > now)
            {
                var diff = activeLimit - now;
                int hours = (int)Math.Floor(diff.TotalHours);
                int mins = diff.Minutes;
                int secs = diff.Seconds;
                txtGlobalCountdown.Text = $"{hours:D2}:{mins:D2}:{secs:D2}";
                txtGlobalCountdownSub.Text = $"{glmUsageTag}Resets at {activeLimit:HH:mm:ss}";
                return;
            }

            if (activeLimit != default && now >= activeLimit && (now - activeLimit).TotalMinutes < 15)
            {
                txtGlobalCountdown.Text = "00:00:00";
                txtGlobalCountdownSub.Text = $"{glmUsageTag}Limit reset (Ready)";
                return;
            }

            int workingCount = _sessions.Count(s => !s.IsResumed && s.WaitingForNextReset);
            if (workingCount > 0)
            {
                txtGlobalCountdown.Text = "WORKING";
                txtGlobalCountdownSub.Text = $"{glmUsageTag}{workingCount} session(s) executing";
                return;
            }

            int readyCount = _sessions.Count(s => !s.IsResumed);
            if (readyCount > 0)
            {
                txtGlobalCountdown.Text = "READY";
                txtGlobalCountdownSub.Text = $"{glmUsageTag}{readyCount} ready to wake";
                return;
            }

            if (_sessions.Count > 0 && _sessions.All(s => s.IsResumed))
            {
                txtGlobalCountdown.Text = "DONE";
                txtGlobalCountdownSub.Text = $"{glmUsageTag}All sessions completed";
                return;
            }

            txtGlobalCountdown.Text = "00:00:00";
            txtGlobalCountdownSub.Text = _glmQuotaPercentage.HasValue ? $"GLM {_glmQuotaPercentage.Value}%" : "System Ready";
        }

        private void UpdateBottomScheduleStatus()
        {
            if (pnlScheduledStatus == null || txtBottomScheduleTitles == null) return;

            if (!_isSchedulerRunning || _scheduledSessionIds.Count == 0)
            {
                pnlScheduledStatus.Visibility = Visibility.Collapsed;
                txtBottomScheduleTitles.Text = "";
                return;
            }

            var targetSessions = _sessions.Where(s => _scheduledSessionIds.Contains(s.SessionId)).ToList();
            if (targetSessions.Count == 0)
            {
                pnlScheduledStatus.Visibility = Visibility.Collapsed;
                txtBottomScheduleTitles.Text = "";
                return;
            }

            pnlScheduledStatus.Visibility = Visibility.Visible;
            bool continueLoop = chkContinueLoop?.IsChecked == true;
            if (txtBottomScheduleHeader != null)
            {
                txtBottomScheduleHeader.Text = continueLoop ? "🔄 Loop Mode Active:" : "⏰ Active Schedule:";
            }

            var sessionSummaries = targetSessions.Select((s, idx) =>
            {
                string title = !string.IsNullOrWhiteSpace(s.Title) ? s.Title : (!string.IsNullOrWhiteSpace(s.LastPrompt) ? s.LastPrompt : s.ShortId);
                if (title.Length > 40) title = title.Substring(0, 37) + "...";
                return $"[{idx + 1}] {title} ({s.StatusText})";
            });

            txtBottomScheduleTitles.Text = string.Join("   •   ", sessionSummaries);
        }

        private void RescanLoopSessionState()
        {
            try
            {
                var now = DateTime.Now;
                DateTime? newGlobalReset = null;
                string newGlobalResetStr = "";

                foreach (var session in _sessions.ToList())
                {
                    if (_removedSessionIds.Contains(session.SessionId) ||
                        (!string.IsNullOrEmpty(session.FullPath) && _removedSessionIds.Contains(session.FullPath)) ||
                        (_scheduledSessionIds.Count > 0 && !_scheduledSessionIds.Contains(session.SessionId)))
                    {
                        _sessions.Remove(session);
                        continue;
                    }

                    if (string.IsNullOrEmpty(session.FullPath) || !File.Exists(session.FullPath)) continue;

                    var fi = new FileInfo(session.FullPath);
                    var updated = ClaudeSessionHelper.GetSessionInfoFromFile(session.FullPath);
                    if (updated == null) continue;

                    if (updated.HasRateLimit && updated.ResetTime.HasValue && updated.ResetTime.Value > now)
                    {
                        if (session.ResetTime != updated.ResetTime)
                        {
                            session.ResetTime = updated.ResetTime;
                            session.ResetTimeString = updated.ResetTimeString;
                            session.HasRateLimit = true;
                            session.WaitingForNextReset = false;
                            session.IsResumed = false;
                            session.StatusText = $"⏳ Cooldown (Resets {session.ResetTime.Value:HH:mm:ss})";
                            AppLogger.LogAction($"[LOOP_RATE_LIMIT] Session {session.ShortId} hit limit until {session.ResetTimeString}");
                        }

                        if (!newGlobalReset.HasValue || updated.ResetTime.Value > newGlobalReset.Value)
                        {
                            newGlobalReset = updated.ResetTime.Value;
                            newGlobalResetStr = updated.ResetTimeString;
                        }
                        continue;
                    }

                    if (session.WaitingForNextReset || !session.IsResumed)
                    {
                        double secondsSinceWake = session.LastWokenTime.HasValue ? (now - session.LastWokenTime.Value).TotalSeconds : 9999;
                        double secondsSinceWrite = (now - fi.LastWriteTime).TotalSeconds;

                        bool fileActive = secondsSinceWrite < 30;
                        bool justWoken = secondsSinceWake < 45;

                        if (updated.IsTurnCompleted && !fileActive && !justWoken)
                        {
                            session.IsResumed = true;
                            session.HasRateLimit = false;
                            session.ResetTime = null;
                            session.ResetTimeString = "";
                            session.WaitingForNextReset = false;
                            session.StatusText = "✅ Completed";
                            session.CountdownDisplay = "COMPLETED";
                            AppLogger.LogAction($"[LOOP_COMPLETED] Session {session.ShortId} finished work successfully.");
                        }
                        else
                        {
                            session.IsResumed = false;
                            session.HasRateLimit = false;
                            session.ResetTime = null;
                            session.WaitingForNextReset = true;
                            session.StatusText = "⚡ Working...";
                            session.CountdownDisplay = "WORKING";
                        }
                    }
                }

                if (newGlobalReset.HasValue)
                {
                    foreach (var session in _sessions)
                    {
                        if ((_scheduledSessionIds.Count == 0 || _scheduledSessionIds.Contains(session.SessionId)) && !session.IsResumed && !session.WaitingForNextReset)
                        {
                            if (!session.ResetTime.HasValue || session.ResetTime.Value <= now ||
                                !string.Equals(session.ResetTimeString, newGlobalResetStr, StringComparison.OrdinalIgnoreCase))
                            {
                                session.ResetTime = newGlobalReset.Value;
                                session.ResetTimeString = newGlobalResetStr;
                                session.HasRateLimit = true;
                                session.IsResumed = false;
                                session.WaitingForNextReset = false;
                                session.LastWokenResetTimeString = "";
                                session.StatusText = $"⏳ Cooldown (Resets {newGlobalReset.Value:HH:mm:ss})";
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogException(ex, "RescanLoopSessionState");
            }
        }

        private void StartScheduler()
        {
            bool continueLoop = chkContinueLoop?.IsChecked == true;
            _scheduledSessionIds.Clear();
            foreach (var s in _sessions)
            {
                if (!string.IsNullOrEmpty(s.SessionId))
                {
                    _scheduledSessionIds.Add(s.SessionId);
                }
            }

            int pending = _sessions.Count(s => _scheduledSessionIds.Contains(s.SessionId) && !s.IsResumed);
            if (pending == 0)
            {
                StopScheduler(_sessions.Count > 0 ? "✅ All sessions on screen already resumed." : "No sessions queued.");
                return;
            }

            _isSchedulerRunning = true;
            btnScheduleAll.Content = "🛑 Stop Scheduler";
            txtStatus.Text = $"🟢 Scheduler Active {(continueLoop ? "(Loop Mode)" : "")} · Watching {pending} session(s)...";
            UpdateBottomScheduleStatus();

            AppLogger.LogAction($"[SCHEDULE_START] Started scheduler (LoopMode: {continueLoop}, Pending: {pending})");
        }

        private void StopScheduler(string statusText = "Scheduler Idle")
        {
            _isSchedulerRunning = false;
            _scheduledSessionIds.Clear();
            btnScheduleAll.Content = "⏰ Schedule Wake All Sessions";
            txtStatus.Text = statusText;
            UpdateBottomScheduleStatus();
            AppLogger.LogAction($"[SCHEDULE_STOP] Stopped scheduler: {statusText}");
        }

        private void ToggleScheduler_Click(object sender, RoutedEventArgs e)
        {
            if (_isSchedulerRunning)
            {
                StopScheduler();
            }
            else
            {
                StartScheduler();
            }
        }

        private async void OpenTerminal_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ClaudeSessionInfo session)
            {
                AppLogger.LogAction($"[USER_ACTION] Open Terminal clicked: Session={session.ShortId}");
                ClaudeSessionHelper.LaunchSession(
                    session.ProjectPath,
                    session.SessionId,
                    extraArgs: "--effort max --permission-mode auto",
                    prompt: null,
                    inBackground: false,
                    customFallbackDir: _settings.CustomWorkingDir);

                string title = !string.IsNullOrWhiteSpace(session.Title) ? session.Title : session.ShortId;
                await TelegramHelper.SendAlertAsync($"💻 <b>Claude Code Opened in Terminal</b>\n📌 {title}\n🆔 {session.ShortId}", _settings);
            }
        }

        private void RemoveSession_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ClaudeSessionInfo session)
            {
                if (!string.IsNullOrEmpty(session.SessionId))
                {
                    _removedSessionIds.Add(session.SessionId);
                    _scheduledSessionIds.Remove(session.SessionId);
                }
                if (!string.IsNullOrEmpty(session.FullPath))
                {
                    _removedSessionIds.Add(session.FullPath);
                }

                _sessions.Remove(session);
                AppLogger.LogAction($"[USER_ACTION] Removed session {session.ShortId}");

                if (_sessions.Count > 0)
                {
                    txtSubStatus.Text = $"{_sessions.Count} session(s) queued for auto-wake.";
                    UpdateBottomScheduleStatus();
                }
                else
                {
                    txtSubStatus.Text = "No sessions queued.";
                    if (_isSchedulerRunning)
                    {
                        StopScheduler("Scheduler Idle (Queue empty)");
                    }
                }
            }
        }

        private async void WakeSession(ClaudeSessionInfo session, string reason, bool force = false)
        {
            if (session == null) return;
            bool continueLoop = chkContinueLoop?.IsChecked == true;
            if (!force && !continueLoop && session.IsResumed) return;

            var now = DateTime.Now;
            if (!force && session.LastWokenTime.HasValue && (now - session.LastWokenTime.Value).TotalMinutes < 2)
            {
                return;
            }

            try
            {
                session.LastWokenTime = now;
                session.LastWokenResetTimeString = session.ResetTimeString;

                if (!continueLoop)
                {
                    session.IsResumed = true;
                    session.StatusText = "🟢 Resumed (Silent)";
                    session.CountdownDisplay = "RESUMED";
                }
                else
                {
                    session.IsResumed = false;
                    session.StatusText = "⚡ Continued (Loop Mode)";
                    session.CountdownDisplay = "WORKING";
                    session.ResetTime = null;
                    session.WaitingForNextReset = true;
                }

                AppLogger.LogAction($"[WAKE] Session {session.ShortId} waking. Reason: '{reason}', LoopMode: {continueLoop}");

                ClaudeSessionHelper.LaunchSession(
                    session.ProjectPath,
                    session.SessionId,
                    extraArgs: "--effort max --permission-mode auto",
                    prompt: "continue",
                    inBackground: true,
                    customFallbackDir: _settings.CustomWorkingDir);

                string title = !string.IsNullOrWhiteSpace(session.Title) ? session.Title : session.ShortId;
                NotificationHelper.ShowToast("Claude Code Auto-Woken", $"Resumed session: {title}");

                await TelegramHelper.SendAlertAsync(
                    $"⏰ <b>Claude Code Session Auto-Woken</b> ({reason})\n📌 Title: {title}\n🆔 Session: {session.ShortId}\n🔄 Loop Mode: {(continueLoop ? "ON" : "OFF")}",
                    _settings);
            }
            catch (Exception ex)
            {
                session.StatusText = "❌ Error: " + ex.Message;
                AppLogger.LogException(ex, "WakeSession");
            }
        }

        private void ForceReload_Click(object sender, RoutedEventArgs e)
        {
            if (_isSchedulerRunning)
            {
                txtStatus.Text = "⚠️ Stop the scheduler before Force Reload.";
                return;
            }
            _removedSessionIds.Clear();
            AppLogger.LogAction("[USER_ACTION] Skiplist cleared.");
            ScanSessions();
        }
    }
}
