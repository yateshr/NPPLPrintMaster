using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace NPPLPrintMaster
{
    /// <summary>
    /// Drag payload shared by the library, the page canvas and the page
    /// navigator, so an image can be dragged between any of them.
    /// </summary>
    internal static class CollageDrag
    {
        private const string PathsFormat = "NPPL.Collage.ImagePaths";
        private const string SourceFormat = "NPPL.Collage.DragSource";

        public const string SourceLibrary = "library";
        public const string SourceCanvas = "canvas";

        // KeyState bit reported by DragEventArgs while Shift is held.
        public const int ShiftKeyState = 4;

        public static DataObject Create(IList<string> paths, string source)
        {
            DataObject data = new DataObject();
            data.SetData(PathsFormat, paths.ToArray());
            data.SetData(SourceFormat, source);
            return data;
        }

        public static string[] GetPaths(IDataObject data)
        {
            return data != null && data.GetDataPresent(PathsFormat)
                ? data.GetData(PathsFormat) as string[]
                : null;
        }

        // Files or folders dragged in from Windows Explorer, or null.
        public static string[] GetExternalFiles(IDataObject data)
        {
            if (data == null || !data.GetDataPresent(DataFormats.FileDrop))
                return null;

            string[] files = data.GetData(DataFormats.FileDrop) as string[];

            return files != null && files.Length > 0
                ? files
                : null;
        }

        public static bool IsFromCanvas(IDataObject data)
        {
            return data != null &&
                   data.GetDataPresent(SourceFormat) &&
                   string.Equals(
                       data.GetData(SourceFormat) as string,
                       SourceCanvas,
                       StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Drop-down list that follows the application theme. The standard
    /// combo box keeps a white field and light button whatever colors it is
    /// given, which stands out badly on the dark themes.
    /// </summary>
    internal sealed class CollageThemedComboBox : ComboBox
    {
        private const int ArrowAreaWidth = 20;

        private ThemeColors _theme;
        private bool _hovered;
        public bool RoundedAppearance { get; set; }

        public CollageThemedComboBox(ThemeColors theme)
        {
            _theme = theme;

            DropDownStyle = ComboBoxStyle.DropDownList;
            DrawMode = DrawMode.OwnerDrawFixed;
            FlatStyle = FlatStyle.Flat;
            ItemHeight = 18;
            BackColor = theme.ControlBg;
            ForeColor = theme.TextColor;

            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
        }

        public void SetTheme(ThemeColors theme)
        {
            _theme = theme;
            BackColor = theme.ControlBg;
            ForeColor = theme.TextColor;
            Invalidate();
        }

        // The closed field.
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Rectangle bounds = ClientRectangle;

            Color back =
                !Enabled
                    ? _theme.ReadOnlyBg
                    : _hovered || DroppedDown
                        ? _theme.ButtonHover
                        : _theme.ControlBg;

            if (RoundedAppearance)
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Parent == null ? back : Parent.BackColor);
                using (var shape = CollageShapes.Rounded(new RectangleF(0.5f, 0.5f, bounds.Width - 1, bounds.Height - 1), 6))
                using (var fill = new SolidBrush(back)) g.FillPath(fill, shape);
            }
            else using (Brush backBrush = new SolidBrush(back)) g.FillRectangle(backBrush, bounds);

            Rectangle textBounds =
                new Rectangle(
                    bounds.X + 5,
                    bounds.Y,
                    Math.Max(1, bounds.Width - ArrowAreaWidth - 6),
                    bounds.Height);

            TextRenderer.DrawText(
                g,
                SelectedIndex >= 0 ? GetItemText(Items[SelectedIndex]) : string.Empty,
                Font,
                textBounds,
                Enabled ? _theme.TextColor : _theme.MutedTextColor,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix);

            // Down arrow.
            int centerX = bounds.Right - (ArrowAreaWidth / 2) - 1;
            int centerY = bounds.Y + (bounds.Height / 2);

            using (Brush arrow =
                new SolidBrush(
                    Enabled ? _theme.TextColor : _theme.MutedTextColor))
            {
                g.FillPolygon(
                    arrow,
                    new[]
                    {
                        new Point(centerX - 4, centerY - 2),
                        new Point(centerX + 4, centerY - 2),
                        new Point(centerX, centerY + 3)
                    });
            }

            using (Pen border =
                new Pen(Focused ? _theme.ActiveColor : _theme.BorderColor))
            {
                if (RoundedAppearance)
                    using (var shape = CollageShapes.Rounded(new RectangleF(0.5f, 0.5f, bounds.Width - 1, bounds.Height - 1), 6)) g.DrawPath(border, shape);
                else g.DrawRectangle(
                    border,
                    bounds.X,
                    bounds.Y,
                    bounds.Width - 1,
                    bounds.Height - 1);
            }
        }

        // The rows of the open list.
        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0)
                return;

            bool selected = (e.State & DrawItemState.Selected) != 0;

            Color back = selected ? _theme.ActiveColor : _theme.ControlBg;

            Color fore =
                selected
                    ? ThemeManager.GetContrastTextColor(_theme.ActiveColor)
                    : _theme.TextColor;

            using (Brush backBrush = new SolidBrush(back))
                e.Graphics.FillRectangle(backBrush, e.Bounds);

            TextRenderer.DrawText(
                e.Graphics,
                GetItemText(Items[e.Index]),
                Font,
                new Rectangle(
                    e.Bounds.X + 4,
                    e.Bounds.Y,
                    Math.Max(1, e.Bounds.Width - 6),
                    e.Bounds.Height),
                fore,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix);
        }

        protected override void OnSelectedIndexChanged(EventArgs e)
        {
            base.OnSelectedIndexChanged(e);
            Invalidate();
        }

        protected override void OnDropDownClosed(EventArgs e)
        {
            base.OnDropDownClosed(e);
            Invalidate();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Invalidate();
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hovered = false;
            Invalidate();
        }
    }

    internal static class CollageWorkspaceUi
    {
        private const int EM_SETCUEBANNER = 0x1501;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(
            IntPtr hWnd,
            int msg,
            IntPtr wParam,
            string lParam);

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        private static extern int StrCmpLogicalW(string x, string y);

        // Grey hint text shown while a single-line text box is empty.
        public static void SetCueBanner(TextBox box, string text)
        {
            if (box.IsHandleCreated)
                SendMessage(box.Handle, EM_SETCUEBANNER, (IntPtr)1, text);
            else
                box.HandleCreated +=
                    (s, e) => SendMessage(
                        box.Handle,
                        EM_SETCUEBANNER,
                        (IntPtr)1,
                        text);
        }

        // Explorer-style ordering: "Label 500" sorts before "Label 1000".
        public static int CompareNatural(string x, string y)
        {
            try
            {
                return StrCmpLogicalW(x ?? string.Empty, y ?? string.Empty);
            }
            catch
            {
                return string.Compare(
                    x,
                    y,
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        public static RectangleF Fit(
            int sourceWidth,
            int sourceHeight,
            RectangleF bounds)
        {
            if (sourceWidth <= 0 || sourceHeight <= 0)
                return bounds;

            float scale =
                Math.Min(
                    bounds.Width / sourceWidth,
                    bounds.Height / sourceHeight);

            float width = Math.Max(1f, sourceWidth * scale);
            float height = Math.Max(1f, sourceHeight * scale);

            return new RectangleF(
                bounds.X + ((bounds.Width - width) / 2f),
                bounds.Y + ((bounds.Height - height) / 2f),
                width,
                height);
        }

        public static Color Blend(Color from, Color to, float amount)
        {
            amount = Math.Max(0f, Math.Min(1f, amount));

            return Color.FromArgb(
                (int)Math.Round(from.R + ((to.R - from.R) * amount)),
                (int)Math.Round(from.G + ((to.G - from.G) * amount)),
                (int)Math.Round(from.B + ((to.B - from.B) * amount)));
        }

        // Amber that stays readable on both the light and the dark themes.
        public static Color WarningColor(Color background)
        {
            return ThemeManager.IsLight(background)
                ? Color.FromArgb(170, 92, 0)
                : Color.FromArgb(255, 190, 92);
        }

        public static void DrawThumbnail(
            Graphics g,
            Image image,
            RectangleF target,
            float opacity)
        {
            if (image == null || target.Width < 1f || target.Height < 1f)
                return;

            if (opacity >= 0.999f)
            {
                g.DrawImage(image, target);
                return;
            }

            using (ImageAttributes attributes = new ImageAttributes())
            {
                ColorMatrix matrix = new ColorMatrix { Matrix33 = opacity };
                attributes.SetColorMatrix(matrix);

                PointF[] corners =
                {
                    new PointF(target.Left, target.Top),
                    new PointF(target.Right, target.Top),
                    new PointF(target.Left, target.Bottom)
                };

                g.DrawImage(
                    image,
                    corners,
                    new RectangleF(0, 0, image.Width, image.Height),
                    GraphicsUnit.Pixel,
                    attributes);
            }
        }

        public static void ConfigureScrollBar(
            ScrollBar bar,
            int contentSize,
            int viewSize,
            int value)
        {
            viewSize = Math.Max(1, viewSize);

            int maximumValue = Math.Max(0, contentSize - viewSize);

            bar.Minimum = 0;
            bar.Maximum = Math.Max(0, contentSize - 1);
            bar.LargeChange = Math.Max(1, Math.Min(viewSize, Math.Max(1, contentSize)));
            bar.SmallChange = Math.Max(1, Math.Min(48, bar.LargeChange));
            bar.Value = Math.Max(0, Math.Min(value, maximumValue));
            bar.Enabled = contentSize > viewSize;
        }
    }
}
