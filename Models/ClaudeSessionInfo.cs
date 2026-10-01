using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace QuickClaudeWake.Models
{
    public class ClaudeSessionInfo : INotifyPropertyChanged
    {
        private bool _isSelected = true;
        private string _statusText = "Waiting";
        private string _countdownDisplay = "--:--:--";
        private bool _isResumed = false;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string SessionId { get; set; } = "";
        public string ProjectPath { get; set; } = "";
        public string RealProjectPath { get; set; } = "";
        public string FullPath { get; set; } = "";
        public string Title { get; set; } = "";
        public string LastPrompt { get; set; } = "";
        public string LastMessage { get; set; } = "";
        public DateTime LastModified { get; set; }

        public bool HasRateLimit { get; set; }
        public DateTime? ResetTime { get; set; }
        public string ResetTimeString { get; set; } = "";
        public DateTime? LastWokenTime { get; set; }
        public string LastWokenResetTimeString { get; set; } = "";

        public bool WaitingForNextReset { get; set; }
        public bool IsTurnCompleted { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public string StatusText
        {
            get => _statusText;
            set
            {
                if (_statusText != value)
                {
                    _statusText = value;
                    OnPropertyChanged();
                }
            }
        }

        public string CountdownDisplay
        {
            get => _countdownDisplay;
            set
            {
                if (_countdownDisplay != value)
                {
                    _countdownDisplay = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsResumed
        {
            get => _isResumed;
            set
            {
                if (_isResumed != value)
                {
                    _isResumed = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ProjectDisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(RealProjectPath))
                {
                    return RealProjectPath;
                }
                if (!string.IsNullOrEmpty(ProjectPath))
                {
                    try
                    {
                        return Path.GetFileName(ProjectPath.TrimEnd('\\', '/'));
                    }
                    catch { }
                }
                return "(unknown project)";
            }
        }

        public string ShortId => SessionId.Length > 8 ? SessionId.Substring(0, 8) : SessionId;

        public string LastModifiedDisplay => LastModified.ToString("MM-dd HH:mm");

        private static string Preview(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\r", " ").Replace("\n", " ").Trim();
            while (s.Contains("  ")) s = s.Replace("  ", " ");
            return s.Length > n ? s.Substring(0, n) + "…" : s;
        }

        public string TitlePreview => Preview(Title, 100);

        public string RelativeTime
        {
            get
            {
                if (LastModified == DateTime.MinValue) return "";
                var span = DateTime.Now - LastModified;
                if (span.TotalMinutes < 1) return "just now";
                if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
                if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
                if (span.TotalDays < 3) return $"{(int)span.TotalDays}d ago";
                return $">= {(int)span.TotalDays}d ago";
            }
        }

        public string Latest1000Chars { get; set; } = "";

        public string DetailToolTip
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Latest1000Chars))
                {
                    return Latest1000Chars;
                }
                if (!string.IsNullOrWhiteSpace(LastPrompt))
                {
                    return LastPrompt.Length > 1000 ? LastPrompt.Substring(LastPrompt.Length - 1000) : LastPrompt;
                }
                return !string.IsNullOrWhiteSpace(TitlePreview) ? TitlePreview : "";
            }
        }

        public string RelativeDisplayTitle => $"{RelativeTime} - {TitlePreview}";

        public string DisplayTitle => $"{LastModifiedDisplay}  ·  {Preview(Title, 80)}";

        public override string ToString() => RelativeDisplayTitle;

        public void UpdateCountdown(DateTime now)
        {
            if (IsResumed)
            {
                CountdownDisplay = "COMPLETED";
                StatusText = "✅ Completed";
                return;
            }

            if (WaitingForNextReset && (!ResetTime.HasValue || ResetTime.Value <= now))
            {
                CountdownDisplay = "WORKING";
                StatusText = "⚡ In Progress...";
                return;
            }

            if (!ResetTime.HasValue)
            {
                CountdownDisplay = "READY";
                StatusText = "⚡ Ready to Wake";
                return;
            }

            var diff = ResetTime.Value - now;
            if (diff.TotalSeconds <= 0)
            {
                CountdownDisplay = "00:00:00 (READY)";
                StatusText = "🟢 Cooldown Expired";
            }
            else
            {
                int hours = (int)Math.Floor(diff.TotalHours);
                int mins = diff.Minutes;
                int secs = diff.Seconds;
                CountdownDisplay = $"{hours:D2}:{mins:D2}:{secs:D2}";
                StatusText = $"⏳ Cooldown (Resets {ResetTime.Value:HH:mm:ss})";
            }
        }
    }
}
