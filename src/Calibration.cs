using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;

namespace TranslatorShortcut
{
    // 보정: 사용자가 크롬의 번역 아이콘, 언어 탭, 닫기 버튼, 메뉴를 직접 클릭하면
    // 클릭한 요소의 이름과 클래스 이름을 기록해 ChromeProfile을 만든다.

    internal enum CalibrationStep
    {
        TranslateIcon,
        TargetTab,
        SourceTab,
        CloseButton,
        AppMenuButton,
        MenuItem,
        Done,
    }

    internal sealed class ElementInfo
    {
        public ElementInfo(AutomationElement element)
        {
            AutomationElement.AutomationElementInformation current = element.Current;
            ControlType = current.ControlType;
            ClassName = current.ClassName ?? string.Empty;
            Name = current.Name ?? string.Empty;
        }

        public ControlType ControlType { get; private set; }
        public string ClassName { get; private set; }
        public string Name { get; private set; }

        public override string ToString()
        {
            return Name.Length > 0 ? ClassName + " \"" + Name + "\"" : ClassName;
        }
    }

    // 클릭한 순간의 요소 정보. 닫기 버튼처럼 클릭하면 곧 사라지는 요소가 있어서 바로 기록해 둔다.
    internal sealed class ClickSnapshot
    {
        private ClickSnapshot()
        {
            TabIndex = -1;
        }

        // 클릭한 요소부터 크롬 창까지 거슬러 올라간 경로.
        // [Count-1] = 크롬 창, [Count-2] = BrowserRootView, [Count-3] = NonClientView(툴바·웹페이지) 또는 팝업
        public List<ElementInfo> Chain { get; private set; }
        public IntPtr BrowserWindow { get; private set; }
        // 경로에 탭이 있으면, 그 팝업 안에서 같은 클래스 탭 중 몇 번째인지
        public int TabIndex { get; private set; }
        public int TabCount { get; private set; }

        public ElementInfo RootView
        {
            get { return Chain[Chain.Count - 2]; }
        }

        public ElementInfo Branch
        {
            get { return Chain[Chain.Count - 3]; }
        }

        // 크롬이 아닌 곳을 클릭했으면 null
        public static ClickSnapshot Capture(Point point)
        {
            AutomationElement element = FindChromeElementAt(point);
            if (element == null) return null;

            var elements = new List<AutomationElement>();
            TreeWalker walker = TreeWalker.ControlViewWalker;
            for (AutomationElement e = element; e != null && !e.Equals(AutomationElement.RootElement); e = walker.GetParent(e))
            {
                elements.Add(e);
            }
            if (elements.Count < 3) return null;

            var snapshot = new ClickSnapshot
            {
                Chain = elements.ConvertAll(e => new ElementInfo(e)),
                BrowserWindow = new IntPtr(elements[elements.Count - 1].Current.NativeWindowHandle),
            };

            int tab = snapshot.IndexOf(ControlType.TabItem);
            if (tab >= 0)
            {
                AutomationElement popup = elements[elements.Count - 3];
                AutomationElementCollection tabs = popup.FindAll(TreeScope.Subtree, Uia.ClassIs(snapshot.Chain[tab].ClassName));
                snapshot.TabCount = tabs.Count;
                for (int i = 0; i < tabs.Count; i++)
                {
                    if (Automation.Compare(tabs[i], elements[tab])) snapshot.TabIndex = i;
                }
            }
            return snapshot;
        }

        // 모니터 제어 프로그램 등이 화면 위에 투명한 창을 띄워 두면, 클릭은 그 창을 통과해 크롬으로 가지만
        // FromPoint는 그 투명한 창을 돌려준다. 그때는 그 좌표의 크롬 창에서 직접 내려가며 찾는다.
        private static AutomationElement FindChromeElementAt(Point point)
        {
            var target = new System.Windows.Point(point.X, point.Y);
            AutomationElement element = AutomationElement.FromPoint(target);
            if (element != null && ChromeWindow.IsChromeProcess(element.Current.ProcessId)) return element;

            IntPtr window = ChromeWindow.FindTopmostAt(point);
            return window == IntPtr.Zero ? null : DeepestAt(AutomationElement.FromHandle(window), target);
        }

