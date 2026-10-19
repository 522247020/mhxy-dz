using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace MeditationMini
{
    /// <summary>Windows 全局快捷键：快捷键文本 &lt;-&gt; RegisterHotKey 参数。</summary>
    public static class Hotkey
    {
        public const int HotkeyId = 0x4D48;
        public const int WmHotkey = 0x0312;

        public const uint ModAlt = 0x0001;
        public const uint ModControl = 0x0002;
        public const uint ModShift = 0x0004;
        public const uint ModWin = 0x0008;
        public const uint ModNoRepeat = 0x4000;

        private static readonly string[] KeyNames = new string[]
        {
            "Backspace", "Tab", "Enter", "Pause", "Esc", "Space", "PageUp", "PageDown",
            "End", "Home", "Left", "Up", "Right", "Down", "Print", "Insert", "Delete",
        };

        private static readonly int[] KeyCodes = new int[]
        {
            0x08, 0x09, 0x0D, 0x13, 0x1B, 0x20, 0x21, 0x22,
            0x23, 0x24, 0x25, 0x26, 0x27, 0x28, 0x2C, 0x2D, 0x2E,
        };

        /// <summary>把 "Ctrl+Alt+F9" 这样的文本拆成 (修饰键, 虚拟键)。</summary>
        public static bool TryParse(string text, out uint modifiers, out uint key)
        {
            modifiers = 0;
            key = 0;
            if (string.IsNullOrEmpty(text)) return false;
            string[] parts = text.Split('+');
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim();
                if (part.Length == 0) return false;
                if (i < parts.Length - 1)
                {
                    switch (part.ToLowerInvariant())
                    {
                        case "ctrl":
                        case "control":
                            modifiers |= ModControl;
                            break;
                        case "alt":
                            modifiers |= ModAlt;
                            break;
                        case "shift":
                            modifiers |= ModShift;
                            break;
                        case "win":
                            modifiers |= ModWin;
                            break;
                        default:
                            return false;
                    }
                    continue;
                }
                uint parsed;
                if (!TryParseKey(part, out parsed)) return false;
                key = parsed;
            }
            modifiers |= ModNoRepeat;
            return key != 0;
        }

        public static string ToText(uint modifiers, uint key)
        {
            StringBuilder builder = new StringBuilder();
            if ((modifiers & ModControl) != 0) builder.Append("Ctrl+");
            if ((modifiers & ModAlt) != 0) builder.Append("Alt+");
            if ((modifiers & ModShift) != 0) builder.Append("Shift+");
            if ((modifiers & ModWin) != 0) builder.Append("Win+");
            builder.Append(KeyText(key));
            return builder.ToString();
        }

        private static string KeyText(uint key)
        {
            if (key >= 0x70 && key <= 0x87) return "F" + (key - 0x70 + 1).ToString(CultureInfo.InvariantCulture);
            for (int i = 0; i < KeyCodes.Length; i++)
            {
                if (KeyCodes[i] == (int)key) return KeyNames[i];
            }
            if ((key >= 'A' && key <= 'Z') || (key >= '0' && key <= '9'))
            {
                return ((char)key).ToString();
            }
            return string.Format(CultureInfo.InvariantCulture, "VK{0:X2}", key);
        }

        private static bool TryParseKey(string text, out uint key)
        {
            key = 0;
            string upper = text.ToUpperInvariant();
            if (upper.Length >= 2 && upper[0] == 'F')
            {
                int number;
                if (int.TryParse(upper.Substring(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out number)
                    && number >= 1 && number <= 24)
                {
                    key = (uint)(0x6F + number);
                    return true;
                }
            }
            if (upper.Length == 1)
            {
                char c = upper[0];
                if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
                {
                    key = c;
                    return true;
                }
            }
            for (int i = 0; i < KeyNames.Length; i++)
            {
                if (string.Equals(KeyNames[i], text, StringComparison.OrdinalIgnoreCase))
                {
                    key = (uint)KeyCodes[i];
                    return true;
                }
            }
            return false;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public static bool Register(IntPtr handle, string text, out uint modifiers, out uint key)
        {
            modifiers = 0;
            key = 0;
            if (!TryParse(text, out modifiers, out key)) return false;
            return RegisterHotKey(handle, HotkeyId, modifiers, key);
        }

        public static void Unregister(IntPtr handle)
        {
            if (handle == IntPtr.Zero) return;
            UnregisterHotKey(handle, HotkeyId);
        }
    }
}
