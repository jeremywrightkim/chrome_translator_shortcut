using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Windows.Automation;

namespace TranslatorShortcut
{
    // 크롬 창 하나의 UI Automation 트리에서 번역 관련 요소를 찾는다. 찾는 기준은 ChromeProfile을 따른다.
    //
    // 크롬 154 기준 구조:
    //   (크롬 창)
    //   └─ BrowserRootView
    //       ├─ NonClientView
    //       │    ├─ TopContainerView (툴바: 주소창 번역 아이콘, 크롬 메뉴 버튼)
    //       │    └─ MultiContentsView (웹페이지)
    //       └─ Chrome_WidgetWin_1 (팝업: 번역 창, 크롬 메뉴)
    //            └─ … TabbedPaneTab × 2 (원문 언어, 번역할 언어), ImageButton (번역 옵션, 닫기)
    //
    // 구조 이름(BrowserRootView, TopContainerView 등)을 못 찾으면 더 넓은 범위에서 찾는다. 느려지지만 동작은 한다.
    internal sealed class ChromeUi
    {
        private readonly ChromeProfile profile;
        private readonly AutomationElement rootView;

        public ChromeUi(IntPtr browserWindow, ChromeProfile profile)
        {
            this.profile = profile;
            Window = AutomationElement.FromHandle(browserWindow);
            rootView = Window.FindFirst(TreeScope.Children, Uia.ClassIs(profile.RootViewClass));
        }

        public AutomationElement Window { get; private set; }

        public bool HasRootView
        {
            get { return rootView != null; }
        }

        private AutomationElement RootView
        {
            get { return rootView ?? Window; }
        }

        public AutomationElement FindToolbar()
        {
            return RootView.FindFirst(TreeScope.Descendants, Uia.ClassIs(profile.ToolbarClass));
        }

        public AutomationElement FindTranslateIcon()
        {
            AutomationElement toolbar = FindToolbar() ?? RootView;
            return Uia.FindByName(
                toolbar.FindAll(TreeScope.Descendants, Uia.ClassIs(profile.TranslateIconClass)), profile.TranslateIconName);
        }

        public AutomationElement FindAppMenuButton()
        {
            AutomationElement toolbar = FindToolbar() ?? RootView;
            return toolbar.FindFirst(TreeScope.Descendants, Uia.ClassIs(profile.AppMenuButtonClass));
        }

        public AutomationElement FindTranslateMenuItem()
        {
            foreach (AutomationElement popup in Popups())
            {
                AutomationElement item = Uia.FindByName(
                    popup.FindAll(TreeScope.Subtree, Uia.ClassIs(profile.MenuItemClass)), profile.TranslateMenuItemName);
                if (item != null) return item;
            }
            return null;
        }

        // 번역 창 = 언어 탭이 들어 있는 팝업
        public AutomationElement FindBubble()
        {
            foreach (AutomationElement popup in Popups())
            {
                if (popup.FindFirst(TreeScope.Subtree, Uia.ClassIs(profile.LanguageTabClass)) != null) return popup;
            }
            return null;
        }

        public AutomationElementCollection FindLanguageTabs(AutomationElement bubble)
        {
            return bubble.FindAll(TreeScope.Subtree, Uia.ClassIs(profile.LanguageTabClass));
        }

        public void CloseBubble(AutomationElement bubble)
        {
            try
            {
                AutomationElementCollection buttons = bubble.FindAll(TreeScope.Subtree, Uia.ClassIs(profile.CloseButtonClass));
                AutomationElement close = Uia.FindByName(buttons, profile.CloseButtonName);
                if (close == null && buttons.Count > 0) close = buttons[buttons.Count - 1];
                if (close != null) Uia.Invoke(close);
            }
            catch (ElementNotAvailableException)
            {
                // 이미 닫힘
            }
        }

        public AutomationElement FindPage()
        {
            AutomationElement contents = RootView.FindFirst(TreeScope.Descendants, Uia.ClassIs(profile.ContentsClass)) ?? RootView;
            return contents.FindFirst(
                TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document));
        }

        public bool IsTranslateIcon(AutomationElement element)
        {
            return element != null && element.Current.ClassName == profile.TranslateIconClass;
        }

        // 팝업(번역 창, 메뉴)은 BrowserRootView 바로 아래에 붙는다.
        // 웹페이지 내용이 들어 있는 NonClientView는 건너뛰어 검색을 빠르게 한다.
        private IEnumerable<AutomationElement> Popups()
        {
            TreeWalker walker = TreeWalker.ControlViewWalker;
            for (AutomationElement child = walker.GetFirstChild(RootView); child != null; child = walker.GetNextSibling(child))
            {
                if (child.Current.ClassName != profile.NonClientClass) yield return child;
            }
        }
    }

    internal static class Uia
    {
        public static Condition ClassIs(string className)
        {
            return new PropertyCondition(AutomationElement.ClassNameProperty, className);
        }

        public static AutomationElement FindByName(AutomationElementCollection elements, string name)
        {
            foreach (AutomationElement element in elements)
            {
                if (element.Current.Name == name) return element;
            }
            return null;
        }

        public static void Invoke(AutomationElement element)
        {
            ((InvokePattern)element.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        }

        public static bool IsSelected(AutomationElement element)
        {
            return ((SelectionItemPattern)element.GetCurrentPattern(SelectionItemPattern.Pattern)).Current.IsSelected;
        }

        public static void Select(AutomationElement element)
        {
            ((SelectionItemPattern)element.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
        }

        public static void SetFocusQuietly(AutomationElement element)
        {
            try { element.SetFocus(); }
            catch (ElementNotAvailableException) { }
            catch (InvalidOperationException) { }
        }
    }

    internal static class Wait
    {
        public const int DefaultTimeoutMs = 2000;
        public const int PollIntervalMs = 50;

        public static AutomationElement For(Func<AutomationElement> find)
        {
            AutomationElement found = null;
            Until(() => (found = find()) != null);
            return found;
        }

        public static bool Until(Func<bool> condition)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (true)
            {
                if (condition()) return true;
                if (stopwatch.ElapsedMilliseconds >= DefaultTimeoutMs) return false;
                Thread.Sleep(PollIntervalMs);
            }
        }
    }
}
