using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

namespace NPPLPrintMaster
{
    /// <summary>
    /// Image library for the collage workspace.
    ///
    /// The list is drawn by hand and only the visible tiles are painted, so
    /// it scrolls smoothly with hundreds of images and never rebuilds child
    /// controls. Supports Ctrl / Shift multi-selection, keyboard navigation
    /// and dragging the selection onto a page.
    /// </summary>
    internal sealed class CollageLibraryView : Control
    {
        private const int TileGap = 6;
        private const int TilePadding = 6;

        private readonly CollageArrangementModel _model;
        private readonly CollageThumbnailCache _thumbnails;
        private readonly ThemeColors _theme;
        private readonly VScrollBar _scrollBar;
        private readonly ToolTip _toolTip;
        private readonly Font _nameFont;
        private readonly Font _statusFont;
        private readonly Font _messageFont;
        private readonly StringFormat _nameFormat;
        private readonly StringFormat _centerFormat;

        private List<string> _items = new List<string>();
        private readonly Dictionary<string, int> _indexByPath =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _selected =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private string _focused;
        private string _anchor;
        private string _hover;
        private string _emptyText = string.Empty;

        private int _sizeLevel = 2;
        private int _columns = 1;
        private int _cellWidth = 140;
        private int _tileHeight = 150;
        private int _imageHeight = 92;
        private int _scrollOffset;
        private bool _updatingScroll;

        private string _pressItem;
        private Point _pressPoint;
        private bool _collapseOnRelease;
        private bool _dropActive;
        private bool _dropAdding;

        public event EventHandler SelectionChanged;
        public event Action<string> ItemActivated;
        public event Action<IList<string>> RemoveRequested;
        public event Action<IList<string>> ImagesReturned;
        public event Action<string[]> FilesDropped;

        public CollageLibraryView(
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

            _nameFont = new Font(fontFamily, 8.25f, FontStyle.Regular);
            _statusFont = new Font(fontFamily, 7.25f, FontStyle.Bold);
            _messageFont = new Font(fontFamily, 9f, FontStyle.Regular);

            _nameFormat = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Near,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.LineLimit
            };

            _centerFormat = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter
            };

            _toolTip = new ToolTip
            {
                InitialDelay = 650,
                ReshowDelay = 200,
                AutoPopDelay = 9000
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
        }

        // 1 = small, 2 = medium, 3 = large tiles.
        public int SizeLevel
        {
            get { return _sizeLevel; }
            set
            {
                int level = Math.Max(1, Math.Min(3, value));

                if (level == _sizeLevel)
                    return;

                _sizeLevel = level;
                UpdateLayoutMetrics();
                EnsureVisible(_focused);
                Invalidate();
            }
        }

        // Selected images in the order they are listed.
        public IList<string> SelectedPaths
        {
            get
            {
                return _items
                    .Where(p => _selected.Contains(p))
                    .ToList();
            }
        }

        // True when the image is in the list as currently filtered.
        public bool Contains(string path)
        {
            return IndexOf(path) >= 0;
        }

        public void SetItems(IList<string> items, string emptyText)
        {
            int focusedIndex = IndexOf(_focused);
            bool hadSelection = _selected.Count > 0;

            _items = new List<string>(items);
            _emptyText = emptyText ?? string.Empty;

            _indexByPath.Clear();
            for (int i = 0; i < _items.Count; i++)
                _indexByPath[_items[i]] = i;

            _selected.RemoveWhere(p => !_indexByPath.ContainsKey(p));

            if (_anchor != null && !_indexByPath.ContainsKey(_anchor))
                _anchor = null;

            if (_hover != null && !_indexByPath.ContainsKey(_hover))
                _hover = null;

            if (_focused != null && !_indexByPath.ContainsKey(_focused))
            {
                // The focused image left the list (for example it was just
                // placed while "Unplaced" is showing). Move on to the image
                // that took its position so the next one can be placed with
                // the same key or double-click.
                _focused =
                    _items.Count == 0 || focusedIndex < 0
                        ? null
                        : _items[Math.Min(focusedIndex, _items.Count - 1)];

                if (hadSelection && _selected.Count == 0 && _focused != null)
                {
                    _selected.Add(_focused);
                    _anchor = _focused;
                }
            }

            UpdateLayoutMetrics();
            Invalidate();
        }

