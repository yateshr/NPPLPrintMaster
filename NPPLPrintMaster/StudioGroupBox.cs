using System;
using System.Drawing;
using System.Windows.Forms;

namespace NPPLPrintMaster
{
    internal class StudioGroupBox : GroupBox
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            if (!ModernUiSession.Enabled) { base.OnPaint(e); return; }
            e.Graphics.Clear(Parent == null ? Color.White : Parent.BackColor);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var path = CollageShapes.Rounded(new RectangleF(.5f, 9, Width - 1, Height - 10), 8))
            using (var fill = new SolidBrush(Color.White))
            using (var border = new Pen(Color.FromArgb(218, 226, 234)))
            { e.Graphics.FillPath(fill, path); e.Graphics.DrawPath(border, path); }
            var size = TextRenderer.MeasureText(Text, Font);
            using (var fill = new SolidBrush(Color.White)) e.Graphics.FillRectangle(fill, 12, 0, Math.Min(size.Width + 8, Width - 24), 22);
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(16, 0, Width - 32, 24), ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }
}
