using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Automation;

namespace TranslatorShortcut
{
    // 크롬 기본 번역 창을 열어 언어 탭을 바꾸는 방식으로 번역과 원문을 전환한다.
    internal static class ChromeTranslator
    {
        private const int FocusWatchMs = 500;

        // 실패하면 원인을 로그에 남기고 false를 돌려준다.
        public static bool Toggle(IntPtr browserWindow, ChromeProfile profile)
        {
            AutomationElement previousFocus = AutomationElement.FocusedElement;
            var ui = new ChromeUi(browserWindow, profile);

            // 외국어 페이지를 열면 크롬이 번역 창을 먼저 띄워 두는 경우가 있다.
            AutomationElement bubble = ui.FindBubble();
            if (bubble == null)
            {
                if (!OpenTranslateBubble(ui)) return false;
                bubble = Wait.For(ui.FindBubble);
                if (bubble == null)
                {
                    Log.Write("번역 창이 열리지 않았습니다.");
                    return false;
                }
            }

            bool toggled = false;
            AutomationElementCollection tabs = ui.FindLanguageTabs(bubble);
            int target = profile.TargetTabIndex;
            if (tabs.Count < 2 || target >= tabs.Count)
            {
                Log.Write("번역 창에서 언어 탭을 찾지 못했습니다. 탭 수: " + tabs.Count);
            }
            else
            {
                int source = target == 0 ? 1 : 0;
                Uia.Select(Uia.IsSelected(tabs[target]) ? tabs[source] : tabs[target]);
                toggled = true;
            }

            ui.CloseBubble(bubble);
            Wait.Until(() => ui.FindBubble() == null);
            RestoreFocus(ui, previousFocus);
            return toggled;
        }

        private static bool OpenTranslateBubble(ChromeUi ui)
        {
            AutomationElement icon = ui.FindTranslateIcon();
            if (icon != null)
            {
                Uia.Invoke(icon);
                return true;
            }

            // 크롬이 외국어로 인식하지 못한 페이지는 주소창에 번역 아이콘이 없다. 크롬 메뉴의 "번역…"을 쓴다.
            AutomationElement menuButton = ui.FindAppMenuButton();
            if (menuButton == null)
            {
                Log.Write("크롬 메뉴 버튼을 찾지 못했습니다.");
                return false;
            }

            var menu = (ExpandCollapsePattern)menuButton.GetCurrentPattern(ExpandCollapsePattern.Pattern);
            menu.Expand();
            bool invoked = false;
            try
            {
                AutomationElement item = Wait.For(ui.FindTranslateMenuItem);
                if (item == null)
                {
                    Log.Write("크롬 메뉴에서 번역 항목을 찾지 못했습니다.");
                }
                else if (!item.Current.IsEnabled)
                {
                    // chrome:// 페이지처럼 번역할 수 없는 페이지
                    Log.Write("크롬 메뉴의 번역 항목이 비활성 상태입니다.");
                }
                else
                {
                    Uia.Invoke(item);
                    invoked = true;
                }
            }
            finally
            {
                // 항목을 누르면 메뉴는 저절로 닫힌다. 누르지 못했으면 열린 메뉴를 닫는다.
                if (!invoked) CollapseQuietly(menu);
            }
            return invoked;
        }

        private static void CollapseQuietly(ExpandCollapsePattern menu)
        {
            try { menu.Collapse(); }
            catch (InvalidOperationException) { }
            catch (ElementNotAvailableException) { }
        }

        // 번역 창을 닫으면 크롬이 포커스를 주소창 번역 아이콘으로 옮긴다. 그대로 두면 Space로 스크롤하려 할 때
        // 번역 창이 다시 열리므로, 원래 포커스(사라졌으면 웹페이지)로 되돌린다.
        // 크롬이 아이콘으로 옮기는 시점이 일정하지 않아서 잠시 지켜보며, 아이콘으로 가면 다시 되돌린다.
        private static void RestoreFocus(ChromeUi ui, AutomationElement previous)
        {
            AutomationElement target = FocusTarget(ui, previous);
            if (target == null) return;

            Stopwatch stopwatch = Stopwatch.StartNew();
            Uia.SetFocusQuietly(target);
            while (stopwatch.ElapsedMilliseconds < FocusWatchMs)
            {
                Thread.Sleep(Wait.PollIntervalMs);
                if (IsFocusOnTranslateIcon(ui)) Uia.SetFocusQuietly(target);
            }
        }

        private static AutomationElement FocusTarget(ChromeUi ui, AutomationElement previous)
        {
            try
            {
                if (previous != null && !ui.IsTranslateIcon(previous)) return previous;
            }
            catch (ElementNotAvailableException)
            {
                // 번역 창처럼 이미 사라진 요소
            }
            return ui.FindPage();
        }

        private static bool IsFocusOnTranslateIcon(ChromeUi ui)
        {
            try
            {
                return ui.IsTranslateIcon(AutomationElement.FocusedElement);
            }
            catch (ElementNotAvailableException)
            {
                return false;
            }
        }
    }
}
