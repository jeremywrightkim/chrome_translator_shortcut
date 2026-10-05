using System;
using System.Drawing;
using System.Windows.Forms;

namespace TranslatorShortcut
{
    // 키 조합을 직접 눌러서 단축키를 지정하는 입력 칸
    internal sealed class HotkeyBox : TextBox
    {
        private Keys hotkey;

        public HotkeyBox()
        {
            ReadOnly = true;
            ShortcutsEnabled = false;
            BackColor = SystemColors.Window;
            TextAlign = HorizontalAlignment.Center;
        }

        public event EventHandler HotkeyChanged;

        public Keys Hotkey
        {
            get { return hotkey; }
            set
            {
                hotkey = value;
                IsValid = Hotkeys.IsValid(value);
                Text = Hotkeys.ToDisplayString(value);
                EventHandler handler = HotkeyChanged;
                if (handler != null) handler(this, EventArgs.Empty);
            }
        }

        public bool IsValid { get; private set; }

        // 포커스를 받으면 글자 전체가 선택된 것처럼 보이지 않게 한다.
        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Select(TextLength, 0);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            e.SuppressKeyPress = true;
            e.Handled = true;

            if (Hotkeys.IsModifierKey(e.KeyCode))
            {
                Text = Hotkeys.ModifiersToDisplayString(e.Modifiers) + "+…";
                return;
            }
            Hotkey = e.KeyCode | e.Modifiers;
        }
    }
}
