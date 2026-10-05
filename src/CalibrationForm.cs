using System;
using System.Drawing;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Forms;

namespace TranslatorShortcut
{
    // 보정 안내 창. 크롬을 가리지 않도록 화면 오른쪽 아래에 항상 위로 띄운다.
    internal sealed class CalibrationForm : Form
    {
        private const string IntroText =
            "크롬에서 영어 페이지처럼 외국어 페이지를 열어 두세요. 번역 창이 떠 있으면 먼저 닫아 주세요.\n\n" +
            "[시작]을 누르고 이 창의 안내에 따라 크롬을 직접 클릭하면, 클릭한 화면 요소를 기록합니다. " +
            "보정하는 동안 페이지가 한 번 번역됐다가 원문으로 돌아옵니다.";

        private readonly ChromeProfile baseProfile;
        private readonly IntPtr browserWindow;
        private readonly Label stepLabel;
        private readonly Label instructionLabel;
        private readonly Label statusLabel;
        private readonly ListBox learnedList;
        private readonly Button startButton;
        private readonly Button skipButton;
        private readonly Button restartButton;
        private readonly Button saveButton;

        private CalibrationSession session;
        private MouseClickHook hook;
        private ClickInspector inspector;
        private bool verifying;

        public CalibrationForm(ChromeProfile baseProfile, IntPtr browserWindow)
        {
            this.baseProfile = baseProfile;
            this.browserWindow = browserWindow;

            Text = "크롬 화면 보정 - " + AppInfo.Name;
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.Font;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(12);

            stepLabel = new Label { AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(0, 0, 0, 4) };
            // 첫 안내는 보통 글씨로, 단계별 지시는 굵은 큰 글씨로 보여 준다.
            instructionLabel = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(420, 0),
                Margin = new Padding(0, 0, 0, 8),
                Text = IntroText,
            };
            statusLabel = new Label { AutoSize = true, MaximumSize = new Size(420, 0), Margin = new Padding(0, 0, 0, 8) };
            learnedList = new ListBox { Width = 420, Height = 120, IntegralHeight = false, Margin = new Padding(0, 0, 0, 12), Visible = false };

            startButton = new Button { Text = "시작", AutoSize = true };
            startButton.Click += delegate { Start(); };
            skipButton = new Button { Text = "메뉴 단계 건너뛰기", AutoSize = true, Visible = false };
            skipButton.Click += delegate { session.SkipMenuSteps(); ShowStep(); };
            restartButton = new Button { Text = "처음부터", AutoSize = true, Visible = false };
            restartButton.Click += delegate { Start(); };
            saveButton = new Button { Text = "저장", AutoSize = true, Visible = false };
            saveButton.Click += delegate { Save(); };
            var cancelButton = new Button { Text = "취소", DialogResult = DialogResult.Cancel, AutoSize = true };
            CancelButton = cancelButton;

            var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = Padding.Empty };
            buttons.Controls.AddRange(new Control[] { cancelButton, saveButton, startButton, restartButton, skipButton });

