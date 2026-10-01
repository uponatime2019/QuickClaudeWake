using System;

namespace QuickClaudeWake.Models
{
    public class AgySessionInfo
    {
        public string ConversationId { get; set; } = "";
        public string Title { get; set; } = "";
        public string WorkspacePath { get; set; } = "";
        public DateTime LastModified { get; set; }

        public string ShortId => ConversationId.Length > 8 ? ConversationId.Substring(0, 8) : ConversationId;
        public string LastModifiedDisplay => LastModified.ToString("MM-dd HH:mm");

        public string TitlePreview
        {
            get
            {
                if (string.IsNullOrEmpty(Title)) return "";
                string s = Title.Replace("\r", " ").Replace("\n", " ").Trim();
                while (s.Contains("  ")) s = s.Replace("  ", " ");
                return s.Length > 70 ? s.Substring(0, 70) + "…" : s;
            }
        }

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
                return !string.IsNullOrWhiteSpace(TitlePreview) ? TitlePreview : "";
            }
        }

        public string RelativeDisplayTitle => $"{RelativeTime} - {TitlePreview}";

        public string DisplayTitle => $"{LastModifiedDisplay}  ·  {TitlePreview}";

        public override string ToString() => RelativeDisplayTitle;
    }
}
