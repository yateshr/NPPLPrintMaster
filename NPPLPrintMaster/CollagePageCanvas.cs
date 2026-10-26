using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace NPPLPrintMaster
{
    /// <summary>
    /// The page editor of the collage workspace.
    ///
    /// Shows every PDF page, one below the other, exactly as it will print.
    /// Mouse wheel scrolls, Ctrl + wheel zooms, middle mouse or Space + drag
    /// pans. Images are dragged between cells; dragging near the top or
    /// bottom edge scrolls so an image can be carried to any page.
    /// </summary>
    internal sealed class CollagePageCanvas : Control
    {
        // A4 at 96 DPI, so one PDF point is 96 / 72 world units.
        private const float WorldUnitsPerPoint = 96f / 72f;
        private const float PageShortSide =
            CollageCellLayout.PageShortSidePoints * WorldUnitsPerPoint;
        private const float PageLongSide =
            CollageCellLayout.PageLongSidePoints * WorldUnitsPerPoint;

        private const float SideMargin = 28f;
        private const float TopMargin = 52f;
        private const float PageGap = 52f;
        private const float BottomMargin = 28f;

        private const float MinZoom = 0.12f;
        private const float MaxZoom = 4f;
        private const float MinimumCaptionPixels = 11f;
        private const int AutoScrollZone = 56;

        private readonly CollageArrangementModel _model;
        private readonly CollageThumbnailCache _thumbnails;
        private readonly ThemeColors _theme;
        private readonly VScrollBar _vScroll;
        private readonly HScrollBar _hScroll;
        private readonly Timer _autoScrollTimer;
        private readonly Font _headerFont;
        private readonly Font _tagFont;
        private readonly Font _editorFont;
        private readonly StringFormat _centerFormat;

        private string _displayMode = CollageCellLayout.ModeBoth;
        private float _zoom = 0.6f;
        private int _scrollX;
        private int _scrollY;
        private bool _updatingScroll;
        private bool _viewInitialized;

        private int _selectedSlot = -1;
        private readonly HashSet<string> _selectedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private int _selectionAnchor = -1;
        private bool _collapseSelectionOnClick;
        private int _hoverSlot = -1;

        private int _pressSlot = -1;
        private Point _pressPoint;
        private int _dragSourceSlot = -1;

        private bool _panning;
        private bool _spaceDown;
        private Point _panStart;
        private Point _panStartScroll;

        private string[] _dragPaths;
        private bool _dragFromCanvas;
        private bool _dragExternal;
        private bool _dragShift;
        private Point _dragPoint;
        private int _dropSlot = -1;
        private int _autoScrollSpeed;

        private TextBox _editor;
        private string _editorPath;
        private bool _editorIsNote;
        private bool _editorChanged;

        public bool InsertMode { get; set; }

        public event EventHandler SelectionChanged;
        public event EventHandler ViewChanged;
        public event Action<string[], int, bool> ImagesDropped;
        public event Action<IList<string>> RemoveRequested;
        public event Action<bool> EditSelectionRequested;
        public event EventHandler PendingEditChanged;
        // Image path, true for the additional remark, the new text.
        public event Action<string, bool, string> TextEdited;

        // Image files dragged in from outside the application, with the cell
        // they were dropped on (-1 when not on a cell).
        public event Action<string[], int, bool> FilesDropped;

        public CollagePageCanvas(
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
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable,
                true);

            TabStop = true;
            AllowDrop = true;
            BackColor = theme.BgLight;

            _headerFont = new Font(fontFamily, 8.5f, FontStyle.Bold);
            _tagFont = new Font(fontFamily, 8f, FontStyle.Bold);
            _editorFont = new Font(fontFamily, 10f, FontStyle.Regular);

            _centerFormat = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            _vScroll = new VScrollBar { Dock = DockStyle.Right };
            _hScroll = new HScrollBar { Dock = DockStyle.Bottom, Visible = false };

            _vScroll.ValueChanged +=
                (s, e) =>
                {
                    if (!_updatingScroll)
                        ScrollTo(_scrollX, _vScroll.Value);
                };

            _hScroll.ValueChanged +=
                (s, e) =>
                {
                    if (!_updatingScroll)
                        ScrollTo(_hScroll.Value, _scrollY);
                };

            Controls.Add(_vScroll);
            Controls.Add(_hScroll);

            _autoScrollTimer = new Timer { Interval = 30 };
            _autoScrollTimer.Tick += AutoScrollTimer_Tick;
        }

        // ------------------------------------------------------------
        // Public surface
        // ------------------------------------------------------------
        public float Zoom
        {
            get { return _zoom; }
        }

        public int SelectedSlot
        {
            get { return _selectedSlot; }
        }

        public string SelectedPath
        {
            get { return _model.GetSlot(_selectedSlot); }
        }

        public IList<string> SelectedPaths
        {
            get { return _selectedPaths.Where(_model.IsPlaced).OrderBy(_model.IndexOf).ToList(); }
        }

        public bool IsEditing { get { return _editor != null; } }

        public void SelectPaths(IEnumerable<string> paths)
        {
            _selectedPaths.Clear();
            foreach (string path in paths)
                if (path != null && _model.IsPlaced(path)) _selectedPaths.Add(path);
            if (SelectedPath != null && !_selectedPaths.Contains(SelectedPath))
                _selectedSlot = -1;
            if (_selectedSlot < 0 && _selectedPaths.Count > 0)
                _selectedSlot = _selectedPaths.Min(_model.IndexOf);
            Invalidate();
        }

        public void SelectAllCells(bool currentPageOnly)
        {
            int start = currentPageOnly ? CurrentPage * _model.CellsPerPage : 0;
            int end = currentPageOnly ? start + _model.CellsPerPage : _model.SlotCount;
            SelectPaths(Enumerable.Range(start, Math.Max(0, end - start)).Select(_model.GetSlot));
            _selectionAnchor = _selectedSlot;
            RaiseSelectionChanged();
        }

        public void ClearCellSelection()
        {
            SetSelectedSlot(-1, true);
        }

        // Also used by recovery to retain text that is still being typed.
        public void CapturePendingText(Action<string, bool, string> capture)
        {
            if (_editor != null && _editorPath != null)
                capture(_editorPath, _editorIsNote, _editor.Text);
        }

        public string DisplayMode
        {
            get { return _displayMode; }
            set
            {
                _displayMode =
                    string.IsNullOrWhiteSpace(value)
                        ? CollageCellLayout.ModeBoth
                        : value;

                Invalidate();
            }
        }

        // The page under the middle of the viewport.
        public int CurrentPage
        {
            get
            {
                float worldY = ToWorld(new Point(0, ViewHeight / 2)).Y;

                int page =
                    (int)Math.Floor(
                        (worldY - TopMargin + (PageGap / 2f)) /
                        (PageSize.Height + PageGap));

                return Math.Max(0, Math.Min(_model.PageCount - 1, page));
            }
        }

        public void SelectSlot(int slot, bool ensureVisible)
        {
            SetSelectedSlot(slot, false);

            if (ensureVisible && _selectedSlot >= 0)
                EnsureSlotVisible(_selectedSlot);
        }

        public void SelectPath(string path, bool ensureVisible)
        {
            SelectSlot(_model.IndexOf(path), ensureVisible);
        }

        // Call after the arrangement changed.
        public void OnModelChanged()
        {
            _selectedPaths.RemoveWhere(p => !_model.IsPlaced(p));
            if (_selectedSlot >= _model.SlotCount)
                _selectedSlot = -1;

            _hoverSlot = -1;
            UpdateScrollBars();
            RepositionEditor();
            Invalidate();
        }

        public void FitPage()
        {
            int page = _viewInitialized ? CurrentPage : 0;

            // The horizontal scroll bar is never needed at this zoom, so
            // measure against the full height.
            float zoom =
                Math.Min(
                    (ClientSize.Width - _vScroll.Width) /
                    (PageSize.Width + (SideMargin * 2f)),
                    ClientSize.Height /
                    (TopMargin + PageSize.Height + BottomMargin));

            ApplyZoom(zoom);
            ScrollToPage(page);
        }

        public void FitWidth()
        {
            int page = _viewInitialized ? CurrentPage : 0;

            ApplyZoom(
                (ClientSize.Width - _vScroll.Width) /
                (PageSize.Width + (SideMargin * 2f)));

            ScrollToPage(page);
        }

        public void ZoomBy(float factor)
        {
            ZoomAt(new Point(ViewWidth / 2, ViewHeight / 2), factor);
        }

        public void ScrollToPage(int page)
        {
            page = Math.Max(0, Math.Min(_model.PageCount - 1, page));

            float pageTop = PageRectWorld(page).Y * _zoom;
            float pageHeight = PageSize.Height * _zoom;

            // Center the page when it fits; otherwise show its top with the
            // page title above it.
            float lead =
                Math.Max(
                    TopMargin * _zoom,
                    (ViewHeight - pageHeight) / 2f);

            ScrollTo(_scrollX, (int)Math.Round(pageTop - lead));
        }

        public void EnsureSlotVisible(int slot)
        {
            if (slot < 0 || slot >= _model.SlotCount)
                return;

            RectangleF cell = CellRectScreen(slot);

            bool visibleVertically =
                cell.Top >= 4f && cell.Bottom <= ViewHeight - 4f;

            bool visibleHorizontally =
                cell.Left >= 0f && cell.Right <= ViewWidth;

            if (visibleVertically && visibleHorizontally)
                return;

            int targetX = _scrollX;
            int targetY = _scrollY;

            if (!visibleHorizontally)
            {
                targetX +=
                    (int)Math.Round(
                        cell.Left + (cell.Width / 2f) - (ViewWidth / 2f));
            }

            if (!visibleVertically)
            {
                float pageHeight =
                    (TopMargin + PageSize.Height) * _zoom;

                if (pageHeight <= ViewHeight)
                {
                    ScrollToPage(slot / _model.CellsPerPage);
                    targetY = _scrollY;
                }
                else
                {
                    targetY +=
                        (int)Math.Round(
                            cell.Top + (cell.Height / 2f) - (ViewHeight / 2f));
                }
            }

            ScrollTo(targetX, targetY);
        }

        // ------------------------------------------------------------
        // Geometry
        // ------------------------------------------------------------
        private SizeF PageSize
        {
            get
            {
                return _model.Page.Landscape
                    ? new SizeF(PageLongSide, PageShortSide)
                    : new SizeF(PageShortSide, PageLongSide);
            }
        }

        private int ViewWidth
        {
            get { return Math.Max(1, ClientSize.Width - _vScroll.Width); }
        }

        private int ViewHeight
        {
            get
            {
                return Math.Max(
                    1,
                    ClientSize.Height - (_hScroll.Visible ? _hScroll.Height : 0));
            }
        }

        private float ContentWidthWorld
        {
            get { return PageSize.Width + (SideMargin * 2f); }
        }

        private float ContentHeightWorld
        {
            get
            {
                int pages = Math.Max(1, _model.PageCount);

                return TopMargin +
                       (pages * PageSize.Height) +
                       ((pages - 1) * PageGap) +
                       BottomMargin;
            }
        }

        // Content smaller than the viewport is centered in it.
        private float BaseX
        {
            get
            {
                return Math.Max(
                    0f,
                    (ViewWidth - (ContentWidthWorld * _zoom)) / 2f);
            }
        }

        private float BaseY
        {
            get
            {
                return Math.Max(
                    0f,
                    (ViewHeight - (ContentHeightWorld * _zoom)) / 2f);
            }
        }

        private RectangleF PageRectWorld(int page)
        {
            SizeF size = PageSize;

            return new RectangleF(
                SideMargin,
                TopMargin + (page * (size.Height + PageGap)),
                size.Width,
                size.Height);
        }

        private RectangleF ToScreen(RectangleF world)
        {
            return new RectangleF(
                BaseX - _scrollX + (world.X * _zoom),
                BaseY - _scrollY + (world.Y * _zoom),
                world.Width * _zoom,
                world.Height * _zoom);
        }

        private PointF ToWorld(Point screen)
        {
            return new PointF(
                (screen.X - BaseX + _scrollX) / _zoom,
                (screen.Y - BaseY + _scrollY) / _zoom);
        }

        private RectangleF CellRectScreen(int slot)
        {
            return CollageCellLayout.GetCellBounds(
                ToScreen(PageRectWorld(slot / _model.CellsPerPage)),
                WorldUnitsPerPoint * _zoom,
                _model.Page,
                slot % _model.CellsPerPage);
        }

        // The cell under a point. Anywhere on the white page counts, so a
        // drop in the page margin still lands in the nearest cell.
        private int HitTestSlot(Point screen)
        {
            if (screen.X < 0 || screen.X >= ViewWidth ||
                screen.Y < 0 || screen.Y >= ViewHeight)
                return -1;

            PointF world = ToWorld(screen);
            SizeF size = PageSize;

            int page =
                (int)Math.Floor(
                    (world.Y - TopMargin) / (size.Height + PageGap));

            if (page < 0 || page >= _model.PageCount)
                return -1;

            RectangleF pageRect = PageRectWorld(page);
            if (!pageRect.Contains(world))
                return -1;

            return (page * _model.CellsPerPage) +
                   CollageCellLayout.GetNearestCell(
                       pageRect,
                       WorldUnitsPerPoint,
                       _model.Page,
                       world);
        }

        // ------------------------------------------------------------
        // Scrolling and zoom
        // ------------------------------------------------------------
        private void UpdateScrollBars()
        {
            _updatingScroll = true;
            try
            {
                int contentWidth =
                    (int)Math.Ceiling(ContentWidthWorld * _zoom);

                int contentHeight =
                    (int)Math.Ceiling(ContentHeightWorld * _zoom);

                bool needHorizontal = contentWidth > ViewWidth + 1;

                if (_hScroll.Visible != needHorizontal)
                    _hScroll.Visible = needHorizontal;

                _scrollX =
                    Math.Max(0, Math.Min(_scrollX, Math.Max(0, contentWidth - ViewWidth)));

                _scrollY =
                    Math.Max(0, Math.Min(_scrollY, Math.Max(0, contentHeight - ViewHeight)));

                CollageWorkspaceUi.ConfigureScrollBar(
                    _vScroll,
                    contentHeight,
                    ViewHeight,
                    _scrollY);

                CollageWorkspaceUi.ConfigureScrollBar(
                    _hScroll,
                    contentWidth,
                    ViewWidth,
                    _scrollX);
            }
            finally
            {
                _updatingScroll = false;
            }
        }

        private void ScrollTo(int x, int y)
        {
            _scrollX = x;
            _scrollY = y;

            UpdateScrollBars();
            RepositionEditor();
            Invalidate();
            OnViewChanged();
        }

        private void ApplyZoom(float zoom)
        {
            CommitEdit();
            _zoom = Math.Max(MinZoom, Math.Min(MaxZoom, zoom));
            _viewInitialized = true;
            UpdateScrollBars();
            Invalidate();
            OnViewChanged();
        }

        public void ZoomAt(Point anchor, float factor)
        {
            float zoom = Math.Max(MinZoom, Math.Min(MaxZoom, _zoom * factor));

            if (Math.Abs(zoom - _zoom) < 0.0005f)
                return;

            PointF world = ToWorld(anchor);

            CommitEdit();
            _zoom = zoom;

            // The hScroll visibility may change with the zoom; settle it
            // before the anchor point is restored.
            UpdateScrollBars();

            ScrollTo(
                (int)Math.Round(BaseX + (world.X * _zoom) - anchor.X),
                (int)Math.Round(BaseY + (world.Y * _zoom) - anchor.Y));
        }

        private void OnViewChanged()
        {
            EventHandler handler = ViewChanged;

            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            CommitEdit();
            UpdateScrollBars();
            OnViewChanged();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);

            if ((ModifierKeys & Keys.Control) != 0)
                ZoomAt(e.Location, e.Delta > 0 ? 1.15f : 1f / 1.15f);
            else if ((ModifierKeys & Keys.Shift) != 0)
                ScrollTo(_scrollX - e.Delta, _scrollY);
            else
                ScrollTo(_scrollX, _scrollY - e.Delta);
        }

        // ------------------------------------------------------------
        // Painting
        // ------------------------------------------------------------
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(_theme.BgLight);
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;

            float unitsPerPoint = WorldUnitsPerPoint * _zoom;

            using (Font captionFont = CreateCaptionFont())
            using (StringFormat captionFormat =
                CollageCellLayout.CreateCaptionFormat())
            {
                float stride = (PageSize.Height + PageGap) * _zoom;
                float firstTop = ToScreen(PageRectWorld(0)).Y;

                int first =
                    Math.Max(0, (int)Math.Floor(-firstTop / stride));

                // One extra page so the title of the next page still shows
                // when only the gap above it is in view.
                int last =
                    Math.Min(
                        _model.PageCount - 1,
                        (int)Math.Floor((ViewHeight - firstTop) / stride) + 1);

                for (int page = first; page <= last; page++)
                {
                    DrawPage(
                        g,
                        page,
                        captionFont,
                        captionFormat,
                        unitsPerPoint);
                }
            }

            DrawDropFeedback(g);
        }

        // Names are drawn at their true printed size once the page is
        // zoomed in far enough to read them. Zoomed further out they are
        // kept at a readable size instead of shrinking to nothing.
        private Font CreateCaptionFont()
        {
            float unitsPerPoint = WorldUnitsPerPoint * _zoom;

            float cellWidth =
                (PageSize.Width * _zoom -
                 (CollageCellLayout.OuterMarginPoints * unitsPerPoint * 2f)) /
                _model.Page.Columns;

            float readable =
                Math.Min(MinimumCaptionPixels, cellWidth / 9f);

            return new Font(
                CollageCellLayout.CaptionFontFamily,
                Math.Max(
                    1f,
                    Math.Max(
                        _model.Page.CaptionFontPoints * unitsPerPoint,
                        readable)),
                FontStyle.Regular,
                GraphicsUnit.Pixel);
        }

        private CollageCellLayout LayoutCell(
            Graphics g,
            Font captionFont,
            StringFormat captionFormat,
            string path,
            RectangleF cell,
            out string filenameLine,
            out string remarkLine,
            out string noteLine)
        {
            Size imageSize;
            if (!_thumbnails.TryGetOriginalSize(path, out imageSize))
                imageSize = new Size(4, 3);

            CollageCellLayout.ResolveCaptions(
                path,
                _model.GetRemark(path),
                _model.GetNote(path),
                _displayMode,
                out filenameLine,
                out remarkLine,
                out noteLine);

            return CollageCellLayout.Arrange(
                g,
                captionFont,
                captionFormat,
                cell,
                WorldUnitsPerPoint * _zoom,
                imageSize.Width,
                imageSize.Height,
                filenameLine,
                remarkLine,
                noteLine);
        }

        private void DrawPage(
            Graphics g,
            int page,
            Font captionFont,
            StringFormat captionFormat,
            float unitsPerPoint)
        {
            RectangleF pageRect = ToScreen(PageRectWorld(page));

            using (Brush shadow = new SolidBrush(Color.FromArgb(60, 0, 0, 0)))
            {
                g.FillRectangle(
                    shadow,
                    pageRect.X + 4f,
                    pageRect.Y + 4f,
                    pageRect.Width,
                    pageRect.Height);
            }

            g.FillRectangle(Brushes.White, pageRect);

            using (Pen outline = new Pen(_theme.BorderColor, 1f))
            {
                g.DrawRectangle(
                    outline,
                    pageRect.X,
                    pageRect.Y,
                    pageRect.Width,
                    pageRect.Height);
            }

            DrawPageHeader(g, page, pageRect);

            // The printed header and footer, exactly as the PDF has them.
            CollageCellLayout.DrawHeaderFooter(
                g,
                pageRect,
                unitsPerPoint,
                _model.Page,
                page + 1,
                _model.PageCount,
                DateTime.Now);

            int cells = _model.CellsPerPage;

            for (int cell = 0; cell < cells; cell++)
            {
                DrawCell(
                    g,
                    (page * cells) + cell,
                    CollageCellLayout.GetCellBounds(
                        pageRect,
                        unitsPerPoint,
                        _model.Page,
                        cell),
                    captionFont,
                    captionFormat,
                    unitsPerPoint);
            }
        }

        private void DrawPageHeader(Graphics g, int page, RectangleF pageRect)
        {
            float top = pageRect.Y - _headerFont.Height - 5f;

            if (PageGap * _zoom < _headerFont.Height + 6f ||
                pageRect.Width < 150f)
                return;

            using (Brush titleBrush = new SolidBrush(_theme.TextColor))
            using (Brush mutedBrush = new SolidBrush(_theme.MutedTextColor))
            using (StringFormat right =
                new StringFormat { Alignment = StringAlignment.Far })
            {
                g.DrawString(
                    "PAGE " + (page + 1) + " of " + _model.PageCount,
                    _headerFont,
                    titleBrush,
                    pageRect.X - 2f,
                    top);

                if (pageRect.Width < 290f)
                    return;

                g.DrawString(
                    _model.CountOnPage(page) + " of " +
                    _model.CellsPerPage + " cells filled",
                    _headerFont,
                    mutedBrush,
                    new RectangleF(
                        pageRect.X,
                        top,
                        pageRect.Width + 2f,
                        _headerFont.Height + 2f),
                    right);
            }
        }

        private void DrawCell(
            Graphics g,
            int slot,
            RectangleF cell,
            Font captionFont,
            StringFormat captionFormat,
            float unitsPerPoint)
        {
            string path = _model.GetSlot(slot);

            if (_model.Page.DrawBorders)
            {
                // Printed in the PDF.
                using (Pen border =
                    new Pen(Color.Gray, Math.Max(1f, 0.5f * unitsPerPoint)))
                {
                    g.DrawRectangle(border, cell.X, cell.Y, cell.Width, cell.Height);
                }
            }
            else
            {
                // Guide only; not printed.
                using (Pen guide = new Pen(Color.FromArgb(206, 214, 222), 1f))
                {
                    guide.DashStyle = DashStyle.Dash;
                    g.DrawRectangle(guide, cell.X, cell.Y, cell.Width, cell.Height);
                }
            }

            if (path == null)
            {
                DrawEmptyCell(g, slot, cell);
            }
            else
            {
                DrawImageCell(
                    g,
                    slot,
                    path,
                    cell,
                    captionFont,
                    captionFormat);
            }

            if ((path != null && _selectedPaths.Contains(path)) ||
                (path == null && slot == _selectedSlot))
            {
                using (Brush tint = new SolidBrush(Color.FromArgb(24, _theme.ActiveColor)))
                    g.FillRectangle(tint, cell);
                using (Pen selected = new Pen(_theme.ActiveColor, 2f))
                {
                    g.DrawRectangle(
                        selected,
                        cell.X + 1f,
                        cell.Y + 1f,
                        cell.Width - 2f,
                        cell.Height - 2f);
                }
            }
            else if (slot == _hoverSlot && _dragPaths == null)
            {
                using (Pen hover =
                    new Pen(Color.FromArgb(120, _theme.ActiveColor), 1f))
                {
                    g.DrawRectangle(
                        hover,
                        cell.X + 1f,
                        cell.Y + 1f,
                        cell.Width - 2f,
                        cell.Height - 2f);
                }
            }
        }

        private void DrawEmptyCell(Graphics g, int slot, RectangleF cell)
        {
            float size = Math.Min(cell.Width, cell.Height) * 0.16f;

            if (size < 6f)
                return;

            using (Font font =
                new Font(
                    _headerFont.FontFamily,
                    Math.Min(34f, size),
                    FontStyle.Bold,
                    GraphicsUnit.Pixel))
            using (Brush brush = new SolidBrush(Color.FromArgb(205, 212, 219)))
            {
                g.DrawString(
                    ((slot % _model.CellsPerPage) + 1).ToString(),
                    font,
                    brush,
                    cell,
                    _centerFormat);
            }
        }

        private void DrawImageCell(
            Graphics g,
            int slot,
            string path,
            RectangleF cell,
            Font captionFont,
            StringFormat captionFormat)
        {
            Bitmap thumbnail = _thumbnails.Get(path);

            string filenameLine;
            string remarkLine;
            string noteLine;

            CollageCellLayout layout =
                LayoutCell(
                    g,
                    captionFont,
                    captionFormat,
                    path,
                    cell,
                    out filenameLine,
                    out remarkLine,
                    out noteLine);

            // The image being dragged stays faintly visible in its cell.
            float opacity = slot == _dragSourceSlot ? 0.3f : 1f;

            if (thumbnail != null)
            {
                CollageWorkspaceUi.DrawThumbnail(
                    g,
                    thumbnail,
                    layout.Image,
                    opacity);
            }
            else
            {
                using (Brush placeholder =
                    new SolidBrush(Color.FromArgb(236, 239, 242)))
                using (Brush text =
                    new SolidBrush(Color.FromArgb(140, 148, 156)))
                {
                    g.FillRectangle(placeholder, layout.Image);

                    if (layout.Image.Width > 70f)
                    {
                        g.DrawString(
                            _thumbnails.IsUnreadable(path)
                                ? "Cannot read image"
                                : "Loading…",
                            _tagFont,
                            text,
                            layout.Image,
                            _centerFormat);
                    }
                }
            }

            using (Brush textBrush =
                new SolidBrush(Color.FromArgb((int)(255 * opacity), Color.Black)))
            {
                DrawCaption(
                    g,
                    filenameLine,
                    captionFont,
                    captionFormat,
                    textBrush,
                    layout.Filename);

                DrawCaption(
                    g,
                    remarkLine,
                    captionFont,
                    captionFormat,
                    textBrush,
                    layout.Remark);

                DrawCaption(
                    g,
                    noteLine,
                    captionFont,
                    captionFormat,
                    textBrush,
                    layout.Note);
            }
        }

        private static void DrawCaption(
            Graphics g,
            string text,
            Font font,
            StringFormat format,
            Brush brush,
            RectangleF bounds)
        {
            if (string.IsNullOrEmpty(text) || bounds.IsEmpty)
                return;

            if (font.Size >= 4f)
            {
                g.DrawString(text, font, brush, bounds, format);
                return;
            }

            // Too small to read at this zoom: show where the name sits.
            using (Brush bar = new SolidBrush(Color.FromArgb(170, 176, 182)))
            {
                float width = Math.Min(bounds.Width * 0.6f, text.Length * font.Size * 0.5f);

                g.FillRectangle(
                    bar,
                    bounds.X + ((bounds.Width - width) / 2f),
                    bounds.Y + (bounds.Height / 2f) - 0.5f,
                    width,
                    1f);
            }
        }

        private void DrawDropFeedback(Graphics g)
        {
            if (_dragPaths == null)
                return;

            if (_dropSlot >= 0)
            {
                RectangleF cell = CellRectScreen(_dropSlot);
                string occupant = _model.GetSlot(_dropSlot);
                bool insert = InsertMode ^ _dragShift;

                bool sameCell =
                    _dragFromCanvas &&
                    _dragPaths.Length == 1 &&
                    string.Equals(
                        occupant,
                        _dragPaths[0],
                        StringComparison.OrdinalIgnoreCase);

                string tag;

                using (Brush tint =
                    new SolidBrush(Color.FromArgb(46, _theme.ActiveColor)))
                using (Pen outline = new Pen(_theme.ActiveColor, 2.5f))
                using (Brush solid = new SolidBrush(_theme.ActiveColor))
                {
                    g.FillRectangle(tint, cell);

                    if (insert && occupant != null && !sameCell)
                    {
                        // Insertion bar on the leading edge of the cell.
                        g.FillRectangle(
                            solid,
                            cell.X - 3f,
                            cell.Y,
                            6f,
                            cell.Height);

                        tag = "Insert here";
                    }
                    else
                    {
                        g.DrawRectangle(
                            outline,
                            cell.X + 1f,
                            cell.Y + 1f,
                            cell.Width - 2f,
                            cell.Height - 2f);

                        tag =
                            sameCell
                                ? null
                                : occupant == null
                                    ? "Place here"
                                    : _dragFromCanvas
                                        ? "Swap"
                                        : "Replace";
                    }
                }

                if (tag != null)
                    DrawTag(g, tag, cell.X + 8f, cell.Y + 8f);
            }

            DrawDragGhost(g);
        }

        private void DrawTag(Graphics g, string text, float x, float y)
        {
            SizeF size = g.MeasureString(text, _tagFont);

            RectangleF bounds =
                new RectangleF(x, y, size.Width + 10f, size.Height + 4f);

            using (Brush back = new SolidBrush(_theme.ActiveColor))
            using (Brush fore =
                new SolidBrush(
                    ThemeManager.GetContrastTextColor(_theme.ActiveColor)))
            {
                g.FillRectangle(back, bounds);
                g.DrawString(text, _tagFont, fore, bounds, _centerFormat);
            }
        }

        // A small copy of the dragged image that follows the pointer.
        private void DrawDragGhost(Graphics g)
        {
            if (_dragExternal)
            {
                // Files from outside are not loaded until they are dropped.
                DrawTag(
                    g,
                    _dragPaths.Length == 1
                        ? "Add 1 file"
                        : "Add " + _dragPaths.Length + " files",
                    _dragPoint.X + 16f,
                    _dragPoint.Y + 14f);
                return;
            }

            Bitmap thumbnail = _thumbnails.Get(_dragPaths[0]);

            if (thumbnail == null)
                return;

            RectangleF ghost =
                CollageWorkspaceUi.Fit(
                    thumbnail.Width,
                    thumbnail.Height,
                    new RectangleF(
                        _dragPoint.X + 16f,
                        _dragPoint.Y + 14f,
                        120f,
                        96f));

            ghost.X = _dragPoint.X + 16f;
            ghost.Y = _dragPoint.Y + 14f;

            CollageWorkspaceUi.DrawThumbnail(g, thumbnail, ghost, 0.82f);

            using (Pen outline = new Pen(_theme.ActiveColor, 1.5f))
                g.DrawRectangle(outline, ghost.X, ghost.Y, ghost.Width, ghost.Height);

            if (_dragPaths.Length > 1)
            {
                DrawTag(
                    g,
                    _dragPaths.Length + " images",
                    ghost.X,
                    ghost.Bottom + 3f);
            }
        }

        // ------------------------------------------------------------
        // Mouse
        // ------------------------------------------------------------
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            CommitEdit();

            if (e.Button == MouseButtons.Middle ||
                (e.Button == MouseButtons.Left && _spaceDown))
            {
                _panning = true;
                _panStart = e.Location;
                _panStartScroll = new Point(_scrollX, _scrollY);
                Cursor = Cursors.SizeAll;
                return;
            }

            if (e.Button != MouseButtons.Left &&
                e.Button != MouseButtons.Right)
                return;

            int slot = HitTestSlot(e.Location);
            _collapseSelectionOnClick = false;
            bool control = (ModifierKeys & Keys.Control) != 0;
            bool shift = (ModifierKeys & Keys.Shift) != 0;
            if (e.Button == MouseButtons.Right && _selectedPaths.Contains(_model.GetSlot(slot) ?? string.Empty))
                _selectedSlot = slot;
            else if (control || shift)
                ExtendSelection(slot, control, shift);
            else if (slot >= 0 && _selectedPaths.Count > 1 &&
                _selectedPaths.Contains(_model.GetSlot(slot) ?? string.Empty))
            {
                _selectedSlot = slot;
                _collapseSelectionOnClick = true;
            }
            else SetSelectedSlot(slot, true);

            _pressSlot = -1;

            if (e.Button == MouseButtons.Left &&
                !control && !shift &&
                slot >= 0 &&
                _model.GetSlot(slot) != null)
            {
                _pressSlot = slot;
                _pressPoint = e.Location;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_panning)
            {
                ScrollTo(
                    _panStartScroll.X - (e.X - _panStart.X),
                    _panStartScroll.Y - (e.Y - _panStart.Y));
                return;
            }

            if (_pressSlot >= 0 && (e.Button & MouseButtons.Left) != 0)
            {
                Size dragSize = SystemInformation.DragSize;

                if (Math.Abs(e.X - _pressPoint.X) > dragSize.Width / 2 ||
                    Math.Abs(e.Y - _pressPoint.Y) > dragSize.Height / 2)
                {
                    BeginDrag();
                }

                return;
            }

            int hover = HitTestSlot(e.Location);

            if (hover != _hoverSlot)
            {
                _hoverSlot = hover;
                Invalidate();
            }

            if (!_spaceDown)
            {
                Cursor =
                    hover >= 0 && _model.GetSlot(hover) != null
                        ? Cursors.Hand
                        : Cursors.Default;
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (_panning)
            {
                _panning = false;
                Cursor = _spaceDown ? Cursors.SizeAll : Cursors.Default;
            }

            if (_collapseSelectionOnClick && _pressSlot >= 0)
                SetSelectedSlot(_pressSlot, true);
            _collapseSelectionOnClick = false;
            _pressSlot = -1;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);

            if (_hoverSlot >= 0)
            {
                _hoverSlot = -1;
                Invalidate();
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            _pressSlot = -1;

            if (e.Button != MouseButtons.Left)
                return;

            int slot = HitTestSlot(e.Location);
            string path = _model.GetSlot(slot);

            if (path != null)
                BeginEdit(path, IsOverNote(e.Location, slot));
        }

        private void BeginDrag()
        {
            int slot = _pressSlot;
            _pressSlot = -1;
            _collapseSelectionOnClick = false;

            string path = _model.GetSlot(slot);
            if (path == null)
                return;

            _dragSourceSlot = slot;
            Invalidate();

            try
            {
                DoDragDrop(
                    CollageDrag.Create(SelectedPaths.ToArray(), CollageDrag.SourceCanvas),
                    DragDropEffects.Move);
            }
            finally
            {
                _dragSourceSlot = -1;
                ClearDropFeedback();
            }
        }

        private void SetSelectedSlot(int slot, bool raise)
        {
            if (slot < 0 || slot >= _model.SlotCount)
                slot = -1;

            _selectedSlot = slot;
            _selectionAnchor = slot;
            _selectedPaths.Clear();
            string path = _model.GetSlot(slot);
            if (path != null) _selectedPaths.Add(path);
            Invalidate();

            if (raise) RaiseSelectionChanged();
        }

        private void RaiseSelectionChanged()
        {
            if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
        }

        internal void ExtendSelection(int slot, bool control, bool shift)
        {
            if (slot < 0 || slot >= _model.SlotCount) return;
            if (shift)
            {
                int anchor = _selectionAnchor >= 0 && _selectionAnchor < _model.SlotCount
                    ? _selectionAnchor : slot;
                if (!control) _selectedPaths.Clear();
                for (int i = Math.Min(anchor, slot); i <= Math.Max(anchor, slot); i++)
                {
                    string path = _model.GetSlot(i);
                    if (path != null) _selectedPaths.Add(path);
                }
            }
            else
            {
                string path = _model.GetSlot(slot);
                if (path != null && !_selectedPaths.Remove(path)) _selectedPaths.Add(path);
                _selectionAnchor = slot;
            }
            _selectedSlot = slot;
            Invalidate();
            RaiseSelectionChanged();
        }

        // ------------------------------------------------------------
        // Keyboard
        // ------------------------------------------------------------
        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Up:
                case Keys.Down:
                case Keys.Left:
                case Keys.Right:
                case Keys.Enter:
                    return true;
            }

            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            switch (e.KeyCode)
            {
                case Keys.Space:
                    if (!_spaceDown)
                    {
                        _spaceDown = true;
                        Cursor = Cursors.SizeAll;
                    }
                    break;
                case Keys.Escape:
                    ClearCellSelection();
                    break;
                case Keys.Left:
                    MoveSelection(-1);
                    break;
                case Keys.Right:
                    MoveSelection(1);
                    break;
                case Keys.Up:
                    MoveSelection(-_model.Page.Columns);
                    break;
                case Keys.Down:
                    MoveSelection(_model.Page.Columns);
                    break;
                case Keys.PageUp:
                    ScrollToPage(CurrentPage - 1);
                    break;
                case Keys.PageDown:
                    ScrollToPage(CurrentPage + 1);
                    break;
                case Keys.Home:
                    ScrollToPage(0);
                    break;
                case Keys.End:
                    ScrollToPage(_model.PageCount - 1);
                    break;
                case Keys.Delete:
                case Keys.Back:
                    if (_selectedPaths.Count > 0 && RemoveRequested != null)
                        RemoveRequested(SelectedPaths);
                    break;
                case Keys.F2:
                case Keys.Enter:
                    // Shift edits the additional remark instead.
                    if (_selectedPaths.Count > 1 && EditSelectionRequested != null)
                        EditSelectionRequested(e.Shift);
                    else if (SelectedPath != null)
                        BeginEdit(SelectedPath, e.Shift);
                    break;
                default:
                    return;
            }

            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);

            if (e.KeyCode == Keys.Space)
                ReleaseSpace();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            ReleaseSpace();
        }

        private void ReleaseSpace()
        {
            if (!_spaceDown)
                return;

            _spaceDown = false;

            if (!_panning)
                Cursor = Cursors.Default;
        }

        private void MoveSelection(int delta)
        {
            int slot =
                _selectedSlot < 0
                    ? CurrentPage * _model.CellsPerPage
                    : Math.Max(
                        0,
                        Math.Min(_model.SlotCount - 1, _selectedSlot + delta));

            if ((ModifierKeys & Keys.Shift) != 0)
                ExtendSelection(slot, (ModifierKeys & Keys.Control) != 0, true);
            else SetSelectedSlot(slot, true);
            EnsureSlotVisible(slot);
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
            ClearDropFeedback();
        }

        protected override void OnDragDrop(DragEventArgs e)
        {
            base.OnDragDrop(e);
            UpdateDrag(e);

            string[] paths = _dragPaths;
            int slot = _dropSlot;
            bool insert = InsertMode ^ _dragShift;
            bool external = _dragExternal;

            ClearDropFeedback();

            if (paths == null)
                return;

            if (external)
            {
                // New files may be dropped anywhere; off a cell they are
                // added to the library as not placed.
                if (FilesDropped != null)
                    FilesDropped(paths, slot, insert);
            }
            else if (slot >= 0 && ImagesDropped != null)
            {
                ImagesDropped(paths, slot, insert);
            }
        }

        private void UpdateDrag(DragEventArgs e)
        {
            string[] paths = CollageDrag.GetPaths(e.Data);
            bool external = false;

            if (paths == null || paths.Length == 0)
            {
                // Files dragged in from Windows Explorer.
                paths = CollageDrag.GetExternalFiles(e.Data);
                external = paths != null;
            }

            if (paths == null || paths.Length == 0)
            {
                e.Effect = DragDropEffects.None;
                return;
            }

            _dragPaths = paths;
            _dragExternal = external;
            _dragFromCanvas = CollageDrag.IsFromCanvas(e.Data);
            _dragShift = (e.KeyState & CollageDrag.ShiftKeyState) != 0;
            _dragPoint = PointToClient(new Point(e.X, e.Y));
            _dropSlot = HitTestSlot(_dragPoint);

            UpdateAutoScroll();
            Invalidate();

            e.Effect =
                external
                    ? DragDropEffects.Copy
                    : _dropSlot >= 0
                        ? DragDropEffects.Move
                        : DragDropEffects.None;
        }

        // Dragging near the top or bottom edge scrolls the pages, faster
        // the closer the pointer is to the edge.
        private void UpdateAutoScroll()
        {
            int speed = 0;

            if (_dragPoint.Y < AutoScrollZone)
                speed = -(AutoScrollZone - Math.Max(0, _dragPoint.Y));
            else if (_dragPoint.Y > ViewHeight - AutoScrollZone)
                speed = Math.Min(AutoScrollZone, _dragPoint.Y - (ViewHeight - AutoScrollZone));

            _autoScrollSpeed = speed / 2;

            if (_autoScrollSpeed != 0 && !_autoScrollTimer.Enabled)
                _autoScrollTimer.Start();
            else if (_autoScrollSpeed == 0 && _autoScrollTimer.Enabled)
                _autoScrollTimer.Stop();
        }

        private void AutoScrollTimer_Tick(object sender, EventArgs e)
        {
            if (_dragPaths == null || _autoScrollSpeed == 0)
            {
                _autoScrollTimer.Stop();
                return;
            }

            ScrollTo(_scrollX, _scrollY + _autoScrollSpeed);
            _dropSlot = HitTestSlot(_dragPoint);
        }

        private void ClearDropFeedback()
        {
            _autoScrollTimer.Stop();
            _autoScrollSpeed = 0;
            _dragPaths = null;
            _dropSlot = -1;
            Invalidate();
        }

        // ------------------------------------------------------------
        // Editing the remark in place
        // ------------------------------------------------------------
        // Edits the remark of an image, or its additional remark (the line
        // below it) when note is true.
        public void BeginEdit(string path, bool note)
        {
            int slot = _model.IndexOf(path);
            if (slot < 0)
                return;

            CommitEdit();
            SetSelectedSlot(slot, true);
            EnsureSlotVisible(slot);

            _editorPath = path;
            _editorIsNote = note;
            _editorChanged = false;
            _editor = new TextBox
            {
                BorderStyle = BorderStyle.FixedSingle,
                TextAlign = HorizontalAlignment.Center,
                Font = _editorFont,
                BackColor = Color.White,
                ForeColor = Color.Black,
                Text =
                    note
                        ? _model.GetNote(path) ?? string.Empty
                        : _model.GetEffectiveRemark(path)
            };

            _editor.KeyDown += Editor_KeyDown;
            _editor.TextChanged += (s, e) =>
            {
                _editorChanged = true;
                if (PendingEditChanged != null) PendingEditChanged(this, EventArgs.Empty);
            };
            _editor.LostFocus += (s, e) => CommitEdit();

            Controls.Add(_editor);
            RepositionEditor();
            _editor.BringToFront();
            _editor.SelectAll();
            _editor.Focus();
        }

        private void RepositionEditor()
        {
            if (_editor == null)
                return;

            int slot = _model.IndexOf(_editorPath);

            if (slot < 0)
            {
                EndEdit(false);
                return;
            }

            RectangleF cell = CellRectScreen(slot);
            RectangleF caption = GetCaptionBounds(_editorPath, cell, _editorIsNote);

            int width =
                Math.Max(190, Math.Min(440, (int)cell.Width - 12));

            int height = _editor.PreferredHeight;

            // Sits on the name it edits.
            int x = (int)(cell.X + ((cell.Width - width) / 2f));
            int y = (int)(caption.Y + ((caption.Height - height) / 2f));

            x = Math.Max(4, Math.Min(ViewWidth - width - 4, x));
            y = Math.Max(4, Math.Min(ViewHeight - height - 4, y));

            _editor.Bounds = new Rectangle(x, y, width, height);
        }

        // Where the line of text being edited is drawn for a cell.
        //
        // Remark: its own line when one is shown, otherwise the filename
        // line, otherwise just below the image.
        // Additional remark: its own line, or where that line will appear,
        // directly below the last line of text.
        private RectangleF GetCaptionBounds(
            string path,
            RectangleF cell,
            bool note)
        {
            using (Graphics g = CreateGraphics())
            using (Font captionFont = CreateCaptionFont())
            using (StringFormat captionFormat =
                CollageCellLayout.CreateCaptionFormat())
            {
                g.TextRenderingHint = TextRenderingHint.AntiAlias;

                string filenameLine;
                string remarkLine;
                string noteLine;

                CollageCellLayout layout =
                    LayoutCell(
                        g,
                        captionFont,
                        captionFormat,
                        path,
                        cell,
                        out filenameLine,
                        out remarkLine,
                        out noteLine);

                float lineHeight = captionFont.GetHeight(g);

                RectangleF lastLine =
                    !layout.Remark.IsEmpty
                        ? layout.Remark
                        : !layout.Filename.IsEmpty
                            ? layout.Filename
                            : new RectangleF(
                                cell.X,
                                layout.Image.Bottom + 2f,
                                cell.Width,
                                lineHeight);

                if (!note)
                    return lastLine;

                return !layout.Note.IsEmpty
                    ? layout.Note
                    : new RectangleF(
                        lastLine.X,
                        lastLine.Bottom,
                        lastLine.Width,
                        lineHeight);
            }
        }

        // True when the point is on (or below) the additional remark of the
        // image in that cell, so a double-click there edits that line.
        private bool IsOverNote(Point point, int slot)
        {
            string path = _model.GetSlot(slot);

            if (path == null || _model.GetNote(path) == null)
                return false;

            RectangleF note =
                GetCaptionBounds(path, CellRectScreen(slot), true);

            return point.Y >= note.Top;
        }

        private void Editor_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                EndEdit(true);
                Focus();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                EndEdit(false);
                Focus();
            }
        }

        public void CommitEdit()
        {
            EndEdit(true);
        }

        private void EndEdit(bool save)
        {
            if (_editor == null)
                return;

            TextBox editor = _editor;
            string path = _editorPath;
            string text = editor.Text;
            bool note = _editorIsNote;
            bool textChanged = _editorChanged;

            _editor = null;
            _editorPath = null;

            // Hidden now, disposed after the current event has unwound.
            editor.KeyDown -= Editor_KeyDown;
            editor.Visible = false;

            if (IsHandleCreated && !IsDisposed)
            {
                BeginInvoke(
                    (Action)(() =>
                    {
                        Controls.Remove(editor);
                        editor.Dispose();
                    }));
            }

            if (save && path != null && TextEdited != null)
                TextEdited(path, note, text);

            if (textChanged && PendingEditChanged != null) PendingEditChanged(this, EventArgs.Empty);

            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _autoScrollTimer.Dispose();
                _headerFont.Dispose();
                _tagFont.Dispose();
                _editorFont.Dispose();
                _centerFormat.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
