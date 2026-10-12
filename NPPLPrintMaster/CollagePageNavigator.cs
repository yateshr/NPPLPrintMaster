using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace NPPLPrintMaster
{
    /// <summary>
    /// Strip of miniature pages beside the collage canvas.
    ///
    /// Click a page to jump to it. An image can be dropped straight onto a
    /// cell of any miniature; holding a dragged image over a page for a
    /// moment brings that page into the canvas so it can be dropped there.
    /// </summary>
    internal sealed class CollagePageNavigator : Control
    {
        private const int SidePadding = 14;
        private const int TopPadding = 10;
        private const int AddTileHeight = 38;
        private const int SpringDelayMs = 650;
        private const int AutoScrollZone = 36;

        private readonly CollageArrangementModel _model;
        private readonly CollageThumbnailCache _thumbnails;
        private readonly ThemeColors _theme;
        private readonly VScrollBar _scrollBar;
        private readonly Timer _dragTimer;
        private readonly Font _labelFont;
        private readonly StringFormat _centerFormat;

        private int _thumbWidth = 100;
        private int _thumbHeight = 141;
        private int _itemHeight = 170;
        private int _scrollOffset;
        private bool _updatingScroll;

        private int _currentPage;
        private int _hoverPage = -1;

        private bool _dragActive;
        private Point _dragPoint;
        private int _dropSlot = -1;
        private int _springPage = -1;
        private int _springStart;
        private bool _springFired;

        public bool InsertMode { get; set; }
        public Func<string, bool> IsHighlighted { get; set; }

        // The page that was right-clicked, for the context menu.
        public int ContextPage { get; private set; }

        public event Action<int> PageActivated;
        public event Action<string[], int, bool> ImagesDropped;
        public event EventHandler AddPageRequested;

        public CollagePageNavigator(
            CollageArrangementModel model,
            CollageThumbnailCache thumbnails,
            ThemeColors theme,
            string fontFamily)
        {
            _model = model;
            _thumbnails = thumbnails;
            _theme = theme;

            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);

            AllowDrop = true;
            BackColor = theme.ContentBg;

            _labelFont = new Font(fontFamily, 8f, FontStyle.Bold);

            _centerFormat = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            _scrollBar = new VScrollBar { Dock = DockStyle.Right };
            _scrollBar.ValueChanged +=
                (s, e) =>
                {
                    if (_updatingScroll)
                        return;

                    _scrollOffset = _scrollBar.Value;
                    Invalidate();
                };

            Controls.Add(_scrollBar);

            _dragTimer = new Timer { Interval = 40 };
            _dragTimer.Tick += DragTimer_Tick;
        }

        public int CurrentPage
        {
            get { return _currentPage; }
            set
            {
                int page = Math.Max(0, Math.Min(_model.PageCount - 1, value));

                if (page == _currentPage)
                    return;

                _currentPage = page;

                // Do not scroll the strip away from under a drag.
                if (!_dragActive)
                    EnsurePageVisible(page);

                Invalidate();
            }
        }

        // Call after the arrangement changed.
        public void OnModelChanged()
        {
            _currentPage =
                Math.Max(0, Math.Min(_model.PageCount - 1, _currentPage));

            UpdateLayoutMetrics();
            Invalidate();
        }

        // ------------------------------------------------------------
        // Layout
        // ------------------------------------------------------------
        private int ContentHeight
        {
            get
            {
                return TopPadding +
                       (_model.PageCount * _itemHeight) +
                       AddTileHeight +
                       TopPadding;
            }
        }

        private void UpdateLayoutMetrics()
        {
            float pageWidth =
                _model.Page.Landscape
                    ? CollageCellLayout.PageLongSidePoints
                    : CollageCellLayout.PageShortSidePoints;

            float pageHeight =
                _model.Page.Landscape
                    ? CollageCellLayout.PageShortSidePoints
                    : CollageCellLayout.PageLongSidePoints;

            _thumbWidth =
                Math.Max(
                    40,
                    ClientSize.Width - _scrollBar.Width - (SidePadding * 2));

            _thumbHeight =
                (int)Math.Round(_thumbWidth * pageHeight / pageWidth);

            _itemHeight = _thumbHeight + _labelFont.Height + 18;

            _scrollOffset =
                Math.Max(
                    0,
                    Math.Min(
                        _scrollOffset,
                        Math.Max(0, ContentHeight - ClientSize.Height)));

            _updatingScroll = true;
            try
            {
                CollageWorkspaceUi.ConfigureScrollBar(
                    _scrollBar,
                    ContentHeight,
                    ClientSize.Height,
                    _scrollOffset);
            }
            finally
            {
                _updatingScroll = false;
            }
        }

        private Rectangle GetThumbBounds(int page)
        {
            return new Rectangle(
                SidePadding,
                TopPadding + (page * _itemHeight) - _scrollOffset,
                _thumbWidth,
                _thumbHeight);
        }

        private Rectangle GetAddTileBounds()
        {
            return new Rectangle(
                SidePadding,
                TopPadding + (_model.PageCount * _itemHeight) - _scrollOffset,
                _thumbWidth,
                AddTileHeight - 8);
        }

        private float UnitsPerPoint
        {
            get
            {
                return _thumbWidth /
                       (_model.Page.Landscape
                           ? CollageCellLayout.PageLongSidePoints
                           : CollageCellLayout.PageShortSidePoints);
            }
        }

        private int HitTestPage(Point point)
        {
            if (point.X < 0 || point.X >= ClientSize.Width - _scrollBar.Width)
                return -1;

            int y = point.Y + _scrollOffset - TopPadding;
            if (y < 0)
                return -1;

            int page = y / _itemHeight;

            return page < _model.PageCount ? page : -1;
        }

        // The cell of a miniature page under the pointer; the nearest cell
        // is used when the pointer is on the page margin or its label.
        private int HitTestSlot(Point point)
        {
            int page = HitTestPage(point);
            if (page < 0)
                return -1;

            return (page * _model.CellsPerPage) +
                   CollageCellLayout.GetNearestCell(
                       GetThumbBounds(page),
                       UnitsPerPoint,
                       _model.Page,
                       point);
        }

        private void ScrollTo(int offset)
        {
            offset =
                Math.Max(
                    0,
                    Math.Min(
                        offset,
                        Math.Max(0, ContentHeight - ClientSize.Height)));

            if (offset == _scrollOffset)
                return;

            _scrollOffset = offset;

            _updatingScroll = true;
            try
            {
                _scrollBar.Value = offset;
            }
            finally
            {
                _updatingScroll = false;
            }

            Invalidate();
        }

        private void EnsurePageVisible(int page)
        {
            int top = TopPadding + (page * _itemHeight);
            int bottom = top + _itemHeight;

            if (top - TopPadding < _scrollOffset)
                ScrollTo(top - TopPadding);
            else if (bottom > _scrollOffset + ClientSize.Height)
                ScrollTo(bottom - ClientSize.Height);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateLayoutMetrics();
        }

        // ------------------------------------------------------------
        // Painting
        // ------------------------------------------------------------
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(_theme.ContentBg);
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            int first = Math.Max(0, (_scrollOffset - TopPadding) / _itemHeight);

            int last =
                Math.Min(
                    _model.PageCount - 1,
                    (_scrollOffset + ClientSize.Height) / _itemHeight);

            for (int page = first; page <= last; page++)
                DrawPageItem(g, page);

            DrawAddTile(g);
        }

        private void DrawPageItem(Graphics g, int page)
        {
            Rectangle thumb = GetThumbBounds(page);
            bool current = page == _currentPage;

            Rectangle band =
                new Rectangle(
                    4,
                    thumb.Y - 6,
                    ClientSize.Width - _scrollBar.Width - 8,
                    _itemHeight - 4);

            if (current || page == _hoverPage)
            {
                using (Brush highlight =
                    new SolidBrush(
                        CollageWorkspaceUi.Blend(
                            _theme.ContentBg,
                            _theme.ActiveColor,
                            current ? 0.24f : 0.10f)))
                {
                    g.FillRectangle(highlight, band);
                }
            }

            g.FillRectangle(Brushes.White, thumb);

            int cells = _model.CellsPerPage;
            float unitsPerPoint = UnitsPerPoint;

            for (int cell = 0; cell < cells; cell++)
            {
                int slot = (page * cells) + cell;

                RectangleF bounds =
                    RectangleF.Inflate(
                        CollageCellLayout.GetCellBounds(
                            thumb,
                            unitsPerPoint,
                            _model.Page,
                            cell),
                        -1.5f,
                        -1.5f);

                DrawMiniCell(g, slot, bounds);
            }

            using (Pen outline =
                new Pen(
                    current ? _theme.ActiveColor : _theme.BorderColor,
                    current ? 2f : 1f))
            {
                g.DrawRectangle(
                    outline,
                    thumb.X,
                    thumb.Y,
                    thumb.Width,
                    thumb.Height);
            }

            int filled = _model.CountOnPage(page);

            using (Brush text =
                new SolidBrush(
                    current ? _theme.TextColor : _theme.MutedTextColor))
            {
                g.DrawString(
                    "Page " + (page + 1) + "  ·  " + filled + "/" + cells,
                    _labelFont,
                    text,
                    new RectangleF(
                        band.X,
                        thumb.Bottom + 3,
                        band.Width,
                        _labelFont.Height + 2),
                    _centerFormat);
            }
        }

        private void DrawMiniCell(Graphics g, int slot, RectangleF bounds)
        {
            string path = _model.GetSlot(slot);

            if (path == null)
            {
                using (Brush empty =
                    new SolidBrush(Color.FromArgb(238, 241, 244)))
                {
                    g.FillRectangle(empty, bounds);
                }
            }
            else
            {
                Bitmap thumbnail = _thumbnails.Get(path);

                if (thumbnail != null)
                {
                    g.DrawImage(
                        thumbnail,
                        CollageWorkspaceUi.Fit(
                            thumbnail.Width,
                            thumbnail.Height,
                            bounds));
                }
                else
                {
                    using (Brush loading =
                        new SolidBrush(Color.FromArgb(214, 220, 226)))
                    {
                        g.FillRectangle(loading, bounds);
                    }
                }

                if (IsHighlighted != null && IsHighlighted(path))
                {
                    using (Pen selected = new Pen(_theme.ActiveColor, 2f))
                    {
                        g.DrawRectangle(
                            selected,
                            bounds.X,
                            bounds.Y,
                            bounds.Width,
                            bounds.Height);
                    }
                }
            }

            if (_dragActive && slot == _dropSlot)
            {
                using (Brush tint =
                    new SolidBrush(Color.FromArgb(110, _theme.ActiveColor)))
                using (Pen outline = new Pen(_theme.ActiveColor, 2f))
                {
                    g.FillRectangle(tint, bounds);
                    g.DrawRectangle(
                        outline,
                        bounds.X,
                        bounds.Y,
                        bounds.Width,
                        bounds.Height);
                }
            }
        }

        private void DrawAddTile(Graphics g)
        {
            Rectangle tile = GetAddTileBounds();

            if (tile.Bottom < 0 || tile.Top > ClientSize.Height)
                return;

            using (Pen outline = new Pen(_theme.BorderColor, 1f))
            using (Brush text = new SolidBrush(_theme.MutedTextColor))
            {
                outline.DashStyle = DashStyle.Dash;

                g.DrawRectangle(
                    outline,
                    tile.X,
                    tile.Y,
                    tile.Width,
                    tile.Height);

                g.DrawString(
                    "+  Add page",
                    _labelFont,
                    text,
                    tile,
                    _centerFormat);
            }
        }

        // ------------------------------------------------------------
        // Mouse
        // ------------------------------------------------------------
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            int page = HitTestPage(e.Location);

            if (e.Button == MouseButtons.Right)
            {
                ContextPage = page;
                return;
            }

            if (e.Button != MouseButtons.Left)
                return;

            if (page >= 0)
            {
                if (PageActivated != null)
                    PageActivated(page);
            }
            else if (GetAddTileBounds().Contains(e.Location) &&
                     AddPageRequested != null)
            {
                AddPageRequested(this, EventArgs.Empty);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            int page = HitTestPage(e.Location);

            if (page != _hoverPage)
            {
                _hoverPage = page;
                Invalidate();
            }

            Cursor =
                page >= 0 || GetAddTileBounds().Contains(e.Location)
                    ? Cursors.Hand
                    : Cursors.Default;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);

            if (_hoverPage >= 0)
            {
                _hoverPage = -1;
                Invalidate();
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            ScrollTo(_scrollOffset - (e.Delta * _itemHeight / 240));
        }

        // ------------------------------------------------------------
        // Drop target
        // ------------------------------------------------------------
        protected override void OnDragEnter(DragEventArgs e)
        {
            base.OnDragEnter(e);
            UpdateDrag(e);
        }

        protected override void OnDragOver(DragEventArgs e)
        {
            base.OnDragOver(e);
            UpdateDrag(e);
        }

        protected override void OnDragLeave(EventArgs e)
        {
            base.OnDragLeave(e);
            EndDrag();
        }

        protected override void OnDragDrop(DragEventArgs e)
        {
            base.OnDragDrop(e);
            UpdateDrag(e);

            string[] paths = CollageDrag.GetPaths(e.Data);
            int slot = _dropSlot;

            bool insert =
                InsertMode ^
                ((e.KeyState & CollageDrag.ShiftKeyState) != 0);

            EndDrag();

            if (paths != null && slot >= 0 && ImagesDropped != null)
                ImagesDropped(paths, slot, insert);
        }

        private void UpdateDrag(DragEventArgs e)
        {
            string[] paths = CollageDrag.GetPaths(e.Data);

            if (paths == null || paths.Length == 0)
            {
                e.Effect = DragDropEffects.None;
                return;
            }

            _dragActive = true;
            _dragPoint = PointToClient(new Point(e.X, e.Y));

            int slot = HitTestSlot(_dragPoint);
            int page = slot < 0 ? -1 : slot / _model.CellsPerPage;

            if (page != _springPage)
            {
                _springPage = page;
                _springStart = Environment.TickCount;
                _springFired = false;
            }

            if (slot != _dropSlot)
            {
                _dropSlot = slot;
                Invalidate();
            }

            if (!_dragTimer.Enabled)
                _dragTimer.Start();

            e.Effect =
                slot >= 0
                    ? DragDropEffects.Move
                    : DragDropEffects.None;
        }

        // Runs while an image is dragged over the strip: scrolls near the
        // edges and brings a page into the canvas once the pointer rests
        // on it.
        private void DragTimer_Tick(object sender, EventArgs e)
        {
            if (!_dragActive)
            {
                _dragTimer.Stop();
                return;
            }

            if (_dragPoint.Y < AutoScrollZone)
            {
                ScrollTo(_scrollOffset - 14);
                _dropSlot = HitTestSlot(_dragPoint);
            }
            else if (_dragPoint.Y > ClientSize.Height - AutoScrollZone)
            {
                ScrollTo(_scrollOffset + 14);
                _dropSlot = HitTestSlot(_dragPoint);
            }

            if (!_springFired &&
                _springPage >= 0 &&
                Environment.TickCount - _springStart >= SpringDelayMs)
            {
                _springFired = true;

                if (PageActivated != null)
                    PageActivated(_springPage);
            }
        }

        private void EndDrag()
        {
            _dragTimer.Stop();
            _dragActive = false;
            _dropSlot = -1;
            _springPage = -1;
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _dragTimer.Dispose();
                _labelFont.Dispose();
                _centerFormat.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
