using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MeditationMini
{
    /// <summary>
    /// 托盘图标。小窗本身没有任务栏按钮（WS_EX_TOOLWINDOW），托盘就是它的「找回入口」：
    /// 图标颜色跟着状态变（灰=等待、青=计时、黄=暂停、红=完成），提示里显示剩余时间，
    /// 左键双击重新计时，右键出菜单（显示/隐藏、重新计时、置顶、退出）。
    /// </summary>
    internal sealed class OverlayTray : IDisposable
    {
        private readonly OverlayForm _form;
        private readonly NotifyIcon _icon;
        private readonly ToolStripMenuItem _toggleItem;
        private readonly ToolStripMenuItem _topItem;
        private readonly ToolStripMenuItem _lockItem;
        private Icon _currentIcon;
        private Color _color = Color.Empty;
        private string _tooltip = "";

        public OverlayTray(OverlayForm form)
        {
            _form = form;

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Renderer = new DarkMenuRenderer();
            menu.Font = new Font("Microsoft YaHei UI", 9f);

            _toggleItem = new ToolStripMenuItem("隐藏小窗");
            _toggleItem.Click += delegate { _form.ToggleVisible(); };
            menu.Items.Add(_toggleItem);

            ToolStripMenuItem restart = new ToolStripMenuItem("重新计时");
            restart.Click += delegate { _form.Restart(); };
            menu.Items.Add(restart);

            _topItem = new ToolStripMenuItem("置顶");
            _topItem.Checked = form.OnTop;
            _topItem.Click += delegate { _form.SetOnTop(!_form.OnTop); };
            menu.Items.Add(_topItem);

            _lockItem = new ToolStripMenuItem("锁定（鼠标穿透）");
            _lockItem.Checked = form.Locked;
            _lockItem.Click += delegate { _form.SetLocked(!_form.Locked); };
            menu.Items.Add(_lockItem);

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem quit = new ToolStripMenuItem("退出");
            quit.Click += delegate { _form.Close(); };
            menu.Items.Add(quit);

            menu.Opening += delegate
            {
                _toggleItem.Text = _form.Visible ? "隐藏小窗" : "显示小窗";
                _topItem.Checked = _form.OnTop;
                _lockItem.Checked = _form.Locked;
            };

            _icon = new NotifyIcon();
            _icon.ContextMenuStrip = menu;
            _icon.Text = "打坐倒计时";
            _icon.Icon = CreateIcon(DarkMenuRenderer.Text);
            _icon.Visible = true;
            _icon.DoubleClick += delegate { _form.Restart(); };
        }

        /// <summary>状态色或剩余时间变了就刷新图标和提示文字。</summary>
        public void Update(Color accent, string text)
        {
            if (!accent.Equals(_color))
            {
                _color = accent;
                ReplaceIcon(CreateIcon(accent));
            }
            string tooltip = "打坐 " + text;
            if (tooltip != _tooltip)
            {
                _tooltip = tooltip;
                _icon.Text = tooltip;
            }
        }

        public void Dispose()
        {
            _icon.Visible = false;
            _icon.Dispose();
            if (_currentIcon != null)
            {
                _currentIcon.Dispose();
                _currentIcon = null;
            }
        }

        private void ReplaceIcon(Icon icon)
        {
            Icon previous = _currentIcon;
            _currentIcon = icon;
            _icon.Icon = icon;
            if (previous != null && !ReferenceEquals(previous, icon)) previous.Dispose();
        }

        /// <summary>画一个 32×32 的托盘图标：状态色圆环 + 白色指针。</summary>
        private static Icon CreateIcon(Color accent)
        {
            using (Bitmap bitmap = new Bitmap(32, 32, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    using (Pen ring = new Pen(accent, 3.4f))
                    {
                        g.DrawEllipse(ring, 4.5f, 4.5f, 23f, 23f);
                    }
                    using (Pen hand = new Pen(Color.White, 2.8f))
                    {
                        hand.StartCap = LineCap.Round;
                        hand.EndCap = LineCap.Round;
                        g.DrawLine(hand, 16f, 16f, 16f, 9f);
                        g.DrawLine(hand, 16f, 16f, 22f, 16f);
                    }
                }
                IntPtr handle = bitmap.GetHicon();
                try
                {
                    using (Icon temporary = Icon.FromHandle(handle))
                    {
                        return (Icon)temporary.Clone();
                    }
                }
                finally
                {
                    DestroyIcon(handle);
                }
            }
        }

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr handle);
    }
}

