using System;
using System.Linq;
using System.Drawing;
using System.Diagnostics;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace CS2_AutoAccept
{
    internal static class WindowFinder
    {
        #region DLL Imports
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        #endregion

        internal static Screen? FindApplicationScreen(string processName)
        {
            // Find process
            var proc = Process.GetProcessesByName(processName).FirstOrDefault();
            if (proc == null || proc.MainWindowHandle == IntPtr.Zero)
                return null;

            // Get window rectangle
            if (!GetWindowRect(proc.MainWindowHandle, out RECT rect))
                return null;

            Rectangle bounds = new Rectangle(
                rect.Left,
                rect.Top,
                rect.Right - rect.Left,
                rect.Bottom - rect.Top
            );

            // Window might be minimized: in that case just return the nearest screen
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return Screen.FromHandle(proc.MainWindowHandle);

            // Find which screen intersects most
            Screen bestScreen = Screen.AllScreens[0];
            int maxArea = 0;

            foreach (var screen in Screen.AllScreens)
            {
                Rectangle intersection = Rectangle.Intersect(bounds, screen.Bounds);
                int area = intersection.Width * intersection.Height;

                if (area > maxArea)
                {
                    maxArea = area;
                    bestScreen = screen;
                }
            }

            return bestScreen;
        }
    }
}