        public void SelectSingle(string path, bool ensureVisible)
        {
            _selected.Clear();

            if (path != null && _indexByPath.ContainsKey(path))
            {
                _selected.Add(path);
                _focused = path;
                _anchor = path;

                if (ensureVisible)
                    EnsureVisible(path);
            }

            Invalidate();
        }

        // Selects several images at once (those that are currently listed)
        // and scrolls to the first of them.
        public void SelectMany(IEnumerable<string> paths)
        {
            _selected.Clear();

            string first = null;

            foreach (string path in paths)
            {
                if (path == null || !_indexByPath.ContainsKey(path))
                    continue;

                _selected.Add(path);

                if (first == null || IndexOf(path) < IndexOf(first))
                    first = path;
            }

            if (first != null)
            {
                _focused = first;
                _anchor = first;
                EnsureVisible(first);
            }

            Invalidate();
        }

        public void ClearSelection()
        {
            if (_selected.Count == 0)
                return;

            _selected.Clear();
            Invalidate();
        }

        // ------------------------------------------------------------
        // Layout
        // ------------------------------------------------------------
        private int RowHeight
        {
            get { return _tileHeight + TileGap; }
        }

        private int ContentHeight
        {
            get
            {
                int rows = (_items.Count + _columns - 1) / _columns;
                return TileGap + (rows * RowHeight);
            }
        }

        private void UpdateLayoutMetrics()
        {
            int available =
                Math.Max(
                    60,
                    ClientSize.Width - _scrollBar.Width - TileGap);

            int minimumWidth =
                _sizeLevel == 1 ? 96 : _sizeLevel == 2 ? 132 : 190;

            _imageHeight =
                _sizeLevel == 1 ? 62 : _sizeLevel == 2 ? 94 : 140;

            _columns = Math.Max(1, available / (minimumWidth + TileGap));
            _cellWidth = available / _columns;

            _tileHeight =
                TilePadding +
                _imageHeight +
                4 +
                (_nameFont.Height * 2) +
                2 +
                _statusFont.Height +
                TilePadding;

            int maximumOffset =
                Math.Max(0, ContentHeight - ClientSize.Height);

            _scrollOffset = Math.Max(0, Math.Min(_scrollOffset, maximumOffset));

            _updatingScroll = true;
            try
            {
                CollageWorkspaceUi.ConfigureScrollBar(
                    _scrollBar,
                    ContentHeight,
                    ClientSize.Height,
                    _scrollOffset);

                _scrollBar.SmallChange =
                    Math.Max(1, Math.Min(RowHeight / 2, _scrollBar.LargeChange));
            }
            finally
            {
                _updatingScroll = false;
            }
        }

        private Rectangle GetTileBounds(int index)
        {
            int row = index / _columns;
            int column = index % _columns;

            return new Rectangle(
                TileGap + (column * _cellWidth),
                TileGap + (row * RowHeight) - _scrollOffset,
                _cellWidth - TileGap,
                _tileHeight);
        }

        private int IndexOf(string path)
        {
            int index;

            return path != null && _indexByPath.TryGetValue(path, out index)
                ? index
                : -1;
        }

        private string HitTest(Point point)
        {
            if (point.X < TileGap ||
                point.X >= TileGap + (_columns * _cellWidth))
                return null;

            int y = point.Y + _scrollOffset - TileGap;
            if (y < 0)
                return null;

            int row = y / RowHeight;
            int column = (point.X - TileGap) / _cellWidth;
            int index = (row * _columns) + column;

            if (index < 0 || index >= _items.Count)
                return null;

            return GetTileBounds(index).Contains(point)
                ? _items[index]
                : null;
        }

