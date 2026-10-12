using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace NPPLPrintMaster
{
    /// <summary>
    /// Manual arrangement workspace for the Image Collage Builder.
    ///
    /// Left:   image library (search, filter, multi-select, drag source).
    /// Center: the PDF pages exactly as they will print; images are dragged
    ///         between cells and their names sit centered below them.
    /// Right:  page strip for jumping to, and dropping onto, any page.
    ///
    /// The selected grid stays fixed. Images that are not on a page remain in
    /// the library as "not placed" and are left out of the PDF.
    /// </summary>
    public sealed class CollageArrangementForm : Form
    {
        private enum LibraryFilter
        {
            All,
            Unplaced,
            Placed
        }

        private const int MaxUndoSteps = 100;

        private readonly CollageArrangementModel _model;
        private readonly CollageThumbnailCache _thumbnails;
        private readonly ThemeColors _theme;
        private readonly string _fontFamily;

        // The form's own copy of the page setup. The grid and text size are
        // changed here, and it is only handed back when the arrangement is
        // accepted.
        private readonly CollagePageSetup _page;

        // The collage's images in name order, rebuilt when images are added
        // or removed.
        private List<string> _imagesByName = new List<string>();
        private int _imagesByNameVersion = -1;

        private readonly List<CollageArrangementSnapshot> _undo =
            new List<CollageArrangementSnapshot>();
        private readonly List<CollageArrangementSnapshot> _redo =
            new List<CollageArrangementSnapshot>();

        private readonly List<string> _inspected = new List<string>();
        private readonly HashSet<string> _inspectedSet =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private CollageLibraryView _library;
        private CollagePageCanvas _canvas;
        private CollagePageNavigator _navigator;
        private ToolTip _toolTip;

        private TextBox _searchBox;
        private Button[] _filterButtons;
        private Button[] _sizeButtons;
        private Label _summaryLabel;
        private Label _inspectorName;
        private Label _inspectorLocation;
        private TextBox _remarkBox;
        private TextBox _noteBox;
        private Button _remarkResetButton;
        private Button _placeButton;
        private Button _removeButton;
        private ComboBox _pageCombo;
        private ComboBox _dropModeCombo;
        private ComboBox _displayModeCombo;
        private ComboBox _textSizeCombo;
        private ComboBox _columnsCombo;
        private ComboBox _rowsCombo;
        private Label _subtitleLabel;
        private Button _previousPageButton;
        private Button _nextPageButton;
        private Button _undoButton;
        private Button _redoButton;
        private Label _zoomLabel;
        private Label _statusLabel;
        private ContextMenuStrip _arrangeMenu;

        private LibraryFilter _filter = LibraryFilter.All;

        // What the two text boxes of the inspector are editing: the images,
        // and a key that changes whenever that selection changes.
        private readonly List<string> _editTargets = new List<string>();
        private string _remarkBoxKey;
        private bool _updatingGrid;
        private bool _updatingRemark;
        private bool _remarkUndoPushed;
        private bool _updatingPageCombo;
        private bool _dirty;
        private bool _accepted;

        public IList<string> ResultSlots { get; private set; }
        public IDictionary<string, string> ResultCaptions { get; private set; }
        public IDictionary<string, string> ResultCustomNames { get; private set; }
        public IDictionary<string, string> ResultNotes { get; private set; }
        public string ResultDisplayMode { get; private set; }

        // Every image in the collage when it was accepted. Differs from the
        // images passed in when some were added or removed in the workspace.
        public IList<string> ResultImages { get; private set; }

        // The page setup as it was left: the grid and the text size can be
        // changed in the workspace.
        public CollagePageSetup ResultPage { get; private set; }

        public CollageArrangementForm(
            IList<string> orderedImagePaths,
            CollagePageSetup page,
            AppSettings appSettings,
            IList<string> initialSlots = null,
            IDictionary<string, string> initialCaptions = null,
            IDictionary<string, string> initialCustomNames = null,
            string initialDisplayMode = null,
            IDictionary<string, string> initialNotes = null)
        {
            if (orderedImagePaths == null)
                throw new ArgumentNullException("orderedImagePaths");

            if (page == null)
                throw new ArgumentNullException("page");

            _page = page.Clone();
            _page.Normalize();

            string themeName =
                appSettings == null
                    ? "Forest Graphite"
                    : AppearanceManager.NormalizeTheme(appSettings.Theme);

            _theme = ThemeManager.GetTheme(themeName);

            string typography =
                appSettings == null
                    ? AppearanceManager.NpplOriginalTypography
                    : AppearanceManager.ResolveTypography(
                        themeName,
                        appSettings.TypographyStyle);

            _fontFamily =
                string.Equals(
                    typography,
                    AppearanceManager.BarTender10Typography,
                    StringComparison.OrdinalIgnoreCase)
                    ? "Tahoma"
                    : "Segoe UI";

            List<string> images =
                orderedImagePaths
                    .Where(File.Exists)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

            _model =
                new CollageArrangementModel(
                    images,
                    _page,
                    initialSlots,
                    initialCaptions,
                    initialCustomNames,
                    initialNotes);

            _thumbnails =
                new CollageThumbnailCache(
                    CollageThumbnailCache.ChooseMaxEdge(images.Count));

            ResultDisplayMode = NormalizeDisplayMode(initialDisplayMode);

            BuildInterface();

            _model.Changed += (s, e) => RefreshAll();
            _thumbnails.ThumbnailsReady += Thumbnails_Ready;

            RefreshAll();

            // Everything is loaded in the background, first pages first.
            _thumbnails.Preload(_model.Images);
        }

        private static string NormalizeDisplayMode(string mode)
        {
            foreach (string known in new[]
            {
                CollageCellLayout.ModeFilename,
                CollageCellLayout.ModeBoth,
                CollageCellLayout.ModeRemark,
                CollageCellLayout.ModeNone
            })
            {
                if (string.Equals(known, mode, StringComparison.OrdinalIgnoreCase))
                    return known;
            }

            return CollageCellLayout.ModeBoth;
        }

        // ------------------------------------------------------------
        // Interface
        // ------------------------------------------------------------
        private void BuildInterface()
        {
            Text =
                "NPPL PrintMaster  •  Image Collage Builder  •  Arrange Images";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(1000, 640);
            Size = new Size(1320, 840);
            WindowState = FormWindowState.Maximized;
            BackColor = _theme.BgLight;
            ForeColor = _theme.TextColor;
            Font = new Font(_fontFamily, 9f);

            _toolTip = new ToolTip();

            Panel left = CreateLibraryPanel();
            left.Dock = DockStyle.Left;
            left.Width = 318;

            Splitter splitter = new Splitter
            {
                Dock = DockStyle.Left,
                Width = 5,
                MinSize = 240,
                MinExtra = 430,
                BackColor = _theme.BorderColor
            };

            Panel right = CreateNavigatorPanel();
            right.Dock = DockStyle.Right;
            right.Width = _page.Landscape ? 204 : 178;

            Panel center = CreateEditorPanel();
            center.Dock = DockStyle.Fill;

            // Docking runs from the last control added to the first.
            Controls.Add(center);
            Controls.Add(splitter);
            Controls.Add(left);
            Controls.Add(right);
            Controls.Add(CreateFooter());
            Controls.Add(CreateHeader());

            Shown +=
                (s, e) =>
                {
                    _canvas.FitPage();
                    _canvas.Focus();
                };
        }

        private Panel CreateHeader()
        {
            Panel header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 58,
                BackColor = _theme.ContentBg
            };

            Label title = CreateLabel(
                "Image Collage Builder",
                14f,
                FontStyle.Bold,
                _theme.TextColor);
            title.AutoSize = true;
            title.Location = new Point(14, 6);

            _subtitleLabel = CreateLabel(
                DescribePage(),
                8.5f,
                FontStyle.Regular,
                _theme.MutedTextColor);
            _subtitleLabel.AutoSize = true;
            _subtitleLabel.Location = new Point(16, 34);

            _summaryLabel = CreateLabel(
                string.Empty,
                9f,
                FontStyle.Bold,
                _theme.TextColor);
            _summaryLabel.Dock = DockStyle.Right;
            _summaryLabel.Width = 470;
            _summaryLabel.Padding = new Padding(0, 0, 16, 0);
            _summaryLabel.TextAlign = ContentAlignment.MiddleRight;

            Panel line = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 1,
                BackColor = _theme.BorderColor
            };

            header.Controls.Add(_subtitleLabel);
            header.Controls.Add(title);
            header.Controls.Add(_summaryLabel);
            header.Controls.Add(line);

            return header;
        }

        private string DescribePage()
        {
            return "Arrange images on the PDF pages  •  " +
                   _page.Columns + " × " + _page.Rows + " grid  •  A4 " +
                   (_page.Landscape ? "Landscape" : "Portrait") + "  •  " +
                   _page.CellsPerPage +
                   (_page.CellsPerPage == 1 ? " image" : " images") +
                   " per page";
        }

        private Panel CreateLibraryPanel()
        {
            Panel panel = new Panel
            {
                BackColor = _theme.ContentBg,
                Padding = new Padding(10, 8, 8, 8)
            };

            // Heading with the thumbnail size switch.
            Panel headingRow = new Panel
            {
                Dock = DockStyle.Top,
                Height = 30
            };

            Label heading = CreateLabel(
                "IMAGE LIBRARY",
                9f,
                FontStyle.Bold,
                _theme.TextColor);
            heading.Dock = DockStyle.Fill;
            heading.TextAlign = ContentAlignment.MiddleLeft;

            FlowLayoutPanel sizePanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 152,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight
            };

            Button addButton = CreateButton("+ Add…", 60, false);
            addButton.Height = 24;
            addButton.Margin = new Padding(0, 2, 8, 0);
            addButton.Click += (s, e) => AddImagesFromDialog();
            _toolTip.SetToolTip(
                addButton,
                "Add more images to this collage.\n" +
                "Image files can also be dragged in from a folder window.");
            sizePanel.Controls.Add(addButton);

            string[] sizeNames = { "S", "M", "L" };
            string[] sizeTips =
            {
                "Small thumbnails",
                "Medium thumbnails",
                "Large thumbnails"
            };

            _sizeButtons = new Button[3];

            for (int i = 0; i < 3; i++)
            {
                int level = i + 1;

                Button button = CreateButton(sizeNames[i], 26, false);
                button.Height = 24;
                button.Margin = new Padding(0, 2, 2, 0);
                button.Click += (s, e) => SetLibrarySize(level);
                _toolTip.SetToolTip(button, sizeTips[i]);

                _sizeButtons[i] = button;
                sizePanel.Controls.Add(button);
            }

            headingRow.Controls.Add(heading);
            headingRow.Controls.Add(sizePanel);

            // Search.
            Panel searchRow = new Panel
            {
                Dock = DockStyle.Top,
                Height = 30
            };

            Label searchLabel = CreateLabel(
                "Search",
                8.5f,
                FontStyle.Regular,
                _theme.MutedTextColor);
            searchLabel.Dock = DockStyle.Left;
            searchLabel.Width = 50;
            searchLabel.Padding = new Padding(0, 4, 0, 0);

            _searchBox = new TextBox
            {
                Dock = DockStyle.Fill,
                BackColor = _theme.ControlBg,
                ForeColor = _theme.TextColor,
                BorderStyle = BorderStyle.FixedSingle
            };
            _searchBox.TextChanged += (s, e) => RefreshLibrary();
            CollageWorkspaceUi.SetCueBanner(
                _searchBox,
                "Name or remark  (Ctrl+F)");

            searchRow.Controls.Add(_searchBox);
            searchRow.Controls.Add(searchLabel);

            // All / Not placed / Placed.
            TableLayoutPanel filterRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 34,
                ColumnCount = 3,
                RowCount = 1
            };

            filterRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _filterButtons = new Button[3];

            for (int i = 0; i < 3; i++)
            {
                LibraryFilter filter = (LibraryFilter)i;

                filterRow.ColumnStyles.Add(
                    new ColumnStyle(SizeType.Percent, 33.33f));

                Button button = CreateButton(string.Empty, 60, false);
                button.Dock = DockStyle.Fill;
                button.Margin = new Padding(0, 0, i == 2 ? 0 : 4, 6);
                button.Click += (s, e) => SetFilter(filter);

                _filterButtons[i] = button;
                filterRow.Controls.Add(button, i, 0);
            }

            _library =
                new CollageLibraryView(
                    _model,
                    _thumbnails,
                    _theme,
                    _fontFamily);
            _library.Dock = DockStyle.Fill;
            _library.ContextMenuStrip = CreateLibraryMenu();
            _library.SelectionChanged += Library_SelectionChanged;
            _library.ItemActivated += ActivateLibraryItem;
            _library.RemoveRequested += UnplaceImages;
            _library.ImagesReturned += UnplaceImages;
            _library.FilesDropped += files => AddImages(files, -1, false);

            panel.Controls.Add(_library);
            panel.Controls.Add(CreateInspector());
            panel.Controls.Add(filterRow);
            panel.Controls.Add(searchRow);
            panel.Controls.Add(headingRow);

            return panel;
        }

        private Panel CreateInspector()
        {
            Panel inspector = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 180,
                BackColor = _theme.ContentBg,
                Padding = new Padding(0, 8, 0, 0)
            };

            Label heading = CreateLabel(
                "SELECTED IMAGE",
                8f,
                FontStyle.Bold,
                _theme.MutedTextColor);
            heading.Dock = DockStyle.Top;
            heading.Height = 20;

            _inspectorName = CreateLabel(
                string.Empty,
                9f,
                FontStyle.Bold,
                _theme.TextColor);
            _inspectorName.Dock = DockStyle.Top;
            _inspectorName.Height = 20;

            _inspectorLocation = CreateLabel(
                string.Empty,
                8.25f,
                FontStyle.Regular,
                _theme.MutedTextColor);
            _inspectorLocation.Dock = DockStyle.Top;
            _inspectorLocation.Height = 20;

            // The two editable lines printed under the image.
            Panel remarkRow = new Panel
            {
                Dock = DockStyle.Top,
                Height = 30,
                Padding = new Padding(0, 3, 0, 0)
            };

            _remarkBox = CreateInspectorTextBox();
            _remarkBox.TextChanged += RemarkBox_TextChanged;
            _toolTip.SetToolTip(
                _remarkBox,
                "Remark printed under the file name.\n" +
                "It starts as the file name and can be changed.");

            _remarkResetButton = CreateButton("↺", 30, false);
            _remarkResetButton.Dock = DockStyle.Right;
            _remarkResetButton.Click += (s, e) => ResetRemark();
            _toolTip.SetToolTip(
                _remarkResetButton,
                "Use the file name again");

            remarkRow.Controls.Add(_remarkBox);
            remarkRow.Controls.Add(_remarkResetButton);
            remarkRow.Controls.Add(CreateInspectorRowLabel("Remark"));

            Panel noteRow = new Panel
            {
                Dock = DockStyle.Top,
                Height = 30,
                Padding = new Padding(0, 3, 0, 0)
            };

            _noteBox = CreateInspectorTextBox();
            _noteBox.TextChanged += NoteBox_TextChanged;
            _toolTip.SetToolTip(
                _noteBox,
                "Additional remark, printed on one more line below the remark.\n" +
                "Leave it empty for no extra line.");

            noteRow.Controls.Add(_noteBox);
            noteRow.Controls.Add(CreateInspectorRowLabel("Remark 2"));

            TableLayoutPanel actionRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 38,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(0, 8, 0, 0)
            };
            actionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            actionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            actionRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _placeButton = CreateButton("Place on page", 100, true);
            _placeButton.Dock = DockStyle.Fill;
            _placeButton.Margin = new Padding(0, 0, 3, 0);
            _placeButton.Click += (s, e) => PlaceInNextEmpty(_inspected.ToList());
            _toolTip.SetToolTip(
                _placeButton,
                "Put the selected image into the next empty cell " +
                "(or the empty cell selected on the page)");

            _removeButton = CreateButton("Take off page", 100, false);
            _removeButton.Dock = DockStyle.Fill;
            _removeButton.Margin = new Padding(3, 0, 0, 0);
            _removeButton.Click += (s, e) => UnplaceImages(_inspected.ToList());
            _toolTip.SetToolTip(
                _removeButton,
                "Remove the selected image from its page. " +
                "It stays in the library as not placed.");

            actionRow.Controls.Add(_placeButton, 0, 0);
            actionRow.Controls.Add(_removeButton, 1, 0);

            Panel line = new Panel
            {
                Dock = DockStyle.Top,
                Height = 1,
                BackColor = _theme.BorderColor
            };

            Panel spacer = new Panel
            {
                Dock = DockStyle.Top,
                Height = 8
            };

            inspector.Controls.Add(actionRow);
            inspector.Controls.Add(noteRow);
            inspector.Controls.Add(remarkRow);
            inspector.Controls.Add(_inspectorLocation);
            inspector.Controls.Add(_inspectorName);
            inspector.Controls.Add(heading);
            inspector.Controls.Add(spacer);
            inspector.Controls.Add(line);

            return inspector;
        }

        private TextBox CreateInspectorTextBox()
        {
            TextBox box = new TextBox
            {
                Dock = DockStyle.Fill,
                BackColor = _theme.ControlBg,
                ForeColor = _theme.TextColor,
                BorderStyle = BorderStyle.FixedSingle,
                Enabled = false
            };

            box.Enter += (s, e) => _remarkUndoPushed = false;
            box.Leave +=
                (s, e) =>
                {
                    CommitBulkText(box);
                    RefreshInspector();
                };
            box.KeyDown +=
                (s, e) =>
                {
                    if (e.KeyCode == Keys.Enter)
                    {
                        e.SuppressKeyPress = true;
                        _canvas.Focus();
                    }
                };

            return box;
        }

        private Label CreateInspectorRowLabel(string text)
        {
            Label label = CreateLabel(
                text,
                8.25f,
                FontStyle.Regular,
                _theme.MutedTextColor);
            label.Dock = DockStyle.Left;
            label.Width = 62;
            label.Padding = new Padding(0, 4, 0, 0);
            return label;
        }

        private Panel CreateEditorPanel()
        {
            Panel editor = new Panel
            {
                BackColor = _theme.BgLight
            };

            FlowLayoutPanel toolbar = CreateToolbar();

            _canvas =
                new CollagePageCanvas(
                    _model,
                    _thumbnails,
                    _theme,
                    _fontFamily);
            _canvas.Dock = DockStyle.Fill;
            _canvas.DisplayMode = ResultDisplayMode;
            _canvas.ContextMenuStrip = CreateCanvasMenu();
            _canvas.SelectionChanged += Canvas_SelectionChanged;
            _canvas.ViewChanged += (s, e) => RefreshView();
            _canvas.ImagesDropped += DropImages;
            _canvas.RemoveRequested += path => UnplaceImages(new[] { path });
            _canvas.TextEdited += ApplyEditedText;
            _canvas.FilesDropped += AddImages;

            Panel line = new Panel
            {
                Dock = DockStyle.Top,
                Height = 1,
                BackColor = _theme.BorderColor
            };

            editor.Controls.Add(_canvas);
            editor.Controls.Add(line);
            editor.Controls.Add(toolbar);

            return editor;
        }

        private FlowLayoutPanel CreateToolbar()
        {
            // Wraps onto a second row when the window is narrow, so the
            // tools never overlap.
            FlowLayoutPanel toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(8, 7, 8, 3),
                BackColor = _theme.ContentBg
            };

            // Pages.
            _previousPageButton = CreateToolButton(
                "◀",
                32,
                "Previous page  (Page Up)",
                () => _canvas.ScrollToPage(_canvas.CurrentPage - 1));

            _pageCombo = CreateToolCombo(132);
            _pageCombo.SelectedIndexChanged +=
                (s, e) =>
                {
                    if (!_updatingPageCombo && _pageCombo.SelectedIndex >= 0)
                        _canvas.ScrollToPage(_pageCombo.SelectedIndex);
                };
            _toolTip.SetToolTip(_pageCombo, "Jump to a page");

            _nextPageButton = CreateToolButton(
                "▶",
                32,
                "Next page  (Page Down)",
                () => _canvas.ScrollToPage(_canvas.CurrentPage + 1));

            // Each group wraps as one piece, so a label never ends up on a
            // different row from the control it belongs to.
            toolbar.Controls.Add(
                CreateToolGroup(
                    _previousPageButton,
                    _pageCombo,
                    _nextPageButton,
                    CreateToolSeparator()));

            // Zoom.
            _zoomLabel = CreateLabel(
                "100%",
                8.5f,
                FontStyle.Bold,
                _theme.TextColor);
            _zoomLabel.Size = new Size(46, 30);
            _zoomLabel.Margin = new Padding(0, 0, 4, 4);
            _zoomLabel.TextAlign = ContentAlignment.MiddleCenter;

            toolbar.Controls.Add(
                CreateToolGroup(
                    CreateToolButton(
                        "−",
                        32,
                        "Zoom out  (Ctrl + mouse wheel)",
                        () => _canvas.ZoomBy(1f / 1.2f)),
                    _zoomLabel,
                    CreateToolButton(
                        "+",
                        32,
                        "Zoom in  (Ctrl + mouse wheel)",
                        () => _canvas.ZoomBy(1.2f)),
                    CreateToolButton(
                        "Fit page",
                        70,
                        "Show one whole page  (Ctrl+0)",
                        () => _canvas.FitPage()),
                    CreateToolButton(
                        "Fit width",
                        74,
                        "Make the page as wide as the workspace",
                        () => _canvas.FitWidth()),
                    CreateToolSeparator()));

            // Undo and whole-arrangement actions.
            _undoButton = CreateToolButton("↶ Undo", 72, "Undo  (Ctrl+Z)", Undo);
            _redoButton = CreateToolButton("↷ Redo", 72, "Redo  (Ctrl+Y)", Redo);

            _arrangeMenu = CreateArrangeMenu();

            Button arrange = null;
            arrange = CreateToolButton(
                "Arrange ▾",
                86,
                "Actions for the whole arrangement",
                () => _arrangeMenu.Show(arrange, new Point(0, arrange.Height)));

            toolbar.Controls.Add(
                CreateToolGroup(
                    _undoButton,
                    _redoButton,
                    CreateToolSeparator(),
                    arrange,
                    CreateToolSeparator()));

            // What a drop onto an occupied cell does.
            _dropModeCombo = CreateToolCombo(128);
            _dropModeCombo.Items.AddRange(
                new object[] { "Swap images", "Insert and shift" });
            _dropModeCombo.SelectedIndex = 0;
            _dropModeCombo.SelectedIndexChanged +=
                (s, e) =>
                {
                    bool insert = _dropModeCombo.SelectedIndex == 1;
                    _canvas.InsertMode = insert;
                    _navigator.InsertMode = insert;
                };
            _toolTip.SetToolTip(
                _dropModeCombo,
                "Swap: the two images trade places.\n" +
                "Insert and shift: the image is inserted and the others move along.\n" +
                "Hold Shift while dropping to use the other mode once.");

            toolbar.Controls.Add(
                CreateToolGroup(
                    CreateToolLabel("On drop:"),
                    _dropModeCombo));

            // Text under each image.
            _displayModeCombo = CreateToolCombo(146);
            _displayModeCombo.Items.AddRange(
                new object[]
                {
                    "File name",
                    "File name + remark",
                    "Remark only",
                    "No text"
                });
            _displayModeCombo.SelectedIndex =
                DisplayModeToIndex(ResultDisplayMode);
            _displayModeCombo.SelectedIndexChanged +=
                (s, e) =>
                {
                    ResultDisplayMode =
                        IndexToDisplayMode(_displayModeCombo.SelectedIndex);
                    _canvas.DisplayMode = ResultDisplayMode;
                };
            _toolTip.SetToolTip(
                _displayModeCombo,
                "Text printed below each image in the PDF");

            _textSizeCombo = CreateToolCombo(62);

            foreach (int size in CollagePageSetup.CaptionFontChoices)
                _textSizeCombo.Items.Add(size + " pt");

            _textSizeCombo.SelectedIndex =
                CollagePageSetup.NearestCaptionFontChoice(_page.CaptionFontPoints);
            _textSizeCombo.SelectedIndexChanged +=
                (s, e) =>
                {
                    if (_textSizeCombo.SelectedIndex < 0)
                        return;

                    _page.CaptionFontPoints =
                        CollagePageSetup.CaptionFontChoices[
                            _textSizeCombo.SelectedIndex];
                    _dirty = true;
                    _canvas.Invalidate();
                };
            _toolTip.SetToolTip(
                _textSizeCombo,
                "Size of the text printed below each image");

            toolbar.Controls.Add(
                CreateToolGroup(
                    CreateToolLabel("Text:"),
                    _displayModeCombo,
                    _textSizeCombo,
                    CreateToolSeparator()));

            // Columns x rows on each page. The images keep their order and
            // flow into the new grid.
            _columnsCombo = CreateToolCombo(46);
            _rowsCombo = CreateToolCombo(46);

            for (int i = 1; i <= CollagePageSetup.MaxColumns; i++)
                _columnsCombo.Items.Add(i.ToString());

            for (int i = 1; i <= CollagePageSetup.MaxRows; i++)
                _rowsCombo.Items.Add(i.ToString());

            _columnsCombo.SelectedIndex = _page.Columns - 1;
            _rowsCombo.SelectedIndex = _page.Rows - 1;
            _columnsCombo.SelectedIndexChanged += (s, e) => ChangeGrid();
            _rowsCombo.SelectedIndexChanged += (s, e) => ChangeGrid();
            _toolTip.SetToolTip(_columnsCombo, "Columns on each page");
            _toolTip.SetToolTip(_rowsCombo, "Rows on each page");

            toolbar.Controls.Add(
                CreateToolGroup(
                    CreateToolLabel("Grid:"),
                    _columnsCombo,
                    CreateToolLabel("×"),
                    _rowsCombo));

            return toolbar;
        }

        private void ChangeGrid()
        {
            if (_updatingGrid ||
                _columnsCombo.SelectedIndex < 0 ||
                _rowsCombo.SelectedIndex < 0)
                return;

            int columns = _columnsCombo.SelectedIndex + 1;
            int rows = _rowsCombo.SelectedIndex + 1;

            if (columns == _page.Columns && rows == _page.Rows)
                return;

            string selected = _canvas.SelectedPath;

            PushUndo();
            _model.SetGrid(columns, rows);

            // Show the whole page in its new shape, at the image that was
            // being worked on.
            _canvas.FitPage();

            if (selected != null)
                _canvas.SelectPath(selected, true);
            else
                _canvas.ScrollToPage(0);
        }

        private static FlowLayoutPanel CreateToolGroup(params Control[] controls)
        {
            FlowLayoutPanel group = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = Padding.Empty
            };

            group.Controls.AddRange(controls);
            return group;
        }

        private Panel CreateNavigatorPanel()
        {
            Panel panel = new Panel
            {
                BackColor = _theme.ContentBg
            };

            Label heading = CreateLabel(
                "PAGES",
                9f,
                FontStyle.Bold,
                _theme.TextColor);
            heading.Dock = DockStyle.Top;
            heading.Height = 38;
            heading.Padding = new Padding(12, 0, 0, 0);
            heading.TextAlign = ContentAlignment.MiddleLeft;

            Panel line = new Panel
            {
                Dock = DockStyle.Left,
                Width = 1,
                BackColor = _theme.BorderColor
            };

            _navigator =
                new CollagePageNavigator(
                    _model,
                    _thumbnails,
                    _theme,
                    _fontFamily);
            _navigator.Dock = DockStyle.Fill;
            _navigator.IsHighlighted = path => _inspectedSet.Contains(path);
            _navigator.ContextMenuStrip = CreateNavigatorMenu();
            _navigator.PageActivated += page => _canvas.ScrollToPage(page);
            _navigator.ImagesDropped += DropImages;
            _navigator.AddPageRequested += (s, e) => AddPage();

            panel.Controls.Add(_navigator);
            panel.Controls.Add(heading);
            panel.Controls.Add(line);

            return panel;
        }

        private Panel CreateFooter()
        {
            Panel footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 58,
                BackColor = _theme.ContentBg,
                Padding = new Padding(14, 10, 12, 10)
            };

            Panel line = new Panel
            {
                Dock = DockStyle.Top,
                Height = 1,
                BackColor = _theme.BorderColor
            };

            Panel statusPanel = new Panel
            {
                Dock = DockStyle.Fill
            };

            _statusLabel = CreateLabel(
                string.Empty,
                9f,
                FontStyle.Bold,
                _theme.TextColor);
            _statusLabel.Dock = DockStyle.Top;
            _statusLabel.Height = 19;

            Label tips = CreateLabel(
                "Drag an image to another cell or page  •  Shift + drop inserts  •  " +
                "double-click an image to edit its remark  •  Delete takes it off the page",
                8.25f,
                FontStyle.Regular,
                _theme.MutedTextColor);
            tips.Dock = DockStyle.Top;
            tips.Height = 19;

            statusPanel.Controls.Add(tips);
            statusPanel.Controls.Add(_statusLabel);

            Button cancel = CreateButton("Cancel", 100, false);
            cancel.Dock = DockStyle.Right;
            cancel.Click += (s, e) => Close();

            Panel gap = new Panel
            {
                Dock = DockStyle.Right,
                Width = 8
            };

            Button use = CreateButton("✓  Use This Arrangement", 196, true);
            use.Dock = DockStyle.Right;
            use.Click += (s, e) => AcceptArrangement();

            footer.Controls.Add(statusPanel);
            footer.Controls.Add(cancel);
            footer.Controls.Add(gap);
            footer.Controls.Add(use);

            Panel host = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = footer.Height + 1
            };

            footer.Dock = DockStyle.Fill;
            host.Controls.Add(footer);
            host.Controls.Add(line);

            return host;
        }

        // ------------------------------------------------------------
        // Menus
        // ------------------------------------------------------------
        private ContextMenuStrip CreateArrangeMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();

            ToolStripItem fill = menu.Items.Add("Place all unplaced images");
            fill.Click +=
                (s, e) =>
                {
                    if (_model.UnplacedCount == 0)
                        return;

                    PushUndo();
                    _model.FillEmptyCells();
                };

            ToolStripItem close = menu.Items.Add("Close gaps between images");
            close.Click +=
                (s, e) =>
                {
                    PushUndo();
                    _model.CloseGaps();
                };

            ToolStripItem reset = menu.Items.Add("Reset to automatic order");
            reset.Click +=
                (s, e) =>
                {
                    PushUndo();
                    _model.ResetToDefaultOrder();
                    _canvas.ScrollToPage(0);
                };

            menu.Items.Add(new ToolStripSeparator());

            ToolStripItem add = menu.Items.Add("Add blank page");
            add.Click += (s, e) => AddPage();

            ToolStripItem removeEmpty = menu.Items.Add("Remove empty pages");
            removeEmpty.Click +=
                (s, e) =>
                {
                    PushUndo();
                    _model.RemoveEmptyPages();
                };

            menu.Items.Add(new ToolStripSeparator());

            ToolStripItem clear = menu.Items.Add("Take all images off the pages");
            clear.Click +=
                (s, e) =>
                {
                    if (_model.PlacedCount == 0)
                        return;

                    PushUndo();
                    _model.ClearAll();
                };

            menu.Items.Add(new ToolStripSeparator());

            ToolStripItem addImages = menu.Items.Add("Add images…");
            addImages.Click += (s, e) => AddImagesFromDialog();

            ToolStripItem save = menu.Items.Add("Save layout to a file…");
            save.Click += (s, e) => SaveLayoutToFile();

            menu.Items.Add(new ToolStripSeparator());

            ToolStripItem exportRemarks =
                menu.Items.Add("Export remarks to Excel…");
            exportRemarks.Click += (s, e) => ExportRemarksWithDialog();

            ToolStripItem importRemarks =
                menu.Items.Add("Import remarks from Excel…");
            importRemarks.Click += (s, e) => ImportRemarksWithDialog();

            menu.Opening +=
                (s, e) =>
                {
                    fill.Enabled = _model.UnplacedCount > 0;
                    clear.Enabled = _model.PlacedCount > 0;
                };

            return menu;
        }

        private ContextMenuStrip CreateLibraryMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();

            ToolStripMenuItem place =
                new ToolStripMenuItem("Place on page")
                {
                    ShortcutKeyDisplayString = "Enter"
                };
            place.Click += (s, e) => PlaceInNextEmpty(_library.SelectedPaths);

            ToolStripMenuItem show = new ToolStripMenuItem("Show on page");
            show.Click +=
                (s, e) =>
                {
                    string path =
                        _library.SelectedPaths.FirstOrDefault(_model.IsPlaced);

                    if (path != null)
                        ShowOnPage(path);
                };

            ToolStripMenuItem remove =
                new ToolStripMenuItem("Take off the page")
                {
                    ShortcutKeyDisplayString = "Del"
                };
            remove.Click += (s, e) => UnplaceImages(_library.SelectedPaths);

            ToolStripMenuItem discard =
                new ToolStripMenuItem("Remove from this collage");
            discard.Click += (s, e) => RemoveImages(_library.SelectedPaths);

            menu.Items.AddRange(
                new ToolStripItem[]
                {
                    place,
                    show,
                    remove,
                    new ToolStripSeparator(),
                    discard
                });

            menu.Opening +=
                (s, e) =>
                {
                    IList<string> selected = _library.SelectedPaths;

                    if (selected.Count == 0)
                    {
                        e.Cancel = true;
                        return;
                    }

                    bool anyPlaced = selected.Any(_model.IsPlaced);
                    bool anyUnplaced = selected.Any(p => !_model.IsPlaced(p));

                    place.Enabled = anyUnplaced;
                    show.Enabled = anyPlaced;
                    remove.Enabled = anyPlaced;
                };

            return menu;
        }

        private ContextMenuStrip CreateCanvasMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();

            ToolStripMenuItem edit =
                new ToolStripMenuItem("Edit remark")
                {
                    ShortcutKeyDisplayString = "F2"
                };
            edit.Click +=
                (s, e) =>
                {
                    if (_canvas.SelectedPath != null)
                        _canvas.BeginEdit(_canvas.SelectedPath, false);
                };

            ToolStripMenuItem editNote =
                new ToolStripMenuItem("Edit remark 2 (extra line)")
                {
                    ShortcutKeyDisplayString = "Shift+F2"
                };
            editNote.Click +=
                (s, e) =>
                {
                    if (_canvas.SelectedPath != null)
                        _canvas.BeginEdit(_canvas.SelectedPath, true);
                };

            ToolStripMenuItem remove =
                new ToolStripMenuItem("Take off the page")
                {
                    ShortcutKeyDisplayString = "Del"
                };
            remove.Click +=
                (s, e) =>
                {
                    if (_canvas.SelectedPath != null)
                        UnplaceImages(new[] { _canvas.SelectedPath });
                };

            ToolStripMenuItem locate = new ToolStripMenuItem("Show in library");
            locate.Click +=
                (s, e) =>
                {
                    string path = _canvas.SelectedPath;
                    if (path == null)
                        return;

                    if (!_library.Contains(path))
                    {
                        _searchBox.Clear();
                        SetFilter(LibraryFilter.All);
                    }

                    _library.SelectSingle(path, true);
                    _library.Focus();
                };

            ToolStripMenuItem clearPage =
                new ToolStripMenuItem("Take all images off this page");
            clearPage.Click +=
                (s, e) =>
                {
                    if (_canvas.SelectedSlot >= 0)
                        ClearPage(_canvas.SelectedSlot / _model.CellsPerPage);
                };

            menu.Items.AddRange(
                new ToolStripItem[]
                {
                    edit,
                    editNote,
                    remove,
                    locate,
                    new ToolStripSeparator(),
                    clearPage
                });

            menu.Opening +=
                (s, e) =>
                {
                    if (_canvas.SelectedSlot < 0)
                    {
                        e.Cancel = true;
                        return;
                    }

                    bool hasImage = _canvas.SelectedPath != null;

                    edit.Enabled = hasImage;
                    editNote.Enabled = hasImage;
                    remove.Enabled = hasImage;
                    locate.Enabled = hasImage;
                    clearPage.Enabled =
                        _model.CountOnPage(
                            _canvas.SelectedSlot / _model.CellsPerPage) > 0;
                };

            return menu;
        }

        private ContextMenuStrip CreateNavigatorMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();

            ToolStripMenuItem insert =
                new ToolStripMenuItem("Insert blank page before");
            insert.Click +=
                (s, e) =>
                {
                    int page = _navigator.ContextPage;

                    PushUndo();
                    _model.InsertPage(page);
                    _canvas.ScrollToPage(page);
                };

            ToolStripMenuItem clear =
                new ToolStripMenuItem("Take all images off this page");
            clear.Click += (s, e) => ClearPage(_navigator.ContextPage);

            ToolStripMenuItem delete = new ToolStripMenuItem("Delete page");
            delete.Click +=
                (s, e) =>
                {
                    PushUndo();
                    _model.DeletePage(_navigator.ContextPage);
                };

            menu.Items.AddRange(new ToolStripItem[] { insert, clear, delete });

            menu.Opening +=
                (s, e) =>
                {
                    int page = _navigator.ContextPage;

                    if (page < 0 || page >= _model.PageCount)
                    {
                        e.Cancel = true;
                        return;
                    }

                    clear.Enabled = _model.CountOnPage(page) > 0;
                    delete.Enabled = _model.PageCount > 1;
                };

            return menu;
        }

        // ------------------------------------------------------------
        // Control factories
        // ------------------------------------------------------------
        private Label CreateLabel(
            string text,
            float size,
            FontStyle style,
            Color color)
        {
            return new Label
            {
                Text = text,
                Font = new Font(_fontFamily, size, style),
                ForeColor = color,
                BackColor = Color.Transparent,
                AutoEllipsis = true
            };
        }

        private Button CreateButton(string text, int width, bool primary)
        {
            Button button = new Button
            {
                Text = text,
                Width = width,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
                Font = new Font(
                    _fontFamily,
                    8.5f,
                    primary ? FontStyle.Bold : FontStyle.Regular)
            };

            StyleButton(button, primary);
            return button;
        }

        private void StyleButton(Button button, bool primary)
        {
            Color back = primary ? _theme.ActiveColor : _theme.ControlBg;

            button.BackColor = back;
            button.ForeColor =
                primary
                    ? ThemeManager.GetContrastTextColor(back)
                    : _theme.TextColor;
            button.FlatAppearance.BorderColor =
                primary ? back : _theme.BorderColor;
            button.FlatAppearance.MouseOverBackColor =
                primary
                    ? CollageWorkspaceUi.Blend(back, Color.White, 0.14f)
                    : _theme.ButtonHover;
        }

        private Button CreateToolButton(
            string text,
            int width,
            string toolTip,
            Action onClick)
        {
            Button button = CreateButton(text, width, false);
            button.Margin = new Padding(0, 0, 4, 4);
            button.Click += (s, e) => onClick();
            _toolTip.SetToolTip(button, toolTip);
            return button;
        }

        private ComboBox CreateToolCombo(int width)
        {
            return new CollageThemedComboBox(_theme)
            {
                Width = width,
                Margin = new Padding(0, 3, 4, 4)
            };
        }

        private Label CreateToolLabel(string text)
        {
            Label label = CreateLabel(
                text,
                8.5f,
                FontStyle.Regular,
                _theme.MutedTextColor);
            label.AutoSize = true;
            label.Margin = new Padding(2, 8, 2, 0);
            return label;
        }

        private Panel CreateToolSeparator()
        {
            return new Panel
            {
                Width = 1,
                Height = 22,
                Margin = new Padding(5, 4, 9, 4),
                BackColor = _theme.BorderColor
            };
        }

        private static int DisplayModeToIndex(string mode)
        {
            return mode == CollageCellLayout.ModeFilename
                ? 0
                : mode == CollageCellLayout.ModeRemark
                    ? 2
                    : mode == CollageCellLayout.ModeNone
                        ? 3
                        : 1;
        }

        private static string IndexToDisplayMode(int index)
        {
            return index == 0
                ? CollageCellLayout.ModeFilename
                : index == 2
                    ? CollageCellLayout.ModeRemark
                    : index == 3
                        ? CollageCellLayout.ModeNone
                        : CollageCellLayout.ModeBoth;
        }

        // ------------------------------------------------------------
        // Refreshing
        // ------------------------------------------------------------
        private void RefreshAll()
        {
            _updatingGrid = true;
            try
            {
                _columnsCombo.SelectedIndex = _page.Columns - 1;
                _rowsCombo.SelectedIndex = _page.Rows - 1;
            }
            finally
            {
                _updatingGrid = false;
            }

            _subtitleLabel.Text = DescribePage();

            RefreshLibrary();
            _canvas.OnModelChanged();
            _navigator.OnModelChanged();
            RefreshView();
            RefreshInspector();
            RefreshStatus();
            RefreshUndoButtons();
        }

        private void RefreshLibrary()
        {
            if (_imagesByNameVersion != _model.ImageSetVersion)
            {
                _imagesByName = new List<string>(_model.Images);
                _imagesByName.Sort(
                    (a, b) => CollageWorkspaceUi.CompareNatural(
                        CollageArrangementModel.GetFileTitle(a),
                        CollageArrangementModel.GetFileTitle(b)));

                _imagesByNameVersion = _model.ImageSetVersion;
            }

            IEnumerable<string> query;

            if (_filter == LibraryFilter.Placed)
            {
                // Placed images are listed in page order.
                query =
                    _model.Images
                        .Where(_model.IsPlaced)
                        .OrderBy(_model.IndexOf);
            }
            else if (_filter == LibraryFilter.Unplaced)
            {
                query = _imagesByName.Where(p => !_model.IsPlaced(p));
            }
            else
            {
                query = _imagesByName;
            }

            string search = _searchBox.Text.Trim();

            string[] terms =
                search.Split(
                    new[] { ' ' },
                    StringSplitOptions.RemoveEmptyEntries);

            if (terms.Length > 0)
                query = query.Where(p => MatchesSearch(p, terms));

            string emptyText =
                terms.Length > 0
                    ? "No images match \"" + search + "\"."
                    : _filter == LibraryFilter.Unplaced
                        ? "Every image is on a page.\n\n" +
                          "Drag an image here from a page to take it off."
                        : _filter == LibraryFilter.Placed
                            ? "No images are on a page yet.\n\n" +
                              "Drag images from the library onto a page."
                            : "No images.";

            _library.SetItems(query.ToList(), emptyText);

            _filterButtons[0].Text = "All  " + _model.Images.Count;
            _filterButtons[1].Text = "Not placed  " + _model.UnplacedCount;
            _filterButtons[2].Text = "Placed  " + _model.PlacedCount;

            for (int i = 0; i < _filterButtons.Length; i++)
                StyleButton(_filterButtons[i], (int)_filter == i);

            for (int i = 0; i < _sizeButtons.Length; i++)
                StyleButton(_sizeButtons[i], _library.SizeLevel == i + 1);
        }

        private bool MatchesSearch(string path, string[] terms)
        {
            // The file name and both remarks are searched together.
            string text =
                Path.GetFileName(path) + "\n" +
                _model.GetRemark(path) + "\n" +
                _model.GetNote(path);

            foreach (string term in terms)
            {
                if (text.IndexOf(term, StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
            }

            return true;
        }

        // Follows the canvas as it scrolls and zooms.
        private void RefreshView()
        {
            // The canvas reports its first resize while the window is
            // still being built.
            if (_canvas == null || _navigator == null || _pageCombo == null)
                return;

            int pages = _model.PageCount;
            int current = _canvas.CurrentPage;

            _updatingPageCombo = true;
            try
            {
                if (_pageCombo.Items.Count != pages)
                {
                    _pageCombo.BeginUpdate();
                    _pageCombo.Items.Clear();

                    for (int page = 1; page <= pages; page++)
                        _pageCombo.Items.Add("Page " + page + " of " + pages);

                    _pageCombo.EndUpdate();
                }

                if (_pageCombo.SelectedIndex != current)
                    _pageCombo.SelectedIndex = current;
            }
            finally
            {
                _updatingPageCombo = false;
            }

            _previousPageButton.Enabled = current > 0;
            _nextPageButton.Enabled = current < pages - 1;
            _zoomLabel.Text = Math.Round(_canvas.Zoom * 100f) + "%";
            _navigator.CurrentPage = current;
        }

        private void RefreshInspector()
        {
            // Identifies what the two text boxes edit; null when nothing is
            // selected.
            string key = null;
            string remarkText = string.Empty;
            string noteText = string.Empty;

            if (_inspected.Count == 0)
            {
                _inspectorName.Text = "No image selected";
                _inspectorLocation.Text =
                    "Click an image in the library or on a page.";
                _placeButton.Enabled = false;
                _removeButton.Enabled = false;
            }
            else if (_inspected.Count == 1)
            {
                string path = _inspected[0];
                int slot = _model.IndexOf(path);

                string location =
                    slot < 0
                        ? "Not placed"
                        : "Page " + ((slot / _model.CellsPerPage) + 1) +
                          "  •  Cell " + ((slot % _model.CellsPerPage) + 1);

                Size size;
                if (_thumbnails.TryGetOriginalSize(path, out size))
                    location += "  •  " + size.Width + " × " + size.Height + " px";

                _inspectorName.Text = Path.GetFileName(path);
                _inspectorLocation.Text = location;
                _placeButton.Enabled = slot < 0;
                _removeButton.Enabled = slot >= 0;

                key = path;
                remarkText = _model.GetEffectiveRemark(path);
                noteText = _model.GetNote(path) ?? string.Empty;
            }
            else
            {
                int placed = _inspected.Count(_model.IsPlaced);

                _inspectorName.Text = _inspected.Count + " images selected";
                _inspectorLocation.Text =
                    placed + " placed  •  " +
                    (_inspected.Count - placed) + " not placed";
                _placeButton.Enabled = placed < _inspected.Count;
                _removeButton.Enabled = placed > 0;

                // The same text can be given to all of them at once. Each
                // box shows the text they already share, or nothing when
                // they differ.
                key = string.Join("|", _inspected);
                remarkText = SharedText(_inspected, _model.GetEffectiveRemark);
                noteText = SharedText(_inspected, p => _model.GetNote(p) ?? string.Empty);
            }

            // Only highlighted while it can be used.
            StyleButton(_placeButton, _placeButton.Enabled);

            bool sameSelection =
                key != null &&
                string.Equals(
                    key,
                    _remarkBoxKey,
                    StringComparison.OrdinalIgnoreCase);

            _updatingRemark = true;
            try
            {
                _remarkBoxKey = key;
                _editTargets.Clear();
                _editTargets.AddRange(_inspected);

                ShowInspectorText(_remarkBox, remarkText, key != null, sameSelection);
                ShowInspectorText(_noteBox, noteText, key != null, sameSelection);
            }
            finally
            {
                _updatingRemark = false;
            }

            bool several = _editTargets.Count > 1;

            CollageWorkspaceUi.SetCueBanner(
                _remarkBox,
                several
                    ? "Type, then Enter: same for all " + _editTargets.Count
                    : string.Empty);

            CollageWorkspaceUi.SetCueBanner(
                _noteBox,
                several
                    ? "Type, then Enter: same for all " + _editTargets.Count
                    : _editTargets.Count == 1
                        ? "Additional remark (optional)"
                        : string.Empty);

            _remarkResetButton.Enabled =
                _editTargets.Any(p => _model.GetRemark(p) != null);
        }

        private static string SharedText(
            IList<string> paths,
            Func<string, string> getText)
        {
            string first = getText(paths[0]);

            return paths.All(
                p => string.Equals(getText(p), first, StringComparison.Ordinal))
                ? first
                : string.Empty;
        }

        private static void ShowInspectorText(
            TextBox box,
            string text,
            bool enabled,
            bool sameImage)
        {
            // Never rewrite a box while the user is typing in it.
            if (box.Focused && sameImage)
                return;

            box.Enabled = enabled;

            if (box.Text != text)
                box.Text = text;
        }

        private void RefreshStatus()
        {
            int unplaced = _model.UnplacedCount;
            int total = _model.Images.Count;

            _summaryLabel.Text =
                total + (total == 1 ? " image" : " images") + "  •  " +
                _model.PlacedCount + " placed  •  " +
                unplaced + " not placed  •  " +
                _model.PageCount + (_model.PageCount == 1 ? " page" : " pages");

            if (unplaced == 0)
            {
                _statusLabel.ForeColor = _theme.TextColor;
                _statusLabel.Text =
                    total == 1
                        ? "The image is on a page."
                        : "All " + total + " images are on a page.";
            }
            else
            {
                _statusLabel.ForeColor =
                    CollageWorkspaceUi.WarningColor(_theme.ContentBg);
                _statusLabel.Text =
                    unplaced +
                    (unplaced == 1 ? " image is" : " images are") +
                    " not placed and will be left out of the PDF.";
            }
        }

        private void RefreshUndoButtons()
        {
            _undoButton.Enabled = _undo.Count > 0;
            _redoButton.Enabled = _redo.Count > 0;
        }

        private void Thumbnails_Ready(object sender, EventArgs e)
        {
            _library.Invalidate();
            _canvas.Invalidate();
            _navigator.Invalidate();

            // The pixel size of the selected image may have just arrived.
            if (_inspected.Count == 1)
                RefreshInspector();
        }

        // ------------------------------------------------------------
        // Selection
        // ------------------------------------------------------------
        private void SetInspected(IEnumerable<string> paths)
        {
            _inspected.Clear();
            _inspectedSet.Clear();

            foreach (string path in paths)
            {
                if (path != null && _inspectedSet.Add(path))
                    _inspected.Add(path);
            }

            RefreshInspector();
            _navigator.Invalidate();
        }

        private void Library_SelectionChanged(object sender, EventArgs e)
        {
            IList<string> selected = _library.SelectedPaths;

            if (selected.Count == 1)
            {
                int slot = _model.IndexOf(selected[0]);

                // Highlight it on its page without scrolling there; an
                // empty cell chosen as the drop target stays selected.
                if (slot >= 0)
                    _canvas.SelectSlot(slot, false);
                else if (_canvas.SelectedPath != null)
                    _canvas.SelectSlot(-1, false);
            }

            SetInspected(selected);
        }

        private void Canvas_SelectionChanged(object sender, EventArgs e)
        {
            string path = _canvas.SelectedPath;

            if (path == null)
                return;

            if (_library.Contains(path))
                _library.SelectSingle(path, true);
            else
                _library.ClearSelection();

            SetInspected(new[] { path });
        }

        private void SetFilter(LibraryFilter filter)
        {
            if (_filter == filter)
                return;

            _filter = filter;
            RefreshLibrary();
        }

        private void SetLibrarySize(int level)
        {
            _library.SizeLevel = level;

            for (int i = 0; i < _sizeButtons.Length; i++)
                StyleButton(_sizeButtons[i], level == i + 1);
        }

        // ------------------------------------------------------------
        // Actions
        // ------------------------------------------------------------
        private void DropImages(string[] paths, int slot, bool insert)
        {
            if (paths == null || paths.Length == 0)
                return;

            if (paths.Length == 1 && _model.IndexOf(paths[0]) == slot)
                return;

            PushUndo();

            int placedAt = _model.Place(paths, slot, insert);
            if (placedAt < 0)
                return;

            string first = _model.GetSlot(placedAt);

            _canvas.SelectSlot(placedAt, false);

            if (_library.Contains(first))
                _library.SelectSingle(first, false);

            SetInspected(new[] { first });
        }

        private void UnplaceImages(IList<string> paths)
        {
            if (paths == null || !paths.Any(_model.IsPlaced))
                return;

            PushUndo();
            _model.Unplace(paths);
        }

        private void PlaceInNextEmpty(IList<string> paths)
        {
            List<string> unplaced =
                paths
                    .Where(p => !_model.IsPlaced(p))
                    .ToList();

            if (unplaced.Count == 0)
                return;

            // Start at the empty cell selected on the page, otherwise at
            // the page being looked at.
            int selected = _canvas.SelectedSlot;

            int start =
                selected >= 0 && _model.GetSlot(selected) == null
                    ? selected
                    : _canvas.CurrentPage * _model.CellsPerPage;

            PushUndo();

            int slot = _model.PlaceInNextEmpty(unplaced, start);
            if (slot < 0)
                return;

            _canvas.SelectSlot(slot, true);
            SetInspected(new[] { unplaced[0] });
        }

        private void ActivateLibraryItem(string path)
        {
            if (_model.IsPlaced(path))
            {
                ShowOnPage(path);
                return;
            }

            IList<string> selected = _library.SelectedPaths;

            PlaceInNextEmpty(
                selected.Contains(path, StringComparer.OrdinalIgnoreCase)
                    ? selected
                    : new[] { path });
        }

        private void ShowOnPage(string path)
        {
            int slot = _model.IndexOf(path);
            if (slot < 0)
                return;

            _canvas.SelectSlot(slot, true);
            SetInspected(new[] { path });
        }

        private void AddPage()
        {
            PushUndo();
            _canvas.ScrollToPage(_model.AddPage());
        }

        // ------------------------------------------------------------
        // Adding and removing images
        // ------------------------------------------------------------
        private void AddImagesFromDialog()
        {
            using (OpenFileDialog dialog = new OpenFileDialog
            {
                Multiselect = true,
                Title = "Add images to this collage",
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff"
            })
            {
                if (DialogMemoryManager.ShowOpenDialog(
                    dialog,
                    "Collage.SourceFiles") == DialogResult.OK)
                {
                    AddImages(dialog.FileNames, -1, false);
                }
            }
        }

        // Adds image files (or the images inside dropped folders) to the
        // collage. With a cell given they are placed there straight away;
        // otherwise they wait in the library as not placed.
        private void AddImages(string[] files, int slot, bool insert)
        {
            List<string> usable =
                ExpandFolders(files)
                    .Where(ImageCollageEngine.IsSupportedImageFile)
                    .Where(File.Exists)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

            if (usable.Count == 0)
            {
                MessageBox.Show(
                    this,
                    "No image files that can be used were found.\n\n" +
                    "Supported types: PNG, JPG, BMP, TIF.",
                    "Add Images",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            List<string> fresh =
                usable
                    .Where(p => !_model.Contains(p))
                    .ToList();

            if (fresh.Count > 0 || slot >= 0)
                PushUndo();

            if (fresh.Count > 0)
            {
                _model.AddImages(fresh);
                _thumbnails.Preload(fresh);
            }

            if (slot >= 0)
            {
                int placedAt = _model.Place(usable, slot, insert);

                if (placedAt >= 0)
                {
                    _canvas.SelectSlot(placedAt, false);
                    SetInspected(new[] { _model.GetSlot(placedAt) });
                }

                return;
            }

            // Show the images that were asked for, including any that were
            // already in the collage.
            if (!usable.All(_library.Contains))
            {
                _searchBox.Clear();
                SetFilter(
                    usable.Any(_model.IsPlaced)
                        ? LibraryFilter.All
                        : LibraryFilter.Unplaced);
            }

            _library.SelectMany(usable);
            _library.Focus();
            SetInspected(usable);
        }

        private static IEnumerable<string> ExpandFolders(IEnumerable<string> paths)
        {
            List<string> result = new List<string>();

            if (paths == null)
                return result;

            foreach (string path in paths)
            {
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                if (!Directory.Exists(path))
                {
                    result.Add(path);
                    continue;
                }

                try
                {
                    result.AddRange(Directory.GetFiles(path));
                }
                catch
                {
                    // A folder that cannot be read is skipped.
                }
            }

            return result;
        }

        // Takes images out of the collage altogether. Unlike "take off the
        // page", they no longer count as not placed.
        private void RemoveImages(IList<string> paths)
        {
            if (paths == null || paths.Count == 0)
                return;

            PushUndo();
            _model.RemoveImages(paths);
            SetInspected(new string[0]);
        }

        // ------------------------------------------------------------
        // Saving the layout
        // ------------------------------------------------------------
        private CollageLayout BuildLayout()
        {
            return new CollageLayout
            {
                Page = _page.Clone(),
                DisplayMode = ResultDisplayMode,
                Images = new List<string>(_model.Images),
                Slots = _model.BuildResultSlots(),
                Remarks = _model.BuildResultRemarks(),
                Notes = _model.BuildResultNotes()
            };
        }

        // Saves the arrangement as it is now, so it can be imported later
        // from the Image Collage Builder without creating a PDF first.
        private void SaveLayoutToFile()
        {
            using (SaveFileDialog dialog = new SaveFileDialog
            {
                Title = "Save collage layout",
                Filter =
                    "NPPL collage layout|*" + CollageLayoutFile.Extension,
                DefaultExt = CollageLayoutFile.Extension.TrimStart('.'),
                FileName =
                    "CollageLayout_" +
                    DateTime.Now.ToString("yyyyMMdd_HHmm")
            })
            {
                if (DialogMemoryManager.ShowSaveDialog(
                    dialog,
                    "Collage.SaveLayout") != DialogResult.OK)
                    return;

                try
                {
                    CollageLayoutFile.Save(BuildLayout(), dialog.FileName);

                    MessageBox.Show(
                        this,
                        "Layout saved.\n\n" +
                        dialog.FileName + "\n\n" +
                        "Use \"Import Layout\" in the Image Collage Builder " +
                        "to open it again.",
                        "Save Layout",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        this,
                        "The layout could not be saved.\n\n" + ex.Message,
                        "Save Layout",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }

        private void ClearPage(int page)
        {
            if (page < 0 ||
                page >= _model.PageCount ||
                _model.CountOnPage(page) == 0)
                return;

            PushUndo();
            _model.ClearPage(page);
        }

        // ------------------------------------------------------------
        // Remarks
        // ------------------------------------------------------------
        // With one image selected the page follows the typing. With
        // several selected the text is applied to all of them when the box
        // is left or Enter is pressed (see CommitBulkText), so a stray
        // keystroke cannot overwrite a whole selection.
        private void RemarkBox_TextChanged(object sender, EventArgs e)
        {
            if (_updatingRemark || _editTargets.Count != 1)
                return;

            string path = _editTargets[0];

            if (string.Equals(
                _remarkBox.Text.Trim(),
                _model.GetEffectiveRemark(path),
                StringComparison.Ordinal))
                return;

            // One undo step for the whole edit, not one per keystroke.
            if (!_remarkUndoPushed)
            {
                PushUndo();
                _remarkUndoPushed = true;
            }

            _model.SetRemark(path, _remarkBox.Text);
            ShowRemarksIfHidden();
        }

        private void NoteBox_TextChanged(object sender, EventArgs e)
        {
            if (_updatingRemark || _editTargets.Count != 1)
                return;

            string path = _editTargets[0];

            if (string.Equals(
                _noteBox.Text.Trim(),
                _model.GetNote(path) ?? string.Empty,
                StringComparison.Ordinal))
                return;

            if (!_remarkUndoPushed)
            {
                PushUndo();
                _remarkUndoPushed = true;
            }

            _model.SetNote(path, _noteBox.Text);
            ShowRemarksIfHidden();
        }

        // Gives the text typed in one of the inspector boxes to every
        // selected image, as a single undo step.
        private void CommitBulkText(TextBox box)
        {
            if (_updatingRemark || _editTargets.Count < 2 || !box.Modified)
                return;

            box.Modified = false;
            ApplyTextToImages(_editTargets.ToList(), box == _noteBox, box.Text);
        }

        private int ApplyTextToImages(IList<string> paths, bool note, string text)
        {
            string value = (text ?? string.Empty).Trim();

            List<string> changing =
                paths
                    .Where(
                        p => !string.Equals(
                            value,
                            note
                                ? _model.GetNote(p) ?? string.Empty
                                : value.Length == 0
                                    ? _model.GetRemark(p) ?? string.Empty
                                    : _model.GetEffectiveRemark(p),
                            StringComparison.Ordinal))
                    .ToList();

            if (changing.Count == 0)
                return 0;

            PushUndo();
            _model.BeginUpdate();

            try
            {
                foreach (string path in changing)
                {
                    if (note)
                        _model.SetNote(path, value);
                    else
                        _model.SetRemark(path, value);
                }
            }
            finally
            {
                _model.EndUpdate();
            }

            if (value.Length > 0)
                ShowRemarksIfHidden();

            return changing.Count;
        }

        // Text typed directly on the page: the remark, or the additional
        // remark on the line below it.
        private void ApplyEditedText(string path, bool note, string text)
        {
            string value = (text ?? string.Empty).Trim();

            string current =
                note
                    ? _model.GetNote(path) ?? string.Empty
                    : _model.GetEffectiveRemark(path);

            if (string.Equals(value, current, StringComparison.Ordinal))
                return;

            PushUndo();

            if (note)
                _model.SetNote(path, value);
            else
                _model.SetRemark(path, value);

            ShowRemarksIfHidden();
        }

        // Back to the file name, for every selected image.
        private void ResetRemark()
        {
            ApplyTextToImages(_editTargets.ToList(), false, string.Empty);
        }

        // ------------------------------------------------------------
        // Remarks from and to a sheet
        // ------------------------------------------------------------
        private void ExportRemarksWithDialog()
        {
            using (SaveFileDialog dialog = new SaveFileDialog
            {
                Title = "Export remarks to Excel",
                Filter = "Excel workbook|*.xlsx",
                DefaultExt = "xlsx",
                FileName = "CollageRemarks_" + DateTime.Now.ToString("yyyyMMdd_HHmm")
            })
            {
                if (DialogMemoryManager.ShowSaveDialog(
                    dialog,
                    "Collage.RemarkSheet") != DialogResult.OK)
                    return;

                try
                {
                    int count = ExportRemarksTo(dialog.FileName);

                    MessageBox.Show(
                        this,
                        count + " image(s) written to\n" + dialog.FileName + "\n\n" +
                        "Type the remarks in columns B and C, save the workbook, " +
                        "then use \"Import remarks from Excel\".",
                        "Export Remarks",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        this,
                        "The remarks could not be exported.\n\n" + ex.Message,
                        "Export Remarks",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }

        private int ExportRemarksTo(string path)
        {
            List<CollageRemarkRow> rows = new List<CollageRemarkRow>();

            foreach (string image in _imagesByName)
            {
                int slot = _model.IndexOf(image);

                rows.Add(
                    new CollageRemarkRow
                    {
                        FileName = Path.GetFileName(image),
                        Remark = _model.GetRemark(image) ?? string.Empty,
                        Remark2 = _model.GetNote(image) ?? string.Empty,
                        Location =
                            slot < 0
                                ? "Not placed"
                                : "Page " + ((slot / _model.CellsPerPage) + 1) +
                                  ", cell " + ((slot % _model.CellsPerPage) + 1)
                    });
            }

            CollageRemarkSheet.Write(path, rows);
            return rows.Count;
        }

        private void ImportRemarksWithDialog()
        {
            using (OpenFileDialog dialog = new OpenFileDialog
            {
                Title = "Import remarks",
                Filter = CollageRemarkSheet.OpenFilter
            })
            {
                if (DialogMemoryManager.ShowOpenDialog(
                    dialog,
                    "Collage.RemarkSheet") != DialogResult.OK)
                    return;

                try
                {
                    MessageBox.Show(
                        this,
                        ImportRemarksFrom(dialog.FileName),
                        "Import Remarks",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        this,
                        "The sheet could not be read.\n\n" + ex.Message + "\n\n" +
                        "Column A must hold the image file name, column B the " +
                        "remark and column C the additional remark.",
                        "Import Remarks",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }

        // Applies the remarks of a sheet to the images named in it and
        // returns a short report. Rows are matched by file name, with or
        // without the extension. An empty cell leaves that text as it is.
        private string ImportRemarksFrom(string path)
        {
            List<CollageRemarkRow> rows = CollageRemarkSheet.Read(path);

            Dictionary<string, List<string>> byName =
                new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (string image in _model.Images)
            {
                foreach (string name in new[]
                {
                    Path.GetFileName(image),
                    CollageArrangementModel.GetFileTitle(image)
                })
                {
                    List<string> matches;

                    if (!byName.TryGetValue(name, out matches))
                        byName[name] = matches = new List<string>();

                    if (!matches.Contains(image))
                        matches.Add(image);
                }
            }

            List<string> unmatched = new List<string>();
            List<Action> changes = new List<Action>();
            HashSet<string> touched =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (CollageRemarkRow row in rows)
            {
                List<string> matches;

                if (!byName.TryGetValue(row.FileName, out matches))
                {
                    unmatched.Add(row.FileName);
                    continue;
                }

                foreach (string image in matches)
                {
                    string target = image;
                    string remark = row.Remark;
                    string remark2 = row.Remark2;

                    if (remark.Length > 0 &&
                        !string.Equals(
                            remark,
                            _model.GetEffectiveRemark(target),
                            StringComparison.Ordinal))
                    {
                        changes.Add(() => _model.SetRemark(target, remark));
                        touched.Add(target);
                    }

                    if (remark2.Length > 0 &&
                        !string.Equals(
                            remark2,
                            _model.GetNote(target) ?? string.Empty,
                            StringComparison.Ordinal))
                    {
                        changes.Add(() => _model.SetNote(target, remark2));
                        touched.Add(target);
                    }
                }
            }

            if (changes.Count > 0)
            {
                PushUndo();
                _model.BeginUpdate();

                try
                {
                    foreach (Action change in changes)
                        change();
                }
                finally
                {
                    _model.EndUpdate();
                }

                ShowRemarksIfHidden();
            }

            string report =
                rows.Count + " row(s) read.  " +
                touched.Count + " image(s) updated.";

            if (unmatched.Count > 0)
            {
                report +=
                    "\n\n" + unmatched.Count +
                    " name(s) in the sheet did not match any image, for example:\n   " +
                    string.Join("\n   ", unmatched.Take(5));
            }

            return report;
        }

        // A remark that was just typed should be visible straight away.
        private void ShowRemarksIfHidden()
        {
            if (ResultDisplayMode == CollageCellLayout.ModeFilename)
                _displayModeCombo.SelectedIndex = 1;
        }

        // ------------------------------------------------------------
        // Undo
        // ------------------------------------------------------------
        private void PushUndo()
        {
            _undo.Add(_model.Capture());

            if (_undo.Count > MaxUndoSteps)
                _undo.RemoveAt(0);

            _redo.Clear();
            _dirty = true;
            RefreshUndoButtons();
        }

        private void Undo()
        {
            if (_undo.Count == 0)
                return;

            CollageArrangementSnapshot snapshot = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);

            _redo.Add(_model.Capture());
            _model.Restore(snapshot);
        }

        private void Redo()
        {
            if (_redo.Count == 0)
                return;

            CollageArrangementSnapshot snapshot = _redo[_redo.Count - 1];
            _redo.RemoveAt(_redo.Count - 1);

            _undo.Add(_model.Capture());
            _model.Restore(snapshot);
        }

        // ------------------------------------------------------------
        // Keyboard, accept and close
        // ------------------------------------------------------------
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            bool typing = ActiveControl is TextBoxBase;

            if (keyData == (Keys.Control | Keys.F))
            {
                _searchBox.Focus();
                _searchBox.SelectAll();
                return true;
            }

            if (typing)
            {
                if (keyData == Keys.Escape &&
                    ActiveControl == _searchBox &&
                    _searchBox.TextLength > 0)
                {
                    _searchBox.Clear();
                    return true;
                }

                // Letters, Ctrl+Z and the rest belong to the text box.
                return base.ProcessCmdKey(ref msg, keyData);
            }

            switch (keyData)
            {
                case Keys.Control | Keys.Z:
                    Undo();
                    return true;
                case Keys.Control | Keys.Y:
                case Keys.Control | Keys.Shift | Keys.Z:
                    Redo();
                    return true;
                case Keys.Control | Keys.D0:
                case Keys.Control | Keys.NumPad0:
                    _canvas.FitPage();
                    return true;
                case Keys.Control | Keys.Oemplus:
                case Keys.Control | Keys.Add:
                    _canvas.ZoomBy(1.2f);
                    return true;
                case Keys.Control | Keys.OemMinus:
                case Keys.Control | Keys.Subtract:
                    _canvas.ZoomBy(1f / 1.2f);
                    return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void AcceptArrangement()
        {
            if (_model.PlacedCount == 0)
            {
                MessageBox.Show(
                    this,
                    "There are no images on any page.\n\n" +
                    "Place at least one image before using this arrangement.",
                    "Arrange Images",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            ResultSlots = _model.BuildResultSlots();
            ResultCaptions = _model.BuildResultRemarks();

            // Kept identical to the captions: the PDF generator reads the
            // custom name first.
            ResultCustomNames = _model.BuildResultRemarks();
            ResultNotes = _model.BuildResultNotes();
            ResultImages = new List<string>(_model.Images);
            ResultPage = _page.Clone();

            _accepted = true;
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            if (e.Cancel || _accepted || !_dirty)
                return;

            DialogResult answer =
                MessageBox.Show(
                    this,
                    "Discard the changes made to this arrangement?",
                    "Arrange Images",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2);

            if (answer != DialogResult.Yes)
                e.Cancel = true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _thumbnails.Dispose();

                if (_toolTip != null)
                    _toolTip.Dispose();

                if (_arrangeMenu != null)
                    _arrangeMenu.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
