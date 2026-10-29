using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NPPLPrintMaster
{
    internal sealed class ModernNavigationButton : Button
    {
        public bool ModernStyle;
        public bool Active;
        public int IconIndex;
        private bool hovered;
        public ModernNavigationButton()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.ResizeRedraw, true);
        }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovered = false; Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            if (!ModernStyle) { base.OnPaint(e); return; }
            Color background = Parent.BackColor;
            Color text = Active ? Color.FromArgb(113, 231, 181) : Color.FromArgb(184, 196, 207);
            e.Graphics.Clear(background);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (Active || hovered)
                using (var path = CollageShapes.Rounded(new RectangleF(1, 1, Width - 2, Height - 2), 8))
                using (var fill = new SolidBrush(Active ? Color.FromArgb(24, 65, 54) : Color.FromArgb(36, 47, 55)))
                    e.Graphics.FillPath(fill, path);
            DrawIcon(e.Graphics, new Rectangle(17, (Height - 18) / 2, 18, 18), IconIndex, text);
            TextRenderer.DrawText(e.Graphics, Text.Trim(), Font, new Rectangle(47, 0, Width - 55, Height), text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            if (Focused && ShowFocusCues)
                ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(6, 5, Width - 12, Height - 10), text, background);
        }

        public static void DrawIcon(Graphics g, Rectangle bounds, int index, Color color)
        {
            var state = g.Save();
            g.TranslateTransform(bounds.X, bounds.Y);
            g.ScaleTransform(bounds.Width / 20f, bounds.Height / 20f);
            using (var pen = new Pen(color, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                switch (index)
                {
                    case 0:
                        g.DrawLines(pen, new[] { new Point(2, 9), new Point(10, 2), new Point(18, 9) });
                        g.DrawLines(pen, new[] { new Point(4, 8), new Point(4, 18), new Point(8, 18), new Point(8, 12), new Point(12, 12), new Point(12, 18), new Point(16, 18), new Point(16, 8) }); break;
                    case 1:
                        g.DrawRectangle(pen, 4, 2, 12, 16); g.DrawLine(pen, 10, 5, 10, 13);
                        g.DrawLines(pen, new[] { new Point(7, 10), new Point(10, 13), new Point(13, 10) }); break;
                    case 2:
                        for (int y = 4; y <= 16; y += 6) { g.DrawLine(pen, 2, y, 18, y); g.DrawEllipse(pen, y == 10 ? 11 : 5, y - 2, 4, 4); } break;
                    case 3:
                        g.DrawRectangle(pen, 4, 4, 12, 14); g.DrawRectangle(pen, 7, 2, 6, 4);
                        g.DrawLine(pen, 7, 10, 13, 10); g.DrawLine(pen, 7, 14, 13, 14); break;
                    case 4:
                        g.DrawRectangle(pen, 2, 3, 16, 14); g.DrawEllipse(pen, 5, 6, 3, 3);
                        g.DrawLines(pen, new[] { new Point(3, 16), new Point(9, 10), new Point(13, 14), new Point(16, 11), new Point(18, 13) }); break;
                    case 5:
                        for (int x = 2; x <= 11; x += 9) for (int y = 2; y <= 11; y += 9) g.DrawRectangle(pen, x, y, 6, 6); break;
                    case 6:
                        g.DrawPolygon(pen, new[] { new Point(3, 17), new Point(5, 11), new Point(14, 2), new Point(18, 6), new Point(9, 15) });
                        g.DrawLine(pen, 12, 4, 16, 8); break;
                    case 8:
                        g.DrawPolygon(pen, new[] { new Point(2, 5), new Point(8, 5), new Point(10, 7), new Point(18, 7), new Point(18, 17), new Point(2, 17) }); break;
                    default:
                        g.DrawEllipse(pen, 5, 5, 10, 10); g.DrawEllipse(pen, 8, 8, 4, 4);
                        for (int i = 0; i < 8; i++) { double a = i * Math.PI / 4; g.DrawLine(pen,
                            10 + (float)Math.Cos(a) * 7, 10 + (float)Math.Sin(a) * 7,
                            10 + (float)Math.Cos(a) * 9, 10 + (float)Math.Sin(a) * 9); } break;
                }
            }
            g.Restore(state);
        }
    }

    internal sealed class ModernIconTile : Control
    {
        public int IconIndex;
        public ModernIconTile() { SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent.BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = CollageShapes.Rounded(new RectangleF(0, 0, Width - 1, Height - 1), 7))
            using (var fill = new SolidBrush(Color.FromArgb(229, 245, 237))) e.Graphics.FillPath(fill, path);
            ModernNavigationButton.DrawIcon(e.Graphics, new Rectangle(7, 7, Width - 14, Height - 14), IconIndex, Color.FromArgb(0, 135, 90));
        }
    }
}
