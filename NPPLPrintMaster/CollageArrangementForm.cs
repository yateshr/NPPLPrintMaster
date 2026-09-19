using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace NPPLPrintMaster
{
    /// <summary>
    /// Photoshop/BarTender-style manual collage workspace.
    ///
    /// The selected grid remains fixed. The white A4 sheet is the real PDF
    /// page; everything outside it is an infinite pasteboard. Images may be
    /// dragged between cells and the pasteboard without regenerating a PDF.
    /// </summary>
    public sealed class CollageArrangementForm : Form
    {
        private const string ImagePathFormat = "NPPL.Collage.ImagePath";

        private readonly int _gridSize;
        private readonly bool _landscape;
        private readonly List<string> _originalOrder;
        private readonly List<string> _slots;
        private readonly Dictionary<string, string> _captions =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _customNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _unplaced =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, PointF> _pasteboardPositions =
            new Dictionary<string, PointF>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Image> _thumbnailCache =
            new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);

        private readonly ThemeColors _theme;
        private readonly string _fontFamily;

        private CollageDesignCanvas _canvas;
        private FlowLayoutPanel _imageBrowser;
        private TextBox _searchBox;
        private ComboBox _imageStatusCombo;
        private TrackBar _thumbnailSizeTrackBar;
        private Label _thumbnailSizeLabel;
        private Label _imageCountLabel;
        private Label _selectedLabel;
        private Label _captionLabel;
        private TextBox _captionBox;
        private Button _captionApplyButton;
        private Label _pageLabel;
        private ComboBox _pageCombo;
        private ComboBox _displayModeCombo;
        private Label _statusLabel;
        private Button _undoButton;
        private Button _redoButton;

        private readonly Stack<ArrangementSnapshot> _undo =
            new Stack<ArrangementSnapshot>();
        private readonly Stack<ArrangementSnapshot> _redo =
            new Stack<ArrangementSnapshot>();

        private string _selectedImagePath;
        private int _currentPage;
        private int _totalPages;

        public IList<string> ResultSlots { get; private set; }
        public IDictionary<string, string> ResultCaptions { get; private set; }
        public IDictionary<string, string> ResultCustomNames { get; private set; }
        public string ResultDisplayMode { get; private set; } = "Both";

        public CollageArrangementForm(
            IList<string> orderedImagePaths,
            int gridSize,
            bool landscape,
            AppSettings appSettings,
            IList<string> initialSlots = null,
            IDictionary<string, string> initialCaptions = null,
            IDictionary<string, string> initialCustomNames = null)
        {
            if (orderedImagePaths == null)
                throw new ArgumentNullException("orderedImagePaths");

            if (gridSize < 1 || gridSize > 4)
                throw new ArgumentOutOfRangeException("gridSize");

            _gridSize = gridSize;
            _landscape = landscape;

            _originalOrder = orderedImagePaths
                .Where(File.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

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

            _slots = BuildInitialSlots(initialSlots);

            if (initialCaptions != null)
            {
                foreach (KeyValuePair<string, string> pair in initialCaptions)
                {
                    if (!string.IsNullOrWhiteSpace(pair.Key) &&
                        _originalOrder.Any(
                            p => string.Equals(
                                p,
                                pair.Key,
                                StringComparison.OrdinalIgnoreCase)))
                    {
                        _captions[pair.Key] = pair.Value ?? string.Empty;
                    }
                }
            }

            if (initialCustomNames != null)
            {
                foreach (KeyValuePair<string, string> pair in initialCustomNames)
                {
                    if (!string.IsNullOrWhiteSpace(pair.Key) &&
                        _originalOrder.Any(
                            p => string.Equals(
                                p,
                                pair.Key,
                                StringComparison.OrdinalIgnoreCase)))
                    {
                        _customNames[pair.Key] = pair.Value ?? string.Empty;
                    }
                }
            }

            // Every image gets an editable custom remark initialized from its
            // original filename. Existing saved remarks/custom names are preserved.
            foreach (string path in _originalOrder)
            {
                string existingRemark;
                _captions.TryGetValue(path, out existingRemark);

                string existingCustomName;
                _customNames.TryGetValue(path, out existingCustomName);

                if (string.IsNullOrWhiteSpace(existingRemark))
                {
                    _captions[path] =
                        string.IsNullOrWhiteSpace(existingCustomName)
                            ? Path.GetFileNameWithoutExtension(path)
                            : existingCustomName;
                }
            }

            int slotsPerPage = _gridSize * _gridSize;

            _totalPages = Math.Max(
                1,
                (int)Math.Ceiling(
                    _slots.Count / (double)slotsPerPage));

            while (_slots.Count < _totalPages * slotsPerPage)
                _slots.Add(null);

            RebuildUnplacedSet();
            InitializePasteboardPositions();

            BuildInterface();
            RefreshAll();
        }

        private List<string> BuildInitialSlots(IList<string> initialSlots)
        {
            List<string> result =
                initialSlots == null
                    ? new List<string>(_originalOrder)
                    : new List<string>(initialSlots);

            HashSet<string> valid =
                new HashSet<string>(
                    _originalOrder,
                    StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < result.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(result[i]) ||
                    !valid.Contains(result[i]))
                {
                    result[i] = null;
                }
            }

            foreach (string path in _originalOrder)
            {
                bool alreadyAssigned =
                    result.Any(
                        p => string.Equals(
                            p,
                            path,
                            StringComparison.OrdinalIgnoreCase));

                if (!alreadyAssigned)
                {
                    int emptyIndex =
                        result.FindIndex(
                            p => string.IsNullOrWhiteSpace(p));

                    if (emptyIndex >= 0)
                        result[emptyIndex] = path;
                    else
                        result.Add(path);
                }
            }

            return result;
        }

        private void BuildInterface()
        {
            Text =
                "NPPL PrintMaster  •  Image Collage Builder  •  Design Workspace";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(1200, 760);
            Size = new Size(
                _landscape ? 1500 : 1360,
                900);
            BackColor = _theme.BgLight;
            ForeColor = _theme.TextColor;
            Font = new Font(_fontFamily, 9f);
            KeyPreview = true;

            Panel header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 70,
                BackColor = _theme.ContentBg,
                Padding = new Padding(18, 9, 18, 8)
            };

            Label title = new Label
            {
                Text = "Image Collage Builder",
                Font = new Font(_fontFamily, 16f, FontStyle.Bold),
                ForeColor = _theme.TextColor,
                AutoSize = true,
                Location = new Point(18, 10)
            };

            Label subtitle = new Label
            {
                Text =
                    "Design Workspace  •  drag images around the infinite pasteboard  •  " +
                    "white page = PDF",
                Font = new Font(_fontFamily, 8.5f, FontStyle.Regular),
                ForeColor = _theme.MutedTextColor,
                AutoSize = true,
                Location = new Point(20, 39)
            };

            header.Controls.Add(subtitle);
            header.Controls.Add(title);

            Panel footer = CreateFooter();

            SplitContainer split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 5,
                SplitterDistance = 285,
                Panel1MinSize = 220,
                Panel2MinSize = 0,
                BackColor = _theme.BorderColor
            };

            split.Panel1.BackColor = _theme.ContentBg;
            split.Panel2.BackColor = _theme.BgLight;

            split.Panel1.Controls.Add(CreateImageLibrary());

            Panel editor = CreateEditorPanel();
            split.Panel2.Controls.Add(editor);

            Controls.Add(split);
            Controls.Add(footer);
            Controls.Add(header);

            Shown += (s, e) => _canvas.FitPage();
            KeyDown += CollageArrangementForm_KeyDown;
        }

        private Panel CreateImageLibrary()
        {
            Panel left = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = _theme.ContentBg,
                Padding = new Padding(8)
            };

            Label heading = CreateLabel(
                "IMAGE LIBRARY",
                9f,
                FontStyle.Bold,
                _theme.TextColor);
            heading.Dock = DockStyle.Top;
            heading.Height = 26;

            _imageCountLabel = CreateLabel(
                "",
                8f,
                FontStyle.Regular,
                _theme.MutedTextColor);
            _imageCountLabel.Dock = DockStyle.Top;
            _imageCountLabel.Height = 22;

            _searchBox = new TextBox
            {
                Dock = DockStyle.Top,
                Height = 29,
                BackColor = _theme.ControlBg,
                ForeColor = _theme.TextColor,
                BorderStyle = BorderStyle.FixedSingle
            };
            _searchBox.TextChanged += (s, e) => RefreshImageBrowser();

            _imageStatusCombo = new ComboBox
            {
                Dock = DockStyle.Top,
                Height = 30,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = _theme.ControlBg,
                ForeColor = _theme.TextColor,
                FlatStyle = FlatStyle.Flat
            };
            _imageStatusCombo.Items.AddRange(
                new object[] { "Unplaced", "Placed", "All" });
            _imageStatusCombo.SelectedIndex = 0;
            _imageStatusCombo.SelectedIndexChanged +=
                (s, e) => RefreshImageBrowser();

            Panel thumbOptions = new Panel
            {
                Dock = DockStyle.Top,
                Height = 38,
                BackColor = _theme.ContentBg
            };

            _thumbnailSizeLabel = CreateLabel(
                "Thumbnail Large",
                7.8f,
                FontStyle.Regular,
                _theme.MutedTextColor);
            _thumbnailSizeLabel.Location = new Point(0, 8);
            _thumbnailSizeLabel.Width = 100;

            _thumbnailSizeTrackBar = new TrackBar
            {
                Minimum = 1,
                Maximum = 3,
                Value = 3,
                TickStyle = TickStyle.None,
                Width = 135,
                Height = 30,
                Location = new Point(95, 1),
                BackColor = _theme.ContentBg
            };
            _thumbnailSizeTrackBar.ValueChanged +=
                (s, e) =>
                {
                    _thumbnailSizeLabel.Text =
                        "Thumbnail " +
                        (_thumbnailSizeTrackBar.Value == 1
                            ? "Small"
                            : _thumbnailSizeTrackBar.Value == 2
                                ? "Medium"
                                : "Large");
                    RefreshImageBrowser();
                };

            thumbOptions.Controls.Add(_thumbnailSizeTrackBar);
            thumbOptions.Controls.Add(_thumbnailSizeLabel);

            _captionBox = new TextBox
            {
                Multiline = true,
                Dock = DockStyle.Fill,
                BackColor = _theme.ControlBg,
                ForeColor = _theme.TextColor,
                BorderStyle = BorderStyle.FixedSingle,
                Enabled = false
            };

            _captionLabel = CreateLabel(
                "CUSTOM REMARK  •  starts from original filename and is editable",
                7.8f,
                FontStyle.Bold,
                _theme.TextColor);
            _captionLabel.Dock = DockStyle.Top;
            _captionLabel.Height = 22;

            _selectedLabel = CreateLabel(
                "No image selected",
                7.7f,
                FontStyle.Regular,
                _theme.MutedTextColor);
            _selectedLabel.Dock = DockStyle.Top;
            _selectedLabel.Height = 46;
            _selectedLabel.AutoEllipsis = true;

            _captionApplyButton = CreateButton(
                "Apply Remark",
                112,
                _theme.ActiveColor,
                true);
            _captionApplyButton.Dock = DockStyle.Bottom;
            _captionApplyButton.Height = 30;
            _captionApplyButton.Enabled = false;
            _captionApplyButton.Click += (s, e) => ApplyCaption();

            Panel selectedPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 175,
                BackColor = _theme.ControlBg,
                Padding = new Padding(8)
            };
            selectedPanel.Controls.Add(_captionBox);
            selectedPanel.Controls.Add(_captionLabel);
            selectedPanel.Controls.Add(_selectedLabel);
            selectedPanel.Controls.Add(_captionApplyButton);

            FlowLayoutPanel browser = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(2),
                BackColor = _theme.BgLight
            };

            browser.AllowDrop = true;
            browser.DragEnter +=
                (s, e) =>
                {
                    if (e.Data.GetDataPresent(ImagePathFormat))
                        e.Effect = DragDropEffects.Move;
                    else
                        e.Effect = DragDropEffects.None;
                };
            browser.DragDrop +=
                (s, e) =>
                {
                    string path =
                        e.Data.GetData(ImagePathFormat) as string;
                    if (string.IsNullOrWhiteSpace(path))
                        return;

                    SelectImage(path);
                };

            _imageBrowser = browser;

            left.Controls.Add(browser);
            left.Controls.Add(selectedPanel);
            left.Controls.Add(thumbOptions);
            left.Controls.Add(_imageStatusCombo);
            left.Controls.Add(_searchBox);
            left.Controls.Add(_imageCountLabel);
            left.Controls.Add(heading);

            return left;
        }

        private Panel CreateEditorPanel()
        {
            Panel editor = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = _theme.BgLight
            };

            Panel toolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 52,
                BackColor = _theme.ContentBg,
                Padding = new Padding(8, 8, 8, 7)
            };

            Button fit = CreateButton(
                "Fit Workspace",
                105,
                _theme.ControlBg,
                false);
            fit.Location = new Point(8, 8);
            fit.Click += (s, e) => _canvas.FitPage();

            Button zoomOut = CreateButton(
                "−",
                38,
                _theme.ControlBg,
                false);
            zoomOut.Location = new Point(121, 8);
            zoomOut.Click +=
                (s, e) => _canvas.ZoomAt(
                    new Point(
                        _canvas.ClientSize.Width / 2,
                        _canvas.ClientSize.Height / 2),
                    0.82f);

            Button zoomIn = CreateButton(
                "+",
                38,
                _theme.ControlBg,
                false);
            zoomIn.Location = new Point(163, 8);
            zoomIn.Click +=
                (s, e) => _canvas.ZoomAt(
                    new Point(
                        _canvas.ClientSize.Width / 2,
                        _canvas.ClientSize.Height / 2),
                    1.22f);

            _undoButton = CreateButton(
                "↶ Undo",
                76,
                _theme.ControlBg,
                false);
            _undoButton.Location = new Point(205, 8);
            _undoButton.Click += (s, e) => Undo();

            _redoButton = CreateButton(
                "↷ Redo",
                76,
                _theme.ControlBg,
                false);
            _redoButton.Location = new Point(287, 8);
            _redoButton.Click += (s, e) => Redo();

            Label displayLabel = CreateLabel(
                "Text:",
                8f,
                FontStyle.Bold,
                _theme.MutedTextColor);
            displayLabel.AutoSize = false;
            displayLabel.Location = new Point(375, 8);
            displayLabel.Width = 34;
            displayLabel.Height = 28;
            displayLabel.TextAlign = ContentAlignment.MiddleRight;

            Label instruction = CreateLabel(
                "Continuous PDF design space  •  Wheel = zoom  •  middle mouse / Space+drag = pan  •  drag images across pages",
                8f,
                FontStyle.Regular,
                _theme.MutedTextColor);
            instruction.AutoSize = false;
            instruction.Location = new Point(625, 7);
            instruction.Height = 34;
            instruction.Width = 650;
            instruction.TextAlign = ContentAlignment.MiddleLeft;

            _statusLabel = CreateLabel(
                "",
                8f,
                FontStyle.Bold,
                _theme.MutedTextColor);
            _statusLabel.Dock = DockStyle.Right;
            _statusLabel.Width = 230;
            _statusLabel.TextAlign = ContentAlignment.MiddleRight;

            _displayModeCombo = new ComboBox
            {
                Width = 205,
                Height = 28,
                Location = new Point(414, 8),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = _theme.ControlBg,
                ForeColor = _theme.TextColor,
                FlatStyle = FlatStyle.Flat
            };
            _displayModeCombo.Items.AddRange(
                new object[]
                {
                    "Filename only",
                    "Filename + Custom Remark",
                    "Custom Remark only"
                });
            _displayModeCombo.SelectedIndex = 1;
            _displayModeCombo.SelectedIndexChanged +=
                (sender, args) =>
                {
                    string selected =
                        _displayModeCombo.SelectedItem == null
                            ? "Filename + Custom Remark"
                            : _displayModeCombo.SelectedItem.ToString();

                    ResultDisplayMode =
                        selected == "Filename only"
                            ? "Filename"
                            : selected == "Custom Remark only"
                                ? "Remark"
                                : "Both";

                    if (_canvas != null)
                        _canvas.SetDisplayMode(ResultDisplayMode);
                };

            toolbar.Controls.Add(fit);
            toolbar.Controls.Add(zoomOut);
            toolbar.Controls.Add(zoomIn);
            toolbar.Controls.Add(displayLabel);
            toolbar.Controls.Add(_displayModeCombo);
            toolbar.Controls.Add(_undoButton);
            toolbar.Controls.Add(_redoButton);
            toolbar.Controls.Add(instruction);
            toolbar.Controls.Add(_statusLabel);

            _canvas = new CollageDesignCanvas(
                _theme,
                _fontFamily,
                _gridSize,
                _landscape,
                _slots,
                _unplaced,
                _pasteboardPositions,
                _captions,
                _customNames,
                _originalOrder,
                GetThumbnail,
                _totalPages);

            _canvas.Dock = DockStyle.Fill;
            _canvas.ImageSelected +=
                path => SelectImage(path);
            _canvas.LayoutChanged +=
                (s, e) => RefreshAll();
            _canvas.SnapshotRequested +=
                (s, e) => PushUndoSnapshot();

            editor.Controls.Add(_canvas);
            editor.Controls.Add(toolbar);

            return editor;
        }

        private Panel CreateFooter()
        {
            Panel footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 62,
                BackColor = _theme.ContentBg,
                Padding = new Padding(12, 10, 12, 10)
            };

            Button cancel = CreateButton(
                "Cancel",
                105,
                _theme.ControlBg,
                false);
            cancel.Dock = DockStyle.Right;
            cancel.Click +=
                (s, e) =>
                {
                    DialogResult = DialogResult.Cancel;
                    Close();
                };

            Button use = CreateButton(
                "✓  Use This Arrangement",
                195,
                _theme.ActiveColor,
                true);
            use.Dock = DockStyle.Right;
            use.Margin = new Padding(0, 0, 8, 0);
            use.Click +=
                (s, e) =>
                {
                    ApplyCaption();

                    ResultSlots = new List<string>(_slots);
                    ResultCaptions =
                        new Dictionary<string, string>(
                            _captions,
                            StringComparer.OrdinalIgnoreCase);

                    ResultCustomNames =
                        new Dictionary<string, string>(
                            _customNames,
                            StringComparer.OrdinalIgnoreCase);

                    string selectedDisplay =
                        _displayModeCombo == null ||
                        _displayModeCombo.SelectedItem == null
                            ? "Filename + Custom Remark"
                            : _displayModeCombo.SelectedItem.ToString();

                    ResultDisplayMode =
                        selectedDisplay == "Filename only"
                            ? "Filename"
                            : selectedDisplay == "Custom Remark only"
                                ? "Remark"
                                : "Both";

                    DialogResult = DialogResult.OK;
                    Close();
                };

            Label help = CreateLabel(
                "White page is the PDF. Outside the page is design space only. " +
                "Images left outside the page remain unplaced and will be excluded from the PDF.",
                8.5f,
                FontStyle.Regular,
                _theme.MutedTextColor);
            help.Dock = DockStyle.Left;
            help.Width = 850;
            help.TextAlign = ContentAlignment.MiddleLeft;

            footer.Controls.Add(cancel);
            footer.Controls.Add(use);
            footer.Controls.Add(help);

            return footer;
        }

        private void SetCurrentPage(int pageIndex)
        {
            if (_totalPages <= 0)
                return;

            _currentPage = Math.Max(0, Math.Min(_totalPages - 1, pageIndex));

            if (_pageCombo != null && _pageCombo.SelectedIndex != _currentPage)
                _pageCombo.SelectedIndex = _currentPage;

            if (_canvas != null)
            {
                _canvas.FitPage();
            }

            UpdateStatus();
        }

        private void NavigatePage(int delta)
        {
            SetCurrentPage(_currentPage + delta);
        }

        private void CollageArrangementForm_KeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.Z)
            {
                Undo();
                e.SuppressKeyPress = true;
            }
            else if (e.Control && e.KeyCode == Keys.Y)
            {
                Redo();
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.F)
            {
                _canvas.FitPage();
                e.SuppressKeyPress = true;
            }
        }

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

        private Button CreateButton(
            string text,
            int width,
            Color backColor,
            bool primary)
        {
            Button button = new Button
            {
                Text = text,
                Width = width,
                Height = 34,
                BackColor = backColor,
                ForeColor =
                    primary
                        ? ThemeManager.GetContrastTextColor(backColor)
                        : _theme.TextColor,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font(
                    _fontFamily,
                    8.5f,
                    primary ? FontStyle.Bold : FontStyle.Regular)
            };

            button.FlatAppearance.BorderColor =
                primary
                    ? backColor
                    : _theme.BorderColor;
            button.FlatAppearance.MouseOverBackColor =
                _theme.ButtonHover;

            return button;
        }

        private void RefreshAll()
        {
            _canvas.CurrentPage = _currentPage;
            _canvas.Invalidate();
            RefreshImageBrowser();
            UpdateSelectedLabel();
            UpdateStatus();
            UpdateUndoButtons();
        }

        private void RefreshImageBrowser()
        {
            if (_imageBrowser == null)
                return;

            string filter =
                _searchBox == null
                    ? string.Empty
                    : _searchBox.Text.Trim();

            string status =
                _imageStatusCombo == null ||
                _imageStatusCombo.SelectedItem == null
                    ? "Unplaced"
                    : _imageStatusCombo.SelectedItem.ToString();

            int thumb =
                _thumbnailSizeTrackBar == null
                    ? 3
                    : _thumbnailSizeTrackBar.Value;

            int width =
                thumb == 1
                    ? 96
                    : thumb == 2
                        ? 128
                        : 168;

            int height =
                thumb == 1
                    ? 105
                    : thumb == 2
                        ? 135
                        : 165;

            _imageBrowser.SuspendLayout();
            try
            {
                _imageBrowser.Controls.Clear();

                IEnumerable<string> query = _originalOrder;

                if (string.Equals(status, "Unplaced", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(p => _unplaced.Contains(p));
                else if (string.Equals(status, "Placed", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(p => !_unplaced.Contains(p));

                if (!string.IsNullOrWhiteSpace(filter))
                {
                    query = query.Where(
                        p =>
                            Path.GetFileName(p).IndexOf(
                                filter,
                                StringComparison.OrdinalIgnoreCase) >= 0 ||
                            (_captions.ContainsKey(p) &&
                             _captions[p].IndexOf(
                                 filter,
                                 StringComparison.OrdinalIgnoreCase) >= 0));
                }

                List<string> paths = query.ToList();

                _imageCountLabel.Text =
                    paths.Count + " shown  •  " +
                    (_originalOrder.Count - _unplaced.Count) +
                    " placed  •  " +
                    _unplaced.Count + " unplaced";

                foreach (string path in paths)
                    _imageBrowser.Controls.Add(
                        CreateBrowserCard(path, width, height));
            }
            finally
            {
                _imageBrowser.ResumeLayout();
            }
        }

        private Panel CreateBrowserCard(
            string path,
            int width,
            int height)
        {
            Panel card = new Panel
            {
                Width = width,
                Height = height,
                BackColor = _theme.ControlBg,
                Margin = new Padding(4),
                Cursor = Cursors.Hand,
                Tag = path
            };

            PictureBox picture = new PictureBox
            {
                Location = new Point(5, 5),
                Size = new Size(width - 10, height - 48),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = _theme.BgLight,
                Image = GetThumbnail(path, width - 10, height - 48),
                Tag = path
            };

            Label name = CreateLabel(
                Path.GetFileNameWithoutExtension(path),
                7f,
                FontStyle.Regular,
                _theme.TextColor);
            name.Location = new Point(5, height - 42);
            name.Size = new Size(width - 10, 18);
            name.TextAlign = ContentAlignment.MiddleCenter;
            name.Tag = path;

            string stateText =
                _unplaced.Contains(path)
                    ? "UNPLACED"
                    : "PLACED";
            Label state = CreateLabel(
                stateText,
                6.8f,
                FontStyle.Bold,
                _unplaced.Contains(path)
                    ? _theme.MutedTextColor
                    : _theme.ActiveColor);
            state.Location = new Point(5, height - 24);
            state.Size = new Size(width - 10, 18);
            state.TextAlign = ContentAlignment.MiddleCenter;
            state.Tag = path;

            card.Controls.Add(picture);
            card.Controls.Add(name);
            card.Controls.Add(state);

            EventHandler click =
                (s, e) => SelectImage(path);

            MouseEventHandler down =
                (s, e) =>
                {
                    if (e.Button != MouseButtons.Left)
                        return;

                    SelectImage(path);

                    DataObject data =
                        new DataObject(
                            ImagePathFormat,
                            path);

                    DoDragDrop(
                        data,
                        DragDropEffects.Move);
                };

            card.Click += click;
            picture.Click += click;
            name.Click += click;
            state.Click += click;

            card.MouseDown += down;
            picture.MouseDown += down;
            name.MouseDown += down;
            state.MouseDown += down;

            return card;
        }

        private void SelectImage(string path)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                !_originalOrder.Any(
                    p => string.Equals(
                        p,
                        path,
                        StringComparison.OrdinalIgnoreCase)))
                return;

            if (_captionBox != null &&
                _captionBox.Modified &&
                !string.Equals(
                    _selectedImagePath,
                    path,
                    StringComparison.OrdinalIgnoreCase))
            {
                ApplyCaption();
            }

            _selectedImagePath = path;
            UpdateSelectedLabel();
            _canvas.SelectImage(path);
        }

        private void UpdateSelectedLabel()
        {
            if (_selectedLabel == null)
                return;

            if (string.IsNullOrWhiteSpace(_selectedImagePath))
            {
                _selectedLabel.Text = "No image selected";
                if (_captionBox != null)
                {
                    _captionBox.Text = string.Empty;
                    _captionBox.Enabled = false;
                }
                if (_captionApplyButton != null)
                    _captionApplyButton.Enabled = false;
                return;
            }

            bool placed = !_unplaced.Contains(_selectedImagePath);

            int slot =
                _slots.FindIndex(
                    p => string.Equals(
                        p,
                        _selectedImagePath,
                        StringComparison.OrdinalIgnoreCase));

            string location =
                placed
                    ? "Placed • Page " +
                      ((slot / (_gridSize * _gridSize)) + 1) +
                      " • Cell " +
                      ((slot % (_gridSize * _gridSize)) + 1)
                    : "Unplaced • Pasteboard";

            _selectedLabel.Text =
                Path.GetFileName(_selectedImagePath) +
                Environment.NewLine +
                location;

            string caption;
            _captions.TryGetValue(
                _selectedImagePath,
                out caption);

            _captionBox.Text = caption ?? string.Empty;
            _captionBox.Enabled = true;
            _captionApplyButton.Enabled = true;
        }

        private void ApplyCaption()
        {
            if (string.IsNullOrWhiteSpace(_selectedImagePath) ||
                _captionBox == null)
                return;

            string caption = _captionBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(caption))
            {
                _captions.Remove(_selectedImagePath);
                _customNames.Remove(_selectedImagePath);
            }
            else
            {
                _captions[_selectedImagePath] = caption;
                // Keep the legacy custom-name result synchronized so older
                // saved arrangements retain the user's custom display text.
                _customNames[_selectedImagePath] = caption;
            }

            _captionBox.Modified = false;
            _canvas.Invalidate();
            RefreshImageBrowser();
            UpdateSelectedLabel();
        }

        private void UpdateStatus()
        {
            if (_statusLabel == null)
                return;

            int placed =
                _slots.Count(
                    p => !string.IsNullOrWhiteSpace(p));

            _pageLabelSafe();

            _statusLabel.Text =
                placed + " placed  •  " +
                _unplaced.Count + " unplaced  •  " +
                "Zoom " +
                Math.Round(_canvas == null ? 1.0 : _canvas.Zoom * 100.0) +
                "%";
        }

        private void _pageLabelSafe()
        {
            // Kept intentionally small; the page selector is the source of truth.
        }

        private void RebuildUnplacedSet()
        {
            _unplaced.Clear();

            foreach (string path in _originalOrder)
            {
                bool placed =
                    _slots.Any(
                        p => string.Equals(
                            p,
                            path,
                            StringComparison.OrdinalIgnoreCase));

                if (!placed)
                    _unplaced.Add(path);
            }
        }

        private void InitializePasteboardPositions()
        {
            float pageWidth = _landscape ? 1122f : 794f;
            float pageHeight = _landscape ? 794f : 1122f;

            int index = 0;

            foreach (string path in _unplaced)
            {
                if (_pasteboardPositions.ContainsKey(path))
                    continue;

                int column = index % 3;
                int row = index / 3;

                _pasteboardPositions[path] =
                    new PointF(
                        pageWidth + 90f + column * 205f,
                        50f + row * 175f);

                index++;
            }
        }

        private Image GetThumbnail(
            string path,
            int maxWidth,
            int maxHeight)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                !File.Exists(path))
                return null;

            Image cached;
            if (_thumbnailCache.TryGetValue(path, out cached))
                return cached;

            try
            {
                using (Image source = Image.FromFile(path))
                {
                    Bitmap bmp =
                        new Bitmap(
                            Math.Max(1, maxWidth),
                            Math.Max(1, maxHeight),
                            PixelFormat.Format32bppArgb);

                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.Clear(_theme.BgLight);
                        g.InterpolationMode =
                            InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode =
                            PixelOffsetMode.HighQuality;
                        g.SmoothingMode =
                            SmoothingMode.HighQuality;

                        Rectangle target =
                            FitRectangle(
                                source.Width,
                                source.Height,
                                new Rectangle(
                                    4,
                                    4,
                                    bmp.Width - 8,
                                    bmp.Height - 8));

                        g.DrawImage(
                            source,
                            target);
                    }

                    _thumbnailCache[path] = bmp;
                    return bmp;
                }
            }
            catch
            {
                return null;
            }
        }

        private static Rectangle FitRectangle(
            int sourceWidth,
            int sourceHeight,
            Rectangle bounds)
        {
            if (sourceWidth <= 0 || sourceHeight <= 0)
                return bounds;

            float scale =
                Math.Min(
                    bounds.Width / (float)sourceWidth,
                    bounds.Height / (float)sourceHeight);

            int width =
                Math.Max(
                    1,
                    (int)Math.Round(sourceWidth * scale));

            int height =
                Math.Max(
                    1,
                    (int)Math.Round(sourceHeight * scale));

            return new Rectangle(
                bounds.X + (bounds.Width - width) / 2,
                bounds.Y + (bounds.Height - height) / 2,
                width,
                height);
        }

        private ArrangementSnapshot CaptureSnapshot()
        {
            return new ArrangementSnapshot
            {
                Slots = new List<string>(_slots),
                Positions =
                    new Dictionary<string, PointF>(
                        _pasteboardPositions,
                        StringComparer.OrdinalIgnoreCase)
            };
        }

        private void PushUndoSnapshot()
        {
            _undo.Push(CaptureSnapshot());
            _redo.Clear();
            UpdateUndoButtons();
        }

        private void RestoreSnapshot(ArrangementSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            _slots.Clear();
            _slots.AddRange(snapshot.Slots);

            _pasteboardPositions.Clear();
            foreach (KeyValuePair<string, PointF> pair in snapshot.Positions)
                _pasteboardPositions[pair.Key] = pair.Value;

            RebuildUnplacedSet();
            InitializePasteboardPositions();
            RefreshAll();
        }

        private void Undo()
        {
            if (_undo.Count == 0)
                return;

            _redo.Push(CaptureSnapshot());
            RestoreSnapshot(_undo.Pop());
        }

        private void Redo()
        {
            if (_redo.Count == 0)
                return;

            _undo.Push(CaptureSnapshot());
            RestoreSnapshot(_redo.Pop());
        }

        private void UpdateUndoButtons()
        {
            if (_undoButton != null)
                _undoButton.Enabled = _undo.Count > 0;

            if (_redoButton != null)
                _redoButton.Enabled = _redo.Count > 0;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (Image image in _thumbnailCache.Values)
                {
                    try { image.Dispose(); }
                    catch { }
                }

                _thumbnailCache.Clear();
            }

            base.Dispose(disposing);
        }

        private sealed class ArrangementSnapshot
        {
            public List<string> Slots;
            public Dictionary<string, PointF> Positions;
        }

        /// <summary>
        /// Lightweight custom-rendered infinite workspace. It intentionally
        /// does not create a WinForms control for every image/cell, which keeps
        /// zooming and panning responsive with large image sets.
        /// </summary>
        private sealed class CollageDesignCanvas : Control
        {
            private readonly ThemeColors _theme;
            private readonly string _fontFamily;
            private readonly int _gridSize;
            private readonly bool _landscape;
            private readonly List<string> _slots;
            private readonly HashSet<string> _unplaced;
            private readonly Dictionary<string, PointF> _positions;
            private readonly Dictionary<string, string> _captions;
            private readonly Dictionary<string, string> _customNames;
            private readonly List<string> _allImages;
            private readonly Func<string, int, int, Image> _thumbnailProvider;
            private readonly int _totalPages;
            private const float PageGap = 0f;

            private float _zoom = 0.72f;
            private string _displayMode = "Both";
            private PointF _viewCenter;
            private bool _viewInitialized;

            private bool _panning;
            private Point _panStartScreen;
            private PointF _panStartCenter;
            private bool _spaceDown;

            private bool _dragging;
            private bool _dragMoved;
            private string _dragPath;
            private int _dragSourceSlot = -1;
            private PointF _dragOffset;
            private PointF _dragCurrentWorld;

            private string _selectedPath;
            private TextBox _inlineCaptionEditor;
            private string _inlineCaptionPath;
            private TextBox _inlineCustomNameEditor;
            private string _inlineCustomNamePath;

            public int CurrentPage { get; set; }

            public float Zoom
            {
                get { return _zoom; }
            }

            public event Action<string> ImageSelected;
            public event EventHandler LayoutChanged;
            public event EventHandler SnapshotRequested;

            public CollageDesignCanvas(
                ThemeColors theme,
                string fontFamily,
                int gridSize,
                bool landscape,
                List<string> slots,
                HashSet<string> unplaced,
                Dictionary<string, PointF> positions,
                Dictionary<string, string> captions,
                Dictionary<string, string> customNames,
                List<string> allImages,
                Func<string, int, int, Image> thumbnailProvider,
                int totalPages)
            {
                _theme = theme;
                _fontFamily = fontFamily;
                _gridSize = gridSize;
                _landscape = landscape;
                _slots = slots;
                _unplaced = unplaced;
                _positions = positions;
                _captions = captions;
                _customNames = customNames;
                _allImages = allImages;
                _thumbnailProvider = thumbnailProvider;
                _totalPages = Math.Max(1, totalPages);

                DoubleBuffered = true;
                ResizeRedraw = true;
                BackColor = theme.BgLight;
                TabStop = true;
                AllowDrop = true;

                MouseWheel += Canvas_MouseWheel;
                MouseDown += Canvas_MouseDown;
                MouseMove += Canvas_MouseMove;
                MouseUp += Canvas_MouseUp;
                DragEnter += Canvas_DragEnter;
                DragDrop += Canvas_DragDrop;
            }

            protected override void OnKeyDown(KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Space)
                {
                    _spaceDown = true;
                    e.SuppressKeyPress = true;
                }

                base.OnKeyDown(e);
            }

            protected override void OnKeyUp(KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Space)
                    _spaceDown = false;

                base.OnKeyUp(e);
            }

            public void SelectImage(string path)
            {
                _selectedPath = path;
                Invalidate();
            }

            public void SetDisplayMode(string mode)
            {
                _displayMode = string.IsNullOrWhiteSpace(mode)
                    ? "Both"
                    : mode;
                Invalidate();
            }

            public void FitPage()
            {
                Rectangle client = ClientRectangle;
                if (client.Width <= 0 || client.Height <= 0)
                    return;

                SizeF page = PageSizeWorld();

                float zx =
                    (client.Width - 120f) / page.Width;
                float zy =
                    (client.Height - 120f) / page.Height;

                _zoom =
                    Math.Max(
                        0.25f,
                        Math.Min(
                            1.35f,
                            Math.Min(zx, zy)));

                RectangleF firstPage = PageRectWorld(0);
                _viewCenter =
                    new PointF(
                        firstPage.Left + firstPage.Width / 2f,
                        firstPage.Top + firstPage.Height / 2f);

                _viewInitialized = true;
                Invalidate();
            }

            public void ZoomAt(Point screenPoint, float factor)
            {
                EnsureView();

                PointF before =
                    ScreenToWorld(screenPoint);

                _zoom =
                    Math.Max(
                        0.20f,
                        Math.Min(
                            3.50f,
                            _zoom * factor));

                PointF after =
                    ScreenToWorld(screenPoint);

                _viewCenter =
                    new PointF(
                        _viewCenter.X + before.X - after.X,
                        _viewCenter.Y + before.Y - after.Y);

                Invalidate();
            }

            private void EnsureView()
            {
                if (!_viewInitialized)
                    FitPage();
            }

            private SizeF PageSizeWorld()
            {
                return _landscape
                    ? new SizeF(1122f, 794f)
                    : new SizeF(794f, 1122f);
            }

            private RectangleF PageRectWorld(int pageIndex)
            {
                SizeF size = PageSizeWorld();
                float top = pageIndex * (size.Height + PageGap);
                return new RectangleF(
                    0f,
                    top,
                    size.Width,
                    size.Height);
            }

            private RectangleF PageRectWorld()
            {
                return PageRectWorld(0);
            }

            private PointF WorldToScreen(PointF world)
            {
                EnsureView();

                return new PointF(
                    ClientSize.Width / 2f +
                    (world.X - _viewCenter.X) * _zoom,
                    ClientSize.Height / 2f +
                    (world.Y - _viewCenter.Y) * _zoom);
            }

            private PointF ScreenToWorld(Point screen)
            {
                EnsureView();

                return new PointF(
                    _viewCenter.X +
                    (screen.X - ClientSize.Width / 2f) / _zoom,
                    _viewCenter.Y +
                    (screen.Y - ClientSize.Height / 2f) / _zoom);
            }

            private RectangleF WorldToScreen(RectangleF rect)
            {
                PointF a = WorldToScreen(
                    new PointF(rect.Left, rect.Top));
                PointF b = WorldToScreen(
                    new PointF(rect.Right, rect.Bottom));

                return RectangleF.FromLTRB(
                    a.X,
                    a.Y,
                    b.X,
                    b.Y);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                EnsureView();

                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                DrawPasteboard(g);
                DrawPage(g);
                DrawPasteboardImages(g);
                DrawDraggedImage(g);
                DrawSelectionHint(g);
            }

            private void DrawPasteboard(Graphics g)
            {
                g.Clear(_theme.BgLight);

                RectangleF pageScreen =
                    WorldToScreen(PageRectWorld());

                Rectangle visible =
                    ClientRectangle;

                using (Brush brush =
                    new SolidBrush(_theme.ContentBg))
                {
                    // A subtle design-space field.
                    g.FillRectangle(brush, visible);
                }

                using (Pen pen =
                    new Pen(
                        _theme.BorderColor,
                        1f))
                {
                    float step = 60f * _zoom;
                    if (step >= 22f)
                    {
                        PointF origin =
                            WorldToScreen(
                                new PointF(
                                    0f,
                                    0f));

                        for (float x = origin.X % step;
                             x < ClientSize.Width;
                             x += step)
                        {
                            g.DrawLine(
                                pen,
                                x,
                                0,
                                x,
                                ClientSize.Height);
                        }

                        for (float y = origin.Y % step;
                             y < ClientSize.Height;
                             y += step)
                        {
                            g.DrawLine(
                                pen,
                                0,
                                y,
                                ClientSize.Width,
                                y);
                        }
                    }
                }

                using (Brush brush =
                    new SolidBrush(
                        Color.FromArgb(
                            95,
                            _theme.MutedTextColor)))
                using (Font font =
                    new Font(
                        _fontFamily,
                        8f,
                        FontStyle.Regular))
                {
                    g.DrawString(
                        "DESIGN SPACE  •  OUTSIDE PAGE = NOT INCLUDED IN PDF",
                        font,
                        brush,
                        new PointF(14f, 12f));
                }
            }

            private void DrawPage(Graphics g)
            {
                int cells = _gridSize * _gridSize;
                SizeF pageSize = PageSizeWorld();

                for (int pageIndex = 0; pageIndex < _totalPages; pageIndex++)
                {
                    RectangleF pageWorld = PageRectWorld(pageIndex);
                    RectangleF pageScreen = WorldToScreen(pageWorld);

                    // Keep the A4 reference visible without introducing a
                    // navigational page break. Pages remain one continuous
                    // vertical design surface.
                    RectangleF shadowRect =
                        new RectangleF(
                            pageScreen.X + 5f,
                            pageScreen.Y + 5f,
                            pageScreen.Width,
                            pageScreen.Height);

                    using (Brush shadow =
                        new SolidBrush(Color.FromArgb(45, 0, 0, 0)))
                    {
                        g.FillRectangle(shadow, shadowRect);
                    }

                    using (Brush paper = new SolidBrush(Color.White))
                        g.FillRectangle(paper, pageScreen);

                    using (Pen pageOutline =
                        new Pen(_theme.BorderColor, Math.Max(1.2f, 1.5f * _zoom)))
                    {
                        g.DrawRectangle(
                            pageOutline,
                            pageScreen.X,
                            pageScreen.Y,
                            pageScreen.Width,
                            pageScreen.Height);
                    }

                    // Reference-only marker: keep it inside the white
                    // A4 page at its top-left corner. The pages remain a
                    // continuous vertical workspace with no added page gap.
                    float labelWidth = Math.Min(
                        128f,
                        Math.Max(96f, pageScreen.Width * 0.30f));
                    float labelHeight = Math.Max(24f, 28f * _zoom);
                    RectangleF pageLabel =
                        new RectangleF(
                            pageScreen.X + 8f,
                            pageScreen.Y + 8f,
                            labelWidth,
                            labelHeight);

                    using (Brush labelBrush =
                        new SolidBrush(Color.FromArgb(225, 42, 48, 54)))
                    using (Font labelFont =
                        new Font(
                            _fontFamily,
                            Math.Max(9f, 10f * _zoom),
                            FontStyle.Bold))
                    using (StringFormat labelFormat = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    })
                    {
                        g.FillRectangle(labelBrush, pageLabel);
                        using (Brush labelText =
                            new SolidBrush(Color.White))
                        {
                            g.DrawString(
                                "PDF PAGE " + (pageIndex + 1),
                                labelFont,
                                labelText,
                                pageLabel,
                                labelFormat);
                        }
                    }

                    float cellWidth = pageSize.Width / _gridSize;
                    float cellHeight = pageSize.Height / _gridSize;

                    for (int cell = 0; cell < cells; cell++)
                    {
                        int globalSlot = pageIndex * cells + cell;

                        RectangleF cellWorld =
                            new RectangleF(
                                pageWorld.X + (cell % _gridSize) * cellWidth,
                                pageWorld.Y + (cell / _gridSize) * cellHeight,
                                cellWidth,
                                cellHeight);

                        RectangleF cellScreen = WorldToScreen(cellWorld);

                        string path =
                            globalSlot < _slots.Count
                                ? _slots[globalSlot]
                                : null;

                        using (Pen cellPen = new Pen(
                            _theme.BorderColor,
                            Math.Max(1f, _zoom)))
                        {
                            g.DrawRectangle(
                                cellPen,
                                cellScreen.X,
                                cellScreen.Y,
                                cellScreen.Width,
                                cellScreen.Height);
                        }

                        if (string.IsNullOrWhiteSpace(path))
                        {
                            using (Brush emptyBrush =
                                new SolidBrush(Color.FromArgb(245, 248, 250)))
                            {
                                g.FillRectangle(emptyBrush, cellScreen);
                            }

                            using (Font font =
                                new Font(
                                    _fontFamily,
                                    Math.Max(7f, 9f * _zoom),
                                    FontStyle.Italic))
                            using (Brush brush =
                                new SolidBrush(Color.FromArgb(150, 120, 120, 120)))
                            using (StringFormat format = new StringFormat
                            {
                                Alignment = StringAlignment.Center,
                                LineAlignment = StringAlignment.Center
                            })
                            {
                                g.DrawString(
                                    "EMPTY CELL",
                                    font,
                                    brush,
                                    cellScreen,
                                    format);
                            }
                        }
                        else
                        {
                            DrawCellImage(
                                g,
                                globalSlot,
                                path,
                                cellScreen);
                        }
                    }
                }
            }

            private void DrawCellImage(
                Graphics g,
                int slot,
                string path,
                RectangleF cellScreen)
            {
                int padding =
                    Math.Max(
                        4,
                        (int)Math.Round(10f * _zoom));

                float textHeight = Math.Max(82f, 92f * _zoom);
                RectangleF imageArea =
                    new RectangleF(
                        cellScreen.X + padding,
                        cellScreen.Y + padding,
                        Math.Max(1, cellScreen.Width - padding * 2),
                        Math.Max(1, cellScreen.Height - padding * 2 - textHeight));

                Image image = _thumbnailProvider(path, 620, 480);

                if (image != null)
                {
                    using (Brush white = new SolidBrush(Color.White))
                    {
                        g.FillRectangle(white, imageArea);
                    }

                    Rectangle target = FitRectangle(
                        image.Width,
                        image.Height,
                        Rectangle.Round(imageArea));

                    g.DrawImage(
                        image,
                        target,
                        new Rectangle(0, 0, image.Width, image.Height),
                        GraphicsUnit.Pixel);
                }

                string filename = Path.GetFileNameWithoutExtension(path);
                string customRemark;
                _captions.TryGetValue(path, out customRemark);
                customRemark =
                    string.IsNullOrWhiteSpace(customRemark)
                        ? filename
                        : customRemark;

                RectangleF filenameRect = GetFilenameRect(cellScreen);
                RectangleF remarkRect = GetRemarkRect(cellScreen);

                bool showFilename =
                    string.Equals(_displayMode, "Filename", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(_displayMode, "Both", StringComparison.OrdinalIgnoreCase);
                bool showRemark =
                    string.Equals(_displayMode, "Remark", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(_displayMode, "Both", StringComparison.OrdinalIgnoreCase);

                if (showFilename)
                    DrawCanvasText(g, filename, filenameRect, false);

                if (showRemark)
                    DrawCanvasText(g, customRemark, remarkRect, false);

                if (string.Equals(
                    _selectedPath,
                    path,
                    StringComparison.OrdinalIgnoreCase))
                {
                    using (Pen selectedPen = new Pen(
                        _theme.ActiveColor,
                        Math.Max(2f, 3f * _zoom)))
                    {
                        g.DrawRectangle(
                            selectedPen,
                            cellScreen.X + 2,
                            cellScreen.Y + 2,
                            Math.Max(1, cellScreen.Width - 4),
                            Math.Max(1, cellScreen.Height - 4));
                    }
                }
            }

            private void DrawCanvasText(
                Graphics g,
                string value,
                RectangleF rect,
                bool placeholder)
            {
                if (string.IsNullOrWhiteSpace(value))
                    return;

                RectangleF backgroundRect = rect;
                using (Brush background = new SolidBrush(
                    placeholder
                        ? Color.FromArgb(235, 238, 241)
                        : Color.FromArgb(246, 248, 250)))
                using (Pen border = new Pen(
                    Color.FromArgb(190, 198, 205),
                    Math.Max(1f, _zoom)))
                {
                    g.FillRectangle(background, backgroundRect);
                    g.DrawRectangle(
                        border,
                        backgroundRect.X,
                        backgroundRect.Y,
                        Math.Max(1f, backgroundRect.Width - 1f),
                        Math.Max(1f, backgroundRect.Height - 1f));
                }

                using (Brush brush = new SolidBrush(
                    placeholder
                        ? Color.FromArgb(105, 112, 120)
                        : Color.FromArgb(35, 40, 45)))
                using (Font font = new Font(
                    _fontFamily,
                    Math.Max(9f, 10f * _zoom),
                    placeholder ? FontStyle.Italic : FontStyle.Regular))
                using (StringFormat format = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisWord,
                    FormatFlags = StringFormatFlags.LineLimit
                })
                {
                    g.DrawString(value, font, brush, rect, format);
                }
            }

            private RectangleF GetFilenameRect(RectangleF cellScreen)
            {
                float h = Math.Max(30f, 34f * _zoom);
                float bottomOffset = Math.Max(72f, 76f * _zoom);
                return new RectangleF(
                    cellScreen.X + 8,
                    cellScreen.Bottom - bottomOffset,
                    Math.Max(1, cellScreen.Width - 16),
                    h);
            }

            private RectangleF GetRemarkRect(RectangleF cellScreen)
            {
                float h = Math.Max(34f, 42f * _zoom);
                return new RectangleF(
                    cellScreen.X + 8,
                    cellScreen.Bottom - h - Math.Max(5f, 6f * _zoom),
                    Math.Max(1, cellScreen.Width - 16),
                    h);
            }

            private void DrawPasteboardImages(Graphics g)
            {
                foreach (string path in _unplaced.ToList())
                {
                    if (_dragging &&
                        string.Equals(
                            path,
                            _dragPath,
                            StringComparison.OrdinalIgnoreCase))
                        continue;

                    PointF position;
                    if (!_positions.TryGetValue(
                        path,
                        out position))
                        continue;

                    SizeF size =
                        PasteboardImageSize();

                    RectangleF world =
                        new RectangleF(
                            position.X,
                            position.Y,
                            size.Width,
                            size.Height);

                    RectangleF screen =
                        WorldToScreen(world);

                    DrawPasteboardImage(
                        g,
                        path,
                        screen);
                }
            }

            private SizeF PasteboardImageSize()
            {
                return new SizeF(175f, 145f);
            }

            private void DrawPasteboardImage(
                Graphics g,
                string path,
                RectangleF screen)
            {
                using (Brush cardBrush =
                    new SolidBrush(
                        _theme.ControlBg))
                using (Pen cardPen =
                    new Pen(
                        string.Equals(
                            _selectedPath,
                            path,
                            StringComparison.OrdinalIgnoreCase)
                            ? _theme.ActiveColor
                            : _theme.BorderColor,
                        string.Equals(
                            _selectedPath,
                            path,
                            StringComparison.OrdinalIgnoreCase)
                            ? 2f
                            : 1f))
                {
                    g.FillRectangle(cardBrush, screen);
                    g.DrawRectangle(
                        cardPen,
                        screen.X,
                        screen.Y,
                        screen.Width,
                        screen.Height);
                }

                RectangleF imageArea =
                    new RectangleF(
                        screen.X + 7,
                        screen.Y + 7,
                        Math.Max(1, screen.Width - 14),
                        Math.Max(1, screen.Height - 38));

                Image image =
                    _thumbnailProvider(
                        path,
                        400,
                        300);

                if (image != null)
                {
                    Rectangle target =
                        FitRectangle(
                            image.Width,
                            image.Height,
                            Rectangle.Round(imageArea));

                    g.DrawImage(
                        image,
                        target,
                        new Rectangle(
                            0,
                            0,
                            image.Width,
                            image.Height),
                        GraphicsUnit.Pixel);
                }

                using (Brush textBrush =
                    new SolidBrush(_theme.TextColor))
                using (Font font =
                    new Font(
                        _fontFamily,
                        Math.Max(7f, 8f * _zoom),
                        FontStyle.Bold))
                using (StringFormat format =
                    new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center,
                        Trimming = StringTrimming.EllipsisCharacter
                    })
                {
                    g.DrawString(
                        Path.GetFileNameWithoutExtension(path),
                        font,
                        textBrush,
                        new RectangleF(
                            screen.X + 5,
                            screen.Bottom - 29,
                            Math.Max(1, screen.Width - 10),
                            22),
                        format);
                }

                using (Brush badge =
                    new SolidBrush(
                        Color.FromArgb(
                            225,
                            _theme.BgLight)))
                using (Font badgeFont =
                    new Font(
                        _fontFamily,
                        6.8f,
                        FontStyle.Bold))
                {
                    g.FillRectangle(
                        badge,
                        screen.X + 6,
                        screen.Y + 6,
                        67,
                        17);

                    using (Brush badgeText =
                        new SolidBrush(
                            _theme.MutedTextColor))
                    {
                        g.DrawString(
                            "NOT IN PDF",
                            badgeFont,
                            badgeText,
                            screen.X + 10,
                            screen.Y + 9);
                    }
                }
            }

            private void DrawDraggedImage(Graphics g)
            {
                if (!_dragging ||
                    string.IsNullOrWhiteSpace(_dragPath))
                    return;

                SizeF size =
                    PasteboardImageSize();

                RectangleF world =
                    new RectangleF(
                        _dragCurrentWorld.X - _dragOffset.X,
                        _dragCurrentWorld.Y - _dragOffset.Y,
                        size.Width,
                        size.Height);

                RectangleF screen =
                    WorldToScreen(world);

                using (Brush brush =
                    new SolidBrush(
                        Color.FromArgb(
                            230,
                            _theme.ControlBg)))
                using (Pen pen =
                    new Pen(
                        _theme.ActiveColor,
                        2f))
                {
                    g.FillRectangle(brush, screen);
                    g.DrawRectangle(
                        pen,
                        screen.X,
                        screen.Y,
                        screen.Width,
                        screen.Height);
                }

                Image image =
                    _thumbnailProvider(
                        _dragPath,
                        400,
                        300);

                if (image != null)
                {
                    Rectangle target =
                        FitRectangle(
                            image.Width,
                            image.Height,
                            Rectangle.Round(
                                new RectangleF(
                                    screen.X + 7,
                                    screen.Y + 7,
                                    screen.Width - 14,
                                    screen.Height - 38)));

                    g.DrawImage(
                        image,
                        target,
                        new Rectangle(
                            0,
                            0,
                            image.Width,
                            image.Height),
                        GraphicsUnit.Pixel);
                }
            }

            private void DrawSelectionHint(Graphics g)
            {
                if (!_dragging)
                    return;

                PointF world =
                    _dragCurrentWorld;

                int slot =
                    HitTestSlot(world);

                if (slot >= 0)
                {
                    int cells = _gridSize * _gridSize;
                    int pageIndex = slot / cells;
                    int local = slot % cells;
                    SizeF page = PageSizeWorld();
                    RectangleF pageWorld = PageRectWorld(pageIndex);

                    RectangleF cellWorld =
                        new RectangleF(
                            pageWorld.X + (local % _gridSize) *
                            page.Width / _gridSize,
                            pageWorld.Y + (local / _gridSize) *
                            page.Height / _gridSize,
                            page.Width / _gridSize,
                            page.Height / _gridSize);

                    RectangleF screen =
                        WorldToScreen(cellWorld);

                    using (Brush overlay =
                        new SolidBrush(
                            Color.FromArgb(
                                38,
                                _theme.ActiveColor)))
                    using (Pen pen =
                        new Pen(
                            _theme.ActiveColor,
                            2f))
                    {
                        g.FillRectangle(
                            overlay,
                            screen);
                        g.DrawRectangle(
                            pen,
                            screen.X,
                            screen.Y,
                            screen.Width,
                            screen.Height);
                    }
                }
            }

            private string HitTestFilename(Point screen)
            {
                PointF world = ScreenToWorld(screen);
                int cells = _gridSize * _gridSize;
                SizeF pageSize = PageSizeWorld();
                float pageStride = pageSize.Height + PageGap;
                int pageIndex = (int)Math.Floor(world.Y / pageStride);
                if (pageIndex < 0 || pageIndex >= _totalPages)
                    return null;

                RectangleF page = PageRectWorld(pageIndex);
                if (!page.Contains(world))
                    return null;

                float cellWidth = page.Width / _gridSize;
                float cellHeight = page.Height / _gridSize;
                int column = (int)((world.X - page.X) / cellWidth);
                int row = (int)((world.Y - page.Y) / cellHeight);
                if (column < 0 || column >= _gridSize ||
                    row < 0 || row >= _gridSize)
                    return null;

                int globalSlot = pageIndex * cells + row * _gridSize + column;
                if (globalSlot < 0 || globalSlot >= _slots.Count)
                    return null;

                string path = _slots[globalSlot];
                if (string.IsNullOrWhiteSpace(path))
                    return null;

                RectangleF cellWorld = new RectangleF(
                    page.X + column * cellWidth,
                    page.Y + row * cellHeight,
                    cellWidth,
                    cellHeight);
                return GetFilenameRect(WorldToScreen(cellWorld)).Contains(screen)
                    ? path
                    : null;
            }

            private RectangleF GetFilenameScreenRect(string path)
            {
                if (string.IsNullOrWhiteSpace(path))
                    return RectangleF.Empty;

                int cells = _gridSize * _gridSize;
                SizeF pageSize = PageSizeWorld();
                for (int globalSlot = 0; globalSlot < _slots.Count; globalSlot++)
                {
                    if (!string.Equals(_slots[globalSlot], path, StringComparison.OrdinalIgnoreCase))
                        continue;

                    int pageIndex = globalSlot / cells;
                    int local = globalSlot % cells;
                    RectangleF page = PageRectWorld(pageIndex);
                    float cellWidth = page.Width / _gridSize;
                    float cellHeight = page.Height / _gridSize;
                    RectangleF cellWorld = new RectangleF(
                        page.X + (local % _gridSize) * cellWidth,
                        page.Y + (local / _gridSize) * cellHeight,
                        cellWidth,
                        cellHeight);
                    return GetFilenameRect(WorldToScreen(cellWorld));
                }
                return RectangleF.Empty;
            }

            private string HitTestRemark(Point screen)
            {
                PointF world = ScreenToWorld(screen);
                int cells = _gridSize * _gridSize;
                SizeF pageSize = PageSizeWorld();
                float pageStride = pageSize.Height + PageGap;

                int pageIndex = (int)Math.Floor(world.Y / pageStride);
                if (pageIndex < 0 || pageIndex >= _totalPages)
                    return null;

                RectangleF page = PageRectWorld(pageIndex);
                if (!page.Contains(world))
                    return null;

                float cellWidth = page.Width / _gridSize;
                float cellHeight = page.Height / _gridSize;

                int column = (int)((world.X - page.X) / cellWidth);
                int row = (int)((world.Y - page.Y) / cellHeight);
                if (column < 0 || column >= _gridSize ||
                    row < 0 || row >= _gridSize)
                    return null;

                int globalSlot =
                    pageIndex * cells + row * _gridSize + column;

                if (globalSlot < 0 || globalSlot >= _slots.Count)
                    return null;

                string path = _slots[globalSlot];
                if (string.IsNullOrWhiteSpace(path))
                    return null;

                RectangleF cellWorld = new RectangleF(
                    page.X + column * cellWidth,
                    page.Y + row * cellHeight,
                    cellWidth,
                    cellHeight);

                RectangleF cellScreen = WorldToScreen(cellWorld);
                return GetRemarkRect(cellScreen).Contains(screen)
                    ? path
                    : null;
            }

            private RectangleF GetRemarkScreenRect(string path)
            {
                if (string.IsNullOrWhiteSpace(path))
                    return RectangleF.Empty;

                int cells = _gridSize * _gridSize;
                SizeF pageSize = PageSizeWorld();

                for (int globalSlot = 0;
                     globalSlot < _slots.Count;
                     globalSlot++)
                {
                    if (!string.Equals(
                        _slots[globalSlot],
                        path,
                        StringComparison.OrdinalIgnoreCase))
                        continue;

                    int pageIndex = globalSlot / cells;
                    int local = globalSlot % cells;
                    RectangleF page = PageRectWorld(pageIndex);

                    float cellWidth = page.Width / _gridSize;
                    float cellHeight = page.Height / _gridSize;

                    RectangleF cellWorld = new RectangleF(
                        page.X + (local % _gridSize) * cellWidth,
                        page.Y + (local / _gridSize) * cellHeight,
                        cellWidth,
                        cellHeight);

                    return GetRemarkRect(WorldToScreen(cellWorld));
                }

                return RectangleF.Empty;
            }

            private void BeginInlineCustomNameEdit(string path, RectangleF screenRect)
            {
                if (string.IsNullOrWhiteSpace(path))
                    return;

                EndInlineCaptionEdit(true);
                EndInlineCustomNameEdit(true);

                string value;
                _captions.TryGetValue(path, out value);
                if (string.IsNullOrWhiteSpace(value))
                    value = Path.GetFileNameWithoutExtension(path);

                _inlineCustomNamePath = path;
                _selectedPath = path;
                ImageSelected?.Invoke(path);

                _inlineCustomNameEditor = new TextBox
                {
                    Multiline = true,
                    AcceptsReturn = true,
                    BorderStyle = BorderStyle.FixedSingle,
                    BackColor = Color.White,
                    ForeColor = _theme.TextColor,
                    Font = new Font(_fontFamily, Math.Max(8f, 8.5f * _zoom), FontStyle.Bold),
                    Text = value ?? string.Empty,
                    Bounds = Rectangle.Round(screenRect),
                    Tag = path
                };
                _inlineCustomNameEditor.KeyDown += InlineCustomNameEditor_KeyDown;
                _inlineCustomNameEditor.Leave += (sender, args) => EndInlineCustomNameEdit(true);
                Controls.Add(_inlineCustomNameEditor);
                _inlineCustomNameEditor.BringToFront();
                _inlineCustomNameEditor.SelectAll();
                _inlineCustomNameEditor.Focus();
            }

            private void InlineCustomNameEditor_KeyDown(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape)
                {
                    EndInlineCustomNameEdit(false);
                    e.SuppressKeyPress = true;
                }
                else if (e.KeyCode == Keys.Enter && !e.Shift)
                {
                    EndInlineCustomNameEdit(true);
                    e.SuppressKeyPress = true;
                }
            }

            private void EndInlineCustomNameEdit(bool save)
            {
                if (_inlineCustomNameEditor == null)
                    return;

                string path = _inlineCustomNamePath;
                string value = _inlineCustomNameEditor.Text.Trim();
                TextBox editor = _inlineCustomNameEditor;
                _inlineCustomNameEditor = null;
                _inlineCustomNamePath = null;

                if (save && !string.IsNullOrWhiteSpace(path))
                {
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        _captions.Remove(path);
                        _customNames.Remove(path);
                    }
                    else
                    {
                        _captions[path] = value;
                        _customNames[path] = value;
                    }
                    LayoutChanged?.Invoke(this, EventArgs.Empty);
                }

                editor.KeyDown -= InlineCustomNameEditor_KeyDown;
                Controls.Remove(editor);
                editor.Dispose();
                Invalidate();
            }

            private void BeginInlineCaptionEdit(string path, RectangleF screenRect)
            {
                if (string.IsNullOrWhiteSpace(path))
                    return;

                EndInlineCaptionEdit(true);
                EndInlineCustomNameEdit(true);

                string caption;
                _captions.TryGetValue(path, out caption);

                _inlineCaptionPath = path;
                _selectedPath = path;
                if (ImageSelected != null)
                    ImageSelected(path);
                _inlineCaptionEditor = new TextBox
                {
                    Multiline = true,
                    AcceptsReturn = true,
                    BorderStyle = BorderStyle.FixedSingle,
                    BackColor = Color.White,
                    ForeColor = _theme.TextColor,
                    Font = new Font(_fontFamily, Math.Max(8f, 8.5f * _zoom), FontStyle.Bold),
                    Text = caption ?? string.Empty,
                    Bounds = Rectangle.Round(screenRect),
                    Tag = path
                };

                _inlineCaptionEditor.KeyDown += InlineCaptionEditor_KeyDown;
                _inlineCaptionEditor.Leave += (s, e) => EndInlineCaptionEdit(true);
                Controls.Add(_inlineCaptionEditor);
                _inlineCaptionEditor.BringToFront();
                _inlineCaptionEditor.SelectAll();
                _inlineCaptionEditor.Focus();
            }

            private void InlineCaptionEditor_KeyDown(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape)
                {
                    EndInlineCaptionEdit(false);
                    e.SuppressKeyPress = true;
                }
                else if (e.KeyCode == Keys.Enter && !e.Shift)
                {
                    EndInlineCaptionEdit(true);
                    e.SuppressKeyPress = true;
                }
            }

            private void EndInlineCaptionEdit(bool save)
            {
                if (_inlineCaptionEditor == null)
                    return;

                string path = _inlineCaptionPath;
                string value = _inlineCaptionEditor.Text.Trim();

                TextBox editor = _inlineCaptionEditor;
                _inlineCaptionEditor = null;
                _inlineCaptionPath = null;

                if (save && !string.IsNullOrWhiteSpace(path))
                {
                    if (string.IsNullOrWhiteSpace(value))
                        _captions.Remove(path);
                    else
                        _captions[path] = value;

                    LayoutChanged?.Invoke(this, EventArgs.Empty);
                }

                editor.KeyDown -= InlineCaptionEditor_KeyDown;
                Controls.Remove(editor);
                editor.Dispose();
                Invalidate();
            }

            private void Canvas_MouseWheel(
                object sender,
                MouseEventArgs e)
            {
                float factor =
                    e.Delta > 0
                        ? 1.12f
                        : 0.89f;

                ZoomAt(e.Location, factor);
            }

            private void Canvas_MouseDown(
                object sender,
                MouseEventArgs e)
            {
                Focus();
                EnsureView();

                if (e.Button == MouseButtons.Middle ||
                    (e.Button == MouseButtons.Left && _spaceDown))
                {
                    _panning = true;
                    _panStartScreen = e.Location;
                    _panStartCenter = _viewCenter;
                    Cursor = Cursors.Hand;
                    return;
                }

                if (e.Button != MouseButtons.Left)
                    return;

                string filenamePath = HitTestFilename(e.Location);
                if (!string.IsNullOrWhiteSpace(filenamePath))
                {
                    RectangleF editRect =
                        GetRemarkScreenRect(filenamePath);
                    if (editRect.IsEmpty)
                        editRect = GetFilenameScreenRect(filenamePath);

                    BeginInlineCustomNameEdit(
                        filenamePath,
                        editRect);
                    return;
                }

                string remarkPath = HitTestRemark(e.Location);
                if (!string.IsNullOrWhiteSpace(remarkPath))
                {
                    BeginInlineCaptionEdit(
                        remarkPath,
                        GetRemarkScreenRect(remarkPath));
                    return;
                }

                EndInlineCaptionEdit(true);
                EndInlineCustomNameEdit(true);
                PointF world = ScreenToWorld(e.Location);

                string unplacedPath =
                    HitTestUnplaced(world);

                if (!string.IsNullOrWhiteSpace(unplacedPath))
                {
                    BeginImageDrag(
                        unplacedPath,
                        -1,
                        world);
                    return;
                }

                int globalSlot = HitTestSlot(world);
                if (globalSlot >= 0)
                {
                    if (globalSlot >= 0 &&
                        globalSlot < _slots.Count &&
                        !string.IsNullOrWhiteSpace(
                            _slots[globalSlot]))
                    {
                        BeginImageDrag(
                            _slots[globalSlot],
                            globalSlot,
                            world);
                    }
                    else
                    {
                        ClearSelection();
                    }
                }
                else
                {
                    ClearSelection();
                }
            }

            private void Canvas_MouseMove(
                object sender,
                MouseEventArgs e)
            {
                if (_panning)
                {
                    float dx =
                        (e.X - _panStartScreen.X) /
                        _zoom;
                    float dy =
                        (e.Y - _panStartScreen.Y) /
                        _zoom;

                    _viewCenter =
                        new PointF(
                            _panStartCenter.X - dx,
                            _panStartCenter.Y - dy);

                    Invalidate();
                    return;
                }

                if (!_dragging)
                    return;

                PointF world = ScreenToWorld(e.Location);

                if (!_dragMoved)
                {
                    float dx =
                        world.X - _dragCurrentWorld.X;
                    float dy =
                        world.Y - _dragCurrentWorld.Y;

                    if (Math.Sqrt(dx * dx + dy * dy) < 4f)
                        return;

                    _dragMoved = true;
                }

                _dragCurrentWorld = world;
                Invalidate();
            }

            private void Canvas_MouseUp(
                object sender,
                MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Middle ||
                    (e.Button == MouseButtons.Left && _panning))
                {
                    _panning = false;
                    Cursor = Cursors.Default;
                    return;
                }

                if (e.Button != MouseButtons.Left ||
                    !_dragging)
                    return;

                PointF dropWorld =
                    ScreenToWorld(e.Location);

                bool moved =
                    _dragMoved;

                if (!moved)
                {
                    SelectImageInternal(_dragPath);
                    EndDrag();
                    return;
                }

                ApplyDragDrop(dropWorld);
                EndDrag();
            }

            private void BeginImageDrag(
                string path,
                int sourceSlot,
                PointF world)
            {
                _selectedPath = path;
                SelectImageInternal(path);

                _dragging = true;
                _dragMoved = false;
                _dragPath = path;
                _dragSourceSlot = sourceSlot;
                _dragCurrentWorld = world;

                if (sourceSlot < 0)
                {
                    PointF position;
                    if (_positions.TryGetValue(
                        path,
                        out position))
                    {
                        SizeF size =
                            PasteboardImageSize();

                        _dragOffset =
                            new PointF(
                                world.X - position.X,
                                world.Y - position.Y);
                    }
                    else
                    {
                        _dragOffset = new PointF(0, 0);
                    }
                }
                else
                {
                    _dragOffset = new PointF(0, 0);
                }

                Capture = true;
                Invalidate();
            }

            private void ApplyDragDrop(PointF dropWorld)
            {
                if (string.IsNullOrWhiteSpace(_dragPath))
                    return;

                if (_dragSourceSlot >= 0)
                {
                    SnapshotRequested?.Invoke(this, EventArgs.Empty);

                    int targetGlobal =
                        HitTestSlot(dropWorld);

                    if (targetGlobal >= 0)
                    {
                        if (targetGlobal != _dragSourceSlot)
                        {
                            string sourcePath =
                                _slots[_dragSourceSlot];

                            string targetPath =
                                targetGlobal < _slots.Count
                                    ? _slots[targetGlobal]
                                    : null;

                            _slots[_dragSourceSlot] =
                                targetPath;
                            _slots[targetGlobal] =
                                sourcePath;

                            if (!string.IsNullOrWhiteSpace(targetPath))
                            {
                                _unplaced.Remove(sourcePath);
                                _unplaced.Remove(targetPath);
                            }
                            else
                            {
                                _unplaced.Remove(sourcePath);
                            }

                            LayoutChanged?.Invoke(
                                this,
                                EventArgs.Empty);
                        }
                        return;
                    }

                    // Dropped outside the white page: remove from its cell
                    // and keep it visibly on the infinite pasteboard.
                    _slots[_dragSourceSlot] = null;
                    _unplaced.Add(_dragPath);

                    SizeF size =
                        PasteboardImageSize();

                    _positions[_dragPath] =
                        new PointF(
                            dropWorld.X -
                            Math.Min(
                                size.Width / 2f,
                                size.Width * 0.5f),
                            dropWorld.Y -
                            Math.Min(
                                size.Height / 2f,
                                size.Height * 0.5f));

                    LayoutChanged?.Invoke(
                        this,
                        EventArgs.Empty);
                    return;
                }

                SnapshotRequested?.Invoke(this, EventArgs.Empty);

                int global =
                    HitTestSlot(dropWorld);

                if (global >= 0)
                {

                    string oldPath =
                        _slots[global];

                    if (string.IsNullOrWhiteSpace(oldPath))
                    {
                        _slots[global] = _dragPath;
                        _unplaced.Remove(_dragPath);
                    }
                    else if (!string.Equals(
                        oldPath,
                        _dragPath,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        // The incoming pasteboard image takes the cell and
                        // the previous cell occupant is released to the
                        // pasteboard. This avoids silently deleting anything.
                        _slots[global] = _dragPath;
                        _unplaced.Remove(_dragPath);
                        _unplaced.Add(oldPath);

                        PointF oldPosition =
                            new PointF(
                                PageSizeWorld().Width + 80f,
                                80f);

                        PointF incomingPosition;
                        if (_positions.TryGetValue(
                            _dragPath,
                            out incomingPosition))
                        {
                            oldPosition = incomingPosition;
                        }

                        _positions[oldPath] = oldPosition;
                    }

                    LayoutChanged?.Invoke(
                        this,
                        EventArgs.Empty);
                }
                else
                {
                    _positions[_dragPath] =
                        new PointF(
                            dropWorld.X - 87.5f,
                            dropWorld.Y - 72.5f);
                    _unplaced.Add(_dragPath);

                    LayoutChanged?.Invoke(
                        this,
                        EventArgs.Empty);
                }
            }

            private void EndDrag()
            {
                _dragging = false;
                _dragMoved = false;
                _dragPath = null;
                _dragSourceSlot = -1;
                Capture = false;
                Cursor = Cursors.Default;
                Invalidate();
            }

            private void ClearSelection()
            {
                _selectedPath = null;
                if (ImageSelected != null) ImageSelected(null);
                Invalidate();
            }

            private void SelectImageInternal(string path)
            {
                _selectedPath = path;
                if (ImageSelected != null) ImageSelected(path);
                Invalidate();
            }

            private int HitTestSlot(PointF world)
            {
                SizeF pageSize = PageSizeWorld();
                float pageStride = pageSize.Height + PageGap;

                int pageIndex =
                    (int)Math.Floor(world.Y / pageStride);

                if (pageIndex < 0 || pageIndex >= _totalPages)
                    return -1;

                RectangleF page = PageRectWorld(pageIndex);

                if (!page.Contains(world))
                    return -1;

                int column =
                    (int)((world.X - page.X) /
                          (page.Width / _gridSize));

                int row =
                    (int)((world.Y - page.Y) /
                          (page.Height / _gridSize));

                if (column < 0 ||
                    column >= _gridSize ||
                    row < 0 ||
                    row >= _gridSize)
                    return -1;

                return pageIndex * (_gridSize * _gridSize) +
                    row * _gridSize +
                    column;
            }

            private string HitTestUnplaced(PointF world)
            {
                SizeF size =
                    PasteboardImageSize();

                for (int i = _unplaced.Count - 1; i >= 0; i--)
                {
                    string path =
                        _unplaced.ElementAt(i);

                    PointF position;
                    if (!_positions.TryGetValue(
                        path,
                        out position))
                        continue;

                    RectangleF rect =
                        new RectangleF(
                            position.X,
                            position.Y,
                            size.Width,
                            size.Height);

                    if (rect.Contains(world))
                        return path;
                }

                return null;
            }

            private static Rectangle FitRectangle(
                int sourceWidth,
                int sourceHeight,
                Rectangle bounds)
            {
                if (sourceWidth <= 0 ||
                    sourceHeight <= 0)
                    return bounds;

                float scale =
                    Math.Min(
                        bounds.Width / (float)sourceWidth,
                        bounds.Height / (float)sourceHeight);

                int width =
                    Math.Max(
                        1,
                        (int)Math.Round(
                            sourceWidth * scale));

                int height =
                    Math.Max(
                        1,
                        (int)Math.Round(
                            sourceHeight * scale));

                return new Rectangle(
                    bounds.X +
                    (bounds.Width - width) / 2,
                    bounds.Y +
                    (bounds.Height - height) / 2,
                    width,
                    height);
            }

            private void Canvas_DragEnter(
                object sender,
                DragEventArgs e)
            {
                if (e.Data.GetDataPresent(ImagePathFormat))
                    e.Effect = DragDropEffects.Move;
                else
                    e.Effect = DragDropEffects.None;
            }

            private void Canvas_DragDrop(
                object sender,
                DragEventArgs e)
            {
                string path =
                    e.Data.GetData(ImagePathFormat) as string;

                if (string.IsNullOrWhiteSpace(path) ||
                    !_allImages.Any(
                        p => string.Equals(
                            p,
                            path,
                            StringComparison.OrdinalIgnoreCase)))
                    return;

                Point screen =
                    PointToClient(
                        new Point(
                            e.X,
                            e.Y));

                PointF world =
                    ScreenToWorld(screen);

                SnapshotRequested?.Invoke(
                    this,
                    EventArgs.Empty);

                int global = HitTestSlot(world);
                if (global >= 0)
                {
                    string old =
                        _slots[global];

                    if (string.IsNullOrWhiteSpace(old))
                    {
                        _slots[global] = path;
                        _unplaced.Remove(path);
                    }
                    else if (!string.Equals(
                        old,
                        path,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        _slots[global] = path;
                        _unplaced.Remove(path);
                        _unplaced.Add(old);
                        _positions[old] =
                            new PointF(
                                PageSizeWorld().Width + 80f,
                                80f);
                    }

                    LayoutChanged?.Invoke(
                        this,
                        EventArgs.Empty);
                }
                else
                {
                    _unplaced.Add(path);
                    _positions[path] =
                        new PointF(
                            world.X - 87.5f,
                            world.Y - 72.5f);

                    LayoutChanged?.Invoke(
                        this,
                        EventArgs.Empty);
                }
            }
        }
    }
}