        // 좌표를 포함하는 자식을 따라 가장 안쪽 요소까지 내려간다. 겹치면 나중 자식(위에 그려진 팝업)을 고른다.
        private static AutomationElement DeepestAt(AutomationElement root, System.Windows.Point target)
        {
            TreeWalker walker = TreeWalker.ControlViewWalker;
            AutomationElement current = root;
            while (true)
            {
                AutomationElement next = null;
                for (AutomationElement child = walker.GetFirstChild(current); child != null; child = walker.GetNextSibling(child))
                {
                    System.Windows.Rect bounds = child.Current.BoundingRectangle;
                    if (!bounds.IsEmpty && bounds.Contains(target)) next = child;
                }
                if (next == null) return current;
                current = next;
            }
        }

        // 클릭한 요소에서 위로 올라가며 처음 만나는 해당 종류 요소의 위치 (팝업·NonClientView 아래에서만)
        public int IndexOf(ControlType type)
        {
            for (int i = 0; i < Chain.Count - 3; i++)
            {
                if (Chain[i].ControlType == type) return i;
            }
            return -1;
        }
    }

    // 저수준 마우스 훅으로 왼쪽 버튼을 누른 위치를 알린다. 클릭은 막지 않고 그대로 크롬에 전달된다.
    internal sealed class MouseClickHook : IDisposable
    {
        private readonly NativeMethods.LowLevelMouseProc proc;
        private readonly Action<Point> onLeftButtonDown;
        private IntPtr hook;

        public MouseClickHook(Action<Point> onLeftButtonDown)
        {
            this.onLeftButtonDown = onLeftButtonDown;
            proc = HookProc;
            hook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, proc, NativeMethods.GetModuleHandle(null), 0);
            if (hook == IntPtr.Zero) throw new System.ComponentModel.Win32Exception();
        }

