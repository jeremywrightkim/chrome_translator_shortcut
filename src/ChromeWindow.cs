using System;
using System.Diagnostics;

namespace TranslatorShortcut
{
    // 맨 앞 창이 크롬 브라우저 창인지 판별한다.
    // VS Code 같은 Electron 앱도 창 클래스가 같으므로 프로세스 이름까지 확인한다.
    internal static class ChromeWindow
    {
        private const string WindowClass = "Chrome_WidgetWin_1";
        private const string ProcessName = "chrome";

        private static uint cachedPid;
        private static bool cachedIsChrome;
        private static IntPtr lastBrowserWindow;

        // 크롬 창이면 그 최상위 창 핸들을, 아니면 IntPtr.Zero를 돌려준다.
        // 번역 창이나 메뉴 같은 크롬 팝업에 포커스가 있어도 소유자인 브라우저 창을 찾는다.
        // UI 스레드에서만 호출한다.
        public static IntPtr GetForeground()
        {
            IntPtr hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return IntPtr.Zero;

            IntPtr owner = NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOTOWNER);
            if (owner != IntPtr.Zero) hwnd = owner;

            if (NativeMethods.GetClassName(hwnd) != WindowClass) return IntPtr.Zero;

            uint pid;
            NativeMethods.GetWindowThreadProcessId(hwnd, out pid);
            if (pid != cachedPid)
            {
                cachedPid = pid;
                cachedIsChrome = IsChromeProcess((int)pid);
            }
            if (!cachedIsChrome) return IntPtr.Zero;

            lastBrowserWindow = hwnd;
            return hwnd;
        }

        // 환경설정 창이 떠 있어 크롬이 맨 앞이 아닐 때 검사·보정할 크롬 창.
        // 마지막으로 쓰던 크롬 창이 있으면 그 창을, 없으면 실행 중인 아무 크롬 창을 돌려준다. UI 스레드에서만 호출한다.
        public static IntPtr FindBrowserWindow()
        {
            if (lastBrowserWindow != IntPtr.Zero && NativeMethods.IsWindow(lastBrowserWindow)) return lastBrowserWindow;

            IntPtr found = IntPtr.Zero;
            foreach (Process process in Process.GetProcessesByName(ProcessName))
            {
                using (process)
                {
                    IntPtr hwnd = process.MainWindowHandle;
                    if (found == IntPtr.Zero && hwnd != IntPtr.Zero && NativeMethods.GetClassName(hwnd) == WindowClass) found = hwnd;
                }
            }
            return found;
        }

        // 화면 좌표에 보이는 크롬 창 중 맨 위 창. 번역 창·메뉴 같은 크롬 팝업도 포함한다. 어느 스레드에서나 쓸 수 있다.
        public static IntPtr FindTopmostAt(System.Drawing.Point point)
        {
            IntPtr found = IntPtr.Zero;
            NativeMethods.EnumWindows((hwnd, lParam) =>
            {
                NativeMethods.RECT rect;
                if (!NativeMethods.IsWindowVisible(hwnd) || NativeMethods.IsIconic(hwnd) || NativeMethods.IsCloaked(hwnd)) return true;
                if (!NativeMethods.GetWindowRect(hwnd, out rect)) return true;
                if (point.X < rect.Left || point.X >= rect.Right || point.Y < rect.Top || point.Y >= rect.Bottom) return true;
                if (NativeMethods.GetClassName(hwnd) != WindowClass) return true;

                uint pid;
                NativeMethods.GetWindowThreadProcessId(hwnd, out pid);
                if (!IsChromeProcess((int)pid)) return true;
                found = hwnd;
                return false;
            }, IntPtr.Zero);
            return found;
        }

        public static bool IsChromeProcess(int pid)
        {
            try
            {
                using (Process process = Process.GetProcessById(pid))
                {
                    return string.Equals(process.ProcessName, ProcessName, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (ArgumentException)
            {
                // 그 사이 프로세스가 종료됨
                return false;
            }
        }

        // 크롬 업데이트로 화면 구조가 바뀌었는지 판단하는 근거로 로그와 검사 결과에 남긴다.
        public static string GetVersion(IntPtr hwnd)
        {
            uint pid;
            NativeMethods.GetWindowThreadProcessId(hwnd, out pid);
            try
            {
                using (Process process = Process.GetProcessById((int)pid))
                {
                    return process.MainModule.FileVersionInfo.ProductVersion;
                }
            }
            catch (Exception ex)
            {
                return "알 수 없음 (" + ex.Message + ")";
            }
        }
    }
}
