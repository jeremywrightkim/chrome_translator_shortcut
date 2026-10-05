using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TranslatorShortcut
{
    // %APPDATA%\TranslatorShortcut\settings.txt
    internal sealed class Settings
    {
        public Keys Hotkey { get; set; }

        private static string FilePath
        {
            get { return Path.Combine(AppInfo.DataDir, "settings.txt"); }
        }

        public static Settings Load()
        {
            var settings = new Settings { Hotkey = Hotkeys.Default };
            foreach (KeyValuePair<string, string> entry in KeyValueFile.Read(FilePath))
            {
                Keys hotkey;
                if (entry.Key == "hotkey" && Hotkeys.TryParse(entry.Value, out hotkey)) settings.Hotkey = hotkey;
            }
            return settings;
        }

        public void Save()
        {
            KeyValueFile.Write(FilePath, new[] { new KeyValuePair<string, string>("hotkey", Hotkey.ToString()) }, null);
        }
    }

    // "이름=값" 줄로 된 UTF-8 텍스트 파일. '#'으로 시작하는 줄은 주석.
    internal static class KeyValueFile
    {
        public static List<KeyValuePair<string, string>> Read(string path)
        {
            var entries = new List<KeyValuePair<string, string>>();
            try
            {
                if (!File.Exists(path)) return entries;
                foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
                {
                    if (line.StartsWith("#")) continue;
                    int separator = line.IndexOf('=');
                    if (separator <= 0) continue;
                    entries.Add(new KeyValuePair<string, string>(
                        line.Substring(0, separator).Trim(), line.Substring(separator + 1).Trim()));
                }
            }
            catch (IOException ex)
            {
                Log.Write("설정 파일을 읽지 못했습니다: " + path + " - " + ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                Log.Write("설정 파일을 읽지 못했습니다: " + path + " - " + ex.Message);
            }
            return entries;
        }

        public static void Write(string path, IEnumerable<KeyValuePair<string, string>> entries, string comment)
        {
            var text = new StringBuilder();
            if (comment != null) text.AppendLine("# " + comment);
            foreach (KeyValuePair<string, string> entry in entries) text.AppendLine(entry.Key + "=" + entry.Value);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text.ToString(), Encoding.UTF8);
        }
    }

    // Windows 시작 시 자동 실행 (HKCU\...\Run)
    internal static class Startup
    {
        public const string Argument = "--startup";

        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "TranslatorShortcut";

        private static string Command
        {
            get { return "\"" + Application.ExecutablePath + "\" " + Argument; }
        }

        public static bool IsEnabled
        {
            get
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath))
                {
                    string value = key == null ? null : key.GetValue(ValueName) as string;
                    return string.Equals(value, Command, StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        public static void SetEnabled(bool enabled)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath))
            {
                if (enabled) key.SetValue(ValueName, Command);
                else key.DeleteValue(ValueName, false);
            }
        }
    }

    internal static class Log
    {
        private const long MaxBytes = 512 * 1024;
        private static readonly object Sync = new object();

        public static string FilePath
        {
            get { return Path.Combine(AppInfo.DataDir, "log.txt"); }
        }

        public static void Write(string message)
        {
            try
            {
                lock (Sync)
                {
                    Directory.CreateDirectory(AppInfo.DataDir);
                    var file = new FileInfo(FilePath);
                    if (file.Exists && file.Length > MaxBytes) file.Delete();
                    File.AppendAllText(
                        FilePath,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + message + Environment.NewLine,
                        Encoding.UTF8);
                }
            }
            catch (IOException)
            {
                // 로그를 남기지 못해도 프로그램 동작에는 영향이 없다.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
