using Microsoft.UI.Xaml;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using QuickClaudeWake.Helpers;
using QuickClaudeWake.Models;
using QuickClaudeWake.Services;

namespace QuickClaudeWake
{
    public partial class App : Application
    {
        private const string MutexName = "Local\\QuickClaudeWake_SingleInstance_7F9E2A";
        internal const string RestoreMessageName = "QuickClaudeWakeRestoreMessage_7F9E2A";
        private const string MainWindowTitle = "Quick Claude Wake";

        private static Mutex? _mutex;
        private Window? _window;

        #region Win32 P/Invoke for Instance Restoration

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint RegisterWindowMessage(string lpString);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const int SW_RESTORE = 9;
        private const int SW_SHOW = 5;

        #endregion

        public App()
        {
            InitializeComponent();

            UnhandledException += (s, e) =>
            {
                AppLogger.LogException(e.Exception, "App_UnhandledException");
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                {
                    AppLogger.LogException(ex, "AppDomain_UnhandledException");
                }
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                AppLogger.LogException(e.Exception, "TaskScheduler_UnobservedTaskException");
            };

            try
            {
                NotificationHelper.RegisterAppNotificationManager();
            }
            catch (Exception ex)
            {
                AppLogger.LogException(ex, "RegisterAppNotificationManager");
            }
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            try
            {
                bool createdNew;
                _mutex = new Mutex(true, MutexName, out createdNew);
                AppLogger.LogAction($"Application_Launched, createdNew={createdNew}");

                if (!createdNew)
                {
                    AppLogger.LogAction("SingleInstance_DetectedExisting, Restoring and Exiting");
                    RestoreExistingInstance();
                    Application.Current.Exit();
                    return;
                }

                SyncStartupSetting();

                _window = new MainWindow();
                _window.Activate();
                WindowHelper.BringToForeground(_window);
                AppLogger.LogAction("MainWindow_Activated");
            }
            catch (Exception ex)
            {
                AppLogger.LogException(ex, "App_OnLaunched_Exception");
            }
        }

        private static void RestoreExistingInstance()
        {
            try
            {
                IntPtr hwnd = FindWindow(null, MainWindowTitle);
                if (hwnd != IntPtr.Zero)
                {
                    uint msg = RegisterWindowMessage(RestoreMessageName);
                    if (msg != 0)
                    {
                        PostMessage(hwnd, msg, IntPtr.Zero, IntPtr.Zero);
                    }

                    WindowHelper.BringToForeground(hwnd);
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogException(ex, "RestoreExistingInstance");
            }
        }

        private static void SyncStartupSetting()
        {
            try
            {
                var settings = QuickClaudeWakeSettings.Load();
                new StartupManager("QuickClaudeWake").SetAutoStart(settings.IsRunAtStartup);
            }
            catch { }
        }
    }
}