        private void ScrollTo(int offset)
        {
            int maximumOffset =
                Math.Max(0, ContentHeight - ClientSize.Height);

            offset = Math.Max(0, Math.Min(offset, maximumOffset));

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

        public void EnsureVisible(string path)
        {
            int index = IndexOf(path);
            if (index < 0)
                return;

            int top = TileGap + ((index / _columns) * RowHeight);
            int bottom = top + _tileHeight;

            if (top - TileGap < _scrollOffset)
                ScrollTo(top - TileGap);
            else if (bottom + TileGap > _scrollOffset + ClientSize.Height)
                ScrollTo(bottom + TileGap - ClientSize.Height);
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
            g.Clear(_theme.BgLight);
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            if (_items.Count == 0)
            {
                using (Brush brush = new SolidBrush(_theme.MutedTextColor))
                {
                    g.DrawString(
                        _emptyText,
                        _messageFont,
                        brush,
                        new RectangleF(
                            16,
                            16,
                            Math.Max(1, ClientSize.Width - _scrollBar.Width - 32),
                            Math.Max(1, ClientSize.Height - 32)),
                        _centerFormat);
                }
            }
            else
            {
                int firstRow = Math.Max(0, (_scrollOffset - TileGap) / RowHeight);
                int lastRow = (_scrollOffset + ClientSize.Height) / RowHeight;

                for (int row = firstRow; row <= lastRow; row++)
                {
                    for (int column = 0; column < _columns; column++)
                    {
                        int index = (row * _columns) + column;
                        if (index >= _items.Count)
                            break;

                        DrawTile(g, _items[index], GetTileBounds(index));
                    }
                }
            }

            if (_dropActive)
                DrawDropOverlay(g);
        }

        private void DrawTile(Graphics g, string path, Rectangle bounds)
        {
            bool selected = _selected.Contains(path);
            bool hovered =
                string.Equals(_hover, path, StringComparison.OrdinalIgnoreCase);

            int slot = _model.IndexOf(path);

            Color back =
                selected
                    ? CollageWorkspaceUi.Blend(
                        _theme.ControlBg,
                        _theme.ActiveColor,
                        0.26f)
                    : _theme.ControlBg;

            using (Brush backBrush = new SolidBrush(back))
                g.FillRectangle(backBrush, bounds);

            Rectangle imageBox =
                new Rectangle(
                    bounds.X + TilePadding,
                    bounds.Y + TilePadding,
                    bounds.Width - (TilePadding * 2),
                    _imageHeight);

            g.FillRectangle(Brushes.White, imageBox);

            Bitmap thumbnail = _thumbnails.Get(path);

            if (thumbnail != null)
            {
                g.DrawImage(
                    thumbnail,
                    CollageWorkspaceUi.Fit(
                        thumbnail.Width,
                        thumbnail.Height,
                        RectangleF.Inflate(imageBox, -3f, -3f)));
            }
            else
            {
                using (Brush brush =
                    new SolidBrush(Color.FromArgb(150, 158, 166)))
                {
                    g.DrawString(
                        _thumbnails.IsUnreadable(path)
                            ? "Cannot read image"
                            : "Loading…",
                        _nameFont,
                        brush,
                        imageBox,
                        _centerFormat);
                }
            }

            if (slot >= 0)
                DrawPlacedBadge(g, imageBox);

            RectangleF nameBounds =
                new RectangleF(
                    bounds.X + 3,
                    imageBox.Bottom + 4,
                    bounds.Width - 6,
                    (_nameFont.Height * 2) + 1);

            using (Brush textBrush = new SolidBrush(_theme.TextColor))
            {
                g.DrawString(
                    CollageArrangementModel.GetFileTitle(path),
                    _nameFont,
                    textBrush,
                    nameBounds,
                    _nameFormat);
            }

            RectangleF statusBounds =
                new RectangleF(
                    bounds.X + 3,
                    nameBounds.Bottom + 1,
                    bounds.Width - 6,
                    _statusFont.Height + 2);

            string status;
            Color statusColor;

            if (slot >= 0)
            {
                int page = (slot / _model.CellsPerPage) + 1;
                int cell = (slot % _model.CellsPerPage) + 1;

                status =
                    _sizeLevel == 1
                        ? "P" + page + " · C" + cell
                        : "Page " + page + " · Cell " + cell;

                statusColor =
                    selected
                        ? _theme.TextColor
                        : _theme.ActiveColor;
            }
            else
            {
                status = "Not placed";
                statusColor = CollageWorkspaceUi.WarningColor(back);
            }

            using (Brush statusBrush = new SolidBrush(statusColor))
            {
                g.DrawString(
                    status,
                    _statusFont,
                    statusBrush,
                    statusBounds,
                    _centerFormat);
            }

            Color borderColor =
                selected
                    ? _theme.ActiveColor
                    : hovered
                        ? CollageWorkspaceUi.Blend(
                            _theme.BorderColor,
                            _theme.ActiveColor,
                            0.65f)
                        : _theme.BorderColor;

            using (Pen border = new Pen(borderColor, selected ? 2f : 1f))
            {
                g.DrawRectangle(
                    border,
                    bounds.X,
                    bounds.Y,
                    bounds.Width - 1,
                    bounds.Height - 1);
            }

            if (Focused &&
                string.Equals(_focused, path, StringComparison.OrdinalIgnoreCase))
            {
                ControlPaint.DrawFocusRectangle(
                    g,
                    Rectangle.Inflate(bounds, -3, -3),
                    _theme.TextColor,
                    back);
            }
        }

        // Small tick in the corner of images that are already on a page.
        private void DrawPlacedBadge(Graphics g, Rectangle imageBox)
        {
            Rectangle badge =
                new Rectangle(imageBox.Right - 19, imageBox.Y + 3, 16, 16);

            SmoothingMode previous = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (Brush fill = new SolidBrush(_theme.ActiveColor))
                g.FillEllipse(fill, badge);

            using (Pen tick =
                new Pen(
                    ThemeManager.GetContrastTextColor(_theme.ActiveColor),
                    1.8f))
            {
                g.DrawLines(
                    tick,
                    new[]
                    {
                        new PointF(badge.X + 4f, badge.Y + 8.5f),
                        new PointF(badge.X + 7f, badge.Y + 11.5f),
                        new PointF(badge.X + 12f, badge.Y + 5f)
                    });
            }

            g.SmoothingMode = previous;
        }

        private void DrawDropOverlay(Graphics g)
        {
            Rectangle area =
                new Rectangle(
                    0,
                    0,
                    ClientSize.Width - _scrollBar.Width,
                    ClientSize.Height);

            using (Brush veil =
                new SolidBrush(Color.FromArgb(205, _theme.BgLight)))
            using (Pen outline = new Pen(_theme.ActiveColor, 2f))
            using (Brush text = new SolidBrush(_theme.TextColor))
            {
                outline.DashStyle = DashStyle.Dash;

                g.FillRectangle(veil, area);
                g.DrawRectangle(
                    outline,
                    area.X + 6,
                    area.Y + 6,
                    area.Width - 13,
                    area.Height - 13);
                g.DrawString(
                    _dropAdding
                        ? "Drop here to add these images to the collage"
                        : "Drop here to take the image off the page",
                    _messageFont,
                    text,
                    RectangleF.Inflate(area, -18f, -18f),
                    _centerFormat);
            }
        }

        // ------------------------------------------------------------
        // Mouse
        // ------------------------------------------------------------
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            string item = HitTest(e.Location);
            bool control = (ModifierKeys & Keys.Control) != 0;
            bool shift = (ModifierKeys & Keys.Shift) != 0;

            _pressItem = null;
            _collapseOnRelease = false;

            if (e.Button == MouseButtons.Right)
            {
                // Right-click keeps a multi-selection it lands on.
                if (item != null && !_selected.Contains(item))
                    SelectOnly(item);

                return;
            }

            if (e.Button != MouseButtons.Left)
                return;

            if (item == null)
            {
                if (!control && !shift && _selected.Count > 0)
                {
                    _selected.Clear();
                    Invalidate();
                    OnSelectionChanged();
                }

                return;
            }

            _pressItem = item;
            _pressPoint = e.Location;

            if (shift && _anchor != null && IndexOf(_anchor) >= 0)
            {
                SelectRange(_anchor, item, control);
            }
            else if (control)
            {
                if (!_selected.Remove(item))
                    _selected.Add(item);

                _focused = item;
                _anchor = item;
                Invalidate();
                OnSelectionChanged();
            }
            else if (_selected.Contains(item) && _selected.Count > 1)
            {
                // Might be the start of dragging the whole selection, so
                // only collapse to this one image if the mouse is released
                // without dragging.
                _collapseOnRelease = true;
                _focused = item;
                Invalidate();
            }
            else
            {
                SelectOnly(item);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_pressItem != null && (e.Button & MouseButtons.Left) != 0)
            {
                Size dragSize = SystemInformation.DragSize;

                if (Math.Abs(e.X - _pressPoint.X) > dragSize.Width / 2 ||
                    Math.Abs(e.Y - _pressPoint.Y) > dragSize.Height / 2)
                {
                    BeginDrag();
                }

                return;
            }

            string hover = HitTest(e.Location);

            if (!string.Equals(hover, _hover, StringComparison.OrdinalIgnoreCase))
            {
                _hover = hover;
                _toolTip.SetToolTip(this, hover == null ? null : DescribeItem(hover));
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (e.Button == MouseButtons.Left &&
                _collapseOnRelease &&
                _pressItem != null)
            {
                SelectOnly(_pressItem);
            }

            _pressItem = null;
            _collapseOnRelease = false;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);

            if (_hover != null)
            {
                _hover = null;
                Invalidate();
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);

            if (e.Button != MouseButtons.Left)
                return;

            string item = HitTest(e.Location);
            _pressItem = null;

            if (item != null && ItemActivated != null)
                ItemActivated(item);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            ScrollTo(_scrollOffset - (e.Delta * RowHeight / 180));
        }

