using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace TranslatorShortcut
{
    internal enum CheckStatus
    {
        Ok,
        Info,
        Warning,
        Error,
    }

    internal sealed class CheckResult
    {
        public CheckResult(CheckStatus status, string item, string detail)
        {
            Status = status;
            Item = item;
            Detail = detail;
        }

        public CheckStatus Status { get; private set; }
        public string Item { get; private set; }
        public string Detail { get; private set; }
    }

    // 크롬을 조작하지 않고 읽기만 해서, 번역 단축키가 동작할 조건이 갖춰졌는지 확인한다.
    // 번역 창과 메뉴 항목은 열어야 보이므로 여기서는 확인하지 않는다(보정에서 확인).
    internal static class CompatibilityChecker
    {
        private const string PolicyKeyPath = @"SOFTWARE\Policies\Google\Chrome";

        public static List<CheckResult> Run(IntPtr browserWindow, ChromeProfile profile)
        {
            var results = new List<CheckResult>();
            if (browserWindow == IntPtr.Zero)
            {
                results.Add(new CheckResult(CheckStatus.Error, "크롬 창", "실행 중인 크롬 창을 찾지 못했습니다. 크롬을 연 뒤 다시 검사해 주세요."));
                return results;
            }

            string version = ChromeWindow.GetVersion(browserWindow);
            var ui = new ChromeUi(browserWindow, profile);
            results.Add(new CheckResult(CheckStatus.Ok, "크롬 창", ui.Window.Current.Name + " (크롬 " + version + ")"));

            results.Add(CheckPolicy());
            results.Add(CheckTranslateSetting());

            results.Add(ui.HasRootView
                ? new CheckResult(CheckStatus.Ok, "화면 구조", "인식함")
                : new CheckResult(CheckStatus.Warning, "화면 구조", "구조 이름이 달라 요소를 찾는 데 시간이 더 걸릴 수 있습니다."));
            results.Add(ui.FindToolbar() != null
                ? new CheckResult(CheckStatus.Ok, "툴바", "인식함")
                : new CheckResult(CheckStatus.Warning, "툴바", "툴바 이름이 달라 요소를 찾는 데 시간이 더 걸릴 수 있습니다."));
            results.Add(ui.FindAppMenuButton() != null
                ? new CheckResult(CheckStatus.Ok, "크롬 메뉴 버튼", "인식함")
                : new CheckResult(CheckStatus.Error, "크롬 메뉴 버튼", "찾지 못했습니다. 번역 아이콘이 없는 페이지에서는 번역을 켤 수 없습니다. [보정]을 해 주세요."));
            results.Add(ui.FindPage() != null
                ? new CheckResult(CheckStatus.Ok, "웹페이지 영역", "인식함")
                : new CheckResult(CheckStatus.Warning, "웹페이지 영역", "찾지 못했습니다. 전환 뒤 포커스를 웹페이지로 돌려놓지 못할 수 있습니다."));
            results.Add(ui.FindTranslateIcon() != null
                ? new CheckResult(CheckStatus.Ok, "주소창 번역 아이콘", "현재 페이지에서 인식함")
                : new CheckResult(CheckStatus.Info, "주소창 번역 아이콘",
                    "현재 페이지에는 없습니다. 크롬이 외국어로 인식한 페이지에서만 나타나며, 없으면 크롬 메뉴로 번역 창을 엽니다. " +
                    "외국어 페이지인데도 이렇게 나오면 [보정]을 해 주세요."));

            results.Add(CheckProfile(version, profile));
            return results;
        }

        private static CheckResult CheckPolicy()
        {
            foreach (RegistryKey hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
            {
                using (RegistryKey key = hive.OpenSubKey(PolicyKeyPath))
                {
                    object value = key == null ? null : key.GetValue("TranslateEnabled");
                    if (value is int && (int)value == 0)
                    {
                        return new CheckResult(CheckStatus.Error, "번역 정책", "조직 정책으로 크롬 번역이 차단되어 있습니다. 관리자에게 문의해 주세요.");
                    }
                }
            }
            return new CheckResult(CheckStatus.Ok, "번역 정책", "제한 없음");
        }

        // 크롬 설정 > 언어 > "Google 번역 사용" (프로필별 Preferences 파일의 translate.enabled)
        private static CheckResult CheckTranslateSetting()
        {
            string userData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\User Data");
            if (!Directory.Exists(userData))
            {
                return new CheckResult(CheckStatus.Info, "Google 번역 사용", "크롬 사용자 데이터 폴더를 찾지 못해 확인하지 못했습니다.");
            }

            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            Dictionary<string, string> profileNames = ReadProfileNames(serializer, userData);
            var disabled = new List<string>();
            int checkedCount = 0;
            foreach (string dir in Directory.GetDirectories(userData))
            {
                string name = Path.GetFileName(dir);
                if (name != "Default" && !name.StartsWith("Profile ")) continue;
                string preferences = Path.Combine(dir, "Preferences");
                if (!File.Exists(preferences)) continue;

                try
                {
                    var root = serializer.DeserializeObject(File.ReadAllText(preferences)) as IDictionary;
                    var translate = root == null ? null : root["translate"] as IDictionary;
                    object enabled = translate == null ? null : translate["enabled"];
                    checkedCount++;
                    if (enabled is bool && !(bool)enabled)
                    {
                        string displayName;
                        disabled.Add(profileNames.TryGetValue(name, out displayName) ? displayName : name);
                    }
                }
                catch (Exception ex)
                {
                    Log.Write("크롬 설정을 읽지 못했습니다: " + preferences + " - " + ex.Message);
                }
            }

            if (disabled.Count > 0)
            {
                return new CheckResult(CheckStatus.Error, "Google 번역 사용",
                    "꺼져 있는 크롬 프로필: " + string.Join(", ", disabled.ToArray()) +
                    ". 크롬 설정 > 언어 > Google 번역에서 켜 주세요.");
            }
            return checkedCount > 0
                ? new CheckResult(CheckStatus.Ok, "Google 번역 사용", "켜짐")
                : new CheckResult(CheckStatus.Info, "Google 번역 사용", "크롬 프로필 설정을 찾지 못해 확인하지 못했습니다.");
        }

        // Local State의 profile.info_cache에서 프로필 폴더 이름 → 표시 이름
        private static Dictionary<string, string> ReadProfileNames(JavaScriptSerializer serializer, string userData)
        {
            var names = new Dictionary<string, string>();
            try
            {
                var root = serializer.DeserializeObject(File.ReadAllText(Path.Combine(userData, "Local State"))) as IDictionary;
                var profile = root == null ? null : root["profile"] as IDictionary;
                var cache = profile == null ? null : profile["info_cache"] as IDictionary;
                if (cache == null) return names;
                foreach (DictionaryEntry entry in cache)
                {
                    var info = entry.Value as IDictionary;
                    var name = info == null ? null : info["name"] as string;
                    if (name != null) names[(string)entry.Key] = name;
                }
            }
            catch (Exception ex)
            {
                Log.Write("크롬 프로필 목록을 읽지 못했습니다: " + ex.Message);
            }
            return names;
        }

        private static CheckResult CheckProfile(string chromeVersion, ChromeProfile profile)
        {
            if (!profile.IsCalibrated)
            {
                return new CheckResult(CheckStatus.Info, "인식 정보", "기본값 (크롬 154, 한국어 기준)");
            }
            string detail = "보정됨 (" + profile.CalibratedAt + ", 크롬 " + profile.CalibratedChromeVersion + ")";
            if (profile.CalibratedChromeVersion != chromeVersion)
            {
                return new CheckResult(CheckStatus.Warning, "인식 정보",
                    detail + ". 보정 이후 크롬이 " + chromeVersion + "(으)로 바뀌었습니다. 문제가 있으면 다시 보정해 주세요.");
            }
            return new CheckResult(CheckStatus.Ok, "인식 정보", detail);
        }
    }
}
