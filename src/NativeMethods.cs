using System;
using System.Runtime.InteropServices;
using System.Text;

namespace TranslatorShortcut
{
    internal static class NativeMethods
    {
        public const int WM_HOTKEY = 0x0312;

        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_NOREPEAT = 0x4000;

        public const uint GA_ROOTOWNER = 3;

        public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        public const uint WINEVENT_OUTOFCONTEXT = 0x0000;

        private const int VK_SHIFT = 0x10;
        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12;

        public delegate void WinEventDelegate(
            IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild,
            uint dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern IntPtr SetWinEventHook(
            uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc,
            uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        public static extern bool IsWindow(IntPtr hWnd);

        public const int ASFW_ANY = -1;

        // 두 번째로 실행된 프로그램이, 이미 실행 중인 프로그램의 창이 앞으로 나올 수 있게 허용한다.
        [DllImport("user32.dll")]
        public static extern bool AllowSetForegroundWindow(int dwProcessId);

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        // 최상위 창을 맨 위(Z 순서)부터 차례로 넘겨준다.
        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        private const int DWMWA_CLOAKED = 14;

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

        // 다른 가상 데스크톱에 있는 창처럼, 보이는 상태지만 화면에는 그려지지 않는 창
        public static bool IsCloaked(IntPtr hwnd)
        {
            int cloaked;
            return DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out cloaked, sizeof(int)) == 0 && cloaked != 0;
        }

        // 보정 중 마우스 클릭 위치를 알아내는 저수준 마우스 훅
        public const int WH_MOUSE_LL = 14;
        public const int WM_LBUTTONDOWN = 0x0201;

        public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct MSLLHOOKSTRUCT
        {
            public int X;
            public int Y;
            public uint MouseData;
            public uint Flags;
            public uint Time;
            public IntPtr ExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        // 훅이 주는 좌표는 물리 픽셀이다. 이 좌표로 UI Automation 요소를 찾는 스레드를
        // 모니터별 DPI 인식으로 맞춰야 화면 배율이 다른 모니터에서도 정확하다. (Windows 10 1607 이상)
        private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new IntPtr(-4);

        [DllImport("user32.dll")]
        private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

        public static void UsePerMonitorDpiOnThisThread()
        {
            try
            {
                SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
            }
            catch (EntryPointNotFoundException)
            {
                // 이전 Windows: 시스템 DPI 기준으로 동작
            }
        }

        public static bool AnyModifierPressed()
        {
            return (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0 ||
                   (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0 ||
                   (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
        }

        public static string GetClassName(IntPtr hwnd)
        {
            var buffer = new StringBuilder(256);
            return GetClassName(hwnd, buffer, buffer.Capacity) > 0 ? buffer.ToString() : string.Empty;
        }
    }
}