        private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (int)wParam == NativeMethods.WM_LBUTTONDOWN)
            {
                var info = (NativeMethods.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(NativeMethods.MSLLHOOKSTRUCT));
                onLeftButtonDown(new Point(info.X, info.Y));
            }
            return NativeMethods.CallNextHookEx(hook, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (hook == IntPtr.Zero) return;
            NativeMethods.UnhookWindowsHookEx(hook);
            hook = IntPtr.Zero;
        }
    }

    // 훅 콜백은 빨리 끝나야 하므로, 클릭 위치를 받아 별도 스레드에서 UI Automation으로 요소를 기록한다.
    internal sealed class ClickInspector : IDisposable
    {
        private readonly BlockingCollection<Point> clicks = new BlockingCollection<Point>();
        private readonly Action<ClickSnapshot> onSnapshot;

        // onSnapshot은 작업 스레드에서 호출된다.
        public ClickInspector(Action<ClickSnapshot> onSnapshot)
        {
            this.onSnapshot = onSnapshot;
            new Thread(Run) { IsBackground = true, Name = "ClickInspector" }.Start();
        }

        public void Enqueue(Point point)
        {
            clicks.TryAdd(point);
        }

        private void Run()
        {
            NativeMethods.UsePerMonitorDpiOnThisThread();
            foreach (Point point in clicks.GetConsumingEnumerable())
            {
                try
                {
                    ClickSnapshot snapshot = ClickSnapshot.Capture(point);
                    if (snapshot != null) onSnapshot(snapshot);
                }
                catch (Exception ex)
                {
                    Log.Write("보정 중 클릭한 요소를 읽지 못했습니다: " + ex.Message);
                }
            }
        }

        public void Dispose()
        {
            clicks.CompleteAdding();
        }
    }

    // 단계별로 클릭을 판정하고 인식 정보를 채운다. UI 스레드에서만 쓴다.
    internal sealed class CalibrationSession
    {
        private readonly ChromeProfile profile;
        private readonly SortedDictionary<CalibrationStep, string> learned = new SortedDictionary<CalibrationStep, string>();

        public CalibrationSession(ChromeProfile baseProfile)
        {
            profile = baseProfile.Clone();
            profile.CalibratedAt = null;
            profile.CalibratedChromeVersion = null;
            Step = CalibrationStep.TranslateIcon;
        }

        public CalibrationStep Step { get; private set; }
        public IntPtr BrowserWindow { get; private set; }

        public ChromeProfile Profile
        {
            get { return profile; }
        }

        public IEnumerable<string> Learned
        {
            get { return learned.Values; }
        }

        public static string Instruction(CalibrationStep step)
        {
            switch (step)
            {
                case CalibrationStep.TranslateIcon: return "크롬 주소창 오른쪽의 번역 아이콘을 클릭하세요.";
                case CalibrationStep.TargetTab: return "번역 창에서 '한국어' 탭을 클릭하세요. 페이지가 한국어로 번역됩니다.";
                case CalibrationStep.SourceTab: return "번역 창에서 원래 언어 탭(예: 영어)을 클릭하세요. 페이지가 원문으로 돌아옵니다.";
                case CalibrationStep.CloseButton: return "번역 창 오른쪽 위의 닫기(X) 버튼을 클릭하세요.";
                case CalibrationStep.AppMenuButton: return "크롬 오른쪽 위의 메뉴 버튼(⋮)을 클릭하세요.";
                case CalibrationStep.MenuItem: return "메뉴에서 '번역…'을 클릭하세요.";
                default: return "보정을 마쳤습니다. [저장]을 누르면 적용됩니다.";
            }
        }

        // 클릭 하나를 판정한다. 다음 단계로 넘어가면 true.
        // false일 때 message가 null이면 무시해도 되는 클릭(예: 번역 창을 다시 열려고 아이콘을 누름)이다.
        public bool Accept(ClickSnapshot click, out string message)
        {
            message = null;
            if (Step != CalibrationStep.TranslateIcon && click.BrowserWindow != BrowserWindow)
            {
                message = "처음 보정을 시작한 크롬 창에서 진행해 주세요.";
                return false;
            }

            switch (Step)
            {
                case CalibrationStep.TranslateIcon: return AcceptTranslateIcon(click, out message);
                case CalibrationStep.TargetTab: return AcceptTab(click, true, out message);
                case CalibrationStep.SourceTab: return AcceptTab(click, false, out message);
                case CalibrationStep.CloseButton: return AcceptCloseButton(click, out message);
                case CalibrationStep.AppMenuButton: return AcceptAppMenuButton(click, out message);
                case CalibrationStep.MenuItem: return AcceptMenuItem(click, out message);
                default: return false;
            }
        }

        // 검증에 실패하면 해당 단계부터 다시 한다.
        public void ReturnTo(CalibrationStep step)
        {
            foreach (CalibrationStep later in new List<CalibrationStep>(learned.Keys))
            {
                if (later >= step) learned.Remove(later);
            }
            Step = step;
        }

        public void SkipMenuSteps()
        {
            learned[CalibrationStep.AppMenuButton] = "크롬 메뉴: 건너뜀 (기존 값 유지)";
            Step = CalibrationStep.Done;
        }

        private bool AcceptTranslateIcon(ClickSnapshot click, out string message)
        {
            message = null;
            int index = click.IndexOf(ControlType.Button);
            if (index < 0)
            {
                message = "버튼이 아닌 곳을 클릭했습니다. 주소창 오른쪽의 번역 아이콘을 클릭하세요.";
                return false;
            }

            ElementInfo icon = click.Chain[index];
            profile.RootViewClass = click.RootView.ClassName;
            profile.NonClientClass = click.Branch.ClassName;
            profile.TranslateIconClass = icon.ClassName;
            profile.TranslateIconName = icon.Name;
            BrowserWindow = click.BrowserWindow;
            learned[CalibrationStep.TranslateIcon] = "번역 아이콘: " + icon;
            Step = CalibrationStep.TargetTab;
            return true;
        }

        private bool AcceptTab(ClickSnapshot click, bool target, out string message)
        {
            message = null;
            if (IsTranslateIcon(click)) return false;

            int index = click.IndexOf(ControlType.TabItem);
            if (index < 0 || IsInToolbar(click) || click.TabIndex < 0 || click.TabCount < 2)
            {
                message = "번역 창의 언어 탭이 아닌 곳을 클릭했습니다.";
                return false;
            }

            ElementInfo tab = click.Chain[index];
            if (target)
            {
                profile.LanguageTabClass = tab.ClassName;
                profile.TargetTabIndex = click.TabIndex;
                learned[CalibrationStep.TargetTab] = "번역할 언어 탭: " + tab + " (" + (click.TabIndex + 1) + "번째)";
                Step = CalibrationStep.SourceTab;
                return true;
            }

            if (tab.ClassName != profile.LanguageTabClass || click.TabIndex == profile.TargetTabIndex)
            {
                message = "방금 클릭한 탭과 다른, 원래 언어 탭을 클릭하세요.";
                return false;
            }
            learned[CalibrationStep.SourceTab] = "원문 언어 탭: " + tab + " (" + (click.TabIndex + 1) + "번째)";
            Step = CalibrationStep.CloseButton;
            return true;
        }

        private bool AcceptCloseButton(ClickSnapshot click, out string message)
        {
            message = null;
            if (IsTranslateIcon(click)) return false;

            int index = click.IndexOf(ControlType.Button);
            if (index < 0 || IsInToolbar(click))
            {
                message = "번역 창의 닫기(X) 버튼을 클릭하세요.";
                return false;
            }

            ElementInfo button = click.Chain[index];
            profile.CloseButtonClass = button.ClassName;
            profile.CloseButtonName = button.Name;
            learned[CalibrationStep.CloseButton] = "닫기 버튼: " + button;
            Step = CalibrationStep.AppMenuButton;
            return true;
        }

        private bool AcceptAppMenuButton(ClickSnapshot click, out string message)
        {
            message = null;
            int index = click.IndexOf(ControlType.Button);
            if (index < 0 || !IsInToolbar(click) || IsTranslateIcon(click))
            {
                message = "크롬 오른쪽 위의 메뉴 버튼(⋮)을 클릭하세요.";
                return false;
            }

            ElementInfo button = click.Chain[index];
            profile.AppMenuButtonClass = button.ClassName;
            learned[CalibrationStep.AppMenuButton] = "크롬 메뉴 버튼: " + button;
            Step = CalibrationStep.MenuItem;
            return true;
        }

        private bool AcceptMenuItem(ClickSnapshot click, out string message)
        {
            message = null;
            int index = click.IndexOf(ControlType.MenuItem);
            if (index < 0 || IsInToolbar(click))
            {
                message = "메뉴의 '번역…' 항목을 클릭하세요. 메뉴가 닫혔으면 메뉴 버튼(⋮)부터 다시 클릭하세요.";
                Step = CalibrationStep.AppMenuButton;
                return false;
            }

            ElementInfo item = click.Chain[index];
            profile.MenuItemClass = item.ClassName;
            profile.TranslateMenuItemName = item.Name;
            learned[CalibrationStep.MenuItem] = "번역 메뉴 항목: " + item;
            Step = CalibrationStep.Done;
            return true;
        }

        private bool IsInToolbar(ClickSnapshot click)
        {
            return click.Branch.ClassName == profile.NonClientClass;
        }

        private bool IsTranslateIcon(ClickSnapshot click)
        {
            int index = click.IndexOf(ControlType.Button);
            return index >= 0 && IsInToolbar(click) &&
                   click.Chain[index].ClassName == profile.TranslateIconClass &&
                   click.Chain[index].Name == profile.TranslateIconName;
        }
    }
}
