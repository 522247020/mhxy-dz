using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MeditationMini
{
    /// <summary>
    /// 深色菜单渲染器。子菜单是独立的 ToolStripDropDown，默认会用全局的浅色渲染器，
    /// 所以这里整套自己画（背景、文字、勾选、箭头、分隔线），并挂到
    /// ToolStripManager.Renderer 上，让一级、二级菜单配色一致。
    /// </summary>
    internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public static readonly Color MenuBackground = Color.FromArgb(15, 23, 42);
        public static readonly Color MenuBorder = Color.FromArgb(42, 58, 84);
        public static readonly Color Text = Color.FromArgb(229, 231, 235);
        public static readonly Color DisabledText = Color.FromArgb(120, 133, 152);
        public static readonly Color Selected = Color.FromArgb(37, 99, 235);
        public static readonly Color CheckMark = Color.FromArgb(56, 189, 248);
        public static readonly Color Separator = Color.FromArgb(42, 58, 84);

        public DarkMenuRenderer()
            : base(new DarkColorTable())
        {
            RoundedEdges = false;
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using (SolidBrush brush = new SolidBrush(MenuBackground))
            {
                e.Graphics.FillRectangle(brush, e.AffectedBounds);
            }
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
            using (SolidBrush brush = new SolidBrush(MenuBackground))
            {
                e.Graphics.FillRectangle(brush, e.AffectedBounds);
            }
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled) return;
            Rectangle bounds = new Rectangle(2, 0, e.Item.Width - 4, e.Item.Height);
            if (bounds.Width <= 0 || bounds.Height <= 0) return;
            using (GraphicsPath path = RoundedRect(bounds, 4f))
            using (SolidBrush brush = new SolidBrush(Selected))
            {
                SmoothingMode mode = e.Graphics.SmoothingMode;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.FillPath(brush, path);
                e.Graphics.SmoothingMode = mode;
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (!e.Item.Enabled) e.TextColor = DisabledText;
            else if (e.Item.Selected) e.TextColor = Color.White;
            else e.TextColor = Text;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            Rectangle area = e.ImageRectangle;
            if (area.Width <= 2 || area.Height <= 2) return;
            SmoothingMode mode = e.Graphics.SmoothingMode;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen pen = new Pen(CheckMark, 2f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                float left = area.Left + area.Width * 0.22f;
                float top = area.Top + area.Height * 0.20f;
                float width = area.Width * 0.56f;
                float height = area.Height * 0.60f;
                e.Graphics.DrawLines(pen, new PointF[]
                {
                    new PointF(left, top + height * 0.55f),
                    new PointF(left + width * 0.36f, top + height),
                    new PointF(left + width, top),
                });
            }
            e.Graphics.SmoothingMode = mode;
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = e.Item != null && e.Item.Enabled ? Text : DisabledText;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            Rectangle bounds = e.Item.Bounds;
            int y = bounds.Top + bounds.Height / 2;
            using (Pen pen = new Pen(Separator))
            {
                e.Graphics.DrawLine(pen, bounds.Left + 8, y, bounds.Right - 8, y);
            }
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            Rectangle bounds = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            using (Pen pen = new Pen(MenuBorder))
            {
                e.Graphics.DrawRectangle(pen, bounds);
            }
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float d = radius * 2f;
            path.AddArc(bounds.Left, bounds.Top, d, d, 180f, 90f);
            path.AddArc(bounds.Right - d, bounds.Top, d, d, 270f, 90f);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0f, 90f);
            path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90f, 90f);
            path.CloseFigure();
            return path;
        }
    }

    /// <summary>给上面的渲染器提供基础配色（勾选、选中背景等）。</summary>
    internal sealed class DarkColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground
        {
            get { return DarkMenuRenderer.MenuBackground; }
        }

        public override Color ImageMarginGradientBegin
        {
            get { return DarkMenuRenderer.MenuBackground; }
        }

        public override Color ImageMarginGradientMiddle
        {
            get { return DarkMenuRenderer.MenuBackground; }
        }

        public override Color ImageMarginGradientEnd
        {
            get { return DarkMenuRenderer.MenuBackground; }
        }

        public override Color MenuBorder
        {
            get { return DarkMenuRenderer.MenuBorder; }
        }

        public override Color MenuItemBorder
        {
            get { return DarkMenuRenderer.Selected; }
        }

        public override Color MenuItemSelected
        {
            get { return DarkMenuRenderer.Selected; }
        }

        public override Color MenuItemSelectedGradientBegin
        {
            get { return DarkMenuRenderer.Selected; }
        }

        public override Color MenuItemSelectedGradientEnd
        {
            get { return DarkMenuRenderer.Selected; }
        }

        public override Color MenuItemPressedGradientBegin
        {
            get { return DarkMenuRenderer.Selected; }
        }

        public override Color MenuItemPressedGradientEnd
        {
            get { return DarkMenuRenderer.Selected; }
        }

        public override Color SeparatorDark
        {
            get { return DarkMenuRenderer.Separator; }
        }

        public override Color SeparatorLight
        {
            get { return DarkMenuRenderer.MenuBackground; }
        }

        public override Color CheckBackground
        {
            get { return DarkMenuRenderer.MenuBackground; }
        }

        public override Color CheckSelectedBackground
        {
            get { return DarkMenuRenderer.Selected; }
        }

        public override Color CheckPressedBackground
        {
            get { return DarkMenuRenderer.Selected; }
        }
    }
}
