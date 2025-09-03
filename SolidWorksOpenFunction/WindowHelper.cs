using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SolidWorksOpenFunction
{
    public static class WindowHelper
    {
        // Windows API imports
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsZoomed(IntPtr hWnd);

        [DllImport("kernel32.dll")]
        private static extern uint GetLastError();

        // Constants for ShowWindow
        private const int SW_RESTORE = 9;
        private const int SW_SHOW = 5;
        private const int SW_MINIMIZE = 6;
        private const int SW_MAXIMIZE = 3;

        // Constants for SetWindowPos
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;

        // Constants for keybd_event and mouse_event
        private const byte VK_ALT = 0x12;
        private const byte VK_CONTROL = 0x11;
        private const byte VK_S = 0x53;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;

        // Delegate for window enumeration
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        // Logging delegate
        public delegate void LogHandler(string message);
        public static event LogHandler OnLog;

        private static void Log(string message)
        {
            OnLog?.Invoke(message);
            Console.WriteLine(message); // Fallback to console
        }

        /// <summary>
        /// Brings the main window of the process with the specified PID to the foreground.
        /// </summary>
        /// <param name="pid">The process ID of the target application.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public static bool BringWindowToFront(int pid)
        {
            try
            {
                Process process = Process.GetProcessById(pid);
                IntPtr windowHandle = process.MainWindowHandle;
                if (windowHandle == IntPtr.Zero)
                {
                    Log($"No main window found for process with PID {pid}.");
                    return false;
                }

                IntPtr foregroundWindow = GetForegroundWindow();
                uint foregroundThreadId = GetWindowThreadProcessId(foregroundWindow, out uint _);
                uint targetThreadId = GetWindowThreadProcessId(windowHandle, out uint _);

                ShowWindow(windowHandle, SW_RESTORE);
                bool success = SetForegroundWindow(windowHandle);

                if (!success)
                {
                    uint errorCode = GetLastError();
                    Log($"SetForegroundWindow failed for PID {pid}. Error code: {errorCode}");

                    keybd_event(VK_ALT, 0, 0, UIntPtr.Zero);
                    Thread.Sleep(10);
                    keybd_event(VK_ALT, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

                    if (foregroundThreadId != targetThreadId)
                    {
                        AttachThreadInput(foregroundThreadId, targetThreadId, true);
                        success = SetForegroundWindow(windowHandle);
                        AttachThreadInput(foregroundThreadId, targetThreadId, false);
                    }
                    else
                    {
                        success = SetForegroundWindow(windowHandle);
                    }

                    if (!success)
                    {
                        SetWindowPos(windowHandle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
                        Thread.Sleep(10);
                        SetWindowPos(windowHandle, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
                        success = SetForegroundWindow(windowHandle);
                    }
                }

                if (success)
                {
                    Log($"Window for PID {pid} brought to foreground successfully.");
                    return true;
                }
                else
                {
                    Log($"Failed to bring window for PID {pid} to foreground after all attempts.");
                    return false;
                }
            }
            catch (ArgumentException)
            {
                Log($"No process found with PID {pid}.");
                return false;
            }
            catch (Exception ex)
            {
                Log($"Error bringing window for PID {pid} to foreground: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Brings the main window of the process to the foreground with a timeout.
        /// </summary>
        /// <param name="pid">The process ID of the target application.</param>
        /// <param name="timeoutMs">Timeout in milliseconds.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public static async Task<bool> BringWindowToFrontWithTimeout(int pid, int timeoutMs = 5000)
        {
            try
            {
                var task = Task.Run(() => BringWindowToFront(pid));
                if (await Task.WhenAny(task, Task.Delay(timeoutMs)) == task)
                {
                    return await task;
                }
                Log($"Operation timed out for PID {pid} after {timeoutMs}ms.");
                return false;
            }
            catch (Exception ex)
            {
                Log($"Error in timed operation for PID {pid}: {ex.Message}");
                return false;
            }
        }

		/// <summary>
		/// Brings a window to the foreground by matching its title (partial or full).
		/// </summary>
		/// <param name="windowTitle">The full or partial title of the target window.</param>
		/// <returns>True if successful, false otherwise.</returns>
		public static bool BringWindowToFrontByTitle(string windowTitle)
		{
			try
			{
				IntPtr foundHandle = IntPtr.Zero;

				EnumWindows((hWnd, lParam) =>
				{
					StringBuilder title = new StringBuilder(256);
					GetWindowText(hWnd, title, 256);
					if (title.ToString().IndexOf(windowTitle, StringComparison.OrdinalIgnoreCase) >= 0)
					{
						foundHandle = hWnd;
						return false;
					}
					return true;
				}, IntPtr.Zero);

				if (foundHandle == IntPtr.Zero)
				{
					Log($"No window found with title containing '{windowTitle}'.");
					return false;
				}

				ShowWindow(foundHandle, SW_RESTORE);
				bool success = SetForegroundWindow(foundHandle);
				if (!success)
				{
					uint errorCode = GetLastError();
					Log($"SetForegroundWindow failed for title '{windowTitle}'. Error code: {errorCode}");
				}
				return success;
			}
			catch (Exception ex)
			{
				Log($"Error bringing window with title '{windowTitle}' to foreground: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Minimizes the main window of the process with the specified PID.
		/// </summary>
		/// <param name="pid">The process ID of the target application.</param>
		/// <returns>True if successful, false otherwise.</returns>
		public static bool MinimizeWindow(int pid)
        {
            try
            {
                Process process = Process.GetProcessById(pid);
                IntPtr windowHandle = process.MainWindowHandle;
                if (windowHandle == IntPtr.Zero)
                {
                    Log($"No main window found for process with PID {pid}.");
                    return false;
                }
                return ShowWindow(windowHandle, SW_MINIMIZE);
            }
            catch (Exception ex)
            {
                Log($"Error minimizing window for PID {pid}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Maximizes the main window of the process with the specified PID.
        /// </summary>
        /// <param name="pid">The process ID of the target application.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public static bool MaximizeWindow(int pid)
        {
            try
            {
                Process process = Process.GetProcessById(pid);
                IntPtr windowHandle = process.MainWindowHandle;
                if (windowHandle == IntPtr.Zero)
                {
                    Log($"No main window found for process with PID {pid}.");
                    return false;
                }
                return ShowWindow(windowHandle, SW_MAXIMIZE);
            }
            catch (Exception ex)
            {
                Log($"Error maximizing window for PID {pid}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Sets the position and size of the main window for the specified PID.
        /// </summary>
        /// <param name="pid">The process ID of the target application.</param>
        /// <param name="x">The new x-coordinate of the window.</param>
        /// <param name="y">The new y-coordinate of the window.</param>
        /// <param name="width">The new width of the window.</param>
        /// <param name="height">The new height of the window.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public static bool SetWindowPositionAndSize(int pid, int x, int y, int width, int height)
        {
            try
            {
                Process process = Process.GetProcessById(pid);
                IntPtr windowHandle = process.MainWindowHandle;
                if (windowHandle == IntPtr.Zero)
                {
                    Log($"No main window found for process with PID {pid}.");
                    return false;
                }
                return SetWindowPos(windowHandle, IntPtr.Zero, x, y, width, height, SWP_NOACTIVATE);
            }
            catch (Exception ex)
            {
                Log($"Error setting window position/size for PID {pid}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if the main window of the specified PID is minimized.
        /// </summary>
        /// <param name="pid">The process ID of the target application.</param>
        /// <returns>True if minimized, false otherwise or if an error occurs.</returns>
        public static bool IsWindowMinimized(int pid)
        {
            try
            {
                Process process = Process.GetProcessById(pid);
                IntPtr windowHandle = process.MainWindowHandle;
                if (windowHandle == IntPtr.Zero)
                    return false;
                return IsIconic(windowHandle);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if the main window of the specified PID is maximized.
        /// </summary>
        /// <param name="pid">The process ID of the target application.</param>
        /// <returns>True if maximized, false otherwise or if an error occurs.</returns>
        public static bool IsWindowMaximized(int pid)
        {
            try
            {
                Process process = Process.GetProcessById(pid);
                IntPtr windowHandle = process.MainWindowHandle;
                if (windowHandle == IntPtr.Zero)
                    return false;
                return IsZoomed(windowHandle);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Sends a left mouse click to the specified window at the given coordinates.
        /// </summary>
        /// <param name="pid">The process ID of the target application.</param>
        /// <param name="x">The x-coordinate relative to the window's client area.</param>
        /// <param name="y">The y-coordinate relative to the window's client area.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public static bool SendMouseClick(int pid, int x, int y)
        {
            try
            {
                Process process = Process.GetProcessById(pid);
                IntPtr windowHandle = process.MainWindowHandle;
                if (windowHandle == IntPtr.Zero)
                {
                    Log($"No main window found for process with PID {pid}.");
                    return false;
                }

                if (!BringWindowToFront(pid))
                    return false;

                mouse_event(MOUSEEVENTF_LEFTDOWN, (uint)x, (uint)y, 0, UIntPtr.Zero);
                Thread.Sleep(10);
                mouse_event(MOUSEEVENTF_LEFTUP, (uint)x, (uint)y, 0, UIntPtr.Zero);
                return true;
            }
            catch (Exception ex)
            {
                Log($"Error sending mouse click for PID {pid}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Sends a key combination to the specified window.
        /// </summary>
        /// <param name="pid">The process ID of the target application.</param>
        /// <param name="keys">Array of virtual key codes to send.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public static bool SendKeyCombination(int pid, params byte[] keys)
        {
            try
            {
                Process process = Process.GetProcessById(pid);
                IntPtr windowHandle = process.MainWindowHandle;
                if (windowHandle == IntPtr.Zero)
                {
                    Log($"No main window found for process with PID {pid}.");
                    return false;
                }

                if (!BringWindowToFront(pid))
                    return false;

                foreach (byte key in keys)
                    keybd_event(key, 0, 0, UIntPtr.Zero);

                for (int i = keys.Length - 1; i >= 0; i--)
                    keybd_event(keys[i], 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

                return true;
            }
            catch (Exception ex)
            {
                Log($"Error sending key combination for PID {pid}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets all window handles for the specified process ID.
        /// </summary>
        /// <param name="pid">The process ID of the target application.</param>
        /// <returns>A list of window handles.</returns>
        public static List<IntPtr> GetAllWindows(int pid)
        {
            List<IntPtr> windows = new List<IntPtr>();
            EnumWindows((hWnd, lParam) =>
            {
                GetWindowThreadProcessId(hWnd, out uint windowPid);
                if (windowPid == pid)
                    windows.Add(hWnd);
                return true;
            }, IntPtr.Zero);
            return windows;
        }

        /// <summary>
        /// Brings a specific window to the foreground by index from the process's windows.
        /// </summary>
        /// <param name="pid">The process ID of the target application.</param>
        /// <param name="windowIndex">The index of the window in the process's window list.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public static bool BringWindowToFrontByIndex(int pid, int windowIndex)
        {
            try
            {
                var windows = GetAllWindows(pid);
                if (windowIndex < 0 || windowIndex >= windows.Count)
                {
                    Log($"Invalid window index {windowIndex} for process with PID {pid}.");
                    return false;
                }
                IntPtr windowHandle = windows[windowIndex];
                ShowWindow(windowHandle, SW_RESTORE);
                bool success = SetForegroundWindow(windowHandle);
                if (!success)
                {
                    uint errorCode = GetLastError();
                    Log($"SetForegroundWindow failed for window index {windowIndex}, PID {pid}. Error code: {errorCode}");
                }
                return success;
            }
            catch (Exception ex)
            {
                Log($"Error bringing window index {windowIndex} for PID {pid} to foreground: {ex.Message}");
                return false;
            }
        }
    }
}