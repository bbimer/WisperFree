using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FreeFlowWin.Core.Hooks
{
    public class KeyboardHook : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private readonly LowLevelKeyboardProc _proc;
        private IntPtr _hookId = IntPtr.Zero;

        public event Action<int>? KeyDown;
        public event Action<int>? KeyUp;

        public KeyboardHook()
        {
            _proc = HookCallback;
        }

        public void Start()
        {
            if (_hookId == IntPtr.Zero)
            {
                _hookId = SetHook(_proc);
                if (_hookId == IntPtr.Zero)
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    throw new System.ComponentModel.Win32Exception(errorCode, $"Failed to install low-level keyboard hook. Win32 Error: {errorCode}");
                }
            }
        }

        public void Stop()
        {
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            // GetModuleHandle(null) возвращает дескриптор текущего исполняемого файла (.exe),
            // что гарантирует корректную регистрацию глобального хука в среде .NET Core.
            return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(null), 0);
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// VK-код клавиши, которую хук должен «съедать».
        /// По умолчанию 120 = F9.
        /// </summary>
        public int SuppressedKey { get; set; } = 120;
        public bool NeedCtrl { get; set; } = false;
        public bool NeedAlt { get; set; } = false;
        public bool NeedShift { get; set; } = false;
        public bool NeedWin { get; set; } = false;

        private bool IsModifierPressed(int vk)
        {
            return (GetAsyncKeyState(vk) & 0x8000) != 0;
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                if (vkCode == SuppressedKey)
                {
                    bool ctrlPressed = IsModifierPressed(0x11); // VK_CONTROL
                    bool altPressed = IsModifierPressed(0x12);  // VK_MENU (Alt)
                    bool shiftPressed = IsModifierPressed(0x10); // VK_SHIFT
                    bool winPressed = IsModifierPressed(0x5B) || IsModifierPressed(0x5C); // VK_LWIN / VK_RWIN

                    bool matchesModifiers = (ctrlPressed == NeedCtrl) &&
                                            (altPressed == NeedAlt) &&
                                            (shiftPressed == NeedShift) &&
                                            (winPressed == NeedWin);

                    if (matchesModifiers)
                    {
                        if (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN)
                        {
                            KeyDown?.Invoke(vkCode);
                        }
                        else if (wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP)
                        {
                            KeyUp?.Invoke(vkCode);
                        }
                        return (IntPtr)1;
                    }
                }
            }
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);
    }
}
