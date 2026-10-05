using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace TranslatorShortcut
{
    // 환경설정: 단축키, 자동 실행, 크롬 인식(호환성 검사·보정·기본값 복원)
    // 단축키와 자동 실행은 [확인]을 눌러야 적용되고, 보정과 기본값 복원은 바로 파일에 저장된다.
    internal sealed class SettingsForm : Form
    {
        private const string HotkeyHint = "입력 칸을 누른 뒤 새 단축키를 누르세요. Ctrl 또는 Alt를 함께 누르거나 F1~F24 키를 쓸 수 있습니다.";

        private readonly Func<Keys, bool> isHotkeyAvailable;
        private readonly Keys originalHotkey;
        private readonly HotkeyBox hotkeyBox;
        private readonly Label hotkeyHint;
        private readonly CheckBox startupCheck;
        private readonly Label profileLabel;
        private readonly Button checkButton;
        private readonly RichTextBox checkResults;
        private readonly Button okButton;
        private ChromeProfile profile;

        public SettingsForm(Keys hotkey, bool startupEnabled, ChromeProfile profile, Func<Keys, bool> isHotkeyAvailable)
        {
            this.profile = profile;
            this.isHotkeyAvailable = isHotkeyAvailable;
            originalHotkey = hotkey;

            Text = "환경설정 - " + AppInfo.Name;
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.Font;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(12);

            // 단축키
            hotkeyBox = new HotkeyBox { Width = 220, Margin = new Padding(0, 0, 8, 0) };
            var defaultHotkeyButton = new Button { Text = "기본값 (" + Hotkeys.ToDisplayString(Hotkeys.Default) + ")", AutoSize = true };
            defaultHotkeyButton.Click += delegate { hotkeyBox.Hotkey = Hotkeys.Default; };
            hotkeyHint = new Label { AutoSize = true, MaximumSize = new Size(520, 0), Margin = new Padding(0, 6, 0, 0) };
            hotkeyBox.HotkeyChanged += delegate { UpdateHotkeyHint(); };
            hotkeyBox.Hotkey = hotkey;
            var hotkeyRow = Row(hotkeyBox, defaultHotkeyButton);
            var hotkeyGroup = Group("크롬 번역 켜기/끄기 단축키", hotkeyRow, hotkeyHint);

            // 시작
            startupCheck = new CheckBox { Text = "Windows 시작 시 자동 실행", AutoSize = true, Checked = startupEnabled };
            var startupGroup = Group("시작", startupCheck);

            // 크롬 인식
            profileLabel = new Label { AutoSize = true, MaximumSize = new Size(520, 0), Margin = new Padding(0, 0, 0, 8) };
            checkButton = new Button { Text = "호환성 검사", AutoSize = true };
            checkButton.Click += delegate { RunCheck(); };
            var calibrateButton = new Button { Text = "보정…", AutoSize = true };
            calibrateButton.Click += delegate { Calibrate(); };
            var resetButton = new Button { Text = "기본값으로 되돌리기", AutoSize = true };
            resetButton.Click += delegate { ResetProfile(); };
            var openFileButton = new Button { Text = "인식 정보 파일 열기", AutoSize = true };
            openFileButton.Click += delegate { OpenProfileFile(); };
            checkResults = new RichTextBox
            {
                ReadOnly = true,
                BackColor = SystemColors.Window,
                Width = 520,
                Height = 250,
                Margin = new Padding(0, 8, 0, 4),
                Text = "[호환성 검사]를 누르면 마지막으로 쓰던 크롬 창을 읽어서 번역 단축키가 동작할 조건을 확인합니다. 크롬을 조작하지는 않습니다.",
            };
            var checkNote = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(520, 0),
                ForeColor = SystemColors.GrayText,
                Text = "번역 창과 메뉴 항목은 열어야 보이므로 검사하지 않습니다. 단축키가 동작하지 않으면 [보정]으로 크롬 화면을 다시 인식시켜 주세요.",
            };
            var chromeGroup = Group("크롬 인식", profileLabel,
                Row(checkButton, calibrateButton, resetButton, openFileButton), checkResults, checkNote);
            UpdateProfileLabel();

            // 확인 / 취소
            okButton = new Button { Text = "확인", AutoSize = true };
            okButton.Click += delegate { Accept(); };
            var cancelButton = new Button { Text = "취소", DialogResult = DialogResult.Cancel, AutoSize = true };
            AcceptButton = okButton;
            CancelButton = cancelButton;
            var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = Padding.Empty };
            buttons.Controls.AddRange(new Control[] { cancelButton, okButton });

            var layout = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Dock = DockStyle.Fill };
            layout.Controls.AddRange(new Control[] { hotkeyGroup, startupGroup, chromeGroup, buttons });
            Controls.Add(layout);
        }

        public Keys Hotkey
        {
            get { return hotkeyBox.Hotkey; }
        }

        public bool StartupEnabled
        {
            get { return startupCheck.Checked; }
        }

        private static GroupBox Group(string title, params Control[] contents)
        {
            var inner = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                Dock = DockStyle.Fill,
                Padding = new Padding(4),
            };
            inner.Controls.AddRange(contents);
            var group = new GroupBox { Text = title, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 10) };
            group.Controls.Add(inner);
            return group;
        }

        private static FlowLayoutPanel Row(params Control[] contents)
        {
            var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
            row.Controls.AddRange(contents);
            return row;
        }

        private void UpdateHotkeyHint()
        {
            hotkeyHint.Text = hotkeyBox.IsValid ? HotkeyHint : "쓸 수 없는 조합입니다. " + HotkeyHint;
            hotkeyHint.ForeColor = hotkeyBox.IsValid ? SystemColors.GrayText : Color.Firebrick;
        }

        private void UpdateProfileLabel()
        {
            profileLabel.Text = profile.IsCalibrated
                ? "현재 인식 정보: 보정됨 (" + profile.CalibratedAt + ", 크롬 " + profile.CalibratedChromeVersion + ")"
                : "현재 인식 정보: 기본값 (크롬 154, 한국어 기준)";
        }

        private void Accept()
        {
            if (!hotkeyBox.IsValid)
            {
                MessageBox.Show(this, "쓸 수 없는 단축키입니다. " + HotkeyHint, AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (hotkeyBox.Hotkey != originalHotkey && !isHotkeyAvailable(hotkeyBox.Hotkey))
            {
                MessageBox.Show(this,
                    Hotkeys.ToDisplayString(hotkeyBox.Hotkey) + "는 다른 프로그램이 이미 쓰고 있습니다. 다른 조합을 골라 주세요.",
                    AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            DialogResult = DialogResult.OK;
        }

        private void RunCheck()
        {
            IntPtr browser = ChromeWindow.FindBrowserWindow();
            ChromeProfile checkedProfile = profile;
            checkButton.Enabled = false;
            checkResults.Text = "검사하는 중…";
            ThreadPool.QueueUserWorkItem(delegate
            {
                List<CheckResult> results;
                try
                {
                    results = CompatibilityChecker.Run(browser, checkedProfile);
                }
                catch (Exception ex)
                {
                    Log.Write("호환성 검사 중 오류: " + ex);
                    results = new List<CheckResult> { new CheckResult(CheckStatus.Error, "검사", "검사 중 오류가 났습니다: " + ex.Message) };
                }
                try
                {
                    if (!IsDisposed) BeginInvoke((MethodInvoker)delegate { ShowResults(results); });
                }
                catch (InvalidOperationException)
                {
                    // 창이 닫히는 중
                }
            });
        }

        private void ShowResults(List<CheckResult> results)
        {
            checkButton.Enabled = true;
            checkResults.Clear();
            int problems = 0;
            foreach (CheckResult result in results)
            {
                if (result.Status == CheckStatus.Error) problems++;
                AppendLine("[" + StatusText(result.Status) + "] " + result.Item + ": " + result.Detail, StatusColor(result.Status));
            }
            AppendLine(problems == 0 ? "\n문제가 발견되지 않았습니다." : "\n문제 " + problems + "개가 발견되었습니다.",
                problems == 0 ? SystemColors.WindowText : Color.Firebrick);
        }

        private void AppendLine(string text, Color color)
        {
            checkResults.SelectionStart = checkResults.TextLength;
            checkResults.SelectionColor = color;
            checkResults.AppendText(text + "\n");
        }

        private static string StatusText(CheckStatus status)
        {
            switch (status)
            {
                case CheckStatus.Ok: return "정상";
                case CheckStatus.Info: return "참고";
                case CheckStatus.Warning: return "주의";
                default: return "문제";
            }
        }

        private static Color StatusColor(CheckStatus status)
        {
            switch (status)
            {
                case CheckStatus.Ok: return Color.SeaGreen;
                case CheckStatus.Info: return SystemColors.GrayText;
                case CheckStatus.Warning: return Color.DarkOrange;
                default: return Color.Firebrick;
            }
        }

        // 보정하는 동안에는 이 창을 숨겨서 크롬을 가리지 않게 한다.
        private void Calibrate()
        {
            IntPtr browser = ChromeWindow.FindBrowserWindow();
            if (browser == IntPtr.Zero)
            {
                MessageBox.Show(this, "실행 중인 크롬 창을 찾지 못했습니다. 크롬을 연 뒤 다시 시도해 주세요.",
                    AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Hide();
            try
            {
                using (var form = new CalibrationForm(profile, browser))
                {
                    if (form.ShowDialog() == DialogResult.OK)
                    {
                        profile = form.Profile;
                        UpdateProfileLabel();
                        checkResults.Text = "보정한 인식 정보를 저장했습니다. 바로 적용됩니다.";
                    }
                }
            }
            finally
            {
                Show();
                Activate();
            }
        }

        private void ResetProfile()
        {
            DialogResult answer = MessageBox.Show(this, "보정한 인식 정보를 지우고 기본값으로 되돌릴까요?",
                AppInfo.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes) return;
            try
            {
                ChromeProfile.Reset();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "인식 정보 파일을 지우지 못했습니다.\n" + ex.Message, AppInfo.Name,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            profile = ChromeProfile.CreateDefault();
            UpdateProfileLabel();
            checkResults.Text = "기본값으로 되돌렸습니다.";
        }

        // 파일이 없으면 현재 값으로 만들어서 연다. 이름이 바뀐 크롬에 직접 맞출 때 쓴다.
        // 고친 내용은 환경설정 창을 닫을 때 다시 읽어서 적용된다.
        private void OpenProfileFile()
        {
            try
            {
                if (!System.IO.File.Exists(ChromeProfile.FilePath)) profile.Save();
                Process.Start("notepad.exe", "\"" + ChromeProfile.FilePath + "\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "인식 정보 파일을 열지 못했습니다.\n" + ex.Message, AppInfo.Name,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
