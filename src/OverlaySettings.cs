using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace MeditationMini
{
    /// <summary>
    /// 小窗的配置：自己存一份 meditation.ini（就在 exe 旁边），
    /// 第一次运行时会尝试从 Python 版的 config.json 里把设置读过来。
    /// </summary>
    public sealed class OverlaySettings
    {
        public const string OutlineAuto = "auto";
        public const string TimeSeconds = "seconds";
        public const string TimeClock = "clock";
        public const string OutlineNone = "none";

        public float Opacity = 0.85f;
        public int FontPx;                                  // 0 = 跟随窗口高度自动
        public Color TextColor = Color.White;
        public string Outline = OutlineAuto;
        public string TimeFormat = TimeSeconds;
        public int DurationSeconds = 180;
        public string Hotkey = "F9";
        public bool OnTop = true;
        public bool Locked;                                   // 锁定 = 整窗鼠标穿透
        public Rectangle Bounds = Rectangle.Empty;          // 逻辑像素，Empty 表示没存过
        public int Width = 176;                             // 逻辑像素
        public int Height = 54;

        private readonly string _path;

        private OverlaySettings(string path)
        {
            _path = path;
        }

        public static OverlaySettings Load(string path)
        {
            OverlaySettings settings = new OverlaySettings(path);
            if (File.Exists(path))
            {
                settings.ReadIni(File.ReadAllText(path));
            }
            else
            {
                settings.ImportFromConfigJson();
            }
            settings.Normalize();
            return settings;
        }

        public void Save()
        {
            try
            {
                string dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                using (StreamWriter writer = new StreamWriter(_path, false, System.Text.Encoding.UTF8))
                {
                    writer.WriteLine("# 打坐倒计时小窗的配置，界面上改了会自动写回这里");
                    writer.WriteLine("opacity=" + Opacity.ToString("0.###", CultureInfo.InvariantCulture));
                    writer.WriteLine("font_px=" + FontPx);
                    writer.WriteLine("text_color=" + ToHex(TextColor));
                    writer.WriteLine("outline=" + Outline);
                    writer.WriteLine("time_format=" + TimeFormat);
                    writer.WriteLine("duration_seconds=" + DurationSeconds);
                    writer.WriteLine("hotkey=" + Hotkey);
                    writer.WriteLine("on_top=" + (OnTop ? "true" : "false"));
                    writer.WriteLine("locked=" + (Locked ? "true" : "false"));
                    writer.WriteLine("x=" + Bounds.X);
                    writer.WriteLine("y=" + Bounds.Y);
                    writer.WriteLine("width=" + Width);
                    writer.WriteLine("height=" + Height);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        public static string ToHex(Color color)
        {
            return string.Format("#{0:x2}{1:x2}{2:x2}", color.R, color.G, color.B);
        }

        public static bool TryParseColor(string text, out Color color)
        {
            color = Color.Empty;
            if (string.IsNullOrEmpty(text)) return false;
            text = text.Trim();
            if (text.Length == 7 && text[0] == '#')
            {
                int value;
                if (int.TryParse(text.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
                {
                    color = Color.FromArgb(255, (value >> 16) & 0xFF, (value >> 8) & 0xFF, value & 0xFF);
                    return true;
                }
            }
            return false;
        }

        /// <summary>描边设置规范化：auto / none / #rrggbb 三者之一。</summary>
        public static string NormalizeOutline(string text)
        {
            if (string.IsNullOrEmpty(text)) return OutlineAuto;
            string value = text.Trim().ToLowerInvariant();
            if (value == OutlineAuto || value == OutlineNone) return value;
            Color color;
            if (TryParseColor(value, out color)) return ToHex(color);
            return OutlineAuto;
        }

        private void Normalize()
        {
            if (Opacity < 0f) Opacity = 0f;
            if (Opacity > 1f) Opacity = 1f;
            if (FontPx != 0)
            {
                if (FontPx < 8) FontPx = 8;
                if (FontPx > 96) FontPx = 96;
            }
            if (DurationSeconds < 1) DurationSeconds = 1;
            if (DurationSeconds > 180 * 60) DurationSeconds = 180 * 60;
            if (string.IsNullOrEmpty(Hotkey)) Hotkey = "F9";
            Outline = NormalizeOutline(Outline);
            TimeFormat = NormalizeTimeFormat(TimeFormat);
            if (Width < 132) Width = 132;
            if (Width > 320) Width = 320;
            if (Height < 44) Height = 44;
            if (Height > 96) Height = 96;
        }

        private void ReadIni(string text)
        {
            int? posX = null;
            int? posY = null;
            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line[0] == '#' || line[0] == ';') continue;
                int split = line.IndexOf('=');
                if (split <= 0) continue;
                string key = line.Substring(0, split).Trim().ToLowerInvariant();
                string value = StripInlineComment(line.Substring(split + 1)).Trim();
                switch (key)
                {
                    case "opacity":
                        Opacity = ParseFloat(value, Opacity);
                        break;
                    case "font_px":
                        FontPx = ParseInt(value, FontPx);
                        break;
                    case "text_color":
                        Color textColor;
                        if (TryParseColor(value, out textColor)) TextColor = textColor;
                        break;
                    case "outline":
                        Outline = value;
                        break;
                    case "time_format":
                        TimeFormat = value;
                        break;
                    case "duration_seconds":
                        DurationSeconds = ParseInt(value, DurationSeconds);
                        break;
                    case "hotkey":
                        if (value.Length > 0) Hotkey = value;
                        break;
                    case "locked":
                        Locked = value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
                        break;
                    case "on_top":
                        OnTop = value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
                        break;
                    case "width":
                        Width = ParseInt(value, Width);
                        break;
                    case "height":
                        Height = ParseInt(value, Height);
                        break;
                    case "x":
                        posX = ParseInt(value, 0);
                        break;
                    case "y":
                        posY = ParseInt(value, 0);
                        break;
                }
            }
            if (posX.HasValue && posY.HasValue)
            {
                Bounds = new Rectangle(posX.Value, posY.Value, Width, Height);
            }
        }

        /// <summary>
        /// 首次运行：Python 版把设置写在 config.json 里，这里只挑 meditation/* 的键读一次，
        /// 让已经在用 Python 版的人不用重新配一遍。读不到就用默认值。
        /// </summary>
        private void ImportFromConfigJson()
        {
            string[] candidates = new string[]
            {
                Environment.GetEnvironmentVariable("MHXY_CONFIG_PATH"),
                Path.Combine(Path.GetDirectoryName(_path) ?? ".", "config.json"),
                Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(_path) ?? ".") ?? ".", "config.json"),
            };
            for (int i = 0; i < candidates.Length; i++)
            {
                string candidate = candidates[i];
                if (string.IsNullOrEmpty(candidate) || !File.Exists(candidate)) continue;
                string json;
                try
                {
                    json = File.ReadAllText(candidate);
                }
                catch (IOException)
                {
                    continue;
                }
                float opacity = ParseJsonFloat(json, "meditation/mini/opacity", -1f);
                if (opacity >= 0f) Opacity = opacity;
                float fontPx = ParseJsonFloat(json, "meditation/mini/font_px", -1f);
                if (fontPx > 0f) FontPx = (int)fontPx;
                float duration = ParseJsonFloat(json, "meditation/mini/duration_seconds", -1f);
                if (duration > 0f) DurationSeconds = (int)duration;
                string textColor = ParseJsonString(json, "meditation/mini/text_color");
                Color parsed;
                if (TryParseColor(textColor, out parsed)) TextColor = parsed;
                string outline = ParseJsonString(json, "meditation/mini/outline_color");
                if (!string.IsNullOrEmpty(outline)) Outline = outline;
                string hotkey = ParseJsonString(json, "meditation/hotkey");
                if (!string.IsNullOrEmpty(hotkey)) Hotkey = hotkey;
                return;
            }
        }

        private static float ParseJsonFloat(string json, string key, float fallback)
        {
            Match match = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*(-?[0-9]+(?:\\.[0-9]+)?)");
            if (!match.Success) return fallback;
            float value;
            if (!float.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return fallback;
            }
            return value;
        }

        private static string ParseJsonString(string json, string key)
        {
            Match match = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"([^\"]*)\"");
            return match.Success ? match.Groups[1].Value : null;
        }

        /// <summary>去掉值后面用空格隔开的行内注释；颜色里的 # 不受影响。</summary>
        /// <summary>时间格式：seconds = 只显示秒数，clock = 分:秒。</summary>
        public static string NormalizeTimeFormat(string text)
        {
            if (!string.IsNullOrEmpty(text) && text.Trim().ToLowerInvariant() == TimeClock) return TimeClock;
            return TimeSeconds;
        }

        private static string StripInlineComment(string value)
        {
            for (int i = 1; i < value.Length; i++)
            {
                char c = value[i];
                if ((c == '#' || c == ';') && (value[i - 1] == ' ' || value[i - 1] == '\t'))
                {
                    return value.Substring(0, i);
                }
            }
            return value;
        }

        private static float ParseFloat(string text, float fallback)
        {
            float value;
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return fallback;
            }
            return value;
        }

        private static int ParseInt(string text, int fallback)
        {
            int value;
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return fallback;
            }
            return value;
        }
    }
}




