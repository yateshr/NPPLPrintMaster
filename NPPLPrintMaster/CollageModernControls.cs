using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NPPLPrintMaster
{
    internal static class CollageShapes
    {
        public static GraphicsPath Rounded(RectangleF bounds, float radius)
        {
            float d = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
            var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class CollageCard : Panel
    {
        public Color BorderColor = Color.Gray;
        public Color AccentColor = Color.SeaGreen;
        public int Step;
        public bool ShowStepBadge = true;
        public CollageCard()
        {
            DoubleBuffered = true;
        }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null ? BackColor : Parent.BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var shape = CollageShapes.Rounded(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 12))
            using (var fill = new SolidBrush(BackColor))
            using (var border = new Pen(BorderColor))
            { e.Graphics.FillPath(fill, shape); e.Graphics.DrawPath(border, shape); }
            if (!ShowStepBadge) return;
            using (var badge = CollageShapes.Rounded(new RectangleF(24, 23, 28, 28), 7))
            using (var fill = new SolidBrush(Color.FromArgb(35, AccentColor))) e.Graphics.FillPath(fill, badge);
            using (var font = new Font("Segoe UI", 10, FontStyle.Bold))
                TextRenderer.DrawText(e.Graphics, Step.ToString(), font, new Rectangle(24, 23, 28, 28),
                    AccentColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    internal sealed class CollageTextField : Panel
    {
        public Color BorderColor = Color.Gray;
        public Color MutedColor = Color.Gray;
        public string DisabledCaption;
        private readonly TextBox _text;
        public CollageTextField(TextBox text)
        {
            _text = text;
            DoubleBuffered = true;
            Padding = new Padding(12, 9, 12, 7);
            _text.BorderStyle = BorderStyle.None;
            _text.AutoSize = false;
            _text.Dock = DockStyle.Fill;
            Controls.Add(_text);
            _text.Visible = _text.Enabled;
            _text.EnabledChanged += (s, e) => { _text.Visible = _text.Enabled; Invalidate(); };
            _text.GotFocus += (s, e) => Invalidate();
            _text.LostFocus += (s, e) => Invalidate();
            Click += (s, e) => _text.Focus();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var shape = CollageShapes.Rounded(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 6))
            using (var pen = new Pen(_text.Focused ? ForeColor : BorderColor)) e.Graphics.DrawPath(pen, shape);
            if (!_text.Enabled)
                TextRenderer.DrawText(e.Graphics, DisabledCaption ?? _text.Text, Font,
                    new Rectangle(12, 0, Math.Max(1, Width - 24), Height), MutedColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    internal sealed class ModernNumericField : Panel
    {
        private readonly NumericUpDown input;
        public ModernNumericField(NumericUpDown control)
        {
            input = control; input.BorderStyle = BorderStyle.None; input.BackColor = Color.White;
            BackColor = Color.White; DoubleBuffered = true; Controls.Add(input);
            input.GotFocus += (s, e) => Invalidate(); input.LostFocus += (s, e) => Invalidate();
        }
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (input != null) input.SetBounds(12, Math.Max(1, (Height - input.PreferredSize.Height) / 2), Math.Max(20, Width - 24), input.PreferredSize.Height);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var shape = CollageShapes.Rounded(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 6))
            using (var pen = new Pen(input.Focused ? Color.FromArgb(0, 135, 90) : Color.FromArgb(218, 226, 234))) e.Graphics.DrawPath(pen, shape);
        }
    }

    internal sealed class CollageUiButton : Button
    {
        public Color MutedColor = Color.Gray;
        private bool _hover;
        public CollageUiButton()
        {
            FlatStyle = FlatStyle.Flat;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null ? BackColor : Parent.BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color back = !Enabled && Parent != null ? Parent.BackColor :
                _hover && Enabled ? FlatAppearance.MouseOverBackColor : BackColor;
            using (var shape = CollageShapes.Rounded(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 6))
            using (var fill = new SolidBrush(back))
            using (var pen = new Pen(Enabled ? FlatAppearance.BorderColor : Color.FromArgb(100, MutedColor)))
            { e.Graphics.FillPath(fill, shape); e.Graphics.DrawPath(pen, shape); }
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(8, 0, Math.Max(1, Width - 16), Height),
                Enabled ? ForeColor : MutedColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(4, 4, Width - 8, Height - 8), ForeColor, back);
        }
    }
}
