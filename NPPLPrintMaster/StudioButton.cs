using System.Drawing;
using System.Windows.Forms;

namespace NPPLPrintMaster
{
    internal class StudioButton : Button
    {
        public StudioButton() { SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); }
        protected override void OnPaint(PaintEventArgs e)
        {
            if (!ModernUiSession.Enabled) { base.OnPaint(e); return; }
            e.Graphics.Clear(Parent == null ? Color.White : Parent.BackColor);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            bool hover = ClientRectangle.Contains(PointToClient(Cursor.Position));
            Color fill = !Enabled ? Color.FromArgb(245, 247, 250) : hover && FlatAppearance.MouseOverBackColor != Color.Empty ? FlatAppearance.MouseOverBackColor : BackColor;
            using (var path = CollageShapes.Rounded(new RectangleF(.5f, .5f, Width - 1, Height - 1), 6))
            using (var background = new SolidBrush(fill))
            using (var border = new Pen(Enabled ? FlatAppearance.BorderColor : Color.FromArgb(218, 226, 234)))
            { e.Graphics.FillPath(background, path); e.Graphics.DrawPath(border, path); }
            int padding = Width < 90 ? 2 : 8;
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(padding, 1, Width - padding * 2, Height - 2), Enabled ? ForeColor : Color.FromArgb(100, 116, 133),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(5, 5, Width - 10, Height - 10), ForeColor, fill);
        }
    }
}
