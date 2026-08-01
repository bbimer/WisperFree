using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace FreeFlowWin.Core.Context
{
    public class ContextExtractor
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        public static string GetActiveWindowContext()
        {
            try
            {
                IntPtr hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero) return "Unknown context";

                // Получаем заголовок окна
                StringBuilder titleBuilder = new StringBuilder(256);
                GetWindowText(hwnd, titleBuilder, 256);
                string windowTitle = titleBuilder.ToString();

                // Получаем имя процесса программы
                GetWindowThreadProcessId(hwnd, out uint pid);
                string processName = "Unknown Process";
                if (pid != 0)
                {
                    using (Process proc = Process.GetProcessById((int)pid))
                    {
                        processName = proc.ProcessName;
                    }
                }

                return $"Active App: {processName}.exe, Active Window Title: \"{windowTitle}\"";
            }
            catch (Exception ex)
            {
                return $"Error retrieving context: {ex.Message}";
            }
        }
    }
}
