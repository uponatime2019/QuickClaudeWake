using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.IO;
using System.Runtime.InteropServices;
using Windows.Graphics;
using WinRT.Interop;

namespace QuickClaudeWake.Helpers
{
    public static class WindowHelper
    {
        private const double DefaultDpi = 96.0;

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr windowHandle);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr LoadImage(IntPtr hInst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr lpdwProcessId);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr SetFocus(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        public static double GetScaleRatio(Window window)
        {
            IntPtr windowHandle = WindowNative.GetWindowHandle(window);
            uint dpi = GetDpiForWindow(windowHandle);
            return dpi > 0 ? dpi / DefaultDpi : 1.0;
        }

        public static void Configure(Window window, string title, int logicalWidth, int logicalHeight, bool resizable = true)
        {
            IntPtr hwnd = WindowNative.GetWindowHandle(window);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);
            AppLogger.LogAction($"[CONFIGURE] hwnd={hwnd}, appWindow={(appWindow != null ? "ok" : "null")}");

            if (appWindow != null)
            {
                appWindow.Title = title;

                double scaleRatio = GetScaleRatio(window);
                var scaledSize = new SizeInt32(
                    Math.Max(320, (int)Math.Round(logicalWidth * scaleRatio)),
                    Math.Max(240, (int)Math.Round(logicalHeight * scaleRatio)));
                appWindow.Resize(scaledSize);

                try
                {
                    DisplayArea displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Nearest);
                    if (displayArea != null)
                    {
                        var workArea = displayArea.WorkArea;
                        int x = workArea.X + Math.Max(0, (workArea.Width - scaledSize.Width) / 2);
                        int y = workArea.Y + Math.Max(0, (workArea.Height - scaledSize.Height) / 2);
                        appWindow.Move(new PointInt32(x, y));
                    }
                }
                catch { }

                if (appWindow.Presenter is OverlappedPresenter presenter)
                {
                    presenter.IsResizable = resizable;
                    presenter.IsMaximizable = resizable;
                }

                try
                {
                    appWindow.Show(true);
                }
                catch { }
            }

            SetAppIcon(window, "Assets/AppIcon.ico");
            BringToForeground(hwnd);
        }

        public static void BringToForeground(Window window)
        {
            if (window == null) return;
            IntPtr hwnd = WindowNative.GetWindowHandle(window);
            BringToForeground(hwnd);
        }

        public static void BringToForeground(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;
            AppLogger.LogAction($"[BRING_TO_FOREGROUND] hwnd={hwnd}, IsIconic={IsIconic(hwnd)}");

            const uint SWP_NOSIZE = 0x0001;
            const uint SWP_NOMOVE = 0x0002;
            const uint SWP_SHOWWINDOW = 0x0040;
            const int SW_RESTORE = 9;
            const int SW_SHOW = 5;

            IntPtr HWND_TOPMOST = new IntPtr(-1);
            IntPtr HWND_NOTOPMOST = new IntPtr(-2);

            try
            {
                if (IsIconic(hwnd))
                    ShowWindow(hwnd, SW_RESTORE);
                else
                    ShowWindow(hwnd, SW_SHOW);

                IntPtr foregroundHwnd = GetForegroundWindow();
                uint currentThreadId = GetCurrentThreadId();
                uint foregroundThreadId = foregroundHwnd == IntPtr.Zero
                    ? 0
                    : GetWindowThreadProcessId(foregroundHwnd, IntPtr.Zero);
                bool attached = foregroundThreadId != 0 && foregroundThreadId != currentThreadId;

                try
                {
                    if (attached)
                        attached = AttachThreadInput(currentThreadId, foregroundThreadId, true);

                    SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                    SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                    BringWindowToTop(hwnd);
                    SetForegroundWindow(hwnd);
                    SetFocus(hwnd);
                }
                finally
                {
                    if (attached)
                        AttachThreadInput(currentThreadId, foregroundThreadId, false);
                }
            }
            catch { }
        }

        public static void SetAppIcon(Window window, string relativeIconPath = "Assets/AppIcon.ico")
        {
            IntPtr hwnd = WindowNative.GetWindowHandle(window);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

            string normPath = relativeIconPath.Replace('/', Path.DirectorySeparatorChar);
            string[] searchPaths = new[]
            {
                Path.Combine(AppContext.BaseDirectory, normPath),
                Path.Combine(AppContext.BaseDirectory, "Assets", Path.GetFileName(normPath)),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, normPath),
                Path.GetFullPath(normPath)
            };

            string? iconPath = null;
            foreach (var candidate in searchPaths)
            {
                if (File.Exists(candidate))
                {
                    iconPath = candidate;
                    break;
                }
            }

            if (iconPath != null)
            {
                if (appWindow != null)
                {
                    try { appWindow.SetIcon(iconPath); } catch { }
                }

                if (hwnd != IntPtr.Zero)
                {
                    try
                    {
                        IntPtr hIconSmall = LoadImage(IntPtr.Zero, iconPath, 1, 16, 16, 0x0010);
                        IntPtr hIconBig = LoadImage(IntPtr.Zero, iconPath, 1, 32, 32, 0x0010);
                        if (hIconSmall != IntPtr.Zero)
                            SendMessage(hwnd, 0x0080, (IntPtr)0, hIconSmall);
                        if (hIconBig != IntPtr.Zero)
                            SendMessage(hwnd, 0x0080, (IntPtr)1, hIconBig);
                    }
                    catch { }
                }
            }
        }
    }
}
