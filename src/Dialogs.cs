using System;
using System.Drawing;
using System.Windows.Forms;

namespace MeditationMini
{
    /// <summary>要一个整数的小弹窗：打坐时长、字号都用它。</summary>
    public sealed class PromptIntForm : Form
    {
        private readonly NumericUpDown _input;

        public PromptIntForm(string title, string caption, int value, int minimum, int maximum, string suffix)
        {
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            TopMost = true;
            ClientSize = new Size(300, 132);
            Font = new Font("Microsoft YaHei UI", 9f);

            Label label = new Label();
            label.Text = caption;
            label.AutoSize = true;
            label.Location = new Point(16, 16);
            Controls.Add(label);

            _input = new NumericUpDown();
            _input.Minimum = minimum;
            _input.Maximum = maximum;
            _input.Value = Math.Min(maximum, Math.Max(minimum, value));
            _input.Location = new Point(16, 44);
            _input.Width = 130;
            Controls.Add(_input);

            Label unit = new Label();
            unit.Text = suffix;
            unit.AutoSize = true;
            unit.Location = new Point(152, 47);
            Controls.Add(unit);

            Button ok = new Button();
            ok.Text = "确定";
            ok.DialogResult = DialogResult.OK;
            ok.Location = new Point(120, 90);
            ok.Width = 78;
            Controls.Add(ok);

            Button cancel = new Button();
            cancel.Text = "取消";
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Location = new Point(204, 90);
            cancel.Width = 78;
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
        }

        public int Value
        {
            get { return (int)_input.Value; }
        }
    }

    /// <summary>录制快捷键的小弹窗：按一下组合键就填进去。</summary>
    public sealed class HotkeyCaptureForm : Form
    {
        private readonly TextBox _box;
        private readonly Label _hint;
        private string _hotkeyText = "";

        public HotkeyCaptureForm(string current)
        {
            Text = "设置快捷键";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            TopMost = true;
            ClientSize = new Size(320, 132);
            Font = new Font("Microsoft YaHei UI", 9f);
            KeyPreview = true;

            _hint = new Label();
            _hint.Text = "按下想用的组合键（例如 F9 或 Ctrl+Alt+F9）：";
            _hint.AutoSize = true;
            _hint.Location = new Point(16, 16);
            Controls.Add(_hint);

            _box = new TextBox();
            _box.ReadOnly = true;
            _box.Text = current;
            _box.Location = new Point(16, 44);
            _box.Width = 288;
            _box.TextAlign = HorizontalAlignment.Center;
            Controls.Add(_box);

            Button ok = new Button();
            ok.Text = "确定";
            ok.DialogResult = DialogResult.OK;
            ok.Location = new Point(140, 90);
            ok.Width = 78;
            ok.Enabled = false;
            Controls.Add(ok);
            _okButton = ok;

            Button cancel = new Button();
            cancel.Text = "取消";
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Location = new Point(224, 90);
            cancel.Width = 78;
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
        }

        private readonly Button _okButton;

        /// <summary>录到的快捷键文本，没录到就是空字符串。</summary>
        public string HotkeyText
        {
            get { return _hotkeyText; }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            Keys key = e.KeyCode;
            if (key == Keys.ShiftKey || key == Keys.ControlKey || key == Keys.Menu
                || key == Keys.LWin || key == Keys.RWin)
            {
                e.Handled = true;
                return;
            }
            uint modifiers = 0;
            if ((e.Modifiers & Keys.Control) != 0) modifiers |= Hotkey.ModControl;
            if ((e.Modifiers & Keys.Alt) != 0) modifiers |= Hotkey.ModAlt;
            if ((e.Modifiers & Keys.Shift) != 0) modifiers |= Hotkey.ModShift;
            string text = Hotkey.ToText(modifiers | Hotkey.ModNoRepeat, (uint)key);
            uint parsedModifiers;
            uint parsedKey;
            if (!Hotkey.TryParse(text, out parsedModifiers, out parsedKey))
            {
                e.Handled = true;
                return;
            }
            _hotkeyText = text;
            _box.Text = text;
            _okButton.Enabled = true;
            e.Handled = true;
        }
    }
}
