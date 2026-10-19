using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MeditationMini
{
    /// <summary>
    /// 打坐倒计时小窗：无边框、逐像素透明、可置顶。
    /// 用 WS_EX_LAYERED + UpdateLayeredWindow 直接推一张 ARGB 位图，
    /// 所以四周是真正的透明，不会留下黑底或色键毛边。
    /// </summary>
    public sealed class OverlayForm : Form
    {
        private const int WsExLayered = 0x00080000;
        private const int WsExToolWindow = 0x00000080;
        private const int WsExTransparent = 0x00000020;
        private const int GwlExStyle = -20;
        private const int UlwAlpha = 0x00000002;

        private const float AutoFontRatio = 0.40f;
        private static readonly Color BackgroundColor = Color.FromArgb(11, 18, 32);
        private static readonly Color IdleColor = Color.FromArgb(124, 139, 159);
        private static readonly Color RunningColor = Color.FromArgb(56, 189, 248);
        private static readonly Color PausedColor = Color.FromArgb(251, 191, 36);
        private static readonly Color FinishedColor = Color.FromArgb(248, 113, 113);

        private static readonly string[] ColorNames = { "白色", "黑色", "亮黄", "青色", "橙色", "绿色" };
        private static readonly string[] ColorValues = { "#ffffff", "#111111", "#fbbf24", "#38bdf8", "#fb923c", "#34d399" };
        private static readonly int[] DurationPresets = { 1, 2, 3, 5, 10, 15, 30 };
        private static readonly int[] FontPresets = { 14, 18, 22, 28, 34 };
        private static readonly float[] OpacityPresets = { 0f, 0.3f, 0.5f, 0.7f, 0.85f, 1f };
        private static readonly PointF[] OutlineOffsets = new PointF[]
        {
            new PointF(-1, 0), new PointF(1, 0), new PointF(0, -1), new PointF(0, 1),
            new PointF(-1, -1), new PointF(1, -1), new PointF(-1, 1), new PointF(1, 1),
        };

        private readonly OverlaySettings _settings;
        private readonly MeditationTimer _timer;
        private readonly Timer _tickTimer;
        private readonly Timer _flashTimer;
        private float _scale;

        private bool _onTop;
        private bool _locked;
        private bool _hovered;
        private bool _dragging;
        private bool _moved;
        private Point _pressPoint;
        private Point _pressLocation;
        private int _flashLeft;
        private string _hotkeyText = "";
        private OverlayTray _tray;

        public OverlayForm(OverlaySettings settings)
        {
            _settings = settings;
            _timer = new MeditationTimer(settings.DurationSeconds);
            _onTop = settings.OnTop;
            _locked = settings.Locked;

            // 真正的 DPI 要等窗口句柄出来再量（见 OnHandleCreated），这里先按 1 倍算。
            _scale = 1f;

            FormBorderStyle = FormBorderStyle.None;
            // 尺寸完全由我们自己按 DPI 算，别让 WinForms 再自动缩放一遍。
            AutoScaleMode = AutoScaleMode.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = _onTop;
            Text = "打坐倒计时";
            Cursor = Cursors.Hand;
            BackColor = Color.Black;
            SetStyle(ControlStyles.UserMouse, true);

            _tickTimer = new Timer();
            _tickTimer.Interval = 200;
            _tickTimer.Tick += OnTick;
            _tickTimer.Start();

            _flashTimer = new Timer();
            _flashTimer.Interval = 160;
            _flashTimer.Tick += OnFlashTick;

            _tray = new OverlayTray(this);
        }

        // ---- 窗口样式 ----

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= WsExLayered | WsExToolWindow;
                return parameters;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            _scale = DetectScale(Handle);
            ApplySize();
            ApplyLocation();
            ApplyClickThrough();
            RegisterHotkey();
            Render();
        }

        /// <summary>量一下窗口所在显示器的缩放比例（1.5 = 150%）。</summary>
        private static float DetectScale(IntPtr handle)
        {
            try
            {
                int dpi = GetDpiForWindow(handle);
                if (dpi > 0) return dpi / 96f;
            }
            catch (EntryPointNotFoundException)
            {
            }
            IntPtr dc = GetDC(IntPtr.Zero);
            if (dc != IntPtr.Zero)
            {
                try
                {
                    int dpi = GetDeviceCaps(dc, 88); // LOGPIXELSX
                    if (dpi > 0) return dpi / 96f;
                }
                finally
                {
                    ReleaseDC(IntPtr.Zero, dc);
                }
            }
            return 1f;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _tickTimer.Stop();
            _flashTimer.Stop();
            Hotkey.Unregister(Handle);
            SaveBounds();
            if (_tray != null)
            {
                _tray.Dispose();
                _tray = null;
            }
            base.OnFormClosing(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Hotkey.WmHotkey && m.WParam.ToInt32() == Hotkey.HotkeyId)
            {
                Restart();
                return;
            }
            base.WndProc(ref m);
        }

        // ---- 对外操作 ----

        public void Restart()
        {
            _timer.Restart();
            StopAlert();
            Render();
        }

        public void TogglePause()
        {
            if (_timer.State == MeditationTimer.StateRunning) _timer.Pause();
            else if (_timer.State == MeditationTimer.StatePaused) _timer.Resume();
            else return;
            Render();
        }

        public void SetDuration(int seconds)
        {
            _timer.SetDuration(seconds);
            _settings.DurationSeconds = _timer.Duration;
            _settings.Save();
            StopAlert();
            Render();
        }

        public void SetOpacity(float opacity)
        {
            if (opacity < 0f) opacity = 0f;
            if (opacity > 1f) opacity = 1f;
            _settings.Opacity = opacity;
            _settings.Save();
            Render();
        }

        public void SetFontPx(int pixels)
        {
            _settings.FontPx = pixels <= 0 ? 0 : Math.Min(96, Math.Max(8, pixels));
            _settings.Save();
            Render();
        }

        public void SetTextColor(Color color)
        {
            _settings.TextColor = color;
            _settings.Save();
            Render();
        }

        public void SetOutline(string value)
        {
            _settings.Outline = OverlaySettings.NormalizeOutline(value);
            _settings.Save();
            Render();
        }

        public void SetTimeFormat(string value)
        {
            _settings.TimeFormat = OverlaySettings.NormalizeTimeFormat(value);
            _settings.Save();
            Render();
        }

        public bool OnTop
        {
            get { return _onTop; }
        }

        public void ToggleVisible()
        {
            Visible = !Visible;
        }

        public void SetOnTop(bool enabled)
        {
            _onTop = enabled;
            _settings.OnTop = enabled;
            TopMost = enabled;
            _settings.Save();
            Render();
        }

        /// <summary>锁定：整窗鼠标穿透，怎么点都不挡游戏；要解锁只能用托盘菜单。</summary>
        public bool Locked
        {
            get { return _locked; }
        }

        public void SetLocked(bool locked)
        {
            _locked = locked;
            _settings.Locked = locked;
            _settings.Save();
            ApplyClickThrough();
            Render();
        }

        /// <summary>给窗口加上 / 去掉 WS_EX_TRANSPARENT，实现整窗鼠标穿透。</summary>
        private void ApplyClickThrough()
        {
            if (!IsHandleCreated) return;
            int style = GetWindowLong(Handle, GwlExStyle);
            int updated = _locked ? (style | WsExTransparent) : (style & ~WsExTransparent);
            if (updated != style) SetWindowLong(Handle, GwlExStyle, updated);
        }

        public void Zoom(float factor)
        {
            int width = (int)Math.Round(_settings.Width * factor);
            int height = (int)Math.Round(width * 54.0 / 176.0);
            _settings.Width = Math.Min(320, Math.Max(132, width));
            _settings.Height = Math.Min(96, Math.Max(44, height));
            ApplySize();
            SaveBounds();
            Render();
        }

        // ---- 计时与提醒 ----

        private void OnTick(object sender, EventArgs e)
        {
            string stateBefore = _timer.State;
            _timer.Tick();
            Render();
            if (_timer.State == MeditationTimer.StateFinished && stateBefore != MeditationTimer.StateFinished)
            {
                StartAlert();
            }
        }

        private void StartAlert()
        {
            // 只有视觉提醒：文字闪几下红色，不出声。
            _flashLeft = 6;
            _flashTimer.Start();
            Render();
        }

        private void StopAlert()
        {
            _flashLeft = 0;
            _flashTimer.Stop();
        }

        private void OnFlashTick(object sender, EventArgs e)
        {
            _flashLeft = Math.Max(0, _flashLeft - 1);
            if (_flashLeft == 0) _flashTimer.Stop();
            Render();
        }

        // ---- 鼠标 ----

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Right)
            {
                ShowMenu();
                return;
            }
            if (e.Button != MouseButtons.Left) return;
            _dragging = true;
            _moved = false;
            _pressPoint = Cursor.Position;
            _pressLocation = Location;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_dragging) return;
            Point now = Cursor.Position;
            int dx = now.X - _pressPoint.X;
            int dy = now.Y - _pressPoint.Y;
            if (!_moved && Math.Abs(dx) + Math.Abs(dy) >= 3)
            {
                _moved = true;
                Cursor = Cursors.SizeAll;
            }
            if (_moved)
            {
                Location = new Point(_pressLocation.X + dx, _pressLocation.Y + dy);
                Render();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left || !_dragging) return;
            _dragging = false;
            Cursor = Cursors.Hand;
            if (_moved) SaveBounds();
            else TogglePause();
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button == MouseButtons.Left) Restart();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if ((ModifierKeys & Keys.Control) == 0) return;
            Zoom(e.Delta > 0 ? 1.08f : 1f / 1.08f);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hovered = true;
            Render();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hovered = false;
            Render();
        }

        // ---- 尺寸与位置 ----

        private void ApplySize()
        {
            int width = (int)Math.Round(_settings.Width * _scale);
            int height = (int)Math.Round(_settings.Height * _scale);
            ClientSize = new Size(Math.Max(8, width), Math.Max(8, height));
            Size = ClientSize;
        }

        private void ApplyLocation()
        {
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            Point location;
            if (_settings.Bounds.IsEmpty)
            {
                location = new Point(area.Left + (area.Width - Width) / 2, area.Top + (int)Math.Round(24 * _scale));
            }
            else
            {
                location = new Point(
                    (int)Math.Round(_settings.Bounds.X * _scale),
                    (int)Math.Round(_settings.Bounds.Y * _scale));
            }
            Location = Clamp(location, area);
        }

        private Point Clamp(Point location, Rectangle area)
        {
            int margin = (int)Math.Round(8 * _scale);
            int x = Math.Min(Math.Max(location.X, area.Left + margin), Math.Max(area.Left + margin, area.Right - Width - margin));
            int y = Math.Min(Math.Max(location.Y, area.Top + margin), Math.Max(area.Top + margin, area.Bottom - Height - margin));
            return new Point(x, y);
        }

        private void SaveBounds()
        {
            _settings.Bounds = new Rectangle(
                (int)Math.Round(Left / _scale),
                (int)Math.Round(Top / _scale),
                _settings.Width,
                _settings.Height);
            _settings.Save();
        }

        // ---- 快捷键 ----

        private void RegisterHotkey()
        {
            _hotkeyText = "";
            uint modifiers;
            uint key;
            string stored = _settings.Hotkey ?? "F9";
            if (Hotkey.Register(Handle, stored, out modifiers, out key))
            {
                _hotkeyText = Hotkey.ToText(modifiers, key);
                return;
            }
            if (Hotkey.Register(Handle, "F9", out modifiers, out key))
            {
                _hotkeyText = "F9";
            }
        }

        private void AskHotkey()
        {
            using (HotkeyCaptureForm dialog = new HotkeyCaptureForm(_hotkeyText))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                string text = dialog.HotkeyText;
                if (string.IsNullOrEmpty(text)) return;
                Hotkey.Unregister(Handle);
                uint modifiers;
                uint key;
                if (Hotkey.Register(Handle, text, out modifiers, out key))
                {
                    _settings.Hotkey = text;
                    _settings.Save();
                    _hotkeyText = Hotkey.ToText(modifiers, key);
                    return;
                }
                // 新键注册失败就把原来的注册回去，不让快捷键失效。
                RegisterHotkey();
                MessageBox.Show(this, "这个快捷键不支持，或者已经被别的程序占用，已保留原来的快捷键。",
                    "快捷键无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ---- 右键菜单 ----

        private void ShowMenu()
        {
            ContextMenuStrip menu = BuildMenu();
            menu.Show(Cursor.Position);
        }

        private ContextMenuStrip BuildMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Renderer = new DarkMenuRenderer();
            menu.ForeColor = DarkMenuRenderer.Text;
            menu.Font = new Font("Microsoft YaHei UI", 9f);
            menu.ShowImageMargin = true;

            ToolStripMenuItem restart = new ToolStripMenuItem("重新计时（" + _timer.Duration + " 秒）");
            restart.Click += delegate { Restart(); };
            menu.Items.Add(restart);

            ToolStripMenuItem pause = new ToolStripMenuItem(_timer.State == MeditationTimer.StateRunning ? "暂停" : "继续");
            pause.Enabled = _timer.State == MeditationTimer.StateRunning || _timer.State == MeditationTimer.StatePaused;
            pause.Click += delegate { TogglePause(); };
            menu.Items.Add(pause);

            // 打坐时长
            ToolStripMenuItem duration = new ToolStripMenuItem("打坐时长");
            for (int i = 0; i < DurationPresets.Length; i++)
            {
                int minutes = DurationPresets[i];
                ToolStripMenuItem item = new ToolStripMenuItem(minutes + " 分钟");
                item.Checked = _timer.Duration == minutes * 60;
                item.Click += delegate { SetDuration(minutes * 60); };
                duration.DropDownItems.Add(item);
            }
            duration.DropDownItems.Add(new ToolStripSeparator());
            ToolStripMenuItem customDuration = new ToolStripMenuItem("自定义…");
            customDuration.Click += delegate
            {
                using (PromptIntForm dialog = new PromptIntForm("打坐时长", "倒计时分钟数：", Math.Max(1, _timer.Duration / 60), 1, 180, " 分钟"))
                {
                    if (dialog.ShowDialog(this) == DialogResult.OK) SetDuration(dialog.Value * 60);
                }
            };
            duration.DropDownItems.Add(customDuration);
            menu.Items.Add(duration);

            menu.Items.Add(BuildTimeFormatMenu());

            // 字号
            ToolStripMenuItem fontMenu = new ToolStripMenuItem("字号");
            ToolStripMenuItem autoFont = new ToolStripMenuItem("自动（随窗口大小）");
            autoFont.Checked = _settings.FontPx <= 0;
            autoFont.Click += delegate { SetFontPx(0); };
            fontMenu.DropDownItems.Add(autoFont);
            for (int i = 0; i < FontPresets.Length; i++)
            {
                int pixels = FontPresets[i];
                ToolStripMenuItem item = new ToolStripMenuItem(pixels + " px");
                item.Checked = _settings.FontPx == pixels;
                item.Click += delegate { SetFontPx(pixels); };
                fontMenu.DropDownItems.Add(item);
            }
            fontMenu.DropDownItems.Add(new ToolStripSeparator());
            ToolStripMenuItem customFont = new ToolStripMenuItem("自定义…");
            customFont.Click += delegate
            {
                int current = _settings.FontPx > 0 ? _settings.FontPx : (int)Math.Round(Height * AutoFontRatio);
                using (PromptIntForm dialog = new PromptIntForm("字号", "字体像素大小：", current, 8, 96, " px"))
                {
                    if (dialog.ShowDialog(this) == DialogResult.OK) SetFontPx(dialog.Value);
                }
            };
            fontMenu.DropDownItems.Add(customFont);
            menu.Items.Add(fontMenu);

            menu.Items.Add(BuildColorMenu("文字颜色", _settings.TextColor));
            menu.Items.Add(BuildOutlineMenu());
            menu.Items.Add(BuildOpacityMenu());

            ToolStripMenuItem onTop = new ToolStripMenuItem("置顶");
            onTop.Checked = _onTop;
            onTop.Click += delegate { SetOnTop(!_onTop); };
            menu.Items.Add(onTop);

            ToolStripMenuItem lockItem = new ToolStripMenuItem("锁定（鼠标穿透）");
            lockItem.Checked = _locked;
            lockItem.Click += delegate { SetLocked(!_locked); };
            menu.Items.Add(lockItem);

            ToolStripMenuItem hotkey = new ToolStripMenuItem("设置快捷键…");
            hotkey.Click += delegate { AskHotkey(); };
            menu.Items.Add(hotkey);

            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem status = new ToolStripMenuItem("快捷键：" + (_hotkeyText.Length > 0 ? _hotkeyText : "未注册"));
            status.Enabled = false;
            menu.Items.Add(status);
            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem quit = new ToolStripMenuItem("退出");
            quit.Click += delegate { Close(); };
            menu.Items.Add(quit);
            return menu;
        }

        private ToolStripMenuItem BuildColorMenu(string title, Color current)
        {
            ToolStripMenuItem root = new ToolStripMenuItem(title);
            string currentText = OverlaySettings.ToHex(current);
            for (int i = 0; i < ColorNames.Length; i++)
            {
                string value = ColorValues[i];
                Color color = ColorTranslator.FromHtml(value);
                bool chosen = currentText == value;
                ToolStripMenuItem item = new ToolStripMenuItem(chosen ? "✓ " + ColorNames[i] : ColorNames[i], Swatch(color));
                item.Click += delegate { SetTextColor(color); };
                root.DropDownItems.Add(item);
            }
            root.DropDownItems.Add(new ToolStripSeparator());
            bool custom = Array.IndexOf(ColorValues, currentText) < 0;
            ToolStripMenuItem pick = new ToolStripMenuItem(custom ? "✓ 自定义…" : "自定义…", custom ? Swatch(current) : null);
            pick.Click += delegate
            {
                using (ColorDialog dialog = new ColorDialog())
                {
                    dialog.Color = current;
                    dialog.FullOpen = true;
                    if (dialog.ShowDialog(this) == DialogResult.OK) SetTextColor(dialog.Color);
                }
            };
            root.DropDownItems.Add(pick);
            return root;
        }

        private ToolStripMenuItem BuildOutlineMenu()
        {
            ToolStripMenuItem root = new ToolStripMenuItem("描边颜色");
            string outline = _settings.Outline;

            ToolStripMenuItem auto = new ToolStripMenuItem("自动（跟文字反色）");
            auto.Checked = outline == OverlaySettings.OutlineAuto;
            auto.Click += delegate { SetOutline(OverlaySettings.OutlineAuto); };
            root.DropDownItems.Add(auto);

            ToolStripMenuItem none = new ToolStripMenuItem("无描边");
            none.Checked = outline == OverlaySettings.OutlineNone;
            none.Click += delegate { SetOutline(OverlaySettings.OutlineNone); };
            root.DropDownItems.Add(none);
            root.DropDownItems.Add(new ToolStripSeparator());

            for (int i = 0; i < ColorNames.Length; i++)
            {
                string value = ColorValues[i];
                Color color = ColorTranslator.FromHtml(value);
                ToolStripMenuItem item = new ToolStripMenuItem(outline == value ? "✓ " + ColorNames[i] : ColorNames[i], Swatch(color));
                item.Click += delegate { SetOutline(value); };
                root.DropDownItems.Add(item);
            }
            root.DropDownItems.Add(new ToolStripSeparator());
            bool custom = outline != OverlaySettings.OutlineAuto
                && outline != OverlaySettings.OutlineNone
                && Array.IndexOf(ColorValues, outline) < 0;
            Color outlineColor = Color.Black;
            if (custom) OverlaySettings.TryParseColor(outline, out outlineColor);
            ToolStripMenuItem pick = new ToolStripMenuItem(custom ? "✓ 自定义…" : "自定义…", custom ? Swatch(outlineColor) : null);
            pick.Click += delegate
            {
                using (ColorDialog dialog = new ColorDialog())
                {
                    dialog.Color = outlineColor;
                    dialog.FullOpen = true;
                    if (dialog.ShowDialog(this) == DialogResult.OK) SetOutline(OverlaySettings.ToHex(dialog.Color));
                }
            };
            root.DropDownItems.Add(pick);
            return root;
        }

        private ToolStripMenuItem BuildTimeFormatMenu()
        {
            ToolStripMenuItem root = new ToolStripMenuItem("时间格式");
            ToolStripMenuItem seconds = new ToolStripMenuItem("剩余秒数（180）");
            seconds.Checked = _settings.TimeFormat == OverlaySettings.TimeSeconds;
            seconds.Click += delegate { SetTimeFormat(OverlaySettings.TimeSeconds); };
            root.DropDownItems.Add(seconds);

            ToolStripMenuItem clock = new ToolStripMenuItem("分:秒（03:00）");
            clock.Checked = _settings.TimeFormat == OverlaySettings.TimeClock;
            clock.Click += delegate { SetTimeFormat(OverlaySettings.TimeClock); };
            root.DropDownItems.Add(clock);
            return root;
        }

        private ToolStripMenuItem BuildOpacityMenu()
        {
            ToolStripMenuItem root = new ToolStripMenuItem("背景透明度");
            for (int i = 0; i < OpacityPresets.Length; i++)
            {
                float value = OpacityPresets[i];
                ToolStripMenuItem item = new ToolStripMenuItem(OpacityText(value));
                item.Checked = Math.Abs(_settings.Opacity - value) < 0.01f;
                item.Click += delegate { SetOpacity(value); };
                root.DropDownItems.Add(item);
            }
            return root;
        }

        private static string OpacityText(float value)
        {
            if (value <= 0f) return "全透明";
            return ((int)Math.Round(value * 100)) + "%";
        }

        private static Image Swatch(Color color)
        {
            Bitmap bitmap = new Bitmap(14, 14, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bitmap))
            using (SolidBrush brush = new SolidBrush(color))
            {
                g.Clear(Color.Transparent);
                g.FillRectangle(brush, 0, 0, 14, 14);
            }
            return bitmap;
        }

        // ---- 绘制 ----

        private void Render()
        {
            if (!IsHandleCreated || Width <= 0 || Height <= 0) return;
            using (Bitmap bitmap = new Bitmap(Width, Height, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    g.Clear(Color.Transparent);
                    PaintOverlay(g);
                }
                PushLayered(bitmap);
            }
            if (_tray != null) _tray.Update(StateColor(), DisplayText());
        }

        /// <summary>窗口上显示的文字：按设置决定是「秒数」还是「分:秒」。</summary>
        public string DisplayText()
        {
            int remaining = _timer.RemainingMs();
            return _settings.TimeFormat == OverlaySettings.TimeClock
                ? MeditationTimer.FormatClock(remaining)
                : MeditationTimer.Format(remaining);
        }

        /// <summary>当前状态的代表色（不含悬停/闪烁），给托盘图标用。</summary>
        private Color StateColor()
        {
            if (_timer.State == MeditationTimer.StateRunning) return RunningColor;
            if (_timer.State == MeditationTimer.StatePaused) return PausedColor;
            if (_timer.State == MeditationTimer.StateFinished) return FinishedColor;
            return IdleColor;
        }

        private void PaintOverlay(Graphics g)
        {
            float inset = 1f * _scale;
            RectangleF rect = new RectangleF(inset, inset, Width - inset * 2f, Height - inset * 2f);
            if (rect.Width < 6f || rect.Height < 6f) return;

            string text = DisplayText();
            float pad = Math.Max(10f * _scale, rect.Height * 0.35f);
            RectangleF textRect = new RectangleF(
                rect.X + pad,
                rect.Y + 2f * _scale,
                Math.Max(1f, rect.Width - pad * 2f),
                Math.Max(1f, rect.Height - 4f * _scale));
            if (textRect.Width < 4f || textRect.Height < 4f) return;

            using (Font font = new Font("Consolas", ResolveFontPx(g, text, textRect), FontStyle.Bold, GraphicsUnit.Pixel))
            {
                // 背景只包住数字的「墨迹」（横竖都贴紧），窗口里剩下的空白是透明的 ——
                // 鼠标在那上面会直接穿透到游戏里。
                RectangleF ink = MeasureInk(g, font, text, textRect);
                RectangleF pill = RectangleF.Inflate(ink, font.Size * 0.5f, font.Size * 0.22f);
                pill.Intersect(rect);
                if (pill.Width < 2f || pill.Height < 2f) pill = rect;

                int backgroundAlpha = BackgroundAlpha();
                using (GraphicsPath path = RoundedRect(pill, pill.Height / 2f))
                {
                    if (backgroundAlpha > 0)
                    {
                        using (SolidBrush brush = new SolidBrush(WithAlpha(BackgroundColor, backgroundAlpha)))
                        {
                            g.FillPath(brush, path);
                        }
                    }
                    else
                    {
                        // 全透明：肉眼什么都不画，但在这块「文字区域」留 1/255 的底，
                        // 否则逐像素命中测试会让整块都穿过去，连拖都拖不动。
                        using (SolidBrush brush = new SolidBrush(WithAlpha(BackgroundColor, 1)))
                        {
                            g.FillPath(brush, path);
                        }
                    }
                }

                DrawText(g, font, text, textRect);
            }
        }

        /// <summary>文字实际画出来的墨迹范围（和 DrawString 完全对齐），背景按它收紧。</summary>
        private static RectangleF MeasureInk(Graphics g, Font font, string text, RectangleF textRect)
        {
            using (StringFormat format = TextFormat())
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddString(text, font.FontFamily, (int)font.Style, font.Size, textRect, format);
                RectangleF bounds = path.GetBounds();
                if (bounds.Width <= 0f || bounds.Height <= 0f)
                {
                    SizeF size = g.MeasureString(text, font);
                    return new RectangleF(
                        textRect.X + (textRect.Width - size.Width) / 2f,
                        textRect.Y + (textRect.Height - size.Height) / 2f,
                        size.Width,
                        size.Height);
                }
                return bounds;
            }
        }

        private static StringFormat TextFormat()
        {
            StringFormat format = new StringFormat(StringFormatFlags.NoWrap);
            format.Alignment = StringAlignment.Center;
            format.LineAlignment = StringAlignment.Center;
            return format;
        }

        private void DrawText(Graphics g, Font font, string text, RectangleF textRect)
        {
            float pixels = font.Size;
            using (StringFormat format = TextFormat())
            {
                Color outline = OutlineColor(OutlineAlpha());
                if (outline.A > 0)
                {
                    float step = Math.Max(1f, pixels * 0.06f);
                    using (SolidBrush brush = new SolidBrush(outline))
                    {
                        for (int i = 0; i < OutlineOffsets.Length; i++)
                        {
                            RectangleF shifted = textRect;
                            shifted.Offset(OutlineOffsets[i].X * step, OutlineOffsets[i].Y * step);
                            g.DrawString(text, font, brush, shifted, format);
                        }
                    }
                }
                using (SolidBrush brush = new SolidBrush(TextColor()))
                {
                    g.DrawString(text, font, brush, textRect, format);
                }
            }
        }

        private int ResolveFontPx(Graphics g, string text, RectangleF textRect)
        {
            int size = _settings.FontPx > 0
                ? (int)Math.Round(_settings.FontPx * _scale)
                : (int)Math.Round(textRect.Height * AutoFontRatio);
            int limit = (int)Math.Round(textRect.Height * 0.95f);
            if (size > limit) size = limit;
            if (size < 8) size = 8;
            while (size > 8)
            {
                using (Font font = new Font("Consolas", size, FontStyle.Bold, GraphicsUnit.Pixel))
                {
                    if (g.MeasureString(text, font).Width <= textRect.Width) break;
                }
                size--;
            }
            return size;
        }

        private static GraphicsPath RoundedRect(RectangleF rect, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float d = Math.Max(0.1f, radius * 2f);
            path.AddArc(rect.X, rect.Y, d, d, 180f, 90f);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270f, 90f);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0f, 90f);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90f, 90f);
            path.CloseFigure();
            return path;
        }

        // ---- 颜色与透明度 ----

        private Color AccentColor()
        {
            Color baseColor;
            if (_timer.State == MeditationTimer.StateRunning) baseColor = RunningColor;
            else if (_timer.State == MeditationTimer.StatePaused) baseColor = PausedColor;
            else if (_timer.State == MeditationTimer.StateFinished) baseColor = FinishedColor;
            else baseColor = IdleColor;
            return _hovered ? Lighten(baseColor, 0.12f) : baseColor;
        }

        /// <summary>文字颜色：到点提醒时闪几下红色，其余时间就是设置里的颜色。</summary>
        private Color TextColor()
        {
            if (_flashLeft > 0)
            {
                return _flashLeft % 2 == 0 ? Color.FromArgb(248, 113, 113) : Color.FromArgb(254, 202, 202);
            }
            return _settings.TextColor;
        }

        private int BackgroundAlpha()
        {
            int value = (int)Math.Round(255 * _settings.Opacity);
            if (value <= 0) return 0;
            if (_hovered) value += 14;
            return Math.Min(255, value);
        }

        private int OutlineAlpha()
        {
            float ratio = BackgroundAlpha() / 255f;
            return (int)Math.Round(50 + 190 * (1f - ratio));
        }

        private Color OutlineColor(int alpha)
        {
            string outline = _settings.Outline;
            if (outline == OverlaySettings.OutlineNone) return Color.Transparent;
            Color baseColor;
            if (outline == OverlaySettings.OutlineAuto)
            {
                baseColor = _settings.TextColor.GetBrightness() > 0.5f ? Color.Black : Color.White;
            }
            else if (!OverlaySettings.TryParseColor(outline, out baseColor))
            {
                return Color.Transparent;
            }
            return WithAlpha(baseColor, alpha);
        }

        private static Color WithAlpha(Color color, int alpha)
        {
            if (alpha < 0) alpha = 0;
            if (alpha > 255) alpha = 255;
            return Color.FromArgb(alpha, color.R, color.G, color.B);
        }

        private static Color Lighten(Color color, float amount)
        {
            int r = color.R + (int)((255 - color.R) * amount);
            int g = color.G + (int)((255 - color.G) * amount);
            int b = color.B + (int)((255 - color.B) * amount);
            return Color.FromArgb(color.A, Math.Min(255, r), Math.Min(255, g), Math.Min(255, b));
        }

        // ---- 把位图推给分层窗口 ----

        private void PushLayered(Bitmap bitmap)
        {
            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memoryDc = CreateCompatibleDC(screenDc);
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr previous = IntPtr.Zero;
            try
            {
                hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
                previous = SelectObject(memoryDc, hBitmap);
                Size size = new Size(bitmap.Width, bitmap.Height);
                Point destination = new Point(Left, Top);
                Point source = new Point(0, 0);
                BlendFunction blend = new BlendFunction();
                blend.BlendOp = 0;              // AC_SRC_OVER
                blend.BlendFlags = 0;
                blend.SourceConstantAlpha = 255;
                blend.AlphaFormat = 1;          // AC_SRC_ALPHA
                UpdateLayeredWindow(Handle, screenDc, ref destination, ref size, memoryDc,
                    ref source, 0, ref blend, UlwAlpha);
            }
            finally
            {
                if (hBitmap != IntPtr.Zero)
                {
                    if (previous != IntPtr.Zero) SelectObject(memoryDc, previous);
                    DeleteObject(hBitmap);
                }
                DeleteDC(memoryDc);
                ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BlendFunction
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateLayeredWindow(IntPtr handle, IntPtr screenDc, ref Point destination,
            ref Size size, IntPtr memoryDc, ref Point source, int colorKey, ref BlendFunction blend, int flags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr handle);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr handle, IntPtr dc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr dc);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr dc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr obj);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr handle, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr handle, int index, int value);

        [DllImport("user32.dll")]
        private static extern int GetDpiForWindow(IntPtr handle);

        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr dc, int index);
    }
}










