using System;
using System.Threading;
using System.Windows.Forms;

namespace TranslatorShortcut
{
    internal static class AppInfo
    {
        public const string Name = "번역 단축키";
        public const string SupportUrl = "https://cgcg.review/support/";

        public static string DataDir
        {
            get
            {
                return System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TranslatorShortcut");
            }
        }
    }

    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            bool createdNew;
            using (var mutex = new Mutex(true, @"Local\TranslatorShortcut", out createdNew))
            using (var showSettings = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\TranslatorShortcut.ShowSettings"))
            {
                // 이미 실행 중이면 실행 중인 프로그램의 환경설정 창을 연다.
                // Windows 11은 트레이 아이콘을 숨김 영역에 넣으므로, 프로그램을 다시 실행하는 것이 찾기 쉬운 입구가 된다.
                if (!createdNew)
                {
                    NativeMethods.AllowSetForegroundWindow(NativeMethods.ASFW_ANY);
                    showSettings.Set();
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                bool startedAtLogin = Array.IndexOf(args, Startup.Argument) >= 0;
                Application.Run(new TrayApp(startedAtLogin, showSettings));
            }
        }
    }
}