        private void BeginDrag()
        {
            string pressed = _pressItem;
            _pressItem = null;
            _collapseOnRelease = false;

            if (pressed == null)
                return;

            if (!_selected.Contains(pressed))
                SelectOnly(pressed);

            IList<string> paths = SelectedPaths;
            if (paths.Count == 0)
                return;

            DoDragDrop(
                CollageDrag.Create(paths, CollageDrag.SourceLibrary),
                DragDropEffects.Move);
        }

        private string DescribeItem(string path)
        {
            int slot = _model.IndexOf(path);

            string location =
                slot < 0
                    ? "Not placed"
                    : "Page " + ((slot / _model.CellsPerPage) + 1) +
                      ", cell " + ((slot % _model.CellsPerPage) + 1);

            string remark = _model.GetRemark(path);
            string note = _model.GetNote(path);

            return System.IO.Path.GetFileName(path) +
                   Environment.NewLine +
                   location +
                   (remark == null
                       ? string.Empty
                       : Environment.NewLine + "Remark: " + remark) +
                   (note == null
                       ? string.Empty
                       : Environment.NewLine + "Remark 2: " + note);
        }

        // ------------------------------------------------------------
        // Selection
        // ------------------------------------------------------------
        private void SelectOnly(string item)
        {
            bool changed =
                _selected.Count != 1 || !_selected.Contains(item);

            _selected.Clear();
            _selected.Add(item);
            _focused = item;
            _anchor = item;
            Invalidate();

            if (changed)
                OnSelectionChanged();
        }

