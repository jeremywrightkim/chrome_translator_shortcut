using System;
using System.Collections.Generic;
using System.IO;

namespace TranslatorShortcut
{
    // 크롬 화면 요소를 찾을 때 쓰는 이름과 클래스 이름.
    // 기본값은 크롬 154(한국어)에서 확인한 값이고, 보정 기능이 사용자 환경에 맞춰 파일에 저장한다.
    // %APPDATA%\TranslatorShortcut\chrome-profile.txt 를 지우면 기본값으로 돌아간다.
    internal sealed class ChromeProfile
    {
        public string RootViewClass { get; set; }
        public string NonClientClass { get; set; }
        public string ToolbarClass { get; set; }
        public string ContentsClass { get; set; }
        public string TranslateIconClass { get; set; }
        public string TranslateIconName { get; set; }
        public string AppMenuButtonClass { get; set; }
        public string MenuItemClass { get; set; }
        public string TranslateMenuItemName { get; set; }
        public string LanguageTabClass { get; set; }
        // 번역 창의 언어 탭 중 번역할 언어(한국어) 탭의 위치. 나머지 하나가 원문 언어 탭이다.
        public int TargetTabIndex { get; set; }
        public string CloseButtonClass { get; set; }
        public string CloseButtonName { get; set; }

        public string CalibratedAt { get; set; }
        public string CalibratedChromeVersion { get; set; }

        public bool IsCalibrated
        {
            get { return !string.IsNullOrEmpty(CalibratedAt); }
        }

        public static string FilePath
        {
            get { return Path.Combine(AppInfo.DataDir, "chrome-profile.txt"); }
        }

        public static ChromeProfile CreateDefault()
        {
            return new ChromeProfile
            {
                RootViewClass = "BrowserRootView",
                NonClientClass = "NonClientView",
                ToolbarClass = "TopContainerView",
                ContentsClass = "MultiContentsView",
                TranslateIconClass = "PageActionView",
                TranslateIconName = "번역",
                AppMenuButtonClass = "BrowserAppMenuButton",
                MenuItemClass = "MenuItemView",
                TranslateMenuItemName = "번역…",
                LanguageTabClass = "TabbedPaneTab",
                TargetTabIndex = 1,
                CloseButtonClass = "ImageButton",
                CloseButtonName = "닫기",
            };
        }

        public ChromeProfile Clone()
        {
            return (ChromeProfile)MemberwiseClone();
        }

        // 파일에 없는 항목은 기본값을 쓴다.
        public static ChromeProfile Load()
        {
            ChromeProfile profile = CreateDefault();
            foreach (KeyValuePair<string, string> entry in KeyValueFile.Read(FilePath))
            {
                Field field = FindField(entry.Key);
                if (field != null && entry.Value.Length > 0) field.Set(profile, entry.Value);
            }
            return profile;
        }

        public void Save()
        {
            var entries = new List<KeyValuePair<string, string>>();
            foreach (Field field in Fields)
            {
                string value = field.Get(this);
                if (!string.IsNullOrEmpty(value)) entries.Add(new KeyValuePair<string, string>(field.Key, value));
            }
            KeyValueFile.Write(FilePath, entries,
                "크롬 화면 요소 인식 정보. 환경설정의 [보정]이 기록한다. 이 파일을 지우면 기본값을 쓴다.");
        }

        public static void Reset()
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }

        private static Field FindField(string key)
        {
            foreach (Field field in Fields)
            {
                if (field.Key == key) return field;
            }
            return null;
        }

        private static readonly Field[] Fields =
        {
            new Field("root_view_class", p => p.RootViewClass, (p, v) => p.RootViewClass = v),
            new Field("non_client_class", p => p.NonClientClass, (p, v) => p.NonClientClass = v),
            new Field("toolbar_class", p => p.ToolbarClass, (p, v) => p.ToolbarClass = v),
            new Field("contents_class", p => p.ContentsClass, (p, v) => p.ContentsClass = v),
            new Field("translate_icon_class", p => p.TranslateIconClass, (p, v) => p.TranslateIconClass = v),
            new Field("translate_icon_name", p => p.TranslateIconName, (p, v) => p.TranslateIconName = v),
            new Field("app_menu_button_class", p => p.AppMenuButtonClass, (p, v) => p.AppMenuButtonClass = v),
            new Field("menu_item_class", p => p.MenuItemClass, (p, v) => p.MenuItemClass = v),
            new Field("translate_menu_item_name", p => p.TranslateMenuItemName, (p, v) => p.TranslateMenuItemName = v),
            new Field("language_tab_class", p => p.LanguageTabClass, (p, v) => p.LanguageTabClass = v),
            new Field("target_tab_index", p => p.TargetTabIndex.ToString(), SetTargetTabIndex),
            new Field("close_button_class", p => p.CloseButtonClass, (p, v) => p.CloseButtonClass = v),
            new Field("close_button_name", p => p.CloseButtonName, (p, v) => p.CloseButtonName = v),
            new Field("calibrated_at", p => p.CalibratedAt, (p, v) => p.CalibratedAt = v),
            new Field("calibrated_chrome_version", p => p.CalibratedChromeVersion, (p, v) => p.CalibratedChromeVersion = v),
        };

        private static void SetTargetTabIndex(ChromeProfile profile, string value)
        {
            int index;
            if (int.TryParse(value, out index) && index >= 0) profile.TargetTabIndex = index;
        }

        private sealed class Field
        {
            public readonly string Key;
            public readonly Func<ChromeProfile, string> Get;
            public readonly Action<ChromeProfile, string> Set;

            public Field(string key, Func<ChromeProfile, string> get, Action<ChromeProfile, string> set)
            {
                Key = key;
                Get = get;
                Set = set;
            }
        }
    }
}
