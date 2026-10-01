using System;
using System.Diagnostics;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace QuickClaudeWake.Helpers
{
    public static class NotificationHelper
    {
        public static void RegisterAppNotificationManager()
        {
            try
            {
                if (AppNotificationManager.IsSupported())
                {
                    AppNotificationManager.Default.Register();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AppNotificationManager register failed: {ex.Message}");
            }
        }

        public static void ShowToast(string title, string message)
        {
            try
            {
                if (AppNotificationManager.IsSupported())
                {
                    var notification = new AppNotificationBuilder()
                        .AddText(title)
                        .AddText(message)
                        .BuildNotification();

                    AppNotificationManager.Default.Show(notification);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AppNotificationManager.ShowToast failed: {ex.Message}");
            }
        }
    }
}
