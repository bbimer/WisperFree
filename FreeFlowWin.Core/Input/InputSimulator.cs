using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace FreeFlowWin.Core.Input
{
    public class InputSimulator
    {
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EmptyClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetClipboardData(uint uFormat);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalFree(IntPtr hMem);

        private const uint CF_UNICODETEXT = 13;
        private const uint GMEM_MOVEABLE = 0x0002;

        private const byte VK_CONTROL = 0x11;
        private const byte VK_V = 0x56;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        public static void PasteText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            // 1. Сохраняем исходный буфер обмена пользователя
            string? originalText = GetClipboardText();

            // 2. Устанавливаем новый текст в буфер обмена
            if (!SetClipboardText(text))
            {
                Console.WriteLine("[ERROR] Failed to set text to clipboard.");
                return;
            }

            // 3. Эмулируем нажатие Ctrl+V
            SimulateCtrlV();

            // 4. Восстанавливаем буфер обмена через короткую задержку
            // (даем целевой программе успеть обработать вставку)
            ThreadPool.QueueUserWorkItem(_ =>
            {
                Thread.Sleep(300); // Немного увеличили задержку для надежности
                if (originalText != null)
                {
                    SetClipboardText(originalText);
                }
                else
                {
                    ClearClipboard();
                }
            });
        }

        private static void SimulateCtrlV()
        {
            // keybd_event работает стабильно и не требует выравнивания структур под 64-битные системы
            keybd_event(VK_CONTROL, 0, 0, 0); // Ctrl Down
            keybd_event(VK_V, 0, 0, 0);       // V Down
            keybd_event(VK_V, 0, KEYEVENTF_KEYUP, 0); // V Up
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, 0); // Ctrl Up
        }

        private static string? GetClipboardText()
        {
            if (!OpenClipboard(IntPtr.Zero)) return null;

            try
            {
                IntPtr hGlobal = GetClipboardData(CF_UNICODETEXT);
                if (hGlobal == IntPtr.Zero) return null;

                IntPtr pText = GlobalLock(hGlobal);
                if (pText == IntPtr.Zero) return null;

                try
                {
                    return Marshal.PtrToStringUni(pText);
                }
                finally
                {
                    GlobalUnlock(hGlobal);
                }
            }
            finally
            {
                CloseClipboard();
            }
        }

        private static bool SetClipboardText(string text)
        {
            if (!OpenClipboard(IntPtr.Zero)) return false;

            try
            {
                EmptyClipboard();

                int bytesCount = (text.Length + 1) * 2;
                IntPtr hGlobal = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytesCount);
                if (hGlobal == IntPtr.Zero) return false;

                IntPtr pText = GlobalLock(hGlobal);
                if (pText == IntPtr.Zero)
                {
                    GlobalFree(hGlobal);
                    return false;
                }

                try
                {
                    var chars = text.ToCharArray();
                    Marshal.Copy(chars, 0, pText, chars.Length);
                    Marshal.WriteInt16(pText, chars.Length * 2, 0);
                }
                finally
                {
                    GlobalUnlock(hGlobal);
                }

                if (SetClipboardData(CF_UNICODETEXT, hGlobal) == IntPtr.Zero)
                {
                    GlobalFree(hGlobal);
                    return false;
                }

                return true;
            }
            finally
            {
                CloseClipboard();
            }
        }

        private static void ClearClipboard()
        {
            if (!OpenClipboard(IntPtr.Zero)) return;
            try
            {
                EmptyClipboard();
            }
            finally
            {
                CloseClipboard();
            }
        }
    }
}
