using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NPPLPrintMaster
{
    // ============================================================
    // CUSTOM SCROLL PANEL
    // Prevents normal AutoScroll movement during Ctrl + MouseWheel
    // while still forwarding the wheel event to the zoom engine.
    // ============================================================
    public class CustomScrollPanel : Panel
    {
        public event MouseEventHandler CtrlMouseWheel;

        public CustomScrollPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;

            // The scroll viewport itself owns keyboard/mouse-wheel focus.
            // This avoids focusing the very large PictureBox child, which
            // can make WinForms AutoScroll jump toward the canvas origin.
            SetStyle(ControlStyles.Selectable, true);
            TabStop = true;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if ((Control.ModifierKeys & Keys.Control) != 0)
            {
                CtrlMouseWheel?.Invoke(this, e);

                if (e is HandledMouseEventArgs handled)
                    handled.Handled = true;

                return;
            }

            base.OnMouseWheel(e);
        }
    }

    // ============================================================
    // FREEFORM BUILDER
    // ============================================================
    public class FreeformBuilderForm : Form
    {
        // ========================================================
        // CONSTANTS
        // ========================================================

        private const float ExportDpi = 300f;

        private const int MinimumItemWidth = 200;

        // Large virtual pasteboard. The white export page sits near the
        // middle and the workbench grows further as the user pans/moves art.
        private const int InitialWorkspaceWidth = 10000;
        private const int InitialWorkspaceHeight = 10000;

        private const int WorkbenchEdgeBuffer = 1200;
        private const int WorkbenchGrowBy = 4000;

        // ========================================================
        // UI
        // ========================================================

        private PictureBox pbCanvas;
        private CustomScrollPanel scrollPanel;
        private ToolStrip toolStrip;

        private ToolStripComboBox cmbFont;
        private ToolStripComboBox cmbTemplate;
        private ToolStripComboBox cmbLayout;

        private ToolStripButton btnBold;
        private ToolStripButton btnItalic;
        private ToolStripButton btnRotate;
        private ToolStripButton btnToggleText;
        private ToolStripButton btnEditText;
        private ToolStripButton btnArrange;
        private ToolStripButton btnFitScreen;
        private ToolStripButton btnLoadNppl;

        private ComboBox cmbCustomText;
        private TextBox txtQuickText;
        private ToolStripStatusLabel lblOutputInfo;
        private long lastSavedBmpBytes = -1;
        private int lastSavedBmpWidth = -1;
        private int lastSavedBmpHeight = -1;

        // ========================================================
        // WORKSPACE
        // ========================================================

        public List<CanvasItem> items = new List<CanvasItem>();

        private readonly List<WorkspaceSnapshot> undoStack =
            new List<WorkspaceSnapshot>();

        private List<CanvasItemState> clipboardItems =
            new List<CanvasItemState>();

        private readonly HashSet<Image> ownedImages =
            new HashSet<Image>();

        // Reusable gray-area artwork shelf. Shelf images are copied to
        // %LOCALAPPDATA%\NPPLPrintMaster\FreeformShelf and restored on
        // the next Freeform session until Clear Shelf is used.
        private readonly Dictionary<CanvasItem, FreeformShelfRecord>
            persistentShelfItems =
                new Dictionary<CanvasItem, FreeformShelfRecord>();

        private int workspaceWidth = InitialWorkspaceWidth;
        private int workspaceHeight = InitialWorkspaceHeight;

        // White export page starts as an island in the center of the
        // initial gray workbench (3500 x 3500 inside 10000 x 10000).
        private int pageX = 3250;
        private int pageY = 3250;

        private int pageWidth = 3500;
        private int pageHeight = 3500;

        private float zoom = 0.28f;

        // ========================================================
        // SELECTION / MOUSE STATE
        // ========================================================

        private List<CanvasItem> selectedItems =
            new List<CanvasItem>();

        private bool isDragging;
        private bool isResizing;

        private bool didDragMove;
        private bool didResizeMove;

        // Prevents a normal click / tiny touchpad jitter from being
        // treated as an intentional drag.
        private const int DragStartThresholdPixels = 10;
        private Point dragStartPhysical = Point.Empty;

        private Point lastMousePos = Point.Empty;

        private bool isSpaceDown;
        private bool isPanning;

        private Point panStartPoint = Point.Empty;

        private bool isMarqueeSelecting;

        private Point marqueeStart = Point.Empty;
        private Point marqueeEnd = Point.Empty;

        private int? snapLineX;
        private int? snapLineY;

        // Screen-pixel based snapping is converted to logical units.
        private const int SnapScreenPixels = 8;

        private bool keyboardMovePending;

        // ========================================================
        // FONT STATE
        // ========================================================

        private string lastUsedFontName = "Calibri";
        private FontStyle lastUsedFontStyle = FontStyle.Bold;

        // ========================================================
        // INTERNAL UI FLAGS
        // ========================================================

        private bool isUpdatingUI;
        private bool suppressLayoutEvent;

        // ========================================================
        // CACHED DRAWING RESOURCES
        // ========================================================

        private readonly SolidBrush shadowBrush =
            new SolidBrush(Color.FromArgb(40, 0, 0, 0));

        private readonly Pen pageBorderPen =
            new Pen(Color.Black, 2);

        private readonly Pen exportBorderPen =
            new Pen(Color.LimeGreen, 12)
            {
                DashStyle = DashStyle.Dash
            };

        private readonly Pen snapLinePen =
            new Pen(Color.DeepSkyBlue, 3);

        private readonly Pen selectionPen =
            new Pen(Color.DodgerBlue, 4)
            {
                DashStyle = DashStyle.Dash
            };

        private readonly Pen textPen =
            new Pen(Color.Black, 6);

        private readonly SolidBrush textBrush =
            new SolidBrush(Color.Black);

        private readonly SolidBrush marqueeBrush =
            new SolidBrush(Color.FromArgb(60, 0, 120, 215));

        private readonly Pen marqueePen =
            new Pen(Color.DodgerBlue, 1)
            {
                DashStyle = DashStyle.Dash
            };

        // ========================================================
        // CONSTRUCTOR
        // ========================================================

        public FreeformBuilderForm(
            List<CanvasItem> initialItems = null,
            WorkspaceData workspaceData = null,
            AppSettings appSettings = null,
            string initialSaveDirectory = "")
        {
            Text =
                "Pro Freeform Builder - NPPL PrintMaster";

            Size = new Size(1300, 850);
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;

            KeyPreview = true;
            AllowDrop = true;
            DoubleBuffered = true;

            BackColor = Color.FromArgb(38, 40, 46);

            // ====================================================
            // 1. TOP TOOLBAR
            // ====================================================

            toolStrip = new ToolStrip
            {
                Dock = DockStyle.None,
                GripStyle = ToolStripGripStyle.Visible,
                CanOverflow = true,
                LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow,
                BackColor = Color.FromArgb(28, 29, 33),
                Padding = new Padding(5)
            };

            // A second, movable formatting strip keeps all editing
            // commands visible on lower-resolution displays.
            ToolStrip formatToolStrip = new ToolStrip
            {
                Dock = DockStyle.None,
                GripStyle = ToolStripGripStyle.Visible,
                CanOverflow = true,
                LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow,
                BackColor = Color.FromArgb(28, 29, 33),
                Padding = new Padding(5)
            };

            ToolStripButton btnSave =
                new ToolStripButton
                {
                    Text = "💾 Save Final BMP",
                    Font =
                        new Font(
                            "Segoe UI",
                            9,
                            FontStyle.Bold),

                    ForeColor =
                        Color.FromArgb(252, 213, 53)
                };

            ToolStripSeparator sep0 =
                new ToolStripSeparator();

            ToolStripButton btnAddImage =
                new ToolStripButton
                {
                    Text = "➕ Add Images",
                    Font =
                        new Font(
                            "Segoe UI",
                            9,
                            FontStyle.Bold),

                    ForeColor = Color.White
                };

            btnLoadNppl =
                new ToolStripButton
                {
                    Text = "📂 Load .nppl",
                    Font =
                        new Font(
                            "Segoe UI",
                            9,
                            FontStyle.Bold),

                    ForeColor = Color.LightSkyBlue
                };

            ToolStripSeparator sep00 =
                new ToolStripSeparator();

            ToolStripLabel lblLayout =
                new ToolStripLabel
                {
                    Text = "  📐 Layout:",
                    ForeColor = Color.White
                };

            cmbLayout =
                new ToolStripComboBox
                {
                    DropDownStyle =
                        ComboBoxStyle.DropDownList,

                    Width = 120
                };

            cmbLayout.Items.AddRange(
                new object[]
                {
                    "Free Style",
                    "3-Lane Grid",
                    "4-Lane Grid",
                    "Masonry"
                });

            cmbLayout.SelectedIndex = 1;

            btnArrange =
                new ToolStripButton
                {
                    Text = "🔃 Auto Arrange",
                    ForeColor = Color.White
                };

            ToolStripSeparator sepLayout =
                new ToolStripSeparator();

            btnFitScreen =
                new ToolStripButton
                {
                    Text = "🎯 Fit to Screen",

                    Font =
                        new Font(
                            "Segoe UI",
                            9,
                            FontStyle.Bold),

                    ForeColor = Color.Cyan
                };

            ToolStripSeparator sepCenter =
                new ToolStripSeparator();

            ToolStripButton btnCenterPage =
                new ToolStripButton
                {
                    Text = "◎ Center Page",
                    ToolTipText =
                        "Bring the white export page back to the center without changing zoom.",
                    ForeColor = Color.White
                };

            ToolStripButton btnClearShelf =
                new ToolStripButton
                {
                    Text = "🗑 Clear Shelf",
                    ToolTipText =
                        "Permanently remove reusable artwork saved in the gray workbench.",
                    ForeColor = Color.White
                };

            cmbTemplate =
                new ToolStripComboBox
                {
                    DropDownStyle =
                        ComboBoxStyle.DropDownList,

                    Width = 160
                };

            cmbTemplate.Items.AddRange(
                new object[]
                {
                    "📋 Quick Text...",
                    new CustomTextAsset
                    {
                        Name = "50 x 38 MM",
                        Content = "BARCODE SAMPLE 50 X 38 MM"
                    },
                    new CustomTextAsset
                    {
                        Name = "150 x 200 MM",
                        Content = "BARCODE SAMPLE 150 X 200 MM"
                    },
                    new CustomTextAsset
                    {
                        Name = "Product Barcode - Auto Size",
                        Content = "PRODUCT BARCODE {W} X {H} MM"
                    },
                    new CustomTextAsset
                    {
                        Name = "Carton Barcode - Auto Size",
                        Content = "CARTON BARCODE {W} X {H} MM"
                    }
                });

            cmbTemplate.SelectedIndex = 0;

            cmbFont =
                new ToolStripComboBox
                {
                    DropDownStyle =
                        ComboBoxStyle.DropDownList,

                    Width = 120
                };

            cmbFont.Items.AddRange(
                new object[]
                {
                    "Calibri",
                    "Tahoma",
                    "Times New Roman",
                    "Segoe UI",
                    "Verdana",
                    "Courier New"
                });

            cmbFont.SelectedItem = "Calibri";

            btnBold =
                new ToolStripButton
                {
                    Text = "B",

                    Font =
                        new Font(
                            "Times New Roman",
                            10,
                            FontStyle.Bold),

                    Checked = true
                };

            btnItalic =
                new ToolStripButton
                {
                    Text = "I",

                    Font =
                        new Font(
                            "Times New Roman",
                            10,
                            FontStyle.Italic)
                };

            ToolStripSeparator sep1 =
                new ToolStripSeparator();

            btnEditText =
                new ToolStripButton
                {
                    Text = "📝 Edit Text"
                };

            btnToggleText =
                new ToolStripButton
                {
                    Text = "👁️ Show/Hide"
                };

            btnRotate =
                new ToolStripButton
                {
                    Text = "🔄 Rotate 90°"
                };

            // Main workflow strip.
            toolStrip.Items.AddRange(
                new ToolStripItem[]
                {
                    btnSave,
                    sep0,
                    btnAddImage,
                    btnLoadNppl,
                    sep00,
                    lblLayout,
                    cmbLayout,
                    btnArrange,
                    sepLayout,
                    btnFitScreen,
                    sepCenter,
                    btnCenterPage,
                    btnClearShelf
                });

            // Formatting / object strip.
            formatToolStrip.Items.AddRange(
                new ToolStripItem[]
                {
                    cmbTemplate,
                    cmbFont,
                    btnBold,
                    btnItalic,
                    sep1,
                    btnEditText,
                    btnToggleText,
                    btnRotate
                });

            ToolStripPanel topToolPanel =
                new ToolStripPanel
                {
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    BackColor = Color.FromArgb(28, 29, 33)
                };

            // Put the strips on two rows by default. The grip at the left
            // allows the user to drag/reorganize them at runtime.
            topToolPanel.Join(
                toolStrip,
                new Point(0, 0));

            topToolPanel.Join(
                formatToolStrip,
                new Point(0, toolStrip.Height + 1));

            Controls.Add(topToolPanel);

            // Live export information stays visible while arranging/resizing.
            StatusStrip outputStatusStrip = new StatusStrip
            {
                Dock = DockStyle.Bottom,
                SizingGrip = false,
                ShowItemToolTips = true
            };

            lblOutputInfo = new ToolStripStatusLabel
            {
                Text = "OUTPUT   No printable image",
                Spring = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };

            outputStatusStrip.Items.Add(lblOutputInfo);
            Controls.Add(outputStatusStrip);

            // ====================================================
            // THEME-AWARE TOOLBAR COLORS
            // ====================================================
            // The main application already stores the selected theme in
            // SettingsManager. Use that same theme here so toolbar commands
            // remain readable on both dark and light themes.
            AppSettings builderSettings =
                SettingsManager.Load();

            ThemeColors builderTheme =
                ThemeManager.GetTheme(
                    builderSettings.Theme);

            Color toolbarBackColor =
                builderTheme.SidebarColor;

            // Use a guaranteed high-contrast foreground for the toolbar
            // itself. Some themes intentionally use black TextColor for
            // white content panels, which would disappear on a dark sidebar.
            double toolbarBrightness =
                (toolbarBackColor.R * 0.299) +
                (toolbarBackColor.G * 0.587) +
                (toolbarBackColor.B * 0.114);

            Color toolbarForeColor =
                toolbarBrightness < 150
                    ? Color.White
                    : Color.Black;

            toolStrip.BackColor =
                toolbarBackColor;

            toolStrip.ForeColor =
                toolbarForeColor;

            formatToolStrip.BackColor =
                toolbarBackColor;

            formatToolStrip.ForeColor =
                toolbarForeColor;

            topToolPanel.BackColor =
                toolbarBackColor;

            outputStatusStrip.BackColor = builderTheme.ContentBg;
            outputStatusStrip.ForeColor = builderTheme.TextColor;
            lblOutputInfo.BackColor = builderTheme.ContentBg;
            lblOutputInfo.ForeColor = builderTheme.TextColor;

            foreach (ToolStripItem item in toolStrip.Items)
            {
                item.ForeColor =
                    toolbarForeColor;

                item.BackColor =
                    toolbarBackColor;
            }

            foreach (ToolStripItem item in formatToolStrip.Items)
            {
                item.ForeColor =
                    toolbarForeColor;

                item.BackColor =
                    toolbarBackColor;
            }

            // Keep the Save command visually prominent using the current
            // theme's accent color, while all other commands use readable
            // high-contrast text.
            btnSave.ForeColor =
                toolbarForeColor;

            // ToolStripComboBox hosts a normal ComboBox internally, so its
            // colors must be applied to the hosted control as well.
            cmbLayout.BackColor =
                builderTheme.ControlBg;

            cmbLayout.ForeColor =
                builderTheme.TextColor;

            cmbTemplate.BackColor =
                builderTheme.ControlBg;

            cmbTemplate.ForeColor =
                builderTheme.TextColor;

            cmbFont.BackColor =
                builderTheme.ControlBg;

            cmbFont.ForeColor =
                builderTheme.TextColor;

            // Explicitly keep the formatting commands readable from startup.
            btnBold.ForeColor =
                toolbarForeColor;

            btnItalic.ForeColor =
                toolbarForeColor;

            btnEditText.ForeColor =
                toolbarForeColor;

            btnToggleText.ForeColor =
                toolbarForeColor;

            btnRotate.ForeColor =
                toolbarForeColor;

            btnArrange.ForeColor =
                toolbarForeColor;

            btnFitScreen.ForeColor =
                toolbarForeColor;

            btnLoadNppl.ForeColor =
                toolbarForeColor;

            btnAddImage.ForeColor =
                toolbarForeColor;

            lblLayout.ForeColor =
                toolbarForeColor;

            // ====================================================
            // 2. LEFT TOOL PANEL
            // ====================================================

            Panel pnlTools =
                new Panel
                {
                    Dock = DockStyle.Left,
                    Width = 320,

                    BackColor =
                        Color.FromArgb(28, 29, 33),

                    Padding = new Padding(15)
                };

            Label lblToolHeader =
                new Label
                {
                    Text = "🛠️ BUILDER TOOLS",

                    Font =
                        new Font(
                            "Segoe UI Black",
                            14,
                            FontStyle.Bold),

                    ForeColor =
                        Color.FromArgb(252, 213, 53),

                    AutoSize = false,
                    Width = 290,
                    Height = 40,

                    TextAlign =
                        ContentAlignment.MiddleCenter
                };

            pnlTools.Controls.Add(lblToolHeader);

            // ====================================================
            // QUICK TEXT
            // ====================================================

            GroupBox grpQuickText =
                new GroupBox
                {
                    Text = "1. Quick Text Entry",

                    ForeColor = Color.White,

                    Font =
                        new Font(
                            "Segoe UI",
                            10,
                            FontStyle.Bold),

                    Location =
                        new Point(10, 60),

                    Size =
                        new Size(300, 160)
                };

            txtQuickText =
                new TextBox
                {
                    Location =
                        new Point(15, 30),

                    Size =
                        new Size(270, 70),

                    Multiline = true,

                    ScrollBars =
                        ScrollBars.Vertical,

                    BackColor =
                        Color.FromArgb(50, 52, 59),

                    ForeColor = Color.White,

                    BorderStyle =
                        BorderStyle.FixedSingle,

                    Font =
                        new Font(
                            "Segoe UI",
                            9,
                            FontStyle.Regular)
                };

            Button btnAddQuickText =
                new Button
                {
                    Text =
                        "+ Apply to Selected Label",

                    Location =
                        new Point(15, 110),

                    Size =
                        new Size(270, 35),

                    BackColor =
                        Color.FromArgb(252, 213, 53),

                    ForeColor = Color.Black,

                    FlatStyle =
                        FlatStyle.Flat,

                    Cursor =
                        Cursors.Hand
                };

            btnAddQuickText.FlatAppearance.BorderSize = 0;

            btnAddQuickText.Click +=
                (s, e) =>
                {
                    if (selectedItems.Count == 0)
                    {
                        MessageBox.Show(
                            "Please select at least one image on the canvas first to apply text.",
                            "Select Image",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);

                        return;
                    }

                    if (string.IsNullOrWhiteSpace(
                        txtQuickText.Text))
                    {
                        return;
                    }

                    foreach (CanvasItem item in selectedItems)
                    {
                        item.TextTemplate =
                            txtQuickText.Text;

                        item.ShowText = true;
                    }

                    SaveUndoState();
                    pbCanvas.Invalidate();
                };

            grpQuickText.Controls.AddRange(
                new Control[]
                {
                    txtQuickText,
                    btnAddQuickText
                });

            pnlTools.Controls.Add(grpQuickText);

            // ====================================================
            // TEXT LIBRARY
            // ====================================================

            GroupBox grpLibrary =
                new GroupBox
                {
                    Text = "2. Saved Quick Text Library",

                    ForeColor = Color.White,

                    Font =
                        new Font(
                            "Segoe UI",
                            10,
                            FontStyle.Bold),

                    Location =
                        new Point(10, 240),

                    Size =
                        new Size(300, 150)
                };

            cmbCustomText =
                new ComboBox
                {
                    Location =
                        new Point(15, 35),

                    Size =
                        new Size(270, 25),

                    DropDownStyle =
                        ComboBoxStyle.DropDownList,

                    BackColor =
                        Color.FromArgb(50, 52, 59),

                    ForeColor =
                        Color.White,

                    Font =
                        new Font(
                            "Segoe UI",
                            9,
                            FontStyle.Regular)
                };

            cmbCustomText.SelectedIndexChanged +=
                (s, e) =>
                {
                    CustomTextAsset asset =
                        cmbCustomText.SelectedItem
                        as CustomTextAsset;

                    if (asset != null &&
                        txtQuickText != null)
                    {
                        txtQuickText.Text =
                            asset.Content ?? string.Empty;
                    }
                };

            LoadCustomTextDropdown();

            Button btnInsertSaved =
                new Button
                {
                    Text = "Use Selected Text",

                    Location =
                        new Point(15, 70),

                    Size =
                        new Size(130, 35),

                    BackColor =
                        Color.FromArgb(0, 122, 204),

                    ForeColor = Color.White,

                    FlatStyle =
                        FlatStyle.Flat,

                    Cursor =
                        Cursors.Hand
                };

            btnInsertSaved.FlatAppearance.BorderSize = 0;

            btnInsertSaved.Click +=
                (s, e) =>
                {
                    CustomTextAsset asset =
                        cmbCustomText.SelectedItem
                        as CustomTextAsset;

                    if (asset != null)
                    {
                        txtQuickText.Text =
                            asset.Content;
                    }
                };

            Button btnManageLibrary =
                new Button
                {
                    Text = "➕ Add / Manage...",

                    Location =
                        new Point(155, 70),

                    Size =
                        new Size(130, 35),

                    BackColor =
                        Color.FromArgb(70, 72, 79),

                    ForeColor =
                        Color.White,

                    FlatStyle =
                        FlatStyle.Flat,

                    Cursor =
                        Cursors.Hand
                };

            btnManageLibrary.FlatAppearance.BorderSize = 0;

            btnManageLibrary.Click +=
                (s, e) => OpenLibraryManager();

            grpLibrary.Controls.AddRange(
                new Control[]
                {
                    cmbCustomText,
                    btnInsertSaved,
                    btnManageLibrary
                });

            pnlTools.Controls.Add(grpLibrary);

            // ====================================================
            // 3. CANVAS AREA
            // ====================================================

            Panel pnlCanvasArea =
                new Panel
                {
                    Dock =
                        DockStyle.Fill,

                    BackColor =
                        Color.FromArgb(38, 40, 46)
                };

            scrollPanel =
                new CustomScrollPanel
                {
                    Dock =
                        DockStyle.Fill,

                    AutoScroll = true,

                    BackColor =
                        Color.DarkGray,

                    AllowDrop = true
                };

            pbCanvas =
                new PictureBox
                {
                    BackColor =
                        Color.FromArgb(70, 72, 79),

                    Location =
                        new Point(0, 0),

                    Size =
                        new Size(
                            (int)(workspaceWidth * zoom),
                            (int)(workspaceHeight * zoom)),

                    Cursor =
                        Cursors.Default,

                    AllowDrop = true
                };

            scrollPanel.Controls.Add(pbCanvas);

            pnlCanvasArea.Controls.Add(
                scrollPanel);

            Controls.Add(pnlCanvasArea);

            Controls.Add(
                new Splitter
                {
                    Dock =
                        DockStyle.Left,

                    Width = 3,

                    BackColor =
                        Color.FromArgb(252, 213, 53)
                });

            Controls.Add(pnlTools);

            Action FocusCanvasViewport =
                () =>
                {
                    int keepX =
                        Math.Abs(
                            scrollPanel
                                .AutoScrollPosition
                                .X);

                    int keepY =
                        Math.Abs(
                            scrollPanel
                                .AutoScrollPosition
                                .Y);

                    scrollPanel.Focus();

                    int afterX =
                        Math.Abs(
                            scrollPanel
                                .AutoScrollPosition
                                .X);

                    int afterY =
                        Math.Abs(
                            scrollPanel
                                .AutoScrollPosition
                                .Y);

                    // Defensive restore: focusing the viewport should not move
                    // it, but if Windows changes the scroll position, put the
                    // exact previous view back immediately.
                    if (afterX != keepX ||
                        afterY != keepY)
                    {
                        scrollPanel.AutoScrollPosition =
                            new Point(
                                keepX,
                                keepY);
                    }
                };

            pbCanvas.MouseEnter +=
                (s, e) => FocusCanvasViewport();

            // ====================================================
            // FIT / CENTER
            // ====================================================

            Action CenterViewOnPage =
                () =>
                {
                    UpdateWorkspaceExtent();

                    float padX = 200f;
                    float padY = 200f;

                    float zoomX =
                        (float)scrollPanel.ClientSize.Width /
                        (pageWidth + padX);

                    float zoomY =
                        (float)scrollPanel.ClientSize.Height /
                        (pageHeight + padY);

                    zoom =
                        Math.Min(
                            zoomX,
                            zoomY);

                    zoom =
                        Math.Max(
                            0.05f,
                            Math.Min(
                                2.0f,
                                zoom));

                    UpdateCanvasPhysicalSize();

                    int targetX =
                        (int)(
                            (pageX +
                             pageWidth / 2f) *
                            zoom);

                    int targetY =
                        (int)(
                            (pageY +
                             pageHeight / 2f) *
                            zoom);

                    int scrollX =
                        targetX -
                        scrollPanel.ClientSize.Width / 2;

                    int scrollY =
                        targetY -
                        scrollPanel.ClientSize.Height / 2;

                    scrollPanel.AutoScrollPosition =
                        new Point(
                            Math.Max(
                                0,
                                scrollX),

                            Math.Max(
                                0,
                                scrollY));

                    pbCanvas.Invalidate();
                };

            Shown +=
                (s, e) => CenterViewOnPage();

            btnFitScreen.Click +=
                (s, e) => CenterViewOnPage();

            btnCenterPage.Click +=
                (s, e) => CenterExportPageInViewport();

            btnClearShelf.Click +=
                (s, e) => ClearFreeformShelf();

            // Start with true empty undo state.
            SaveUndoState();

            // ====================================================
            // AUTO ARRANGE
            // ====================================================

            Action<string> ApplyLayout =
                mode =>
                {
                    List<CanvasItem> layoutItems =
                        items
                            .Where(
                                i =>
                                    i != null &&
                                    !IsParkedPersistentShelfItem(i))
                            .ToList();

                    if (layoutItems.Count == 0)
                        return;

                    if (mode == "Free Style")
                        return;

                    int cols =
                        mode == "3-Lane Grid"
                            ? 3
                            : 4;

                    int pad = 50;
                    int spacing = 40;

                    int laneWidth =
                        (pageWidth -
                         pad * 2 -
                         spacing * (cols - 1)) /
                        cols;

                    int[] colHeights =
                        new int[cols];

                    for (int i = 0;
                         i < cols;
                         i++)
                    {
                        colHeights[i] =
                            pageY + pad;
                    }

                    int currentRowMaxY =
                        pageY + pad;

                    for (int i = 0;
                         i < layoutItems.Count;
                         i++)
                    {
                        CanvasItem item =
                            layoutItems[i];

                        double aspect =
                            item.OriginalAspect;

                        if (aspect <= 0)
                            aspect = 1.0;

                        item.Width =
                            laneWidth;

                        item.Height =
                            Math.Max(
                                1,
                                (int)Math.Round(
                                    laneWidth /
                                    aspect));

                        int dynBoxHeight =
                            item.ShowText
                                ? GetTextAreaHeight(
                                    item)
                                : 0;

                        int totalH =
                            item.Height +
                            dynBoxHeight;

                        if (mode.Contains("Grid"))
                        {
                            int col =
                                i % cols;

                            if (col == 0 &&
                                i > 0)
                            {
                                for (int c = 0;
                                     c < cols;
                                     c++)
                                {
                                    colHeights[c] =
                                        currentRowMaxY +
                                        spacing;
                                }
                            }

                            item.X =
                                pageX +
                                pad +
                                col *
                                (laneWidth +
                                 spacing);

                            item.Y =
                                colHeights[col];

                            if (item.Y +
                                totalH >
                                currentRowMaxY)
                            {
                                currentRowMaxY =
                                    item.Y +
                                    totalH;
                            }
                        }
                        else if (
                            mode ==
                            "Masonry")
                        {
                            int minCol = 0;

                            for (int c = 1;
                                 c < cols;
                                 c++)
                            {
                                if (colHeights[c] <
                                    colHeights[minCol])
                                {
                                    minCol = c;
                                }
                            }

                            item.X =
                                pageX +
                                pad +
                                minCol *
                                (laneWidth +
                                 spacing);

                            item.Y =
                                colHeights[minCol];

                            colHeights[minCol] +=
                                totalH +
                                spacing;
                        }
                    }

                    int finalMaxY =
                        mode == "Masonry"
                            ? colHeights.Max()
                            : currentRowMaxY;

                    pageHeight =
                        Math.Max(
                            3500,

                            finalMaxY -
                            pageY +
                            pad +
                            200);

                    UpdateWorkspaceExtent();

                    SaveUndoState();

                    CenterViewOnPage();
                };

            btnArrange.Click +=
                (s, e) =>
                {
                    string mode =
                        cmbLayout.SelectedItem
                            ?.ToString()
                        ?? "Free Style";

                    if (mode ==
                        "Free Style")
                    {
                        MessageBox.Show(
                            "Please select a Grid or Masonry layout from the dropdown first.",
                            "Free Style Active",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);

                        return;
                    }

                    ApplyLayout(mode);
                };

            cmbLayout.SelectedIndexChanged +=
                (s, e) =>
                {
                    if (suppressLayoutEvent)
                        return;

                    string mode =
                        cmbLayout.SelectedItem
                            ?.ToString()
                        ?? "Free Style";

                    if (mode !=
                        "Free Style")
                    {
                        ApplyLayout(mode);
                    }
                };

            // ====================================================
            // ASYNC IMAGE IMPORT
            // ====================================================

            Func<string[], Task> ImportFilesAsync =
                async files =>
                {
                    if (files == null ||
                        files.Length == 0)
                    {
                        return;
                    }

                    Cursor =
                        Cursors.WaitCursor;

                    pbCanvas.Cursor =
                        Cursors.WaitCursor;

                    string importFontName =
                        lastUsedFontName;

                    FontStyle importFontStyle =
                        lastUsedFontStyle;

                    try
                    {
                        ImportBatchResult result =
                            await Task.Run(
                                () =>
                                {
                                    ImportBatchResult batch =
                                        new ImportBatchResult();

                                    foreach (
                                        string file
                                        in files)
                                    {
                                        if (string.IsNullOrWhiteSpace(
                                            file))
                                        {
                                            continue;
                                        }

                                        string ext =
                                            Path.GetExtension(
                                                file)
                                                .ToLowerInvariant();

                                        if (ext != ".jpg" &&
                                            ext != ".jpeg" &&
                                            ext != ".png" &&
                                            ext != ".bmp")
                                        {
                                            continue;
                                        }

                                        try
                                        {
                                            using (
                                                Image orig =
                                                    Image.FromFile(
                                                        file))
                                            {
                                                int colWidth =
                                                    1000;

                                                double scale =
                                                    (double)colWidth /
                                                    Math.Max(
                                                        1,
                                                        orig.Width);

                                                int drawW =
                                                    colWidth;

                                                int drawH =
                                                    Math.Max(
                                                        1,
                                                        (int)Math.Round(
                                                            orig.Height *
                                                            scale));

                                                string baseName =
                                                    Path.GetFileNameWithoutExtension(
                                                        file)
                                                        .ToUpperInvariant();

                                                int maxDisplayDim =
                                                    1200;

                                                int dispW =
                                                    Math.Max(
                                                        1,
                                                        orig.Width);

                                                int dispH =
                                                    Math.Max(
                                                        1,
                                                        orig.Height);

                                                if (dispW >
                                                        maxDisplayDim ||
                                                    dispH >
                                                        maxDisplayDim)
                                                {
                                                    double dispScale =
                                                        Math.Min(
                                                            (double)maxDisplayDim /
                                                            dispW,

                                                            (double)maxDisplayDim /
                                                            dispH);

                                                    dispW =
                                                        Math.Max(
                                                            1,
                                                            (int)Math.Round(
                                                                dispW *
                                                                dispScale));

                                                    dispH =
                                                        Math.Max(
                                                            1,
                                                            (int)Math.Round(
                                                                dispH *
                                                                dispScale));
                                                }

                                                Bitmap displayBmp =
                                                    new Bitmap(
                                                        dispW,
                                                        dispH,
                                                        PixelFormat
                                                            .Format32bppPArgb);

                                                using (
                                                    Graphics gDisp =
                                                        Graphics.FromImage(
                                                            displayBmp))
                                                {
                                                    gDisp.Clear(
                                                        Color.White);

                                                    gDisp.InterpolationMode =
                                                        InterpolationMode
                                                            .Low;

                                                    gDisp.PixelOffsetMode =
                                                        PixelOffsetMode
                                                            .HighSpeed;

                                                    gDisp.DrawImage(
                                                        orig,
                                                        0,
                                                        0,
                                                        dispW,
                                                        dispH);
                                                }

                                                CanvasItem newItem =
                                                    new CanvasItem
                                                    {
                                                        FilePath =
                                                            file,

                                                        Img =
                                                            displayBmp,

                                                        X =
                                                            pageX,

                                                        Y =
                                                            pageY,

                                                        Width =
                                                            drawW,

                                                        Height =
                                                            drawH,

                                                        OriginalAspect =
                                                            (double)drawW /
                                                            drawH,

                                                        Rotation =
                                                            0,

                                                        TextTemplate =
                                                            baseName +
                                                            " {W} X {H} MM",

                                                        ShowText =
                                                            true,

                                                        ItemFont =
                                                            CreateFontSafe(
                                                                importFontName,
                                                                14f,
                                                                importFontStyle)
                                                    };

                                                batch.Items.Add(
                                                    newItem);
                                            }
                                        }
                                        catch
                                        {
                                            batch.FailedFiles.Add(
                                                file);
                                        }
                                    }

                                    return batch;
                                });

                        foreach (
                            CanvasItem newItem
                            in result.Items)
                        {
                            if (newItem.Img != null)
                            {
                                ownedImages.Add(
                                    newItem.Img);
                            }
                        }

                        if (result.Items.Count >
                            0)
                        {
                            items.AddRange(
                                result.Items);

                            string layout =
                                cmbLayout.SelectedItem
                                    ?.ToString()
                                ?? "Free Style";

                            if (layout ==
                                "Free Style")
                            {
                                suppressLayoutEvent =
                                    true;

                                cmbLayout.SelectedIndex =
                                    1;

                                suppressLayoutEvent =
                                    false;

                                ApplyLayout(
                                    "3-Lane Grid");
                            }
                            else
                            {
                                ApplyLayout(
                                    layout);
                            }
                        }

                        if (result.FailedFiles.Count >
                            0)
                        {
                            string names =
                                string.Join(
                                    Environment.NewLine,

                                    result.FailedFiles
                                        .Take(10)
                                        .Select(
                                            Path.GetFileName));

                            string additional =
                                result.FailedFiles.Count >
                                10
                                    ? Environment.NewLine +
                                      "...and " +
                                      (result.FailedFiles.Count -
                                       10) +
                                      " more."
                                    : string.Empty;

                            MessageBox.Show(
                                "Some images could not be loaded:" +
                                Environment.NewLine +
                                Environment.NewLine +
                                names +
                                additional,

                                "Import Warning",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            "Unexpected image import error:" +
                            Environment.NewLine +
                            ex.Message,

                            "Import Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                    finally
                    {
                        Cursor =
                            Cursors.Default;

                        pbCanvas.Cursor =
                            Cursors.Default;
                    }
                };

            btnAddImage.Click +=
                async (s, e) =>
                {
                    using (
                        OpenFileDialog ofd =
                            new OpenFileDialog())
                    {
                        ofd.Multiselect = true;

                        ofd.Filter =
                            "Images|*.jpg;*.jpeg;*.png;*.bmp";

                        if (ofd.ShowDialog() ==
                            DialogResult.OK)
                        {
                            await ImportFilesAsync(
                                ofd.FileNames);
                        }
                    }
                };

            // ====================================================
            // LOAD NPPL
            // ====================================================

            btnLoadNppl.Click +=
                async (s, e) =>
                {
                    using (
                        OpenFileDialog ofd =
                            new OpenFileDialog())
                    {
                        ofd.Filter =
                            "NPPL Project|*.nppl";

                        // Loading an NPPL must never change the folder used
                        // later by Save Final BMP.
                        ofd.RestoreDirectory = true;

                        if (ofd.ShowDialog() !=
                            DialogResult.OK)
                        {
                            return;
                        }

                        try
                        {
                            // Existing project class.
                            WorkspaceData data =
                                WorkspaceEngine.LoadFromFile(
                                    ofd.FileName);

                            List<string> allFiles =
                                new List<string>();

                            if (data.Lane1Files != null)
                            {
                                allFiles.AddRange(
                                    data.Lane1Files);
                            }

                            if (data.Lane2Files != null)
                            {
                                allFiles.AddRange(
                                    data.Lane2Files);
                            }

                            if (data.Lane3Files != null)
                            {
                                allFiles.AddRange(
                                    data.Lane3Files);
                            }

                            if (allFiles.Count >
                                0)
                            {
                                await ImportFilesAsync(
                                    allFiles.ToArray());
                            }
                            else
                            {
                                MessageBox.Show(
                                    "The loaded .nppl file contains no images.",
                                    "Empty Workspace",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Information);
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(
                                "Error loading .nppl file:" +
                                Environment.NewLine +
                                ex.Message,

                                "Import Error",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                        }
                    }
                };

            // ====================================================
            // DRAG & DROP
            // ====================================================

            DragEventHandler dragEnter =
                (s, e) =>
                {
                    if (e.Data != null &&
                        e.Data.GetDataPresent(
                            DataFormats.FileDrop))
                    {
                        e.Effect =
                            DragDropEffects.Copy;
                    }
                };

            DragEventHandler dragDrop =
                async (s, e) =>
                {
                    if (e.Data == null)
                        return;

                    string[] files =
                        e.Data.GetData(
                            DataFormats.FileDrop)
                        as string[];

                    if (files != null)
                    {
                        await ImportFilesAsync(
                            files);
                    }
                };

            pbCanvas.DragEnter += dragEnter;
            pbCanvas.DragDrop += dragDrop;

            scrollPanel.DragEnter += dragEnter;
            scrollPanel.DragDrop += dragDrop;

            DragEnter += dragEnter;
            DragDrop += dragDrop;

            // ====================================================
            // PAINT ENGINE
            // ====================================================

            pbCanvas.Paint +=
                (s, ev) =>
                {
                    Graphics g =
                        ev.Graphics;

                    // ClipRectangle is physical/screen pixels.
                    RectangleF logicalClip =
                        new RectangleF(
                            ev.ClipRectangle.X /
                            zoom,

                            ev.ClipRectangle.Y /
                            zoom,

                            ev.ClipRectangle.Width /
                            zoom,

                            ev.ClipRectangle.Height /
                            zoom);

                    g.ScaleTransform(
                        zoom,
                        zoom);

                    bool interactionMode =
                        isDragging ||
                        isResizing ||
                        isPanning ||
                        isMarqueeSelecting;

                    if (interactionMode)
                    {
                        g.InterpolationMode =
                            InterpolationMode.Low;

                        g.SmoothingMode =
                            SmoothingMode.HighSpeed;

                        g.PixelOffsetMode =
                            PixelOffsetMode.HighSpeed;

                        g.CompositingQuality =
                            CompositingQuality.HighSpeed;
                    }
                    else
                    {
                        g.InterpolationMode =
                            InterpolationMode
                                .HighQualityBicubic;

                        g.SmoothingMode =
                            SmoothingMode.HighQuality;

                        g.PixelOffsetMode =
                            PixelOffsetMode.HighQuality;

                        g.CompositingQuality =
                            CompositingQuality.HighQuality;
                    }

                    g.FillRectangle(
                        shadowBrush,
                        pageX + 20,
                        pageY + 20,
                        pageWidth,
                        pageHeight);

                    g.FillRectangle(
                        Brushes.White,
                        pageX,
                        pageY,
                        pageWidth,
                        pageHeight);

                    g.DrawRectangle(
                        pageBorderPen,
                        pageX,
                        pageY,
                        pageWidth,
                        pageHeight);

                    int minX = int.MaxValue;
                    int minY = int.MaxValue;

                    int maxX = int.MinValue;
                    int maxY = int.MinValue;

                    bool hasPrintableItems =
                        false;

                    using (
                        StringFormat sf =
                            CreateCenteredStringFormat())
                    {
                        foreach (
                            CanvasItem item
                            in items)
                        {
                            int totalItemHeight =
                                GetTotalItemHeight(
                                    item);

                            Rectangle itemBounds =
                                new Rectangle(
                                    item.X,
                                    item.Y,
                                    item.Width,
                                    totalItemHeight);

                            bool intersectsPage =
                                item.X +
                                    item.Width >
                                pageX &&

                                item.X <
                                pageX +
                                pageWidth &&

                                item.Y +
                                    totalItemHeight >
                                pageY &&

                                item.Y <
                                pageY +
                                pageHeight;

                            if (intersectsPage)
                            {
                                hasPrintableItems =
                                    true;

                                // The green preview boundary is always
                                // clipped to the white export island.
                                minX =
                                    Math.Min(
                                        minX,
                                        Math.Max(
                                            pageX,
                                            item.X));

                                minY =
                                    Math.Min(
                                        minY,
                                        Math.Max(
                                            pageY,
                                            item.Y));

                                maxX =
                                    Math.Max(
                                        maxX,
                                        Math.Min(
                                            pageX + pageWidth,
                                            item.X + item.Width));

                                maxY =
                                    Math.Max(
                                        maxY,
                                        Math.Min(
                                            pageY + pageHeight,
                                            item.Y + totalItemHeight));
                            }

                            // Performance:
                            // Skip expensive drawing for items
                            // outside the current viewport.
                            if (!logicalClip.IntersectsWith(
                                itemBounds))
                            {
                                continue;
                            }

                            if (item.Img != null)
                            {
                                DrawImageWithRotation(
                                    g,
                                    item.Img,
                                    new Rectangle(
                                        item.X,
                                        item.Y,
                                        item.Width,
                                        item.Height),
                                    item.Rotation);
                            }

                            if (item.ShowText)
                            {
                                DrawItemText(
                                    g,
                                    item,
                                    sf);
                            }

                            if (selectedItems.Contains(
                                item))
                            {
                                g.DrawRectangle(
                                    selectionPen,
                                    item.X,
                                    item.Y,
                                    item.Width,
                                    totalItemHeight);

                                Rectangle drawHandle =
                                    new Rectangle(
                                        item.X +
                                        item.Width -
                                        15,

                                        item.Y +
                                        totalItemHeight -
                                        15,

                                        30,
                                        30);

                                g.FillRectangle(
                                    Brushes.DodgerBlue,
                                    drawHandle);

                                g.DrawRectangle(
                                    Pens.White,
                                    drawHandle);
                            }
                        }
                    }

                    if (hasPrintableItems)
                    {
                        // Keep the full 12px green stroke inside the
                        // white page rather than letting half of the pen
                        // bleed into the gray workbench.
                        const int exportStrokeInset = 6;

                        int exportLeft =
                            Math.Max(
                                pageX + exportStrokeInset,
                                minX - 60);

                        int exportTop =
                            Math.Max(
                                pageY + exportStrokeInset,
                                minY - 60);

                        int exportRight =
                            Math.Min(
                                pageX + pageWidth - exportStrokeInset,
                                maxX + 60);

                        int exportBottom =
                            Math.Min(
                                pageY + pageHeight - exportStrokeInset,
                                maxY + 60);

                        if (exportRight > exportLeft &&
                            exportBottom > exportTop)
                        {
                            g.DrawRectangle(
                                exportBorderPen,
                                exportLeft,
                                exportTop,
                                exportRight - exportLeft,
                                exportBottom - exportTop);
                        }
                    }

                    if (isDragging &&
                        snapLineX.HasValue &&
                        selectedItems.Count ==
                        1)
                    {
                        g.DrawLine(
                            snapLinePen,

                            snapLineX.Value,
                            0,

                            snapLineX.Value,
                            workspaceHeight);
                    }

                    if (isDragging &&
                        snapLineY.HasValue &&
                        selectedItems.Count ==
                        1)
                    {
                        g.DrawLine(
                            snapLinePen,

                            0,
                            snapLineY.Value,

                            workspaceWidth,
                            snapLineY.Value);
                    }

                    if (isMarqueeSelecting)
                    {
                        Rectangle rect =
                            CreateNormalizedRectangle(
                                marqueeStart,
                                marqueeEnd);

                        g.FillRectangle(
                            marqueeBrush,
                            rect);

                        g.DrawRectangle(
                            marqueePen,
                            rect);
                    }

                    UpdateLiveOutputInfo();
                };

            // ====================================================
            // TOOLBAR STATE
            // ====================================================

            Action UpdateToolbar =
                () =>
                {
                    isUpdatingUI = true;

                    try
                    {
                        // Keep these tools visible and enabled from startup.
                        // Their click handlers already safely return when
                        // the current selection is not valid.
                        btnRotate.Enabled =
                            true;

                        btnToggleText.Enabled =
                            true;

                        btnEditText.Enabled =
                            true;

                        btnRotate.Visible =
                            true;

                        btnToggleText.Visible =
                            true;

                        btnEditText.Visible =
                            true;

                        if (selectedItems.Count ==
                            1)
                        {
                            CanvasItem item =
                                selectedItems[0];

                            Font font =
                                item.ItemFont;

                            string fontName =
                                font != null
                                    ? font.FontFamily.Name
                                    : "Calibri";

                            if (!cmbFont.Items.Contains(
                                fontName))
                            {
                                cmbFont.Items.Add(
                                    fontName);
                            }

                            cmbFont.SelectedItem =
                                fontName;

                            btnBold.Checked =
                                font != null &&
                                font.Bold;

                            btnItalic.Checked =
                                font != null &&
                                font.Italic;

                            txtQuickText.Text =
                                item.TextTemplate ??
                                string.Empty;

                            object matchingQuickText =
                                cmbTemplate.Items
                                    .Cast<object>()
                                    .FirstOrDefault(
                                        option =>
                                        {
                                            CustomTextAsset asset =
                                                option as CustomTextAsset;

                                            return asset != null &&
                                                string.Equals(
                                                    asset.Content,
                                                    item.TextTemplate,
                                                    StringComparison.Ordinal);
                                        });

                            if (matchingQuickText != null)
                            {
                                cmbTemplate.SelectedItem =
                                    matchingQuickText;
                            }
                            else
                            {
                                cmbTemplate.SelectedIndex =
                                    0;
                            }
                        }
                        else if (
                            selectedItems.Count >
                            1)
                        {
                            txtQuickText.Text =
                                "[Multiple Items Selected]";
                        }
                        else
                        {
                            txtQuickText.Clear();
                        }
                    }
                    finally
                    {
                        isUpdatingUI =
                            false;
                    }
                };

            // ====================================================
            // APPLY FONT
            // ====================================================

            Action ApplyFont =
                () =>
                {
                    if (isUpdatingUI ||
                        selectedItems.Count ==
                        0)
                    {
                        return;
                    }

                    FontStyle style =
                        FontStyle.Regular;

                    if (btnBold.Checked)
                    {
                        style |=
                            FontStyle.Bold;
                    }

                    if (btnItalic.Checked)
                    {
                        style |=
                            FontStyle.Italic;
                    }

                    string fontName =
                        cmbFont.SelectedItem
                            ?.ToString();

                    if (string.IsNullOrWhiteSpace(
                        fontName))
                    {
                        fontName =
                            "Calibri";
                    }

                    foreach (
                        CanvasItem item
                        in selectedItems)
                    {
                        Font newFont =
                            CreateFontSafe(
                                fontName,
                                14f,
                                style);

                        Font oldFont =
                            item.ItemFont;

                        item.ItemFont =
                            newFont;

                        if (oldFont != null)
                        {
                            try
                            {
                                oldFont.Dispose();
                            }
                            catch
                            {
                            }
                        }
                    }

                    lastUsedFontName =
                        fontName;

                    lastUsedFontStyle =
                        style;

                    pbCanvas.Invalidate();
                };

            cmbTemplate.SelectedIndexChanged +=
                (s, e) =>
                {
                    if (isUpdatingUI ||
                        selectedItems.Count ==
                        0 ||
                        cmbTemplate.SelectedIndex ==
                        0)
                    {
                        return;
                    }

                    object selectedTemplate =
                        cmbTemplate.SelectedItem;

                    string template =
                        selectedTemplate is CustomTextAsset quickTextAsset
                            ? quickTextAsset.Content ?? string.Empty
                            : selectedTemplate?.ToString() ?? string.Empty;

                    foreach (
                        CanvasItem item
                        in selectedItems)
                    {
                        item.TextTemplate =
                            template;

                        item.ShowText = true;
                    }

                    SaveUndoState();
                    pbCanvas.Invalidate();
                };

            Func<string, string> PromptText =
                currentText =>
                {
                    using (
                        Form prompt =
                            new Form())
                    {
                        prompt.Width =
                            450;

                        prompt.Height =
                            200;

                        prompt.FormBorderStyle =
                            FormBorderStyle.FixedDialog;

                        prompt.Text =
                            "Edit Text";

                        prompt.StartPosition =
                            FormStartPosition.CenterParent;

                        prompt.MaximizeBox =
                            false;

                        prompt.MinimizeBox =
                            false;

                        Label lbl =
                            new Label
                            {
                                Left = 20,
                                Top = 20,

                                Text =
                                    "Enter new text (Use {W} and {H} for auto-sizes):",

                                AutoSize = true
                            };

                        TextBox tb =
                            new TextBox
                            {
                                Left = 20,
                                Top = 45,
                                Width = 390,
                                Height = 60,

                                Multiline = true,

                                Text =
                                    currentText ??
                                    string.Empty
                            };

                        Button btnOk =
                            new Button
                            {
                                Text = "Save",
                                Left = 310,
                                Top = 115,
                                Width = 100,

                                DialogResult =
                                    DialogResult.OK
                            };

                        Button btnCancel =
                            new Button
                            {
                                Text = "Cancel",
                                Left = 200,
                                Top = 115,
                                Width = 100,

                                DialogResult =
                                    DialogResult.Cancel
                            };

                        prompt.Controls.AddRange(
                            new Control[]
                            {
                                lbl,
                                tb,
                                btnOk,
                                btnCancel
                            });

                        prompt.AcceptButton =
                            btnOk;

                        prompt.CancelButton =
                            btnCancel;

                        if (prompt.ShowDialog(this) ==
                            DialogResult.OK)
                        {
                            return tb.Text;
                        }

                        return null;
                    }
                };

            cmbFont.SelectedIndexChanged +=
                (s, e) =>
                {
                    if (isUpdatingUI ||
                        selectedItems.Count ==
                        0)
                    {
                        return;
                    }

                    ApplyFont();
                    SaveUndoState();
                };

            btnBold.Click +=
                (s, e) =>
                {
                    btnBold.Checked =
                        !btnBold.Checked;

                    ApplyFont();
                    SaveUndoState();
                };

            btnItalic.Click +=
                (s, e) =>
                {
                    btnItalic.Checked =
                        !btnItalic.Checked;

                    ApplyFont();
                    SaveUndoState();
                };

            // ====================================================
            // ROTATE
            // ====================================================

            btnRotate.Click +=
                (s, e) =>
                {
                    if (selectedItems.Count ==
                        0)
                    {
                        return;
                    }

                    foreach (
                        CanvasItem item
                        in selectedItems)
                    {
                        item.Rotation =
                            NormalizeRotation(
                                item.Rotation +
                                90);

                        int temp =
                            item.Width;

                        item.Width =
                            item.Height;

                        item.Height =
                            temp;

                        if (item.Height >
                            0)
                        {
                            item.OriginalAspect =
                                (double)item.Width /
                                item.Height;
                        }
                    }

                    SaveUndoState();
                    pbCanvas.Invalidate();
                };

            // ====================================================
            // TEXT VISIBILITY
            // ====================================================

            btnToggleText.Click +=
                (s, e) =>
                {
                    if (selectedItems.Count ==
                        0)
                    {
                        return;
                    }

                    bool anyHidden =
                        selectedItems.Any(
                            i => !i.ShowText);

                    foreach (
                        CanvasItem item
                        in selectedItems)
                    {
                        item.ShowText =
                            anyHidden;
                    }

                    SaveUndoState();
                    pbCanvas.Invalidate();
                };

            // ====================================================
            // EDIT TEXT
            // ====================================================

            btnEditText.Click +=
                (s, e) =>
                {
                    if (selectedItems.Count !=
                        1)
                    {
                        return;
                    }

                    CanvasItem item =
                        selectedItems[0];

                    string edited =
                        PromptText(
                            item.TextTemplate);

                    if (edited == null)
                        return;

                    if (edited ==
                        item.TextTemplate)
                    {
                        return;
                    }

                    item.TextTemplate =
                        edited;

                    SaveUndoState();
                    UpdateToolbar();
                    pbCanvas.Invalidate();
                };

            // ====================================================
            // ZOOM
            // ====================================================

            MouseEventHandler ZoomHandler =
                (s, ev) =>
                {
                    if ((Control.ModifierKeys &
                         Keys.Control) == 0 ||
                        ev.Delta == 0)
                    {
                        return;
                    }

                    float oldZoom = zoom;

                    // Multiplicative zoom feels much smoother than adding a
                    // fixed amount. It also gives finer control when zoomed
                    // out and avoids large jumps around the fit-to-screen
                    // range.
                    double wheelSteps =
                        ev.Delta / 120.0;

                    double zoomFactor =
                        Math.Pow(
                            1.12,
                            wheelSteps);

                    zoom =
                        (float)Math.Max(
                            0.03,
                            Math.Min(
                                4.0,
                                oldZoom *
                                zoomFactor));

                    if (Math.Abs(
                            zoom -
                            oldZoom) >
                        0.00001f)
                    {
                        // Anchor zoom to the physical mouse position. The
                        // logical point underneath the cursor stays under
                        // the cursor after the zoom.
                        Point mousePos =
                            scrollPanel.PointToClient(
                                Control.MousePosition);

                        int oldScrollX =
                            Math.Abs(
                                scrollPanel
                                    .AutoScrollPosition
                                    .X);

                        int oldScrollY =
                            Math.Abs(
                                scrollPanel
                                    .AutoScrollPosition
                                    .Y);

                        double logicalX =
                            (oldScrollX +
                             mousePos.X) /
                            oldZoom;

                        double logicalY =
                            (oldScrollY +
                             mousePos.Y) /
                            oldZoom;

                        UpdateCanvasPhysicalSize();

                        int newScrollX =
                            (int)Math.Round(
                                logicalX *
                                zoom -
                                mousePos.X);

                        int newScrollY =
                            (int)Math.Round(
                                logicalY *
                                zoom -
                                mousePos.Y);

                        scrollPanel.AutoScrollPosition =
                            new Point(
                                Math.Max(
                                    0,
                                    newScrollX),

                                Math.Max(
                                    0,
                                    newScrollY));

                        pbCanvas.Invalidate();
                    }

                    if (ev is HandledMouseEventArgs handled)
                    {
                        handled.Handled = true;
                    }
                };

            // Let the scroll panel own Ctrl+wheel. This avoids the same
            // wheel gesture being handled more than once by child/form
            // MouseWheel subscriptions.
            scrollPanel.CtrlMouseWheel +=
                ZoomHandler;

            // ====================================================
            // MOUSE DOWN
            // ====================================================

            pbCanvas.MouseDown +=
                (s, ev) =>
                {
                    FocusCanvasViewport();

                    if (isSpaceDown &&
                        ev.Button ==
                        MouseButtons.Left)
                    {
                        isPanning =
                            true;

                        panStartPoint =
                            Control.MousePosition;

                        return;
                    }

                    int mx =
                        (int)(ev.X /
                              zoom);

                    int my =
                        (int)(ev.Y /
                              zoom);

                    CanvasItem clickedItem =
                        null;

                    for (
                        int i =
                            items.Count - 1;
                        i >= 0;
                        i--)
                    {
                        CanvasItem item =
                            items[i];

                        Rectangle hitRect =
                            new Rectangle(
                                item.X,
                                item.Y,
                                item.Width,
                                GetTotalItemHeight(
                                    item));

                        if (hitRect.Contains(
                            mx,
                            my))
                        {
                            clickedItem =
                                item;

                            break;
                        }
                    }

                    if (clickedItem !=
                        null)
                    {
                        int totalHeight =
                            GetTotalItemHeight(
                                clickedItem);

                        Rectangle resizeHitZone =
                            new Rectangle(
                                clickedItem.X +
                                clickedItem.Width -
                                40,

                                clickedItem.Y +
                                totalHeight -
                                40,

                                80,
                                80);

                        if (selectedItems.Count ==
                                1 &&
                            clickedItem ==
                                selectedItems[0] &&
                            resizeHitZone.Contains(
                                mx,
                                my))
                        {
                            isResizing =
                                true;

                            didResizeMove =
                                false;

                            return;
                        }

                        if ((Control.ModifierKeys &
                             Keys.Control) !=
                            0)
                        {
                            if (selectedItems.Contains(
                                clickedItem))
                            {
                                selectedItems.Remove(
                                    clickedItem);
                            }
                            else
                            {
                                selectedItems.Add(
                                    clickedItem);
                            }
                        }
                        else
                        {
                            if (!selectedItems.Contains(
                                clickedItem))
                            {
                                selectedItems.Clear();

                                selectedItems.Add(
                                    clickedItem);
                            }
                        }

                        isDragging =
                            true;

                        didDragMove =
                            false;

                        // Store the original screen/picture-box pixel point.
                        // We only begin moving after the mouse travels a few
                        // real pixels, so a normal selection click is safe.
                        dragStartPhysical =
                            ev.Location;

                        lastMousePos =
                            new Point(
                                mx,
                                my);
                    }
                    else
                    {
                        if ((Control.ModifierKeys &
                             Keys.Control) ==
                            0)
                        {
                            selectedItems.Clear();
                        }

                        isMarqueeSelecting =
                            true;

                        marqueeStart =
                            new Point(
                                mx,
                                my);

                        marqueeEnd =
                            marqueeStart;
                    }

                    UpdateToolbar();
                    pbCanvas.Invalidate();
                };

            // ====================================================
            // DOUBLE CLICK TEXT
            // ====================================================

            pbCanvas.MouseDoubleClick +=
                (s, ev) =>
                {
                    int mx =
                        (int)(ev.X /
                              zoom);

                    int my =
                        (int)(ev.Y /
                              zoom);

                    for (
                        int i =
                            items.Count - 1;
                        i >= 0;
                        i--)
                    {
                        CanvasItem item =
                            items[i];

                        if (!item.ShowText)
                            continue;

                        Rectangle textRect =
                            GetTextRectangle(
                                item);

                        if (!textRect.Contains(
                            mx,
                            my))
                        {
                            continue;
                        }

                        string edited =
                            PromptText(
                                item.TextTemplate);

                        if (edited == null ||
                            edited ==
                                item.TextTemplate)
                        {
                            return;
                        }

                        item.TextTemplate =
                            edited;

                        SaveUndoState();

                        if (selectedItems.Contains(
                            item))
                        {
                            UpdateToolbar();
                        }

                        pbCanvas.Invalidate();

                        return;
                    }
                };

            // ====================================================
            // MOUSE MOVE
            // ====================================================

            pbCanvas.MouseMove +=
                (s, ev) =>
                {
                    if (isPanning)
                    {
                        Point currentScreenPos =
                            Control.MousePosition;

                        int dx =
                            currentScreenPos.X -
                            panStartPoint.X;

                        int dy =
                            currentScreenPos.Y -
                            panStartPoint.Y;

                        if (dx != 0 ||
                            dy != 0)
                        {
                            Point currentScroll =
                                scrollPanel
                                    .AutoScrollPosition;

                            int currentX =
                                Math.Abs(
                                    currentScroll.X);

                            int currentY =
                                Math.Abs(
                                    currentScroll.Y);

                            scrollPanel.AutoScrollPosition =
                                new Point(
                                    Math.Max(
                                        0,
                                        currentX -
                                        dx),

                                    Math.Max(
                                        0,
                                        currentY -
                                        dy));

                            EnsureInfiniteWorkbench();

                            panStartPoint =
                                currentScreenPos;
                        }

                        return;
                    }

                    int mx =
                        (int)(ev.X /
                              zoom);

                    int my =
                        (int)(ev.Y /
                              zoom);

                    if (isMarqueeSelecting)
                    {
                        marqueeEnd =
                            new Point(
                                mx,
                                my);

                        pbCanvas.Invalidate();

                        return;
                    }

                    if (selectedItems.Count ==
                        0)
                    {
                        return;
                    }

                    // =================================================
                    // DRAGGING
                    // =================================================

                    if (isDragging)
                    {
                        // A click often produces a tiny MouseMove event.
                        // Do not move the item until the pointer has travelled
                        // a deliberate number of physical pixels.
                        if (!didDragMove)
                        {
                            int physicalDx =
                                ev.X -
                                dragStartPhysical.X;

                            int physicalDy =
                                ev.Y -
                                dragStartPhysical.Y;

                            int distanceSquared =
                                physicalDx *
                                physicalDx +
                                physicalDy *
                                physicalDy;

                            int thresholdSquared =
                                DragStartThresholdPixels *
                                DragStartThresholdPixels;

                            if (distanceSquared <
                                thresholdSquared)
                            {
                                return;
                            }

                            // Start the drag from the current point instead
                            // of applying all of the small pre-drag jitter.
                            // The next MouseMove performs the first movement.
                            didDragMove = true;

                            lastMousePos =
                                new Point(
                                    mx,
                                    my);

                            return;
                        }

                        int dx =
                            mx -
                            lastMousePos.X;

                        int dy =
                            my -
                            lastMousePos.Y;

                        if (dx == 0 &&
                            dy == 0)
                        {
                            return;
                        }

                        string layout =
                            cmbLayout.SelectedItem
                                ?.ToString()
                            ?? "Free Style";

                        if (selectedItems.Count ==
                                1 &&
                            layout ==
                                "Free Style")
                        {
                            snapLineX =
                                null;

                            snapLineY =
                                null;

                            CanvasItem mainItem =
                                selectedItems[0];

                            int targetX =
                                mainItem.X +
                                dx;

                            int targetY =
                                mainItem.Y +
                                dy;

                            int logicalSnapDistance =
                                Math.Max(
                                    1,

                                    (int)Math.Round(
                                        SnapScreenPixels /
                                        Math.Max(
                                            0.05f,
                                            zoom)));

                            foreach (
                                CanvasItem other
                                in items)
                            {
                                if (selectedItems.Contains(
                                    other))
                                {
                                    continue;
                                }

                                // LEFT EDGE
                                if (Math.Abs(
                                        targetX -
                                        other.X) <
                                    logicalSnapDistance)
                                {
                                    targetX =
                                        other.X;

                                    snapLineX =
                                        other.X;

                                    dx =
                                        targetX -
                                        mainItem.X;
                                }

                                // RIGHT EDGE
                                else if (
                                    Math.Abs(
                                        targetX +
                                        mainItem.Width -
                                        (other.X +
                                         other.Width)) <
                                    logicalSnapDistance)
                                {
                                    targetX =
                                        other.X +
                                        other.Width -
                                        mainItem.Width;

                                    snapLineX =
                                        other.X +
                                        other.Width;

                                    dx =
                                        targetX -
                                        mainItem.X;
                                }

                                // TOP EDGE
                                if (Math.Abs(
                                        targetY -
                                        other.Y) <
                                    logicalSnapDistance)
                                {
                                    targetY =
                                        other.Y;

                                    snapLineY =
                                        other.Y;

                                    dy =
                                        targetY -
                                        mainItem.Y;
                                }

                                // BOTTOM EDGE
                                else if (
                                    Math.Abs(
                                        targetY +
                                        GetTotalItemHeight(
                                            mainItem) -
                                        (other.Y +
                                         GetTotalItemHeight(
                                             other))) <
                                    logicalSnapDistance)
                                {
                                    targetY =
                                        other.Y +
                                        GetTotalItemHeight(
                                            other) -
                                        GetTotalItemHeight(
                                            mainItem);

                                    snapLineY =
                                        other.Y +
                                        GetTotalItemHeight(
                                            other);

                                    dy =
                                        targetY -
                                        mainItem.Y;
                                }
                            }
                        }

                        foreach (
                            CanvasItem item
                            in selectedItems)
                        {
                            item.X += dx;
                            item.Y += dy;
                        }

                        didDragMove =
                            true;

                        lastMousePos =
                            new Point(
                                mx,
                                my);

                        pbCanvas.Invalidate();
                    }

                    // =================================================
                    // RESIZING
                    // =================================================

                    else if (
                        isResizing &&
                        selectedItems.Count ==
                        1)
                    {
                        CanvasItem item =
                            selectedItems[0];

                        int newWidth =
                            Math.Max(
                                MinimumItemWidth,

                                mx -
                                item.X);

                        if (newWidth ==
                            item.Width)
                        {
                            return;
                        }

                        item.Width =
                            newWidth;

                        double aspect =
                            item.OriginalAspect;

                        if (aspect <= 0)
                            aspect = 1;

                        item.Height =
                            Math.Max(
                                1,

                                (int)Math.Round(
                                    newWidth /
                                    aspect));

                        didResizeMove =
                            true;

                        pbCanvas.Invalidate();
                    }
                };

            // ====================================================
            // MOUSE UP
            // ====================================================

            pbCanvas.MouseUp +=
                (s, ev) =>
                {
                    isPanning =
                        false;

                    if (isMarqueeSelecting)
                    {
                        Rectangle marqueeRect =
                            CreateNormalizedRectangle(
                                marqueeStart,
                                marqueeEnd);

                        foreach (
                            CanvasItem item
                            in items)
                        {
                            Rectangle itemRect =
                                new Rectangle(
                                    item.X,
                                    item.Y,
                                    item.Width,
                                    GetTotalItemHeight(
                                        item));

                            if (marqueeRect.IntersectsWith(
                                    itemRect) &&
                                !selectedItems.Contains(
                                    item))
                            {
                                selectedItems.Add(
                                    item);
                            }
                        }

                        isMarqueeSelecting =
                            false;

                        UpdateToolbar();
                    }

                    bool geometryChanged =
                        didDragMove ||
                        didResizeMove;

                    if (geometryChanged)
                    {
                        SaveUndoState();

                        string layout =
                            cmbLayout.SelectedItem
                                ?.ToString()
                            ?? "Free Style";

                        if (layout !=
                            "Free Style")
                        {
                            suppressLayoutEvent =
                                true;

                            cmbLayout.SelectedIndex =
                                0;

                            suppressLayoutEvent =
                                false;
                        }

                        EnsureInfiniteWorkbench();
                    }

                    isDragging =
                        false;

                    isResizing =
                        false;

                    didDragMove =
                        false;

                    didResizeMove =
                        false;

                    dragStartPhysical =
                        Point.Empty;

                    snapLineX =
                        null;

                    snapLineY =
                        null;

                    pbCanvas.Invalidate();
                };

            // ====================================================
            // KEYBOARD
            // ====================================================

            KeyDown +=
                (s, ev) =>
                {
                    // Let text fields and combo boxes use
                    // normal Ctrl+A/C/V/Z/Delete etc.
                    if (IsEditingControlFocused())
                    {
                        return;
                    }

                    // SPACE = PAN
                    if (ev.KeyCode ==
                        Keys.Space)
                    {
                        isSpaceDown =
                            true;

                        pbCanvas.Cursor =
                            Cursors.Hand;

                        ev.Handled =
                            true;

                        ev.SuppressKeyPress =
                            true;

                        return;
                    }

                    // CTRL+A
                    if (ev.Control &&
                        ev.KeyCode ==
                        Keys.A)
                    {
                        selectedItems =
                            items.ToList();

                        UpdateToolbar();
                        pbCanvas.Invalidate();

                        ev.Handled =
                            true;

                        ev.SuppressKeyPress =
                            true;

                        return;
                    }

                    // CTRL+Z
                    if (ev.Control &&
                        ev.KeyCode ==
                        Keys.Z)
                    {
                        UndoLastState();

                        UpdateToolbar();
                        pbCanvas.Invalidate();

                        ev.Handled =
                            true;

                        ev.SuppressKeyPress =
                            true;

                        return;
                    }

                    // CTRL+C
                    if (ev.Control &&
                        ev.KeyCode ==
                        Keys.C)
                    {
                        if (selectedItems.Count >
                            0)
                        {
                            clipboardItems =
                                selectedItems
                                    .Select(
                                        CanvasItemState
                                            .FromItem)
                                    .ToList();
                        }

                        ev.Handled =
                            true;

                        ev.SuppressKeyPress =
                            true;

                        return;
                    }

                    // CTRL+V
                    if (ev.Control &&
                        ev.KeyCode ==
                        Keys.V)
                    {
                        if (clipboardItems.Count >
                            0)
                        {
                            selectedItems.Clear();

                            foreach (
                                CanvasItemState state
                                in clipboardItems)
                            {
                                CanvasItem clone =
                                    state.ToCanvasItem();

                                clone.X += 50;
                                clone.Y += 50;

                                items.Add(
                                    clone);

                                selectedItems.Add(
                                    clone);
                            }

                            SaveUndoState();
                            UpdateToolbar();
                            pbCanvas.Invalidate();
                        }

                        ev.Handled =
                            true;

                        ev.SuppressKeyPress =
                            true;

                        return;
                    }

                    if (selectedItems.Count ==
                        0)
                    {
                        return;
                    }

                    // DELETE
                    if (ev.KeyCode ==
                        Keys.Delete)
                    {
                        foreach (
                            CanvasItem item
                            in selectedItems
                                .ToList())
                        {
                            items.Remove(
                                item);

                            DisposeItemFont(
                                item);
                        }

                        selectedItems.Clear();

                        SaveUndoState();

                        UpdateToolbar();
                        pbCanvas.Invalidate();

                        ev.Handled =
                            true;

                        ev.SuppressKeyPress =
                            true;

                        return;
                    }

                    int step =
                        ev.Shift
                            ? 10
                            : 1;

                    bool moved =
                        false;

                    if (ev.KeyCode ==
                        Keys.Up)
                    {
                        foreach (
                            CanvasItem item
                            in selectedItems)
                        {
                            item.Y -=
                                step;
                        }

                        moved = true;
                    }
                    else if (
                        ev.KeyCode ==
                        Keys.Down)
                    {
                        foreach (
                            CanvasItem item
                            in selectedItems)
                        {
                            item.Y +=
                                step;
                        }

                        moved = true;
                    }
                    else if (
                        ev.KeyCode ==
                        Keys.Left)
                    {
                        foreach (
                            CanvasItem item
                            in selectedItems)
                        {
                            item.X -=
                                step;
                        }

                        moved = true;
                    }
                    else if (
                        ev.KeyCode ==
                        Keys.Right)
                    {
                        foreach (
                            CanvasItem item
                            in selectedItems)
                        {
                            item.X +=
                                step;
                        }

                        moved = true;
                    }

                    if (moved)
                    {
                        keyboardMovePending =
                            true;

                        pbCanvas.Invalidate();

                        ev.Handled =
                            true;

                        ev.SuppressKeyPress =
                            true;
                    }
                };

            KeyUp +=
                (s, ev) =>
                {
                    if (ev.KeyCode ==
                        Keys.Space)
                    {
                        isSpaceDown =
                            false;

                        isPanning =
                            false;

                        pbCanvas.Cursor =
                            Cursors.Default;
                    }

                    bool arrowKey =
                        ev.KeyCode ==
                            Keys.Up ||
                        ev.KeyCode ==
                            Keys.Down ||
                        ev.KeyCode ==
                            Keys.Left ||
                        ev.KeyCode ==
                            Keys.Right;

                    if (arrowKey &&
                        keyboardMovePending)
                    {
                        keyboardMovePending =
                            false;

                        SaveUndoState();

                        string layout =
                            cmbLayout.SelectedItem
                                ?.ToString()
                            ?? "Free Style";

                        if (layout !=
                            "Free Style")
                        {
                            suppressLayoutEvent =
                                true;

                            cmbLayout.SelectedIndex =
                                0;

                            suppressLayoutEvent =
                                false;
                        }

                        EnsureInfiniteWorkbench();
                    }
                };

            // ====================================================
            // HIGH RESOLUTION 300 DPI EXPORT
            // ====================================================

            btnSave.Click +=
                (s, ev) =>
                {
                    List<CanvasItem> printableItems =
                        items
                            .Where(
                                ItemIntersectsPage)
                            .ToList();

                    if (printableItems.Count ==
                        0)
                    {
                        MessageBox.Show(
                            "There are no images inside the white canvas to save!" +
                            Environment.NewLine +
                            "(Items in the gray pasteboard are ignored).",

                            "Export Empty",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);

                        return;
                    }

                    int padding =
                        60;

                    int minX =
                        printableItems.Min(
                            i => i.X);

                    int minY =
                        printableItems.Min(
                            i => i.Y);

                    int maxX =
                        printableItems.Max(
                            i =>
                                i.X +
                                i.Width);

                    int maxY =
                        printableItems.Max(
                            i =>
                                i.Y +
                                GetTotalItemHeight(
                                    i));

                    int cropX =
                        Math.Max(
                            pageX,
                            minX -
                            padding);

                    int cropY =
                        Math.Max(
                            pageY,
                            minY -
                            padding);

                    int finalRight =
                        Math.Min(
                            pageX +
                            pageWidth,

                            maxX +
                            padding);

                    int finalBottom =
                        Math.Min(
                            pageY +
                            pageHeight,

                            maxY +
                            padding);

                    int finalW =
                        finalRight -
                        cropX;

                    int finalH =
                        finalBottom -
                        cropY;

                    if (finalW <= 0 ||
                        finalH <= 0)
                    {
                        MessageBox.Show(
                            "The printable area could not be calculated.",
                            "Export Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);

                        return;
                    }

                    using (
                        Bitmap finalBmp =
                            new Bitmap(
                                finalW,
                                finalH,
                                PixelFormat
                                    .Format24bppRgb))
                    {
                        finalBmp.SetResolution(
                            ExportDpi,
                            ExportDpi);

                        using (
                            Graphics g =
                                Graphics.FromImage(
                                    finalBmp))
                        {
                            g.Clear(
                                Color.White);

                            g.TranslateTransform(
                                -cropX,
                                -cropY);

                            /*
                             * Maximum-quality 1x export:
                             * Keep the original canvas pixel dimensions
                             * (small BMP), but resample original source
                             * artwork using the same smooth rendering
                             * approach as the old builder.
                             */
                            g.InterpolationMode =
                                InterpolationMode
                                    .HighQualityBicubic;

                            g.PixelOffsetMode =
                                PixelOffsetMode
                                    .HighQuality;

                            g.SmoothingMode =
                                SmoothingMode
                                    .HighQuality;

                            g.CompositingQuality =
                                CompositingQuality
                                    .HighQuality;

                            g.CompositingMode =
                                CompositingMode
                                    .SourceOver;

                            using (
                                Pen thickPen =
                                    new Pen(
                                        Color.Black,
                                        12))
                            {
                                g.DrawRectangle(
                                    thickPen,

                                    cropX + 10,
                                    cropY + 10,

                                    Math.Max(
                                        1,
                                        finalW -
                                        20),

                                    Math.Max(
                                        1,
                                        finalH -
                                        20));
                            }

                            using (
                                StringFormat sf =
                                    CreateCenteredStringFormat())
                            {
                                foreach (
                                    CanvasItem item
                                    in printableItems)
                                {
                                    bool drawnFromSource =
                                        false;

                                    if (!string.IsNullOrWhiteSpace(
                                            item.FilePath) &&
                                        File.Exists(
                                            item.FilePath))
                                    {
                                        try
                                        {
                                            using (
                                                Image highResImg =
                                                    Image.FromFile(
                                                        item.FilePath))
                                            {
                                                DrawImageWithRotation(
                                                    g,
                                                    highResImg,

                                                    new Rectangle(
                                                        item.X,
                                                        item.Y,
                                                        item.Width,
                                                        item.Height),

                                                    item.Rotation);

                                                drawnFromSource =
                                                    true;
                                            }
                                        }
                                        catch
                                        {
                                            drawnFromSource =
                                                false;
                                        }
                                    }

                                    if (!drawnFromSource &&
                                        item.Img != null)
                                    {
                                        DrawImageWithRotation(
                                            g,
                                            item.Img,

                                            new Rectangle(
                                                item.X,
                                                item.Y,
                                                item.Width,
                                                item.Height),

                                            item.Rotation);
                                    }

                                    if (item.ShowText)
                                    {
                                        DrawItemText(
                                            g,
                                            item,
                                            sf);
                                    }
                                }
                            }
                        }

                        using (
                            SaveFileDialog sfd =
                                new SaveFileDialog())
                        {
                            sfd.Filter =
                                "BMP Image|*.bmp";

                            sfd.FileName =
                                "JobCard_Final.bmp";

                            sfd.AddExtension = true;
                            sfd.DefaultExt = "bmp";
                            sfd.RestoreDirectory = true;

                            // Re-read the persisted setting at the exact time
                            // the Save dialog opens. This prevents an earlier
                            // OpenFileDialog (such as Load .nppl) from becoming
                            // the apparent save location.
                            AppSettings latestSettings =
                                SettingsManager.Load();

                            string rememberedSaveFolder =
                                initialSaveDirectory;

                            if (latestSettings != null &&
                                !string.IsNullOrWhiteSpace(
                                    latestSettings.LastJobCardSaveFolder) &&
                                Directory.Exists(
                                    latestSettings.LastJobCardSaveFolder))
                            {
                                rememberedSaveFolder =
                                    latestSettings.LastJobCardSaveFolder;
                            }
                            else if (appSettings != null &&
                                     !string.IsNullOrWhiteSpace(
                                         appSettings.LastJobCardSaveFolder) &&
                                     Directory.Exists(
                                         appSettings.LastJobCardSaveFolder))
                            {
                                rememberedSaveFolder =
                                    appSettings.LastJobCardSaveFolder;
                            }

                            if (!string.IsNullOrWhiteSpace(
                                    rememberedSaveFolder) &&
                                Directory.Exists(
                                    rememberedSaveFolder))
                            {
                                sfd.InitialDirectory =
                                    rememberedSaveFolder;
                            }

                            if (sfd.ShowDialog() ==
                                DialogResult.OK)
                            {
                                try
                                {
                                    finalBmp.Save(
                                        sfd.FileName,
                                        ImageFormat.Bmp);

                                    string savedBmpPath =
                                        sfd.FileName;

                                    try
                                    {
                                        lastSavedBmpBytes =
                                            new FileInfo(savedBmpPath).Length;
                                        lastSavedBmpWidth = finalBmp.Width;
                                        lastSavedBmpHeight = finalBmp.Height;
                                        UpdateLiveOutputInfo();
                                    }
                                    catch
                                    {
                                        lastSavedBmpBytes = -1;
                                    }

                                    string savedBmpFolder =
                                        Path.GetDirectoryName(
                                            savedBmpPath);

                                    // Remember the last final-BMP folder.
                                    if (appSettings != null &&
                                        !string.IsNullOrWhiteSpace(
                                            savedBmpFolder) &&
                                        Directory.Exists(
                                            savedBmpFolder))
                                    {
                                        appSettings.LastJobCardSaveFolder =
                                            savedBmpFolder;

                                        SettingsManager.Save(
                                            appSettings);
                                    }

                                    // Keep the freshly loaded settings object
                                    // synchronized too, so this same builder
                                    // instance remembers the new BMP folder.
                                    if (latestSettings != null &&
                                        !string.IsNullOrWhiteSpace(
                                            savedBmpFolder) &&
                                        Directory.Exists(
                                            savedBmpFolder))
                                    {
                                        latestSettings.LastJobCardSaveFolder =
                                            savedBmpFolder;

                                        SettingsManager.Save(
                                            latestSettings);
                                    }

                                    // Save the matching NPPL workspace from
                                    // the same Final BMP command.
                                    string userProfile =
                                        Environment.GetFolderPath(
                                            Environment.SpecialFolder
                                                .UserProfile);

                                    if (string.IsNullOrWhiteSpace(
                                            userProfile))
                                    {
                                        throw new InvalidOperationException(
                                            "Windows user profile folder could not be determined.");
                                    }

                                    string npplFolder =
                                        Path.Combine(
                                            userProfile,
                                            "Documents",
                                            "NPPLPrintMaster");

                                    Directory.CreateDirectory(
                                        npplFolder);

                                    string baseName =
                                        Path.GetFileNameWithoutExtension(
                                            savedBmpPath);

                                    if (string.IsNullOrWhiteSpace(
                                            baseName))
                                    {
                                        throw new InvalidOperationException(
                                            "Unable to determine the NPPL workspace filename.");
                                    }

                                    string npplPath =
                                        Path.Combine(
                                            npplFolder,
                                            baseName + ".nppl");

                                    WorkspaceData dataToSave =
                                        workspaceData;

                                    // Standalone Freeform Builder can still
                                    // save an NPPL by recording all current
                                    // source images when no Quick Job Card
                                    // workspace was supplied.
                                    if (dataToSave == null)
                                    {
                                        dataToSave =
                                            new WorkspaceData
                                            {
                                                Lane1Files =
                                                    items
                                                        .Where(
                                                            i =>
                                                                i != null &&
                                                                !string.IsNullOrWhiteSpace(
                                                                    i.FilePath))
                                                        .Select(
                                                            i => i.FilePath)
                                                        .Distinct(
                                                            StringComparer
                                                                .OrdinalIgnoreCase)
                                                        .ToList(),

                                                Lane2Files =
                                                    new List<string>(),

                                                Lane3Files =
                                                    new List<string>(),

                                                LayoutMemory =
                                                    new List<string>()
                                            };
                                    }

                                    WorkspaceEngine.SaveToFile(
                                        npplPath,
                                        dataToSave);

                                    if (!File.Exists(
                                            npplPath))
                                    {
                                        throw new IOException(
                                            "The NPPL workspace was not created. Expected file:" +
                                            Environment.NewLine +
                                            npplPath);
                                    }

                                    FileInfo npplInfo =
                                        new FileInfo(
                                            npplPath);

                                    if (npplInfo.Length <= 0)
                                    {
                                        throw new IOException(
                                            "The NPPL workspace was created but is empty:" +
                                            Environment.NewLine +
                                            npplPath);
                                    }

                                    Logger.LogAction(
                                        "FREEFORM_NPPL_AUTO_SAVE",
                                        "BMP: " +
                                        savedBmpPath +
                                        " | NPPL: " +
                                        npplPath);

                                    long fileBytes =
                                        new FileInfo(
                                            savedBmpPath)
                                            .Length;

                                    double fileMb =
                                        fileBytes /
                                        (1024d * 1024d);

                                    MessageBox.Show(
                                        "Saved successfully." +
                                        Environment.NewLine +
                                        Environment.NewLine +
                                        "Quality: HighQualityBicubic" +
                                        Environment.NewLine +
                                        "DPI: 300" +
                                        Environment.NewLine +
                                        "Output: " +
                                        finalW +
                                        " × " +
                                        finalH +
                                        " pixels" +
                                        Environment.NewLine +
                                        "File size: " +
                                        fileMb.ToString("0.0") +
                                        " MB" +
                                        Environment.NewLine +
                                        Environment.NewLine +
                                        "NPPL workspace:" +
                                        Environment.NewLine +
                                        npplPath,
                                        "Final BMP + NPPL Saved",
                                        MessageBoxButtons.OK,
                                        MessageBoxIcon.Information);
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show(
                                        "Unable to save BMP:" +
                                        Environment.NewLine +
                                        ex.Message,

                                        "Export Error",
                                        MessageBoxButtons.OK,
                                        MessageBoxIcon.Error);
                                }
                            }
                        }
                    }
                };

            // ====================================================
            // INITIAL ITEMS
            // ====================================================

            if (initialItems != null &&
                initialItems.Count >
                0)
            {
                foreach (
                    CanvasItem source
                    in initialItems)
                {
                    if (source == null)
                        continue;

                    if (source.Img != null)
                    {
                        ownedImages.Add(
                            source.Img);
                    }

                    string fontName =
                        source.ItemFont != null
                            ? source.ItemFont.FontFamily.Name
                            : lastUsedFontName;

                    FontStyle fontStyle =
                        source.ItemFont != null
                            ? source.ItemFont.Style
                            : lastUsedFontStyle;

                    CanvasItem clone =
                        new CanvasItem
                        {
                            FilePath =
                                source.FilePath,

                            Img =
                                source.Img,

                            X =
                                source.X +
                                pageX,

                            Y =
                                source.Y +
                                pageY,

                            Width =
                                source.Width,

                            Height =
                                source.Height,

                            OriginalAspect =
                                source.OriginalAspect >
                                0
                                    ? source.OriginalAspect
                                    : GetSafeAspect(
                                        source.Width,
                                        source.Height),

                            TextTemplate =
                                source.TextTemplate,

                            ShowText =
                                source.ShowText,

                            Rotation =
                                NormalizeRotation(
                                    source.Rotation),

                            ItemFont =
                                CreateFontSafe(
                                    fontName,
                                    14f,
                                    fontStyle)
                        };

                    items.Add(
                        clone);
                }

                string layout =
                    cmbLayout.SelectedItem
                        ?.ToString()
                    ?? "Free Style";

                if (layout !=
                    "Free Style")
                {
                    ApplyLayout(
                        layout);
                }
                else
                {
                    SaveUndoState();
                    pbCanvas.Invalidate();
                }
            }

            LoadFreeformShelf();
            UpdateWorkspaceExtent();
            SaveUndoState();
            UpdateToolbar();
        }

        // ========================================================
        // DRAW IMAGE WITH NON-DESTRUCTIVE ROTATION
        // ========================================================

        private static void DrawImageWithRotation(
            Graphics g,
            Image image,
            Rectangle destination,
            int rotation)
        {
            if (g == null ||
                image == null ||
                destination.Width <= 0 ||
                destination.Height <= 0)
            {
                return;
            }

            int normalized =
                NormalizeRotation(
                    rotation);

            if (normalized ==
                0)
            {
                g.DrawImage(
                    image,
                    destination);

                return;
            }

            GraphicsState state =
                g.Save();

            try
            {
                float centerX =
                    destination.X +
                    destination.Width /
                    2f;

                float centerY =
                    destination.Y +
                    destination.Height /
                    2f;

                g.TranslateTransform(
                    centerX,
                    centerY);

                g.RotateTransform(
                    normalized);

                if (normalized ==
                        90 ||
                    normalized ==
                        270)
                {
                    float preWidth =
                        destination.Height;

                    float preHeight =
                        destination.Width;

                    g.DrawImage(
                        image,

                        -preWidth / 2f,
                        -preHeight / 2f,

                        preWidth,
                        preHeight);
                }
                else
                {
                    g.DrawImage(
                        image,

                        -destination.Width /
                        2f,

                        -destination.Height /
                        2f,

                        destination.Width,
                        destination.Height);
                }
            }
            finally
            {
                g.Restore(
                    state);
            }
        }

        // ========================================================
        // TEXT DRAWING
        // ========================================================

        private void DrawItemText(
            Graphics g,
            CanvasItem item,
            StringFormat sf)
        {
            if (g == null ||
                item == null ||
                !item.ShowText)
            {
                return;
            }

            Rectangle textRect =
                GetTextRectangle(
                    item);

            if (textRect.Width <= 0 ||
                textRect.Height <= 0)
            {
                return;
            }

            g.DrawRectangle(
                textPen,
                textRect);

            string liveText =
                BuildLiveText(
                    item);

            if (string.IsNullOrEmpty(
                liveText))
            {
                return;
            }

            float maxTextW =
                Math.Max(
                    1f,
                    textRect.Width -
                    40f);

            float maxTextH =
                Math.Max(
                    1f,
                    textRect.Height -
                    20f);

            Font itemFont =
                item.ItemFont ??
                CreateFontSafe(
                    "Calibri",
                    14f,
                    FontStyle.Bold);

            bool temporaryItemFont =
                item.ItemFont ==
                null;

            try
            {
                using (
                    Font testFont =
                        CreateFontSafe(
                            itemFont.FontFamily.Name,
                            100f,
                            itemFont.Style))
                {
                    SizeF testSize =
                        g.MeasureString(
                            liveText,
                            testFont);

                    float scaleW =
                        testSize.Width >
                        0
                            ? maxTextW /
                              testSize.Width
                            : 1f;

                    float scaleH =
                        testSize.Height >
                        0
                            ? maxTextH /
                              testSize.Height
                            : 1f;

                    float finalSize =
                        Math.Max(
                            1f,

                            100f *
                            Math.Min(
                                scaleW,
                                scaleH));

                    using (
                        Font autoFont =
                            CreateFontSafe(
                                itemFont.FontFamily.Name,
                                finalSize,
                                itemFont.Style))
                    {
                        g.DrawString(
                            liveText,

                            autoFont,

                            textBrush,

                            new RectangleF(
                                textRect.X,
                                textRect.Y,
                                textRect.Width,
                                textRect.Height),

                            sf);
                    }
                }
            }
            finally
            {
                if (temporaryItemFont)
                {
                    itemFont.Dispose();
                }
            }
        }

        // ========================================================
        // BUILD TEXT
        // Uses the final 300 DPI export coordinate system.
        //
        // 300 canvas pixels = 1 inch
        // 1 inch = 25.4 mm
        // ========================================================

        private static string BuildLiveText(
            CanvasItem item)
        {
            if (item == null)
                return string.Empty;

            string template =
                item.TextTemplate ??
                string.Empty;

            int wMM =
                PixelsToMillimeters(
                    item.Width);

            int hMM =
                PixelsToMillimeters(
                    item.Height);

            return template
                .Replace(
                    "{W}",
                    wMM.ToString())
                .Replace(
                    "{H}",
                    hMM.ToString());
        }

        private bool TryGetCurrentExportBounds(
            out int finalWidth,
            out int finalHeight)
        {
            finalWidth = 0;
            finalHeight = 0;

            List<CanvasItem> printableItems =
                items
                    .Where(ItemIntersectsPage)
                    .ToList();

            if (printableItems.Count == 0)
                return false;

            const int padding = 60;

            int minX = printableItems.Min(i => i.X);
            int minY = printableItems.Min(i => i.Y);
            int maxX = printableItems.Max(i => i.X + i.Width);
            int maxY = printableItems.Max(
                i => i.Y + GetTotalItemHeight(i));

            int cropX = Math.Max(pageX, minX - padding);
            int cropY = Math.Max(pageY, minY - padding);

            int finalRight = Math.Min(
                pageX + pageWidth,
                maxX + padding);

            int finalBottom = Math.Min(
                pageY + pageHeight,
                maxY + padding);

            finalWidth = finalRight - cropX;
            finalHeight = finalBottom - cropY;

            return finalWidth > 0 && finalHeight > 0;
        }

        private void UpdateLiveOutputInfo()
        {
            if (lblOutputInfo == null)
                return;

            int width;
            int height;

            if (!TryGetCurrentExportBounds(out width, out height))
            {
                lastSavedBmpBytes = -1;
                lblOutputInfo.Text =
                    "OUTPUT   No printable image inside white canvas";
                return;
            }

            double widthMm =
                width / (double)ExportDpi * 25.4;

            double heightMm =
                height / (double)ExportDpi * 25.4;

            // 24-bit BMP rows are aligned to 4-byte boundaries.
            long stride =
                ((long)width * 3L + 3L) / 4L * 4L;

            long estimatedBytes =
                54L + stride * height;

            bool savedSizeMatches =
                lastSavedBmpBytes >= 0 &&
                lastSavedBmpWidth == width &&
                lastSavedBmpHeight == height;

            string sizeText =
                FormatFileSize(
                    savedSizeMatches
                        ? lastSavedBmpBytes
                        : estimatedBytes);

            string sizeCaption =
                savedSizeMatches
                    ? "Saved BMP "
                    : "Est. BMP ";

            lblOutputInfo.Text =
                string.Format(
                    "OUTPUT   {0} × {1} px   |   {2:0} DPI   |   {3:0.0} × {4:0.0} mm   |   {5}{6}",
                    width,
                    height,
                    ExportDpi,
                    widthMm,
                    heightMm,
                    sizeCaption,
                    sizeText);
        }

        private static string FormatFileSize(
            long bytes)
        {
            if (bytes < 1024)
                return bytes + " B";

            double kb = bytes / 1024.0;
            if (kb < 1024.0)
                return kb.ToString("0.0") + " KB";

            double mb = kb / 1024.0;
            if (mb < 1024.0)
                return mb.ToString("0.0") + " MB";

            return (mb / 1024.0).ToString("0.00") + " GB";
        }

        private static int PixelsToMillimeters(
            int pixels)
        {
            return
                (int)Math.Round(
                    pixels /
                    ExportDpi *
                    25.4f);
        }

        // ========================================================
        // ITEM GEOMETRY
        // ========================================================

        private static int GetTextBoxHeight(
            CanvasItem item)
        {
            if (item == null)
                return 0;

            return Math.Max(
                1,
                (int)Math.Round(
                    item.Width *
                    0.13));
        }

        private static int GetTextAreaHeight(
            CanvasItem item)
        {
            if (item == null ||
                !item.ShowText)
            {
                return 0;
            }

            // 10 pixel gap + box
            return
                GetTextBoxHeight(
                    item) +
                10;
        }

        private static int GetTotalItemHeight(
            CanvasItem item)
        {
            if (item == null)
                return 0;

            return
                item.Height +
                GetTextAreaHeight(
                    item);
        }

        private static Rectangle GetTextRectangle(
            CanvasItem item)
        {
            int boxHeight =
                GetTextBoxHeight(
                    item);

            return
                new Rectangle(
                    item.X,
                    item.Y +
                    item.Height +
                    10,
                    item.Width,
                    boxHeight);
        }

        private bool ItemIntersectsPage(
            CanvasItem item)
        {
            if (item == null)
                return false;

            int totalHeight =
                GetTotalItemHeight(
                    item);

            return
                item.X +
                    item.Width >
                pageX &&

                item.X <
                pageX +
                pageWidth &&

                item.Y +
                    totalHeight >
                pageY &&

                item.Y <
                pageY +
                pageHeight;
        }

        // ========================================================
        // WORKSPACE EXTENT
        // Allows dynamically tall layouts.
        // ========================================================

        private void UpdateWorkspaceExtent()
        {
            int requiredRight =
                pageX +
                pageWidth +
                WorkbenchEdgeBuffer;

            int requiredBottom =
                pageY +
                pageHeight +
                WorkbenchEdgeBuffer;

            foreach (CanvasItem item in items)
            {
                if (item == null)
                    continue;

                requiredRight =
                    Math.Max(
                        requiredRight,
                        item.X +
                        item.Width +
                        WorkbenchEdgeBuffer);

                requiredBottom =
                    Math.Max(
                        requiredBottom,
                        item.Y +
                        GetTotalItemHeight(item) +
                        WorkbenchEdgeBuffer);
            }

            // Never shrink the pasteboard. This is what gives the builder
            // its effectively infinite workbench behavior while keeping a
            // finite WinForms control internally.
            workspaceWidth =
                Math.Max(
                    workspaceWidth,
                    Math.Max(
                        InitialWorkspaceWidth,
                        requiredRight));

            workspaceHeight =
                Math.Max(
                    workspaceHeight,
                    Math.Max(
                        InitialWorkspaceHeight,
                        requiredBottom));

            UpdateCanvasPhysicalSize();
        }

        private void UpdateCanvasPhysicalSize()
        {
            if (pbCanvas == null)
                return;

            int width =
                Math.Max(
                    1,

                    (int)Math.Round(
                        workspaceWidth *
                        zoom));

            int height =
                Math.Max(
                    1,

                    (int)Math.Round(
                        workspaceHeight *
                        zoom));

            pbCanvas.Size =
                new Size(
                    width,
                    height);
        }

        // ========================================================
        // UNDO ENGINE
        // Images remain immutable and shared.
        // Snapshot stores only state/metadata.
        // ========================================================

        private void SaveUndoState()
        {
            WorkspaceSnapshot snapshot =
                CaptureSnapshot();

            if (undoStack.Count >
                0)
            {
                WorkspaceSnapshot last =
                    undoStack[
                        undoStack.Count -
                        1];

                if (SnapshotsEqual(
                    last,
                    snapshot))
                {
                    return;
                }
            }

            undoStack.Add(
                snapshot);

            if (undoStack.Count >
                15)
            {
                undoStack.RemoveAt(
                    0);
            }
        }

        private WorkspaceSnapshot CaptureSnapshot()
        {
            return
                new WorkspaceSnapshot
                {
                    PageHeight =
                        pageHeight,

                    Items =
                        items
                            .Select(
                                CanvasItemState
                                    .FromItem)
                            .ToList()
                };
        }

        private void UndoLastState()
        {
            if (undoStack.Count <=
                1)
            {
                return;
            }

            // Current state.
            undoStack.RemoveAt(
                undoStack.Count -
                1);

            WorkspaceSnapshot target =
                undoStack[
                    undoStack.Count -
                    1];

            DisposeCurrentItemFonts();

            items =
                target.Items
                    .Select(
                        state =>
                            state.ToCanvasItem())
                    .ToList();

            pageHeight =
                target.PageHeight;

            selectedItems.Clear();

            UpdateWorkspaceExtent();
        }

        private static bool SnapshotsEqual(
            WorkspaceSnapshot a,
            WorkspaceSnapshot b)
        {
            if (a == null ||
                b == null)
            {
                return false;
            }

            if (a.PageHeight !=
                b.PageHeight)
            {
                return false;
            }

            if (a.Items.Count !=
                b.Items.Count)
            {
                return false;
            }

            for (int i = 0;
                 i < a.Items.Count;
                 i++)
            {
                if (!CanvasItemState.AreEqual(
                    a.Items[i],
                    b.Items[i]))
                {
                    return false;
                }
            }

            return true;
        }

        // ========================================================
        // FONT HELPERS
        // ========================================================

        private static Font CreateFontSafe(
            string fontName,
            float size,
            FontStyle style)
        {
            if (string.IsNullOrWhiteSpace(
                fontName))
            {
                fontName =
                    "Calibri";
            }

            size =
                Math.Max(
                    1f,
                    size);

            try
            {
                return
                    new Font(
                        fontName,
                        size,
                        style,
                        GraphicsUnit.Point);
            }
            catch
            {
                try
                {
                    return
                        new Font(
                            "Calibri",
                            size,
                            style,
                            GraphicsUnit.Point);
                }
                catch
                {
                    return
                        new Font(
                            FontFamily.GenericSansSerif,
                            size,
                            FontStyle.Regular,
                            GraphicsUnit.Point);
                }
            }
        }

        private static void DisposeItemFont(
            CanvasItem item)
        {
            if (item == null ||
                item.ItemFont == null)
            {
                return;
            }

            try
            {
                item.ItemFont.Dispose();
            }
            catch
            {
            }

            item.ItemFont =
                null;
        }

        private void DisposeCurrentItemFonts()
        {
            foreach (
                CanvasItem item
                in items)
            {
                DisposeItemFont(
                    item);
            }
        }

        // ========================================================
        // INPUT HELPERS
        // ========================================================

        private bool IsEditingControlFocused()
        {
            if (txtQuickText != null &&
                txtQuickText.Focused)
            {
                return true;
            }

            if (cmbCustomText != null &&
                cmbCustomText.Focused)
            {
                return true;
            }

            if (cmbFont != null &&
                cmbFont.ComboBox != null &&
                cmbFont.ComboBox.Focused)
            {
                return true;
            }

            if (cmbTemplate != null &&
                cmbTemplate.ComboBox != null &&
                cmbTemplate.ComboBox.Focused)
            {
                return true;
            }

            if (cmbLayout != null &&
                cmbLayout.ComboBox != null &&
                cmbLayout.ComboBox.Focused)
            {
                return true;
            }

            if (ActiveControl is TextBoxBase)
            {
                return true;
            }

            if (ActiveControl is ComboBox)
            {
                return true;
            }

            return false;
        }

        // ========================================================
        // GENERIC HELPERS
        // ========================================================

        private static Rectangle CreateNormalizedRectangle(
            Point a,
            Point b)
        {
            return
                new Rectangle(
                    Math.Min(
                        a.X,
                        b.X),

                    Math.Min(
                        a.Y,
                        b.Y),

                    Math.Abs(
                        a.X -
                        b.X),

                    Math.Abs(
                        a.Y -
                        b.Y));
        }

        private static StringFormat CreateCenteredStringFormat()
        {
            return
                new StringFormat
                {
                    Alignment =
                        StringAlignment.Center,

                    LineAlignment =
                        StringAlignment.Center
                };
        }

        private static int NormalizeRotation(
            int rotation)
        {
            rotation %=
                360;

            if (rotation <
                0)
            {
                rotation +=
                    360;
            }

            // Application only uses 90-degree increments.
            int nearest =
                ((rotation + 45) /
                 90) *
                90;

            nearest %=
                360;

            return nearest;
        }

        private static double GetSafeAspect(
            int width,
            int height)
        {
            if (height <=
                0)
            {
                return 1.0;
            }

            return
                (double)Math.Max(
                    1,
                    width) /
                height;
        }

        // ========================================================
        // TEXT LIBRARY UI
        // ========================================================

        private void LoadCustomTextDropdown()
        {
            List<CustomTextAsset> library =
                TextLibraryManager.LoadLibrary();

            if (cmbCustomText != null)
            {
                cmbCustomText.Items.Clear();

                foreach (
                    CustomTextAsset asset
                    in library)
                {
                    cmbCustomText.Items.Add(
                        asset);
                }

                if (cmbCustomText.Items.Count >
                    0)
                {
                    cmbCustomText.SelectedIndex =
                        0;
                }
            }

            // Keep the top Quick Text dropdown synchronized with the
            // user-managed library. The dropdown shows the short Name/Label,
            // while selecting it applies the full Content.
            if (cmbTemplate != null)
            {
                cmbTemplate.Items.Clear();

                cmbTemplate.Items.Add(
                    "📋 Quick Text...");

                cmbTemplate.Items.Add(
                    new CustomTextAsset
                    {
                        Name = "50 x 38 MM",
                        Content = "BARCODE SAMPLE 50 X 38 MM"
                    });

                cmbTemplate.Items.Add(
                    new CustomTextAsset
                    {
                        Name = "150 x 200 MM",
                        Content = "BARCODE SAMPLE 150 X 200 MM"
                    });

                cmbTemplate.Items.Add(
                    new CustomTextAsset
                    {
                        Name = "Product Barcode - Auto Size",
                        Content = "PRODUCT BARCODE {W} X {H} MM"
                    });

                cmbTemplate.Items.Add(
                    new CustomTextAsset
                    {
                        Name = "Carton Barcode - Auto Size",
                        Content = "CARTON BARCODE {W} X {H} MM"
                    });

                foreach (
                    CustomTextAsset asset
                    in library)
                {
                    cmbTemplate.Items.Add(
                        asset);
                }

                cmbTemplate.SelectedIndex =
                    0;
            }
        }

        private void OpenLibraryManager()
        {
            using (
                Form managerForm =
                    new Form())
            {
                managerForm.Text =
                    "Manage Text Library";

                managerForm.Size =
                    new Size(
                        400,
                        480);

                managerForm.StartPosition =
                    FormStartPosition.CenterParent;

                managerForm.BackColor =
                    Color.FromArgb(
                        38,
                        40,
                        46);

                managerForm.ForeColor =
                    Color.White;

                managerForm.FormBorderStyle =
                    FormBorderStyle.FixedDialog;

                managerForm.MaximizeBox =
                    false;

                managerForm.MinimizeBox =
                    false;

                Label lblName =
                    new Label
                    {
                        Text =
                            "New Text Name/Label:",

                        Location =
                            new Point(
                                15,
                                15),

                        AutoSize =
                            true
                    };

                TextBox txtName =
                    new TextBox
                    {
                        Location =
                            new Point(
                                15,
                                35),

                        Size =
                            new Size(
                                350,
                                25),

                        BackColor =
                            Color.FromArgb(
                                50,
                                52,
                                59),

                        ForeColor =
                            Color.White,

                        BorderStyle =
                            BorderStyle.FixedSingle
                    };

                Label lblContent =
                    new Label
                    {
                        Text =
                            "Actual Text Content:",

                        Location =
                            new Point(
                                15,
                                70),

                        AutoSize =
                            true
                    };

                TextBox txtContent =
                    new TextBox
                    {
                        Location =
                            new Point(
                                15,
                                90),

                        Size =
                            new Size(
                                350,
                                70),

                        Multiline =
                            true,

                        ScrollBars =
                            ScrollBars.Vertical,

                        BackColor =
                            Color.FromArgb(
                                50,
                                52,
                                59),

                        ForeColor =
                            Color.White,

                        BorderStyle =
                            BorderStyle.FixedSingle
                    };

                Button btnSaveLib =
                    new Button
                    {
                        Text =
                            "💾 Save New Entry",

                        Location =
                            new Point(
                                15,
                                170),

                        Size =
                            new Size(
                                350,
                                35),

                        BackColor =
                            Color.FromArgb(
                                252,
                                213,
                                53),

                        ForeColor =
                            Color.Black,

                        FlatStyle =
                            FlatStyle.Flat,

                        Cursor =
                            Cursors.Hand
                    };

                btnSaveLib
                    .FlatAppearance
                    .BorderSize = 0;

                Label lblExisting =
                    new Label
                    {
                        Text =
                            "Saved Library Entries:",

                        Location =
                            new Point(
                                15,
                                220),

                        AutoSize =
                            true,

                        ForeColor =
                            Color.DarkGray
                    };

                ListBox lstLibrary =
                    new ListBox
                    {
                        Location =
                            new Point(
                                15,
                                240),

                        Size =
                            new Size(
                                350,
                                110),

                        BackColor =
                            Color.FromArgb(
                                28,
                                29,
                                33),

                        ForeColor =
                            Color.White,

                        BorderStyle =
                            BorderStyle.FixedSingle,

                        Font =
                            new Font(
                                "Segoe UI",
                                9)
                    };

                Button btnDelete =
                    new Button
                    {
                        Text =
                            "🗑️ Delete Selected Entry",

                        Location =
                            new Point(
                                15,
                                360),

                        Size =
                            new Size(
                                350,
                                35),

                        BackColor =
                            Color.FromArgb(
                                38,
                                40,
                                46),

                        ForeColor =
                            Color.IndianRed,

                        FlatStyle =
                            FlatStyle.Flat,

                        Cursor =
                            Cursors.Hand
                    };

                btnDelete
                    .FlatAppearance
                    .BorderColor =
                    Color.IndianRed;

                btnDelete
                    .FlatAppearance
                    .BorderSize = 1;

                Action RefreshList =
                    () =>
                    {
                        lstLibrary.Items.Clear();

                        foreach (
                            CustomTextAsset asset
                            in TextLibraryManager.LoadLibrary())
                        {
                            lstLibrary.Items.Add(
                                asset);
                        }
                    };

                RefreshList();

                btnSaveLib.Click +=
                    (s, e) =>
                    {
                        if (string.IsNullOrWhiteSpace(
                                txtName.Text) ||
                            string.IsNullOrWhiteSpace(
                                txtContent.Text))
                        {
                            MessageBox.Show(
                                "Please enter both a name and text content.",
                                "Missing Information",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);

                            return;
                        }

                        try
                        {
                            List<CustomTextAsset> assets =
                                TextLibraryManager.LoadLibrary();

                            assets.Add(
                                new CustomTextAsset
                                {
                                    Name =
                                        txtName.Text.Trim(),

                                    Content =
                                        txtContent.Text
                                });

                            TextLibraryManager.SaveLibrary(
                                assets);

                            txtName.Clear();
                            txtContent.Clear();

                            RefreshList();
                            LoadCustomTextDropdown();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(
                                "Unable to save the text library:" +
                                Environment.NewLine +
                                ex.Message,

                                "Library Error",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                        }
                    };

                btnDelete.Click +=
                    (s, e) =>
                    {
                        CustomTextAsset selectedAsset =
                            lstLibrary.SelectedItem
                            as CustomTextAsset;

                        if (selectedAsset ==
                            null)
                        {
                            return;
                        }

                        DialogResult answer =
                            MessageBox.Show(
                                "Are you sure you want to delete '" +
                                selectedAsset.Name +
                                "'?",

                                "Confirm Delete",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Warning);

                        if (answer !=
                            DialogResult.Yes)
                        {
                            return;
                        }

                        try
                        {
                            List<CustomTextAsset> assets =
                                TextLibraryManager.LoadLibrary();

                            CustomTextAsset itemToRemove =
                                assets.FirstOrDefault(
                                    a =>
                                        a.Name ==
                                            selectedAsset.Name &&
                                        a.Content ==
                                            selectedAsset.Content);

                            if (itemToRemove !=
                                null)
                            {
                                assets.Remove(
                                    itemToRemove);

                                TextLibraryManager.SaveLibrary(
                                    assets);

                                RefreshList();
                                LoadCustomTextDropdown();
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(
                                "Unable to update the text library:" +
                                Environment.NewLine +
                                ex.Message,

                                "Library Error",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                        }
                    };

                managerForm.Controls.AddRange(
                    new Control[]
                    {
                        lblName,
                        txtName,
                        lblContent,
                        txtContent,
                        btnSaveLib,
                        lblExisting,
                        lstLibrary,
                        btnDelete
                    });

                managerForm.ShowDialog(
                    this);
            }
        }

        // ========================================================
        // INFINITE WORKBENCH / PERSISTENT FREEFORM SHELF
        // ========================================================

        private bool IsParkedPersistentShelfItem(
            CanvasItem item)
        {
            return
                item != null &&
                !ItemIntersectsPage(item);
        }

        private string GetFreeformShelfRoot()
        {
            string localAppData =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData);

            return Path.Combine(
                localAppData,
                "NPPLPrintMaster",
                "FreeformShelf");
        }

        private string GetFreeformShelfImagesFolder()
        {
            return Path.Combine(
                GetFreeformShelfRoot(),
                "Images");
        }

        private string GetFreeformShelfMetadataPath()
        {
            return Path.Combine(
                GetFreeformShelfRoot(),
                "shelf.dat");
        }

        private static string EncodeShelfText(
            string value)
        {
            return Convert.ToBase64String(
                Encoding.UTF8.GetBytes(
                    value ?? string.Empty));
        }

        private static string DecodeShelfText(
            string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return Encoding.UTF8.GetString(
                Convert.FromBase64String(
                    value));
        }

        private static bool IsSupportedShelfImageExtension(
            string extension)
        {
            string ext =
                (extension ?? string.Empty)
                    .ToLowerInvariant();

            return
                ext == ".jpg" ||
                ext == ".jpeg" ||
                ext == ".png" ||
                ext == ".bmp";
        }

        private static bool SamePath(
            string a,
            string b)
        {
            if (string.IsNullOrWhiteSpace(a) ||
                string.IsNullOrWhiteSpace(b))
            {
                return false;
            }

            try
            {
                return string.Equals(
                    Path.GetFullPath(a),
                    Path.GetFullPath(b),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return string.Equals(
                    a,
                    b,
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        private void CenterExportPageInViewport()
        {
            if (scrollPanel == null ||
                pbCanvas == null)
            {
                return;
            }

            UpdateWorkspaceExtent();

            int targetX =
                (int)Math.Round(
                    (pageX +
                     pageWidth / 2f) *
                    zoom);

            int targetY =
                (int)Math.Round(
                    (pageY +
                     pageHeight / 2f) *
                    zoom);

            int scrollX =
                targetX -
                scrollPanel.ClientSize.Width / 2;

            int scrollY =
                targetY -
                scrollPanel.ClientSize.Height / 2;

            scrollPanel.AutoScrollPosition =
                new Point(
                    Math.Max(
                        0,
                        scrollX),

                    Math.Max(
                        0,
                        scrollY));

            pbCanvas.Invalidate();
        }

        private void ShiftLogicalWorkspace(
            int shiftX,
            int shiftY)
        {
            if (shiftX == 0 &&
                shiftY == 0)
            {
                return;
            }

            foreach (
                CanvasItem item
                in items)
            {
                if (item == null)
                    continue;

                item.X += shiftX;
                item.Y += shiftY;
            }

            pageX += shiftX;
            pageY += shiftY;

            // Undo snapshots use absolute logical coordinates. Keep them
            // aligned with the page whenever the virtual workbench rebases.
            foreach (
                WorkspaceSnapshot snapshot
                in undoStack)
            {
                if (snapshot?.Items == null)
                    continue;

                foreach (
                    CanvasItemState state
                    in snapshot.Items)
                {
                    if (state == null)
                        continue;

                    state.X += shiftX;
                    state.Y += shiftY;
                }
            }

            if (clipboardItems != null)
            {
                foreach (
                    CanvasItemState state
                    in clipboardItems)
                {
                    if (state == null)
                        continue;

                    state.X += shiftX;
                    state.Y += shiftY;
                }
            }
        }

        private void EnsureInfiniteWorkbench()
        {
            if (scrollPanel == null ||
                pbCanvas == null ||
                zoom <= 0)
            {
                return;
            }

            int oldScrollX =
                Math.Abs(
                    scrollPanel
                        .AutoScrollPosition
                        .X);

            int oldScrollY =
                Math.Abs(
                    scrollPanel
                        .AutoScrollPosition
                        .Y);

            int logicalViewLeft =
                (int)Math.Floor(
                    oldScrollX /
                    Math.Max(
                        0.01f,
                        zoom));

            int logicalViewTop =
                (int)Math.Floor(
                    oldScrollY /
                    Math.Max(
                        0.01f,
                        zoom));

            int logicalViewRight =
                (int)Math.Ceiling(
                    (oldScrollX +
                     scrollPanel.ClientSize.Width) /
                    Math.Max(
                        0.01f,
                        zoom));

            int logicalViewBottom =
                (int)Math.Ceiling(
                    (oldScrollY +
                     scrollPanel.ClientSize.Height) /
                    Math.Max(
                        0.01f,
                        zoom));

            int minX =
                Math.Min(
                    pageX,
                    logicalViewLeft);

            int minY =
                Math.Min(
                    pageY,
                    logicalViewTop);

            int maxX =
                Math.Max(
                    pageX +
                    pageWidth,
                    logicalViewRight);

            int maxY =
                Math.Max(
                    pageY +
                    pageHeight,
                    logicalViewBottom);

            foreach (
                CanvasItem item
                in items)
            {
                if (item == null)
                    continue;

                minX =
                    Math.Min(
                        minX,
                        item.X);

                minY =
                    Math.Min(
                        minY,
                        item.Y);

                maxX =
                    Math.Max(
                        maxX,
                        item.X +
                        item.Width);

                maxY =
                    Math.Max(
                        maxY,
                        item.Y +
                        GetTotalItemHeight(
                            item));
            }

            int shiftX =
                minX <
                WorkbenchEdgeBuffer
                    ? WorkbenchGrowBy
                    : 0;

            int shiftY =
                minY <
                WorkbenchEdgeBuffer
                    ? WorkbenchGrowBy
                    : 0;

            if (shiftX > 0 ||
                shiftY > 0)
            {
                ShiftLogicalWorkspace(
                    shiftX,
                    shiftY);

                workspaceWidth +=
                    shiftX;

                workspaceHeight +=
                    shiftY;

                maxX += shiftX;
                maxY += shiftY;

                oldScrollX +=
                    (int)Math.Round(
                        shiftX *
                        zoom);

                oldScrollY +=
                    (int)Math.Round(
                        shiftY *
                        zoom);
            }

            if (maxX >
                workspaceWidth -
                WorkbenchEdgeBuffer)
            {
                workspaceWidth =
                    Math.Max(
                        workspaceWidth +
                        WorkbenchGrowBy,

                        maxX +
                        WorkbenchGrowBy);
            }

            if (maxY >
                workspaceHeight -
                WorkbenchEdgeBuffer)
            {
                workspaceHeight =
                    Math.Max(
                        workspaceHeight +
                        WorkbenchGrowBy,

                        maxY +
                        WorkbenchGrowBy);
            }

            UpdateCanvasPhysicalSize();

            if (shiftX > 0 ||
                shiftY > 0)
            {
                scrollPanel.AutoScrollPosition =
                    new Point(
                        Math.Max(
                            0,
                            oldScrollX),

                        Math.Max(
                            0,
                            oldScrollY));
            }

            pbCanvas.Invalidate();
        }

        private Bitmap LoadShelfDisplayBitmap(
            string path)
        {
            using (
                Image orig =
                    Image.FromFile(
                        path))
            {
                int dispW =
                    Math.Max(
                        1,
                        orig.Width);

                int dispH =
                    Math.Max(
                        1,
                        orig.Height);

                const int maxDisplayDim =
                    1200;

                if (dispW >
                        maxDisplayDim ||
                    dispH >
                        maxDisplayDim)
                {
                    double scale =
                        Math.Min(
                            (double)maxDisplayDim /
                            dispW,

                            (double)maxDisplayDim /
                            dispH);

                    dispW =
                        Math.Max(
                            1,
                            (int)Math.Round(
                                dispW *
                                scale));

                    dispH =
                        Math.Max(
                            1,
                            (int)Math.Round(
                                dispH *
                                scale));
                }

                Bitmap displayBmp =
                    new Bitmap(
                        dispW,
                        dispH,
                        PixelFormat
                            .Format32bppPArgb);

                using (
                    Graphics g =
                        Graphics.FromImage(
                            displayBmp))
                {
                    g.Clear(
                        Color.White);

                    g.InterpolationMode =
                        InterpolationMode.Low;

                    g.PixelOffsetMode =
                        PixelOffsetMode.HighSpeed;

                    g.DrawImage(
                        orig,
                        0,
                        0,
                        dispW,
                        dispH);
                }

                return displayBmp;
            }
        }

        private void LoadFreeformShelf()
        {
            string metadataPath =
                GetFreeformShelfMetadataPath();

            string imagesFolder =
                GetFreeformShelfImagesFolder();

            if (!File.Exists(
                    metadataPath))
            {
                return;
            }

            string[] lines;

            try
            {
                lines =
                    File.ReadAllLines(
                        metadataPath,
                        Encoding.UTF8);
            }
            catch
            {
                return;
            }

            foreach (
                string line
                in lines)
            {
                if (string.IsNullOrWhiteSpace(
                    line))
                {
                    continue;
                }

                string[] parts =
                    line.Split('|');

                if (parts.Length != 11 ||
                    parts[0] != "v1")
                {
                    continue;
                }

                try
                {
                    string cacheFileName =
                        DecodeShelfText(
                            parts[1]);

                    string cachedPath =
                        Path.Combine(
                            imagesFolder,
                            cacheFileName);

                    if (!File.Exists(
                            cachedPath))
                    {
                        continue;
                    }

                    int offsetX;
                    int offsetY;
                    int width;
                    int height;
                    int rotation;
                    int showTextValue;
                    int fontStyleValue;

                    if (!int.TryParse(
                            parts[2],
                            out offsetX) ||
                        !int.TryParse(
                            parts[3],
                            out offsetY) ||
                        !int.TryParse(
                            parts[4],
                            out width) ||
                        !int.TryParse(
                            parts[5],
                            out height) ||
                        !int.TryParse(
                            parts[6],
                            out rotation) ||
                        !int.TryParse(
                            parts[7],
                            out showTextValue) ||
                        !int.TryParse(
                            parts[10],
                            out fontStyleValue))
                    {
                        continue;
                    }

                    string textTemplate =
                        DecodeShelfText(
                            parts[8]);

                    string fontName =
                        DecodeShelfText(
                            parts[9]);

                    if (string.IsNullOrWhiteSpace(
                            fontName))
                    {
                        fontName =
                            "Calibri";
                    }

                    Bitmap displayBmp =
                        LoadShelfDisplayBitmap(
                            cachedPath);

                    CanvasItem item =
                        new CanvasItem
                        {
                            FilePath =
                                cachedPath,

                            Img =
                                displayBmp,

                            X =
                                pageX +
                                offsetX,

                            Y =
                                pageY +
                                offsetY,

                            Width =
                                Math.Max(
                                    1,
                                    width),

                            Height =
                                Math.Max(
                                    1,
                                    height),

                            OriginalAspect =
                                GetSafeAspect(
                                    width,
                                    height),

                            TextTemplate =
                                textTemplate,

                            ShowText =
                                showTextValue != 0,

                            Rotation =
                                NormalizeRotation(
                                    rotation),

                            ItemFont =
                                CreateFontSafe(
                                    fontName,
                                    14f,
                                    (FontStyle)fontStyleValue)
                        };

                    FreeformShelfRecord record =
                        new FreeformShelfRecord
                        {
                            CacheFileName =
                                cacheFileName,

                            OffsetX =
                                offsetX,

                            OffsetY =
                                offsetY,

                            Width =
                                item.Width,

                            Height =
                                item.Height,

                            Rotation =
                                item.Rotation,

                            ShowText =
                                item.ShowText,

                            TextTemplate =
                                item.TextTemplate,

                            FontName =
                                fontName,

                            FontStyle =
                                (FontStyle)fontStyleValue
                        };

                    ownedImages.Add(
                        displayBmp);

                    items.Add(
                        item);

                    persistentShelfItems[item] =
                        record;
                }
                catch
                {
                    // One bad shelf item must not prevent the rest from
                    // loading or stop the Freeform Builder from opening.
                }
            }

            UpdateWorkspaceExtent();
        }

        private FreeformShelfRecord FindShelfRecordByCachedPath(
            string filePath,
            IEnumerable<FreeformShelfRecord> records)
        {
            if (string.IsNullOrWhiteSpace(
                    filePath) ||
                records == null)
            {
                return null;
            }

            string imagesFolder =
                GetFreeformShelfImagesFolder();

            foreach (
                FreeformShelfRecord record
                in records)
            {
                if (record == null ||
                    string.IsNullOrWhiteSpace(
                        record.CacheFileName))
                {
                    continue;
                }

                string cachedPath =
                    Path.Combine(
                        imagesFolder,
                        record.CacheFileName);

                if (SamePath(
                    filePath,
                    cachedPath))
                {
                    return record;
                }
            }

            return null;
        }

        private void UpdateShelfRecordFromItem(
            FreeformShelfRecord record,
            CanvasItem item)
        {
            if (record == null ||
                item == null)
            {
                return;
            }

            record.OffsetX =
                item.X -
                pageX;

            record.OffsetY =
                item.Y -
                pageY;

            record.Width =
                Math.Max(
                    1,
                    item.Width);

            record.Height =
                Math.Max(
                    1,
                    item.Height);

            record.Rotation =
                NormalizeRotation(
                    item.Rotation);

            record.ShowText =
                item.ShowText;

            record.TextTemplate =
                item.TextTemplate ??
                string.Empty;

            if (item.ItemFont != null)
            {
                record.FontName =
                    item.ItemFont
                        .FontFamily
                        .Name;

                record.FontStyle =
                    item.ItemFont.Style;
            }

            if (!string.IsNullOrWhiteSpace(
                    item.FilePath) &&
                File.Exists(
                    item.FilePath))
            {
                record.SourcePath =
                    item.FilePath;
            }

            record.LiveImage =
                item.Img;
        }

        private void EnsureShelfCacheFile(
            FreeformShelfRecord record,
            string imagesFolder)
        {
            if (record == null)
                return;

            Directory.CreateDirectory(
                imagesFolder);

            string existingCachePath =
                string.IsNullOrWhiteSpace(
                    record.CacheFileName)
                    ? string.Empty
                    : Path.Combine(
                        imagesFolder,
                        record.CacheFileName);

            if (!string.IsNullOrWhiteSpace(
                    existingCachePath) &&
                File.Exists(
                    existingCachePath))
            {
                return;
            }

            string sourcePath =
                record.SourcePath;

            string extension =
                !string.IsNullOrWhiteSpace(
                    sourcePath)
                    ? Path.GetExtension(
                        sourcePath)
                    : string.Empty;

            bool canCopySource =
                !string.IsNullOrWhiteSpace(
                    sourcePath) &&
                File.Exists(
                    sourcePath) &&
                IsSupportedShelfImageExtension(
                    extension);

            if (!canCopySource)
            {
                extension =
                    ".png";
            }

            record.CacheFileName =
                Guid.NewGuid()
                    .ToString("N") +
                extension.ToLowerInvariant();

            string destination =
                Path.Combine(
                    imagesFolder,
                    record.CacheFileName);

            if (canCopySource)
            {
                File.Copy(
                    sourcePath,
                    destination,
                    true);

                return;
            }

            if (record.LiveImage == null)
            {
                throw new InvalidOperationException(
                    "Shelf artwork no longer has a readable source image.");
            }

            record.LiveImage.Save(
                destination,
                ImageFormat.Png);
        }

        private void SaveFreeformShelf()
        {
            string root =
                GetFreeformShelfRoot();

            string imagesFolder =
                GetFreeformShelfImagesFolder();

            Directory.CreateDirectory(
                root);

            Directory.CreateDirectory(
                imagesFolder);

            List<FreeformShelfRecord> records =
                persistentShelfItems
                    .Values
                    .Where(
                        r => r != null)
                    .Distinct()
                    .ToList();

            // Loaded shelf items remain persistent even if they were dragged
            // into the white page or deleted from this particular session.
            // If they are still parked in gray, remember their new parking
            // position and size.
            foreach (
                KeyValuePair<CanvasItem, FreeformShelfRecord> pair
                in persistentShelfItems.ToList())
            {
                CanvasItem item =
                    pair.Key;

                FreeformShelfRecord record =
                    pair.Value;

                if (item != null &&
                    items.Contains(
                        item) &&
                    !ItemIntersectsPage(
                        item))
                {
                    UpdateShelfRecordFromItem(
                        record,
                        item);
                }
            }

            // Any ordinary image the user parks completely in gray becomes
            // a reusable shelf item automatically.
            foreach (
                CanvasItem item
                in items)
            {
                if (item == null ||
                    item.Img == null ||
                    ItemIntersectsPage(
                        item))
                {
                    continue;
                }

                FreeformShelfRecord existing =
                    persistentShelfItems
                        .ContainsKey(
                            item)
                            ? persistentShelfItems[item]
                            : FindShelfRecordByCachedPath(
                                item.FilePath,
                                records);

                if (existing != null)
                {
                    UpdateShelfRecordFromItem(
                        existing,
                        item);

                    if (!records.Contains(
                            existing))
                    {
                        records.Add(
                            existing);
                    }

                    continue;
                }

                FreeformShelfRecord record =
                    new FreeformShelfRecord();

                UpdateShelfRecordFromItem(
                    record,
                    item);

                records.Add(
                    record);
            }

            // Do not create an empty metadata file if there is no shelf.
            if (records.Count == 0)
            {
                string emptyMetadata =
                    GetFreeformShelfMetadataPath();

                if (File.Exists(
                        emptyMetadata))
                {
                    try
                    {
                        File.Delete(
                            emptyMetadata);
                    }
                    catch
                    {
                    }
                }

                return;
            }

            List<string> lines =
                new List<string>();

            foreach (
                FreeformShelfRecord record
                in records)
            {
                try
                {
                    EnsureShelfCacheFile(
                        record,
                        imagesFolder);

                    if (string.IsNullOrWhiteSpace(
                            record.CacheFileName))
                    {
                        continue;
                    }

                    lines.Add(
                        "v1|" +
                        EncodeShelfText(
                            record.CacheFileName) +
                        "|" +
                        record.OffsetX +
                        "|" +
                        record.OffsetY +
                        "|" +
                        Math.Max(
                            1,
                            record.Width) +
                        "|" +
                        Math.Max(
                            1,
                            record.Height) +
                        "|" +
                        NormalizeRotation(
                            record.Rotation) +
                        "|" +
                        (record.ShowText
                            ? "1"
                            : "0") +
                        "|" +
                        EncodeShelfText(
                            record.TextTemplate) +
                        "|" +
                        EncodeShelfText(
                            string.IsNullOrWhiteSpace(
                                record.FontName)
                                ? "Calibri"
                                : record.FontName) +
                        "|" +
                        (int)record.FontStyle);
                }
                catch
                {
                    // Skip only the item that could not be cached.
                }
            }

            if (lines.Count == 0)
                return;

            string metadataPath =
                GetFreeformShelfMetadataPath();

            string tempMetadata =
                metadataPath +
                ".tmp";

            File.WriteAllLines(
                tempMetadata,
                lines.ToArray(),
                Encoding.UTF8);

            File.Copy(
                tempMetadata,
                metadataPath,
                true);

            try
            {
                File.Delete(
                    tempMetadata);
            }
            catch
            {
            }
        }

        private void ClearFreeformShelf()
        {
            DialogResult result =
                MessageBox.Show(
                    "Clear all reusable artwork from the gray Freeform Shelf?" +
                    Environment.NewLine +
                    Environment.NewLine +
                    "This deletes only NPPLPrintMaster's saved shelf cache." +
                    Environment.NewLine +
                    "Your original image files are not deleted.",

                    "Clear Freeform Shelf",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

            if (result !=
                DialogResult.Yes)
            {
                return;
            }

            List<CanvasItem> grayItems =
                items
                    .Where(
                        i =>
                            i != null &&
                            !ItemIntersectsPage(i))
                    .ToList();

            foreach (
                CanvasItem item
                in grayItems)
            {
                selectedItems.Remove(
                    item);

                items.Remove(
                    item);

                DisposeItemFont(
                    item);
            }

            persistentShelfItems.Clear();

            string root =
                GetFreeformShelfRoot();

            try
            {
                if (Directory.Exists(
                        root))
                {
                    Directory.Delete(
                        root,
                        true);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "The shelf was cleared from this session, but Windows could not remove the cache folder." +
                    Environment.NewLine +
                    Environment.NewLine +
                    ex.Message,

                    "Freeform Shelf",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            SaveUndoState();
            UpdateWorkspaceExtent();
            pbCanvas.Invalidate();
        }

        private sealed class FreeformShelfRecord
        {
            public string CacheFileName { get; set; }
            public string SourcePath { get; set; }
            public Image LiveImage { get; set; }

            public int OffsetX { get; set; }
            public int OffsetY { get; set; }

            public int Width { get; set; }
            public int Height { get; set; }

            public int Rotation { get; set; }

            public bool ShowText { get; set; }

            public string TextTemplate { get; set; }

            public string FontName { get; set; }

            public FontStyle FontStyle { get; set; }
        }

        // ========================================================
        // CLEANUP
        // ========================================================

        protected override void OnFormClosed(
            FormClosedEventArgs e)
        {
            try
            {
                SaveFreeformShelf();
            }
            catch
            {
                // Shelf persistence must never block application shutdown.
            }

            DisposeCurrentItemFonts();

            foreach (
                Image image
                in ownedImages.ToList())
            {
                if (image == null)
                    continue;

                try
                {
                    image.Dispose();
                }
                catch
                {
                }
            }

            ownedImages.Clear();

            shadowBrush.Dispose();
            pageBorderPen.Dispose();
            exportBorderPen.Dispose();
            snapLinePen.Dispose();
            selectionPen.Dispose();
            textPen.Dispose();
            textBrush.Dispose();
            marqueeBrush.Dispose();
            marqueePen.Dispose();

            undoStack.Clear();
            clipboardItems.Clear();

            base.OnFormClosed(
                e);
        }

        // ========================================================
        // INTERNAL IMPORT RESULT
        // ========================================================

        private sealed class ImportBatchResult
        {
            public List<CanvasItem> Items { get; } =
                new List<CanvasItem>();

            public List<string> FailedFiles { get; } =
                new List<string>();
        }

        // ========================================================
        // WORKSPACE SNAPSHOT
        // ========================================================

        private sealed class WorkspaceSnapshot
        {
            public int PageHeight { get; set; }

            public List<CanvasItemState> Items { get; set; } =
                new List<CanvasItemState>();
        }

        // ========================================================
        // ITEM SNAPSHOT
        // ========================================================

        private sealed class CanvasItemState
        {
            public string FilePath { get; set; }

            public Image Img { get; set; }

            public int X { get; set; }

            public int Y { get; set; }

            public int Width { get; set; }

            public int Height { get; set; }

            public double OriginalAspect { get; set; }

            public string TextTemplate { get; set; }

            public bool ShowText { get; set; }

            public string FontName { get; set; }

            public System.Drawing.FontStyle ItemFontStyle { get; set; }

            public int Rotation { get; set; }

            public static CanvasItemState FromItem(CanvasItem item)
            {
                Font font = item.ItemFont;

                return new CanvasItemState
                {
                    FilePath = item.FilePath,

                    Img = item.Img,

                    X = item.X,
                    Y = item.Y,

                    Width = item.Width,
                    Height = item.Height,

                    OriginalAspect = item.OriginalAspect,

                    TextTemplate = item.TextTemplate,

                    ShowText = item.ShowText,

                    FontName =
                        font != null
                            ? font.FontFamily.Name
                            : "Calibri",

                    ItemFontStyle =
                        font != null
                            ? font.Style
                            : System.Drawing.FontStyle.Bold,

                    Rotation = NormalizeRotation(item.Rotation)
                };
            }

            public CanvasItem ToCanvasItem()
            {
                return new CanvasItem
                {
                    FilePath = FilePath,

                    Img = Img,

                    X = X,
                    Y = Y,

                    Width = Width,
                    Height = Height,

                    OriginalAspect = OriginalAspect,

                    TextTemplate = TextTemplate,

                    ShowText = ShowText,

                    Rotation = NormalizeRotation(Rotation),

                    ItemFont = CreateFontSafe(
                        FontName,
                        14f,
                        ItemFontStyle)
                };
            }

            public static bool AreEqual(
                CanvasItemState a,
                CanvasItemState b)
            {
                if (a == null || b == null)
                    return false;

                return
                    a.FilePath == b.FilePath &&
                    ReferenceEquals(a.Img, b.Img) &&

                    a.X == b.X &&
                    a.Y == b.Y &&

                    a.Width == b.Width &&
                    a.Height == b.Height &&

                    Math.Abs(
                        a.OriginalAspect -
                        b.OriginalAspect) < 0.0000001 &&

                    a.TextTemplate == b.TextTemplate &&

                    a.ShowText == b.ShowText &&

                    a.FontName == b.FontName &&

                    a.ItemFontStyle == b.ItemFontStyle &&

                    a.Rotation == b.Rotation;
            }
        }

    }

    // ============================================================
    // CUSTOM TEXT ASSET
    // ============================================================

    public class CustomTextAsset
    {
        public string Name { get; set; }

        public string Content { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }

    // ============================================================
    // TEXT LIBRARY MANAGER
    //
    // v2 storage format:
    //
    // v2|Base64(Name)|Base64(Content)
    //
    // Base64 makes multiline text safe and prevents delimiters
    // inside the user's content from corrupting the file.
    //
    // The loader also understands the old:
    //
    // Name|||Content
    //
    // format for backwards compatibility.
    // ============================================================

    public static class TextLibraryManager
    {
        private static readonly string filePath =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData),

                "NPPLPrintMaster_CustomTextLibrary.txt");

        public static List<CustomTextAsset> LoadLibrary()
        {
            List<CustomTextAsset> assets =
                new List<CustomTextAsset>();

            if (!File.Exists(
                filePath))
            {
                return assets;
            }

            try
            {
                string[] lines =
                    File.ReadAllLines(
                        filePath,
                        Encoding.UTF8);

                foreach (
                    string line
                    in lines)
                {
                    if (string.IsNullOrWhiteSpace(
                        line))
                    {
                        continue;
                    }

                    // =============================================
                    // NEW V2 FORMAT
                    // =============================================

                    if (line.StartsWith(
                        "v2|",
                        StringComparison.Ordinal))
                    {
                        string[] parts =
                            line.Split(
                                new[]
                                {
                                    '|'
                                },

                                3);

                        if (parts.Length !=
                            3)
                        {
                            continue;
                        }

                        try
                        {
                            string name =
                                DecodeBase64(
                                    parts[1]);

                            string content =
                                DecodeBase64(
                                    parts[2]);

                            assets.Add(
                                new CustomTextAsset
                                {
                                    Name =
                                        name,

                                    Content =
                                        content
                                });
                        }
                        catch
                        {
                            // Skip malformed record.
                        }

                        continue;
                    }

                    // =============================================
                    // LEGACY FORMAT
                    // =============================================

                    string[] legacyParts =
                        line.Split(
                            new[]
                            {
                                "|||"
                            },

                            StringSplitOptions.None);

                    if (legacyParts.Length ==
                        2)
                    {
                        assets.Add(
                            new CustomTextAsset
                            {
                                Name =
                                    legacyParts[0],

                                Content =
                                    legacyParts[1]
                            });
                    }
                }
            }
            catch
            {
                // Return whatever could be loaded safely.
            }

            return assets;
        }

        public static void SaveLibrary(
            List<CustomTextAsset> assets)
        {
            if (assets ==
                null)
            {
                assets =
                    new List<CustomTextAsset>();
            }

            string directory =
                Path.GetDirectoryName(
                    filePath);

            if (!string.IsNullOrWhiteSpace(
                directory))
            {
                Directory.CreateDirectory(
                    directory);
            }

            List<string> lines =
                new List<string>();

            foreach (
                CustomTextAsset asset
                in assets)
            {
                if (asset ==
                    null)
                {
                    continue;
                }

                string name =
                    asset.Name ??
                    string.Empty;

                string content =
                    asset.Content ??
                    string.Empty;

                string record =
                    "v2|" +
                    EncodeBase64(
                        name) +
                    "|" +
                    EncodeBase64(
                        content);

                lines.Add(
                    record);
            }

            string tempFile =
                filePath +
                ".tmp";

            File.WriteAllLines(
                tempFile,
                lines,
                Encoding.UTF8);

            File.Copy(
                tempFile,
                filePath,
                true);

            try
            {
                File.Delete(
                    tempFile);
            }
            catch
            {
            }
        }

        private static string EncodeBase64(
            string value)
        {
            byte[] bytes =
                Encoding.UTF8.GetBytes(
                    value ??
                    string.Empty);

            return Convert.ToBase64String(
                bytes);
        }

        private static string DecodeBase64(
            string value)
        {
            byte[] bytes =
                Convert.FromBase64String(
                    value);

            return Encoding.UTF8.GetString(
                bytes);
        }
    }
}