        private void SelectRange(string from, string to, bool keepExisting)
        {
            int a = IndexOf(from);
            int b = IndexOf(to);

            if (a < 0 || b < 0)
                return;

            if (!keepExisting)
                _selected.Clear();

            for (int i = Math.Min(a, b); i <= Math.Max(a, b); i++)
                _selected.Add(_items[i]);

            _focused = to;
            Invalidate();
            OnSelectionChanged();
        }

        private void OnSelectionChanged()
        {
            EventHandler handler = SelectionChanged;

            if (handler != null)
                handler(this, EventArgs.Empty);
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

            if (_items.Count == 0)
                return;

            int rowsPerPage = Math.Max(1, ClientSize.Height / RowHeight);

            switch (e.KeyCode)
            {
                case Keys.Left:
                    MoveFocus(-1, e.Shift);
                    break;
                case Keys.Right:
                    MoveFocus(1, e.Shift);
                    break;
                case Keys.Up:
                    MoveFocus(-_columns, e.Shift);
                    break;
                case Keys.Down:
                    MoveFocus(_columns, e.Shift);
                    break;
                case Keys.PageUp:
                    MoveFocus(-_columns * rowsPerPage, e.Shift);
                    break;
                case Keys.PageDown:
                    MoveFocus(_columns * rowsPerPage, e.Shift);
                    break;
                case Keys.Home:
                    MoveFocus(-_items.Count, e.Shift);
                    break;
                case Keys.End:
                    MoveFocus(_items.Count, e.Shift);
                    break;
                case Keys.A:
                    if (!e.Control)
                        return;

                    _selected.Clear();
                    foreach (string item in _items)
                        _selected.Add(item);

                    Invalidate();
                    OnSelectionChanged();
                    break;
                case Keys.Enter:
                    if (_focused != null && ItemActivated != null)
                        ItemActivated(_focused);
                    break;
                case Keys.Delete:
                    if (_selected.Count > 0 && RemoveRequested != null)
                        RemoveRequested(SelectedPaths);
                    break;
                default:
                    return;
            }

            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        private void MoveFocus(int delta, bool extend)
        {
            int current = IndexOf(_focused);

            int target =
                current < 0
                    ? 0
                    : Math.Max(0, Math.Min(_items.Count - 1, current + delta));

            string item = _items[target];

            if (extend && _anchor != null && IndexOf(_anchor) >= 0)
                SelectRange(_anchor, item, false);
            else
                SelectOnly(item);

            EnsureVisible(item);
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

        // ------------------------------------------------------------
        // Drop target: an image dragged back from a page is unplaced, and
        // image files dragged in from Windows Explorer are added
        // ------------------------------------------------------------
        protected override void OnDragEnter(DragEventArgs e)
        {
            base.OnDragEnter(e);
            UpdateDropState(e);
        }

        protected override void OnDragOver(DragEventArgs e)
        {
            base.OnDragOver(e);
            UpdateDropState(e);
        }

        protected override void OnDragLeave(EventArgs e)
        {
            base.OnDragLeave(e);
            SetDropActive(false, false);
        }

        protected override void OnDragDrop(DragEventArgs e)
        {
            base.OnDragDrop(e);

            string[] paths = CollageDrag.GetPaths(e.Data);
            string[] files = CollageDrag.GetExternalFiles(e.Data);
            bool accept = _dropActive;

            SetDropActive(false, false);

            if (!accept)
                return;

            if (paths != null)
            {
                if (ImagesReturned != null)
                    ImagesReturned(paths);
            }
            else if (files != null && FilesDropped != null)
            {
                FilesDropped(files);
            }
        }

        private void UpdateDropState(DragEventArgs e)
        {
            bool returning =
                CollageDrag.IsFromCanvas(e.Data) &&
                CollageDrag.GetPaths(e.Data) != null;

            bool adding =
                !returning &&
                CollageDrag.GetPaths(e.Data) == null &&
                CollageDrag.GetExternalFiles(e.Data) != null;

            e.Effect =
                returning
                    ? DragDropEffects.Move
                    : adding
                        ? DragDropEffects.Copy
                        : DragDropEffects.None;

            SetDropActive(returning || adding, adding);
        }

        private void SetDropActive(bool active, bool adding)
        {
            if (_dropActive == active && _dropAdding == adding)
                return;

            _dropActive = active;
            _dropAdding = adding;
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _toolTip.Dispose();
                _nameFont.Dispose();
                _statusFont.Dispose();
                _messageFont.Dispose();
                _nameFormat.Dispose();
                _centerFormat.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
