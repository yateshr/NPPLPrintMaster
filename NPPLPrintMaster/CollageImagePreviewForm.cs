using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NPPLPrintMaster
{
    internal sealed class CollageImagePreviewForm : Form
    {
        private readonly string _path;
        private readonly OriginalImageCanvas _canvas;
        private readonly Label _status;
        private readonly List<Button> _zoomButtons = new List<Button>();

        public CollageImagePreviewForm(string path, ThemeColors theme, Font font)
        {
            _path = path;
            Text = "Original image - " + Path.GetFileName(path);
            Size = new Size(1100, 760);
            MinimumSize = new Size(640, 420);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            Font = font;
            BackColor = theme.ContentBg;
            ForeColor = theme.TextColor;
            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, Height = 48, Padding = new Padding(8),
                WrapContents = false, BackColor = theme.ContentBg
            };
            _canvas = new OriginalImageCanvas { Dock = DockStyle.Fill, BackColor = theme.BgLight };
            _status = new Label
            {
                Dock = DockStyle.Bottom, Height = 36, Padding = new Padding(10, 8, 0, 0),
                Text = "Loading original image...", AutoEllipsis = true
            };
            AddZoomButton(toolbar, "Fit", () => _canvas.FitImage());
            AddZoomButton(toolbar, "100%", () => _canvas.ActualSize());
            AddZoomButton(toolbar, "Zoom -", () => _canvas.ZoomBy(1f / 1.25f));
            AddZoomButton(toolbar, "Zoom +", () => _canvas.ZoomBy(1.25f));
            toolbar.Controls.Add(new Label
            {
                Text = "Wheel: zoom   |   Drag: pan   |   Esc: close",
                AutoSize = true, Padding = new Padding(10, 7, 0, 0)
            });
            Controls.Add(_canvas);
            Controls.Add(toolbar);
            Controls.Add(_status);
            _canvas.ViewChanged += (s, e) => RefreshStatus();
            Shown += async (s, e) => await LoadImageAsync();
        }

        private void AddZoomButton(FlowLayoutPanel toolbar, string text, Action click)
        {
            var button = new Button { Text = text, Width = 80, Height = 30, Enabled = false };
            button.Click += (s, e) => { click(); _canvas.Focus(); };
            toolbar.Controls.Add(button);
            _zoomButtons.Add(button);
        }

        // Decode only the image being inspected. Clone at native dimensions so
        // the file is released and no thumbnail resizing affects the preview.
        internal static Bitmap LoadOriginal(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete))
            using (var source = Image.FromStream(stream, false, true))
                return new Bitmap(source);
        }

        private async Task LoadImageAsync()
        {
            try
            {
                Bitmap bitmap = await Task.Run(() => LoadOriginal(_path));
                if (IsDisposed || Disposing) { bitmap.Dispose(); return; }
                _canvas.SetImage(bitmap);
                foreach (var button in _zoomButtons) button.Enabled = true;
                _canvas.Focus();
                RefreshStatus();
            }
            catch (Exception ex)
            {
                if (IsDisposed || Disposing) return;
                _canvas.Message = "The original image could not be opened.";
                _canvas.Invalidate();
                _status.Text = "Unable to load " + Path.GetFileName(_path) + ": " + ex.Message;
            }
        }

        private void RefreshStatus()
        {
            if (_canvas.ImageSize.IsEmpty) return;
            _status.Text = Path.GetFileName(_path) + "  |  " + _canvas.ImageSize.Width + " x " +
                _canvas.ImageSize.Height + " pixels  |  " + (_canvas.Zoom * 100).ToString("0.#") +
                "%  |  Full-resolution source";
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Escape: Close(); return true;
                case Keys.D0:
                case Keys.Control | Keys.D0: _canvas.FitImage(); return true;
                case Keys.D1:
                case Keys.Control | Keys.D1: _canvas.ActualSize(); return true;
                case Keys.Control | Keys.Oemplus:
                case Keys.Control | Keys.Add: _canvas.ZoomBy(1.25f); return true;
                case Keys.Control | Keys.OemMinus:
                case Keys.Control | Keys.Subtract: _canvas.ZoomBy(1f / 1.25f); return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }

    internal sealed class OriginalImageCanvas : Control
    {
        private Bitmap _image;
        private float _zoom = 1;
        private PointF _offset;
        private bool _fit = true;
        private bool _panning;
        private Point _press;
        private PointF _pressOffset;
        public string Message { get; set; } = "Loading original image...";
        public float Zoom { get { return _zoom; } }
        public Size ImageSize { get { return _image == null ? Size.Empty : _image.Size; } }
        public event EventHandler ViewChanged;

        public OriginalImageCanvas()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
        }

        // Takes ownership of the full-size bitmap; there is only one per viewer.
        public void SetImage(Bitmap image)
        {
            if (_image != null) _image.Dispose();
            _image = image;
            FitImage();
        }

        public void FitImage()
        {
            if (_image == null) return;
            _fit = true;
            _zoom = Math.Max(0.001f, Math.Min(1f, Math.Min(
                Math.Max(1, ClientSize.Width - 40) / (float)_image.Width,
                Math.Max(1, ClientSize.Height - 40) / (float)_image.Height)));
            CenterImage();
            NotifyViewChanged();
        }

        public void ActualSize()
        {
            if (_image == null) return;
            _fit = false;
            _zoom = 1;
            CenterImage();
            NotifyViewChanged();
        }

        public void ZoomBy(float factor)
        {
            ZoomAt(new Point(ClientSize.Width / 2, ClientSize.Height / 2), factor);
        }

        internal void ZoomAt(Point anchor, float factor)
        {
            if (_image == null || factor <= 0 || float.IsNaN(factor)) return;
            float zoom = Math.Max(0.001f, Math.Min(16, _zoom * factor));
            _offset = new PointF(anchor.X - (anchor.X - _offset.X) * zoom / _zoom,
                anchor.Y - (anchor.Y - _offset.Y) * zoom / _zoom);
            _zoom = zoom;
            _fit = false;
            ClampOffset();
            NotifyViewChanged();
        }

        private void CenterImage()
        {
            _offset = new PointF((ClientSize.Width - _image.Width * _zoom) / 2,
                (ClientSize.Height - _image.Height * _zoom) / 2);
        }

        private void ClampOffset()
        {
            if (_image == null) return;
            float width = _image.Width * _zoom, height = _image.Height * _zoom;
            _offset.X = width <= ClientSize.Width ? (ClientSize.Width - width) / 2 :
                Math.Max(ClientSize.Width - width - 30, Math.Min(30, _offset.X));
            _offset.Y = height <= ClientSize.Height ? (ClientSize.Height - height) / 2 :
                Math.Max(ClientSize.Height - height - 30, Math.Min(30, _offset.Y));
        }

        private void NotifyViewChanged()
        {
            Invalidate();
            if (ViewChanged != null) ViewChanged(this, EventArgs.Empty);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_image == null) return;
            if (_fit) FitImage();
            else { ClampOffset(); Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_image == null)
            {
                TextRenderer.DrawText(e.Graphics, Message, Font, ClientRectangle, SystemColors.GrayText,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
                return;
            }
            var bounds = new RectangleF(_offset.X, _offset.Y, _image.Width * _zoom, _image.Height * _zoom);
            e.Graphics.FillRectangle(Brushes.White, bounds);
            // At 100% and above, show source pixels without smoothing away
            // fine barcode lines. Fit-to-window uses high-quality downsampling.
            e.Graphics.InterpolationMode = _zoom >= 1 ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBicubic;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
            e.Graphics.DrawImage(_image, bounds, new RectangleF(0, 0, _image.Width, _image.Height), GraphicsUnit.Pixel);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            ZoomAt(e.Location, e.Delta > 0 ? 1.25f : 1f / 1.25f);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (_image == null || (e.Button != MouseButtons.Left && e.Button != MouseButtons.Middle)) return;
            _panning = true;
            _press = e.Location;
            _pressOffset = _offset;
            _fit = false;
            Capture = true;
            Cursor = Cursors.SizeAll;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_panning) return;
            _offset = new PointF(_pressOffset.X + e.X - _press.X, _pressOffset.Y + e.Y - _press.Y);
            ClampOffset();
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            Capture = false;
            _panning = false;
            Cursor = Cursors.Default;
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!Capture) { _panning = false; Cursor = Cursors.Default; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _image != null) { _image.Dispose(); _image = null; }
            base.Dispose(disposing);
        }
    }
}
