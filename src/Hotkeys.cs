using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace TranslatorShortcut
{
    // 단축키(Keys 조합) 검사, 표시, 저장, RegisterHotKey 변환
    internal static class Hotkeys
    {
        public const Keys Default = Keys.Alt | Keys.T;

        // Ctrl 또는 Alt가 포함되어 있거나, F1~F24 단독 키여야 한다.
        public static bool IsValid(Keys hotkey)
        {
            Keys key = hotkey & Keys.KeyCode;
            if (key == Keys.None || IsModifierKey(key)) return false;
            if ((hotkey & (Keys.Control | Keys.Alt)) != 0) return true;
            return key >= Keys.F1 && key <= Keys.F24;
        }

        public static bool IsModifierKey(Keys key)
        {
            switch (key)
            {
                case Keys.ShiftKey:
                case Keys.ControlKey:
                case Keys.Menu:
                case Keys.LWin:
                case Keys.RWin:
                    return true;
                default:
                    return false;
            }
        }

        public static void ToNative(Keys hotkey, out uint modifiers, out uint virtualKey)
        {
            modifiers = 0;
            if ((hotkey & Keys.Control) != 0) modifiers |= NativeMethods.MOD_CONTROL;
            if ((hotkey & Keys.Alt) != 0) modifiers |= NativeMethods.MOD_ALT;
            if ((hotkey & Keys.Shift) != 0) modifiers |= NativeMethods.MOD_SHIFT;
            virtualKey = (uint)(hotkey & Keys.KeyCode);
        }

        // 화면 표시용: "Ctrl+Alt+T"
        public static string ToDisplayString(Keys hotkey)
        {
            var parts = new List<string>();
            AddModifiers(hotkey, parts);
            Keys key = hotkey & Keys.KeyCode;
            if (key != Keys.None && !IsModifierKey(key)) parts.Add(KeyName(key));
            return string.Join("+", parts.ToArray());
        }

        public static string ModifiersToDisplayString(Keys modifiers)
        {
            var parts = new List<string>();
            AddModifiers(modifiers, parts);
            return string.Join("+", parts.ToArray());
        }

        // 설정 파일에는 Keys 열거형 이름("T, Alt")으로 저장한다.
        public static bool TryParse(string text, out Keys hotkey)
        {
            hotkey = Keys.None;
            try
            {
                hotkey = (Keys)Enum.Parse(typeof(Keys), text, true);
            }
            catch (ArgumentException)
            {
                return false;
            }
            return IsValid(hotkey);
        }

        private static void AddModifiers(Keys hotkey, List<string> parts)
        {
            if ((hotkey & Keys.Control) != 0) parts.Add("Ctrl");
            if ((hotkey & Keys.Alt) != 0) parts.Add("Alt");
            if ((hotkey & Keys.Shift) != 0) parts.Add("Shift");
        }

        private static string KeyName(Keys key)
        {
            if (key >= Keys.D0 && key <= Keys.D9) return ((char)('0' + (key - Keys.D0))).ToString();
            if (key >= Keys.NumPad0 && key <= Keys.NumPad9) return "Num" + (key - Keys.NumPad0);
            switch (key)
            {
                case Keys.OemMinus: return "-";
                case Keys.Oemplus: return "=";
                case Keys.Oemcomma: return ",";
                case Keys.OemPeriod: return ".";
                case Keys.OemQuestion: return "/";
                case Keys.OemSemicolon: return ";";
                case Keys.OemQuotes: return "'";
                case Keys.OemOpenBrackets: return "[";
                case Keys.OemCloseBrackets: return "]";
                case Keys.OemPipe: return "\\";
                case Keys.Oemtilde: return "`";
                case Keys.Space: return "Space";
                case Keys.Return: return "Enter";
                default: return key.ToString();
            }
        }
    }
}