            var layout = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Dock = DockStyle.Fill };
            layout.Controls.AddRange(new Control[] { stepLabel, instructionLabel, statusLabel, learnedList, buttons });
            Controls.Add(layout);

            Load += delegate
            {
                Rectangle area = Screen.FromHandle(browserWindow).WorkingArea;
                Location = new Point(area.Right - Width - 16, area.Bottom - Height - 16);
            };
            FormClosed += delegate { StopCapture(); };
        }

        public ChromeProfile Profile { get; private set; }

        private void Start()
        {
            StopCapture();
            session = new CalibrationSession(baseProfile);
            verifying = false;
            try
            {
                inspector = new ClickInspector(snapshot => PostToForm(delegate { OnClick(snapshot); }));
                hook = new MouseClickHook(inspector.Enqueue);
            }
            catch (Exception ex)
            {
                Log.Write("보정용 마우스 훅 설치 실패: " + ex);
                SetStatus("마우스 클릭을 감지하지 못해 보정을 시작할 수 없습니다: " + ex.Message, true);
                return;
            }
            startButton.Visible = false;
            restartButton.Visible = true;
            learnedList.Visible = true;
            instructionLabel.Font = new Font(Font.FontFamily, Font.Size + 1.5f, FontStyle.Bold);
            SetStatus(string.Empty, false);
            ShowStep();
        }

        private void StopCapture()
        {
            if (hook != null) hook.Dispose();
            if (inspector != null) inspector.Dispose();
            hook = null;
            inspector = null;
        }

        private void OnClick(ClickSnapshot click)
        {
            if (IsDisposed || session == null || verifying || session.Step == CalibrationStep.Done) return;

            CalibrationStep step = session.Step;
            string message;
            if (!session.Accept(click, out message))
            {
                if (message != null) SetStatus(message, true);
                ShowStep();
                return;
            }

            SetStatus(string.Empty, false);
            if (step == CalibrationStep.CloseButton) VerifyBubbleClosed();
            else if (step == CalibrationStep.MenuItem) VerifyMenuItemOpensBubble();
            ShowStep();
        }

        // 닫기 버튼 대신 '번역 옵션' 같은 다른 버튼을 눌렀는지 확인한다.
        private void VerifyBubbleClosed()
        {
            RunVerification(
                ui => Wait.Until(() => ui.FindBubble() == null),
                CalibrationStep.CloseButton,
                "번역 창이 닫히지 않았습니다. 번역 창이 떠 있는 상태에서 닫기(X) 버튼을 클릭하세요.");
        }

        // 메뉴 항목이 정말 번역 창을 여는지 확인하고, 기록한 닫기 버튼으로 닫아 본다.
        private void VerifyMenuItemOpensBubble()
        {
            RunVerification(
                ui =>
                {
                    AutomationElement bubble = Wait.For(ui.FindBubble);
                    if (bubble == null) return false;
                    ui.CloseBubble(bubble);
                    return Wait.Until(() => ui.FindBubble() == null);
                },
                CalibrationStep.AppMenuButton,
                "번역 창이 열리지 않았거나, 기록한 닫기 버튼으로 닫히지 않았습니다. 메뉴 버튼(⋮)부터 다시 해 주세요.");
        }

        private void RunVerification(Func<ChromeUi, bool> check, CalibrationStep retryStep, string failureMessage)
        {
            verifying = true;
            ChromeProfile profile = session.Profile.Clone();
            IntPtr window = session.BrowserWindow;
            SetStatus("확인하는 중…", false);
            ThreadPool.QueueUserWorkItem(delegate
            {
                bool ok;
                try
                {
                    ok = check(new ChromeUi(window, profile));
                }
                catch (Exception ex)
                {
                    Log.Write("보정 확인 중 오류: " + ex);
                    ok = false;
                }
                PostToForm(delegate
                {
                    verifying = false;
                    if (ok)
                    {
                        SetStatus(string.Empty, false);
                    }
                    else
                    {
                        session.ReturnTo(retryStep);
                        SetStatus(failureMessage, true);
                    }
                    ShowStep();
                });
            });
        }

        // 작업 스레드에서 UI 스레드로 넘긴다. 그 사이 창이 닫혔으면 버린다.
        private void PostToForm(MethodInvoker action)
        {
            try
            {
                if (!IsDisposed) BeginInvoke(action);
            }
            catch (InvalidOperationException)
            {
                // 창이 닫히는 중
            }
        }

        private void ShowStep()
        {
            CalibrationStep step = session.Step;
            bool done = step == CalibrationStep.Done && !verifying;
            stepLabel.Text = step == CalibrationStep.Done ? "완료" : "단계 " + ((int)step + 1) + " / 6";
            instructionLabel.Text = verifying ? "잠시 기다려 주세요." : CalibrationSession.Instruction(step);
            skipButton.Visible = step == CalibrationStep.AppMenuButton || step == CalibrationStep.MenuItem;
            saveButton.Visible = done;
            if (done) StopCapture();

            learnedList.Items.Clear();
            foreach (string line in session.Learned) learnedList.Items.Add(line);
        }

        private void SetStatus(string message, bool isError)
        {
            statusLabel.Text = message;
            statusLabel.ForeColor = isError ? Color.Firebrick : SystemColors.GrayText;
        }

        private void Save()
        {
            ChromeProfile profile = session.Profile;
            profile.CalibratedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            profile.CalibratedChromeVersion = ChromeWindow.GetVersion(session.BrowserWindow);
            try
            {
                profile.Save();
            }
            catch (Exception ex)
            {
                Log.Write("인식 정보 저장 실패: " + ex.Message);
                SetStatus("저장하지 못했습니다: " + ex.Message, true);
                return;
            }
            Log.Write("보정 완료 (크롬 " + profile.CalibratedChromeVersion + "): " + string.Join(" / ", session.Learned));
            Profile = profile;
            DialogResult = DialogResult.OK;
        }
    }
}
