using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace TranslatorShortcut
{
    internal sealed class TrayApp : ApplicationContext
    {
        private const int HotkeyId = 1;
        private const int TestHotkeyId = 2;
        private const int ModifierWaitTimeoutMs = 1500;

        private readonly Settings settings;
        private readonly HotkeySink sink;
        private readonly NotifyIcon tray;
        private readonly ToolStripMenuItem hotkeyLabel;
        private readonly NativeMethods.WinEventDelegate winEventProc;
        private readonly IntPtr winEventHook;
        private readonly System.Windows.Forms.Timer foregroundTimer;
        private readonly RegisteredWaitHandle showSettingsWait;

        // 작업 스레드는 단축키를 누른 시점의 값을 받아 쓰고, 바뀔 때는 객체를 통째로 바꾼다.
        private ChromeProfile profile;
        private bool hotkeyRegistered;
        private bool conflictWarned;
        private bool busy;
        private SettingsForm settingsForm;

        public TrayApp(bool startedAtLogin, EventWaitHandle showSettings)
        {
            settings = Settings.Load();
            profile = ChromeProfile.Load();

            sink = new HotkeySink();
            sink.HotkeyPressed += OnHotkeyPressed;

            hotkeyLabel = new ToolStripMenuItem { Enabled = false };
            var settingsItem = new ToolStripMenuItem("환경설정…", null, OnOpenSettings);
            settingsItem.Font = new Font(settingsItem.Font, FontStyle.Bold);

            var menu = new ContextMenuStrip();
            menu.Items.Add(hotkeyLabel);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(settingsItem);
            menu.Items.Add("로그 폴더 열기", null, OnOpenLogFolder);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("종료", null, OnExit);

            tray = new NotifyIcon
            {
                Icon = LoadTrayIcon(),
                ContextMenuStrip = menu,
                Visible = true,
            };
            tray.DoubleClick += OnOpenSettings;
            UpdateHotkeyText();

            // 프로그램을 다시 실행하면 환경설정 창을 연다 (Program.Main 참고).
            showSettingsWait = ThreadPool.RegisterWaitForSingleObject(showSettings, delegate
            {
                sink.BeginInvoke((MethodInvoker)delegate { OnOpenSettings(this, EventArgs.Empty); });
            }, null, Timeout.Infinite, false);

            // 크롬이 맨 앞에 있을 때만 단축키를 등록해서, 다른 프로그램의 같은 단축키를 막지 않는다.
            winEventProc = OnWinEvent;
            winEventHook = NativeMethods.SetWinEventHook(
                NativeMethods.EVENT_SYSTEM_FOREGROUND, NativeMethods.EVENT_SYSTEM_FOREGROUND, IntPtr.Zero,
                winEventProc, 0, 0, NativeMethods.WINEVENT_OUTOFCONTEXT);
            // 이벤트만으로는 상태가 어긋난 채 남을 수 있다. 예를 들어 시작 순간 맨 앞 창이 잠깐 다른 창이었다가
            // 크롬으로 돌아오면 이벤트가 다시 오지 않아 단축키가 등록되지 않는다. 그래서 주기적으로도 확인한다.
            foregroundTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            foregroundTimer.Tick += delegate { UpdateHotkeyRegistration(); };
            foregroundTimer.Start();
            UpdateHotkeyRegistration();

            if (!startedAtLogin)
            {
                tray.ShowBalloonTip(
                    4000, AppInfo.Name,
                    "크롬에서 " + Hotkeys.ToDisplayString(settings.Hotkey) + "를 누르면 페이지 번역이 켜지고 꺼집니다.",
                    ToolTipIcon.Info);
            }
        }

        private void OnWinEvent(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
        {
            UpdateHotkeyRegistration();
        }

        private void UpdateHotkeyRegistration()
        {
            bool shouldRegister = ChromeWindow.GetForeground() != IntPtr.Zero;
            if (shouldRegister == hotkeyRegistered) return;

            if (!shouldRegister)
            {
                NativeMethods.UnregisterHotKey(sink.Handle, HotkeyId);
                hotkeyRegistered = false;
                return;
            }

            uint modifiers, virtualKey;
            Hotkeys.ToNative(settings.Hotkey, out modifiers, out virtualKey);
            hotkeyRegistered = NativeMethods.RegisterHotKey(
                sink.Handle, HotkeyId, modifiers | NativeMethods.MOD_NOREPEAT, virtualKey);
            if (!hotkeyRegistered && !conflictWarned)
            {
                conflictWarned = true;
                Log.Write("단축키 등록 실패: " + Hotkeys.ToDisplayString(settings.Hotkey));
                tray.ShowBalloonTip(
                    5000, AppInfo.Name,
                    Hotkeys.ToDisplayString(settings.Hotkey) + "를 다른 프로그램이 이미 쓰고 있습니다. 환경설정에서 단축키를 바꿔 주세요.",
                    ToolTipIcon.Warning);
            }
        }

        private void OnHotkeyPressed(object sender, EventArgs e)
        {
            IntPtr browser = ChromeWindow.GetForeground();
            if (browser == IntPtr.Zero)
            {
                UpdateHotkeyRegistration();
                return;
            }
            if (busy) return;

            busy = true;
            ChromeProfile currentProfile = profile;
            ThreadPool.QueueUserWorkItem(delegate
            {
                bool toggled;
                try
                {
                    WaitForModifierRelease();
                    toggled = ChromeTranslator.Toggle(browser, currentProfile);
                }
                catch (Exception ex)
                {
                    Log.Write("번역 전환 중 오류: " + ex);
                    toggled = false;
                }
                if (!toggled) Log.Write("번역 전환 실패 (크롬 " + ChromeWindow.GetVersion(browser) + ")");

                sink.BeginInvoke((MethodInvoker)delegate
                {
                    busy = false;
                    if (!toggled)
                    {
                        tray.ShowBalloonTip(
                            3000, AppInfo.Name, "번역을 전환하지 못했습니다. 환경설정의 [호환성 검사]로 원인을 확인해 주세요.", ToolTipIcon.Warning);
                    }
                });
            });
        }

        // 단축키를 누른 손가락이 아직 Alt 등을 누르고 있으면, 그 키를 뗄 때의 입력이
        // 막 열린 크롬 메뉴나 번역 창을 닫아 버린다. 키를 다 뗄 때까지 기다렸다가 조작한다.
        private static void WaitForModifierRelease()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (NativeMethods.AnyModifierPressed() && stopwatch.ElapsedMilliseconds < ModifierWaitTimeoutMs)
            {
                Thread.Sleep(10);
            }
            // 크롬이 남은 키 입력을 처리할 시간
            Thread.Sleep(50);
        }

        private void OnOpenSettings(object sender, EventArgs e)
        {
            if (settingsForm != null)
            {
                settingsForm.Activate();
                return;
            }

            DialogResult answer;
            Keys selectedHotkey;
            bool startupEnabled;
            using (settingsForm = new SettingsForm(settings.Hotkey, Startup.IsEnabled, profile, IsHotkeyAvailable))
            {
                answer = settingsForm.ShowDialog();
                selectedHotkey = settingsForm.Hotkey;
                startupEnabled = settingsForm.StartupEnabled;
            }
            settingsForm = null;

            // 보정·기본값 복원·파일 직접 수정은 [취소]와 상관없이 파일에 반영되어 있다.
            profile = ChromeProfile.Load();
            if (answer != DialogResult.OK) return;

            if (selectedHotkey != settings.Hotkey) ChangeHotkey(selectedHotkey);
            if (startupEnabled != Startup.IsEnabled)
            {
                try
                {
                    Startup.SetEnabled(startupEnabled);
                }
                catch (Exception ex)
                {
                    Log.Write("자동 실행 설정 실패: " + ex.Message);
                    MessageBox.Show("자동 실행을 설정하지 못했습니다.\n" + ex.Message, AppInfo.Name,
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void ChangeHotkey(Keys hotkey)
        {
            if (hotkeyRegistered) NativeMethods.UnregisterHotKey(sink.Handle, HotkeyId);
            hotkeyRegistered = false;
            conflictWarned = false;
            settings.Hotkey = hotkey;
            try
            {
                settings.Save();
            }
            catch (Exception ex)
            {
                Log.Write("설정 저장 실패: " + ex.Message);
            }
            UpdateHotkeyText();
            UpdateHotkeyRegistration();
        }

        private bool IsHotkeyAvailable(Keys hotkey)
        {
            uint modifiers, virtualKey;
            Hotkeys.ToNative(hotkey, out modifiers, out virtualKey);
            if (!NativeMethods.RegisterHotKey(sink.Handle, TestHotkeyId, modifiers, virtualKey)) return false;
            NativeMethods.UnregisterHotKey(sink.Handle, TestHotkeyId);
            return true;
        }

        private void UpdateHotkeyText()
        {
            string hotkey = Hotkeys.ToDisplayString(settings.Hotkey);
            hotkeyLabel.Text = "크롬 번역 켜기/끄기: " + hotkey;
            tray.Text = AppInfo.Name + " (" + hotkey + ")";
        }

        private void OnOpenLogFolder(object sender, EventArgs e)
        {
            System.IO.Directory.CreateDirectory(AppInfo.DataDir);
            Process.Start("explorer.exe", "\"" + AppInfo.DataDir + "\"");
        }

        private void OnExit(object sender, EventArgs e)
        {
            ExitThread();
        }

        protected override void ExitThreadCore()
        {
            foregroundTimer.Stop();
            showSettingsWait.Unregister(null);
            if (winEventHook != IntPtr.Zero) NativeMethods.UnhookWinEvent(winEventHook);
            if (hotkeyRegistered) NativeMethods.UnregisterHotKey(sink.Handle, HotkeyId);
            tray.Visible = false;
            tray.Dispose();
            sink.Dispose();
            base.ExitThreadCore();
        }

        private static Icon LoadTrayIcon()
        {
            using (var stream = typeof(TrayApp).Assembly.GetManifestResourceStream("TranslatorShortcut.app.ico"))
            {
                return new Icon(stream, SystemInformation.SmallIconSize);
            }
        }

        // WM_HOTKEY를 받고, 작업 스레드의 결과를 UI 스레드로 넘기는 보이지 않는 컨트롤
        private sealed class HotkeySink : Control
        {
            public HotkeySink()
            {
                CreateHandle();
            }

            public event EventHandler HotkeyPressed;

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == NativeMethods.WM_HOTKEY && (int)m.WParam == HotkeyId)
                {
                    EventHandler handler = HotkeyPressed;
                    if (handler != null) handler(this, EventArgs.Empty);
                    return;
                }
                base.WndProc(ref m);
            }
        }
    }
}
