using System;
#pragma warning disable IDE0270
#pragma warning disable IDE0060
#pragma warning disable IDE0059
#pragma warning disable IDE0039
#pragma warning disable IDE0038
#pragma warning disable IDE0031
#pragma warning disable IDE0019
#pragma warning disable IDE0018
#pragma warning disable IDE0017
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Windows.Forms;

namespace NPPLPrintMaster
{
    public partial class Form1 : Form
    {
        private BtwImageLibraryEngine btwImageLibraryEngine;

        private TextBox txtBtwLibrarySearch;
        private ComboBox cmbBtwLibraryStatus;
        private DataGridView dgvBtwLibrary;
        private PictureBox picBtwLibraryPreview;

        private Label lblBtwLibraryStats;

        private Label lblBtwLibraryStage;
        private Label lblBtwLibraryProgressText;
        private Label lblBtwLibraryProgressPercent;
        private Label lblBtwLibraryProgressDetails;
        private Panel pnlBtwLibraryProgress;

        private Button btnBtwLibraryIndex;
        private Button btnBtwLibraryUseImage;
        private Button btnBtwLibraryOpen;
        private Button btnBtwLibraryFolder;
        private Button btnBtwLibraryCreateImages;
        private Button btnBtwLibraryIndexBarcodes;
        private Button btnBtwLibraryCancel;
        private Button btnBtwLibrarySettings;

        private BtwImageLibraryModuleSettings btwLibraryModuleSettings;
        private string btwLibrarySettingsFile;

        private ListBox lstBtwLibraryInclude;
        private ListBox lstBtwLibraryExclude;

        private CancellationTokenSource btwLibraryCts;
        private string selectedBtwLibraryImage = "";

        private void EnsureBtwLibrarySettings()
        {
            if (currentSettings == null)
            {
                currentSettings = SettingsManager.Load();
            }

            if (currentSettings == null)
            {
                currentSettings = new AppSettings();
            }
        }

        private readonly List<Panel> btwLibraryThemePanels =
            new List<Panel>();

        private void BuildBtwImageLibrary()
        {
            EnsureBtwLibrarySettings();

            string dataRoot = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "NPPLPrintMaster",
                "BTWImageLibrary");

            Directory.CreateDirectory(dataRoot);

            btwLibrarySettingsFile =
                Path.Combine(dataRoot, "Settings.ini");

            btwLibraryModuleSettings =
                BtwImageLibraryModuleSettings.Load(
                    btwLibrarySettingsFile,
                    currentSettings.DefaultDpi);

            btwImageLibraryEngine =
                new BtwImageLibraryEngine(
                    Path.Combine(dataRoot, "Library.db"),
                    Path.Combine(dataRoot, "Images"));

            Color pageBack = GetBtwLibraryPageBack();
            Color surface = GetBtwLibrarySurface(pageBack);
            Color textColor =
                ThemeManager.GetContrastTextColor(pageBack);
            Color borderColor =
                activeTheme == null
                    ? Color.FromArgb(85, 95, 90)
                    : activeTheme.BorderColor;
            Color accentColor =
                activeTheme == null
                    ? Color.FromArgb(72, 185, 112)
                    : activeTheme.ActiveColor;
            Color inputBack =
                IsBtwLibraryDark(pageBack)
                    ? BlendBtwLibraryColor(
                        pageBack,
                        Color.White,
                        0.075)
                    : Color.White;
            Color inputText =
                ThemeManager.GetContrastTextColor(
                    inputBack);

            Font normalFont =
                new Font("Segoe UI", 9F);
            Font smallFont =
                new Font("Segoe UI", 8.25F);
            Font boldFont =
                new Font(
                    "Segoe UI Semibold",
                    8.75F,
                    FontStyle.Bold);

            btwLibraryThemePanels.Clear();

            Panel root =
                new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = pageBack,
                    Padding = new Padding(12),
                    Margin = new Padding(0)
                };

            TableLayoutPanel main =
                new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 5,
                    BackColor = pageBack,
                    Padding = new Padding(0),
                    Margin = new Padding(0)
                };

            main.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    100F));

            // Responsive vertical rhythm. The top three sections get enough
            // room for their captions and content without crowding each other.
            main.RowStyles.Add(
                new RowStyle(
                    SizeType.Absolute,
                    58F));
            main.RowStyles.Add(
                new RowStyle(
                    SizeType.Absolute,
                    116F));
            // Progress needs enough vertical room for stage, bar and the
            // complete progress summary without clipping at the bottom.
            main.RowStyles.Add(
                new RowStyle(
                    SizeType.Absolute,
                    150F));
            main.RowStyles.Add(
                new RowStyle(
                    SizeType.Absolute,
                    114F));
            main.RowStyles.Add(
                new RowStyle(
                    SizeType.Percent,
                    100F));

            // ------------------------------------------------------------
            // MODULE HEADER
            // ------------------------------------------------------------
            Panel header =
                new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = pageBack,
                    Margin = new Padding(0, 0, 0, 5),
                    Padding = new Padding(0)
                };

            Label title =
                new Label
                {
                    Text = "BTW Image Library",
                    Dock = DockStyle.Top,
                    Height = 31,
                    Font =
                        new Font(
                            "Segoe UI Semibold",
                            17F,
                            FontStyle.Bold),
                    Padding = new Padding(0, 1, 0, 0),
                    ForeColor = textColor,
                    BackColor = Color.Transparent,
                    TextAlign =
                        ContentAlignment.MiddleLeft
                };

            Label subtitle =
                new Label
                {
                    Text =
                        "Central BTW repository • search, clean-image generation and barcode indexing",
                    Dock = DockStyle.Fill,
                    Font = smallFont,
                    ForeColor = textColor,
                    BackColor = Color.Transparent,
                    TextAlign =
                        ContentAlignment.MiddleLeft
                };

            header.Controls.Add(subtitle);
            header.Controls.Add(title);

            // ------------------------------------------------------------
            // SEARCH CONTROLS - CENTERED
            // ------------------------------------------------------------
            Panel commandPanel =
                CreateBtwSectionPanel(
                    "Search Library Controls",
                    surface,
                    textColor,
                    borderColor);

            TableLayoutPanel commandBody =
                new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 2,
                    BackColor = surface,
                    Padding = new Padding(8, 7, 8, 6),
                    Margin = new Padding(0)
                };

            commandBody.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    100F));

            commandBody.RowStyles.Add(
                new RowStyle(
                    SizeType.Absolute,
                    44F));

            commandBody.RowStyles.Add(
                new RowStyle(
                    SizeType.Percent,
                    100F));

            // A real centered content strip is used here instead of percentage
            // columns. This keeps the buttons readable and centered at 720p,
            // 1080p and larger resolutions.
            Panel searchHost =
                new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = surface,
                    Margin = new Padding(0),
                    Padding = new Padding(0)
                };

            FlowLayoutPanel searchFlow =
                new FlowLayoutPanel
                {
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    WrapContents = false,
                    FlowDirection = FlowDirection.LeftToRight,
                    BackColor = surface,
                    Margin = new Padding(0),
                    Padding = new Padding(0),
                    Anchor = AnchorStyles.Top,
                    Height = 36
                };

            txtBtwLibrarySearch =
                new TextBox
                {
                    Width = 390,
                    Height = 30,
                    Font = new Font("Segoe UI", 10F),
                    BackColor = inputBack,
                    ForeColor = inputText,
                    BorderStyle = BorderStyle.FixedSingle,
                    Margin = new Padding(0, 3, 7, 3),
                    Anchor = AnchorStyles.Top
                };

            txtBtwLibrarySearch.TextChanged +=
                (s, e) =>
                    RefreshBtwLibraryResults();

            cmbBtwLibraryStatus =
                new ComboBox
                {
                    Width = 150,
                    Height = 30,
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Font = smallFont,
                    BackColor = inputBack,
                    ForeColor = inputText,
                    Margin = new Padding(0, 3, 7, 3),
                    Anchor = AnchorStyles.Top
                };

            cmbBtwLibraryStatus.Items.AddRange(
                new object[]
                {
                    "All",
                    "New",
                    "Image Ready - Barcode Pending",
                    "Ready",
                    "Ready - Barcode Not Detected",
                    "Error",
                    "Missing Image",
                    "Duplicate Barcode"
                });

            cmbBtwLibraryStatus.SelectedIndex = 0;

            cmbBtwLibraryStatus.SelectedIndexChanged +=
                (s, e) =>
                    RefreshBtwLibraryResults();

            btnBtwLibraryIndex =
                CreateBtwCompactButton(
                    "Index BTW",
                    accentColor,
                    ThemeManager.GetContrastTextColor(
                        accentColor),
                    boldFont,
                    90);

            btnBtwLibraryCreateImages =
                CreateBtwCompactButton(
                    "Create Images",
                    surface,
                    textColor,
                    boldFont,
                    112);

            btnBtwLibraryIndexBarcodes =
                CreateBtwCompactButton(
                    "Index Barcodes",
                    surface,
                    textColor,
                    boldFont,
                    112);

            btnBtwLibraryCancel =
                CreateBtwCompactButton(
                    "Cancel",
                    surface,
                    textColor,
                    smallFont,
                    68);

            btnBtwLibrarySettings =
                CreateBtwCompactButton(
                    "Settings",
                    surface,
                    textColor,
                    smallFont,
                    80);

            btnBtwLibraryIndex.Click +=
                BtnBtwLibraryIndex_Click;

            btnBtwLibraryCreateImages.Click +=
                BtnBtwLibraryCreateImages_Click;

            btnBtwLibraryIndexBarcodes.Click +=
                BtnBtwLibraryIndexBarcodes_Click;

            btnBtwLibraryCancel.Enabled = false;
            btnBtwLibraryCancel.Click +=
                (s, e) =>
                {
                    if (btwLibraryCts != null)
                        btwLibraryCts.Cancel();
                };

            btnBtwLibrarySettings.Click +=
                (s, e) =>
                    ShowBtwLibrarySettingsDialog();

            searchFlow.Controls.Add(txtBtwLibrarySearch);
            searchFlow.Controls.Add(cmbBtwLibraryStatus);
            searchFlow.Controls.Add(btnBtwLibraryIndex);
            searchFlow.Controls.Add(btnBtwLibraryCreateImages);
            searchFlow.Controls.Add(btnBtwLibraryIndexBarcodes);
            searchFlow.Controls.Add(btnBtwLibraryCancel);
            searchFlow.Controls.Add(btnBtwLibrarySettings);

            searchHost.Controls.Add(searchFlow);

            Action centerSearchControls =
                () =>
                {
                    if (searchHost.ClientSize.Width <= 0)
                        return;

                    // Keep the search box responsive while the action buttons
                    // retain stable compact widths.
                    int fixedWidth =
                        150 +
                        90 +
                        112 +
                        112 +
                        68 +
                        80 +
                        8 * 5;

                    txtBtwLibrarySearch.Width =
                        Math.Max(
                            220,
                            Math.Min(
                                390,
                                searchHost.ClientSize.Width -
                                fixedWidth));

                    searchFlow.PerformLayout();

                    searchFlow.Left =
                        Math.Max(
                            0,
                            (searchHost.ClientSize.Width -
                             searchFlow.Width) / 2);

                    searchFlow.Top =
                        Math.Max(
                            0,
                            (searchHost.ClientSize.Height -
                             searchFlow.Height) / 2);
                };

            searchHost.Resize +=
                (s, e) =>
                    centerSearchControls();

            searchHost.HandleCreated +=
                (s, e) =>
                    centerSearchControls();

            commandBody.Controls.Add(
                searchHost,
                0,
                0);

            TableLayoutPanel infoRow =
                new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 2,
                    RowCount = 1,
                    BackColor = surface,
                    Margin = new Padding(2, 1, 2, 0)
                };

            infoRow.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    45F));

            infoRow.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    55F));

            Label searchHelp =
                new Label
                {
                    Text =
                        "Barcode scanner • Product • BTW filename • Folder / path",
                    Dock = DockStyle.Fill,
                    Font = smallFont,
                    ForeColor = textColor,
                    BackColor = Color.Transparent,
                    AutoEllipsis = true,
                    TextAlign =
                        ContentAlignment.MiddleLeft,
                    Padding = new Padding(3, 0, 0, 0),
                    Margin = new Padding(0)
                };

            lblBtwLibraryStats =
                new Label
                {
                    Text =
                        "Templates: 0    Images: 0    Missing: 0    Errors: 0    Duplicate barcodes: 0",
                    Dock = DockStyle.Fill,
                    Font = smallFont,
                    ForeColor = textColor,
                    BackColor = Color.Transparent,
                    AutoEllipsis = true,
                    TextAlign =
                        ContentAlignment.MiddleRight,
                    Padding = new Padding(0, 0, 3, 0),
                    Margin = new Padding(0)
                };

            infoRow.Controls.Add(
                searchHelp,
                0,
                0);

            infoRow.Controls.Add(
                lblBtwLibraryStats,
                1,
                0);

            commandBody.Controls.Add(
                infoRow,
                0,
                1);

            ((Panel)commandPanel.Tag).Controls.Add(
                commandBody);

            // ------------------------------------------------------------
            // PROGRESS
            // ------------------------------------------------------------

            Panel progressPanel =
                CreateBtwSectionPanel(
                    "Library Refresh Progress",
                    surface,
                    textColor,
                    borderColor);

            TableLayoutPanel progressLayout =
                new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 3,
                    Padding =
                        new Padding(
                            9,
                            6,
                            9,
                            6),
                    BackColor = surface
                };

            // Keep the progress information in three clearly separated
            // visual bands: stage, progress bar, and details.
            progressLayout.RowStyles.Add(
                new RowStyle(
                    SizeType.Absolute,
                    25F));
            progressLayout.RowStyles.Add(
                new RowStyle(
                    SizeType.Absolute,
                    34F));
            progressLayout.RowStyles.Add(
                new RowStyle(
                    SizeType.Percent,
                    100F));

            TableLayoutPanel progressTop =
                new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 2,
                    RowCount = 1,
                    BackColor = surface
                };

            progressTop.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    78F));
            progressTop.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    22F));

            lblBtwLibraryStage =
                new Label
                {
                    Text =
                        "Ready — choose a manual stage above.",
                    Dock = DockStyle.Fill,
                    Font = boldFont,
                    ForeColor = textColor,
                    BackColor = Color.Transparent,
                    AutoEllipsis = true,
                    TextAlign =
                        ContentAlignment.MiddleLeft
                };

            lblBtwLibraryProgressPercent =
                new Label
                {
                    Text = "0%",
                    Dock = DockStyle.Fill,
                    Font = boldFont,
                    ForeColor = textColor,
                    BackColor = Color.Transparent,
                    TextAlign =
                        ContentAlignment.MiddleRight
                };

            progressTop.Controls.Add(
                lblBtwLibraryStage, 0, 0);
            progressTop.Controls.Add(
                lblBtwLibraryProgressPercent, 1, 0);

            pnlBtwLibraryProgress =
                new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = surface,
                    BorderStyle =
                        BorderStyle.FixedSingle,
                    Margin =
                        new Padding(1, 2, 1, 2)
                };

            pnlBtwLibraryProgress.Paint +=
                PnlBtwLibraryProgress_Paint;

            TableLayoutPanel progressBottom =
                new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 2,
                    RowCount = 2,
                    BackColor = surface
                };

            progressBottom.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 20F));
            progressBottom.RowStyles.Add(
                new RowStyle(SizeType.Percent, 100F));

            progressBottom.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Absolute,
                    95F));
            progressBottom.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    100F));

            lblBtwLibraryProgressText =
                new Label
                {
                    Text = "Ready",
                    Dock = DockStyle.Fill,
                    Font = smallFont,
                    ForeColor = textColor,
                    BackColor = Color.Transparent,
                    TextAlign =
                        ContentAlignment.MiddleLeft,
                    Padding = new Padding(0, 2, 0, 0)
                };

            lblBtwLibraryProgressDetails =
                new Label
                {
                    Text =
                        "Found: 0   Indexed: 0   Created: 0   Detected: 0   Not detected: 0   Errors: 0   Duplicates: 0",
                    Dock = DockStyle.Fill,
                    Font = smallFont,
                    ForeColor = textColor,
                    BackColor = Color.Transparent,
                    AutoEllipsis = false,
                    AutoSize = false,
                    TextAlign =
                        ContentAlignment.MiddleLeft,
                    Padding = new Padding(0, 1, 0, 0)
                };

            progressBottom.Controls.Add(
                lblBtwLibraryProgressText, 0, 0);
            progressBottom.Controls.Add(
                lblBtwLibraryProgressDetails, 0, 1);
            progressBottom.SetColumnSpan(
                lblBtwLibraryProgressDetails,
                2);

            progressLayout.Controls.Add(
                progressTop, 0, 0);
            progressLayout.Controls.Add(
                pnlBtwLibraryProgress, 0, 1);
            progressLayout.Controls.Add(
                progressBottom, 0, 2);

            ((Panel)progressPanel.Tag).Controls.Add(
                progressLayout);

            // ------------------------------------------------------------
            // SOURCE FOLDERS - VERY COMPACT
            // ------------------------------------------------------------
            Panel sourcePanel =
                CreateBtwSectionPanel(
                    "BTW Source Folders",
                    surface,
                    textColor,
                    borderColor);

            TableLayoutPanel folders =
                new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 2,
                    RowCount = 1,
                    BackColor = surface,
                    Padding =
                        new Padding(
                            6,
                            5,
                            6,
                            5)
                };

            folders.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    50F));
            folders.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    50F));

            lstBtwLibraryInclude =
                CreateCompactBtwFolderList(
                    inputBack,
                    inputText);

            lstBtwLibraryExclude =
                CreateCompactBtwFolderList(
                    inputBack,
                    inputText);

            folders.Controls.Add(
                CreateCompactBtwFolderEditor(
                    "Include folders",
                    lstBtwLibraryInclude,
                    textColor,
                    surface,
                    accentColor,
                    true),
                0,
                0);

            folders.Controls.Add(
                CreateCompactBtwFolderEditor(
                    "Exclude folders",
                    lstBtwLibraryExclude,
                    textColor,
                    surface,
                    accentColor,
                    false),
                1,
                0);

            ((Panel)sourcePanel.Tag).Controls.Add(
                folders);

            // ------------------------------------------------------------
            // RESULTS
            // ------------------------------------------------------------
            TableLayoutPanel results =
                new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 2,
                    RowCount = 1,
                    BackColor = pageBack,
                    Margin = new Padding(0)
                };

            results.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    65F));
            results.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    35F));

            Panel templatesPanel =
                CreateBtwSectionPanel(
                    "BTW Templates",
                    surface,
                    textColor,
                    borderColor);

            dgvBtwLibrary =
                CreateBtwGrid(
                    pageBack,
                    surface,
                    inputBack,
                    inputText,
                    textColor,
                    borderColor,
                    accentColor,
                    smallFont,
                    boldFont);

            ((Panel)templatesPanel.Tag).Controls.Add(
                dgvBtwLibrary);

            Panel selectedPanel =
                CreateBtwSectionPanel(
                    "Selected BTW",
                    surface,
                    textColor,
                    borderColor);

            TableLayoutPanel selected =
                new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 2,
                    BackColor = surface,
                    Padding = new Padding(6),
                    Margin = new Padding(0)
                };

            selected.RowStyles.Add(
                new RowStyle(
                    SizeType.Percent,
                    100F));
            selected.RowStyles.Add(
                new RowStyle(
                    SizeType.Absolute,
                    31F));

            picBtwLibraryPreview =
                new PictureBox
                {
                    Dock = DockStyle.Fill,
                    SizeMode =
                        PictureBoxSizeMode.Zoom,
                    BackColor = inputBack,
                    BorderStyle =
                        BorderStyle.FixedSingle,
                    Cursor = Cursors.Hand,
                    Margin =
                        new Padding(0, 0, 0, 4)
                };

            picBtwLibraryPreview.Click +=
                (s, e) =>
                    ShowLargeBtwLibraryPreview();

            TableLayoutPanel selectedActions =
                new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 5,
                    RowCount = 1,
                    BackColor = surface,
                    Margin = new Padding(0)
                };

            for (int i = 0; i < 5; i++)
            {
                selectedActions.ColumnStyles.Add(
                    new ColumnStyle(
                        SizeType.Percent,
                        20F));
            }

            btnBtwLibraryUseImage =
                CreateBtwCompactButton(
                    "Use Image",
                    accentColor,
                    ThemeManager.GetContrastTextColor(
                        accentColor),
                    smallFont,
                    72);

            btnBtwLibraryOpen =
                CreateBtwCompactButton(
                    "Open BTW",
                    surface,
                    textColor,
                    smallFont,
                    72);

            btnBtwLibraryFolder =
                CreateBtwCompactButton(
                    "Folder",
                    surface,
                    textColor,
                    smallFont,
                    62);

            Button openImage =
                CreateBtwCompactButton(
                    "Image",
                    surface,
                    textColor,
                    smallFont,
                    58);

            Button copyPath =
                CreateBtwCompactButton(
                    "Copy Path",
                    surface,
                    textColor,
                    smallFont,
                    76);

            btnBtwLibraryUseImage.Click +=
                (s, e) =>
                    UseSelectedBtwLibraryImage();

            btnBtwLibraryOpen.Click +=
                (s, e) =>
                    OpenSelectedBtwLibrary();

            btnBtwLibraryFolder.Click +=
                (s, e) =>
                    OpenSelectedBtwLibraryFolder();

            openImage.Click +=
                (s, e) =>
                    OpenSelectedBtwImage();

            copyPath.Click +=
                (s, e) =>
                    CopySelectedBtwLibraryPath();

            selectedActions.Controls.Add(
                btnBtwLibraryUseImage, 0, 0);
            selectedActions.Controls.Add(
                btnBtwLibraryOpen, 1, 0);
            selectedActions.Controls.Add(
                btnBtwLibraryFolder, 2, 0);
            selectedActions.Controls.Add(
                openImage, 3, 0);
            selectedActions.Controls.Add(
                copyPath, 4, 0);

            foreach (Control actionControl
                in selectedActions.Controls)
            {
                actionControl.Dock =
                    DockStyle.Fill;
            }

            selected.Controls.Add(
                picBtwLibraryPreview, 0, 0);
            selected.Controls.Add(
                selectedActions, 0, 1);

            ((Panel)selectedPanel.Tag).Controls.Add(
                selected);

            results.Controls.Add(
                templatesPanel, 0, 0);
            results.Controls.Add(
                selectedPanel, 1, 0);

            main.Controls.Add(
                header, 0, 0);
            main.Controls.Add(
                commandPanel, 0, 1);
            main.Controls.Add(
                progressPanel, 0, 2);
            main.Controls.Add(
                sourcePanel, 0, 3);
            main.Controls.Add(
                results, 0, 4);

            root.Controls.Add(main);

            pageBtwLibrary.Controls.Clear();
            pageBtwLibrary.Controls.Add(root);

            CreateBtwLibraryContextMenu();

            pageBtwLibrary.Enter +=
                (sender, args) =>
                {
                    ApplyBtwLibraryTheme();

                    if (txtBtwLibrarySearch != null)
                        txtBtwLibrarySearch.Focus();
                };

            LoadBtwLibraryFolders();
            RefreshBtwLibraryStats();
            RefreshBtwLibraryResults();
            RestoreBtwLibraryFoldersFromIndexIfMissing();
            SetBtwLibraryStageIdle();

            ApplyBtwLibraryTheme();
        }

        private Color GetBtwLibraryPageBack()
        {
            Color candidate =
                pageBtwLibrary != null &&
                pageBtwLibrary.Parent != null
                    ? pageBtwLibrary.Parent.BackColor
                    : Color.FromArgb(28, 32, 30);

            if (candidate == Color.Transparent ||
                candidate == Color.Empty)
            {
                candidate = Color.FromArgb(
                    28,
                    32,
                    30);
            }

            return candidate;
        }

        private Color GetBtwLibrarySurface(
            Color pageBack)
        {
            return IsBtwLibraryDark(pageBack)
                ? BlendBtwLibraryColor(
                    pageBack,
                    Color.White,
                    0.055)
                : BlendBtwLibraryColor(
                    pageBack,
                    Color.Black,
                    0.035);
        }

        private bool IsBtwLibraryDark(
            Color color)
        {
            double luminance =
                (0.299 * color.R) +
                (0.587 * color.G) +
                (0.114 * color.B);

            return luminance < 145;
        }

        private Color BlendBtwLibraryColor(
            Color first,
            Color second,
            double amount)
        {
            amount =
                Math.Max(
                    0,
                    Math.Min(
                        1,
                        amount));

            return Color.FromArgb(
                (int)Math.Round(
                    first.R +
                    ((second.R - first.R) * amount)),
                (int)Math.Round(
                    first.G +
                    ((second.G - first.G) * amount)),
                (int)Math.Round(
                    first.B +
                    ((second.B - first.B) * amount)));
        }

        private Panel CreateBtwSectionPanel(
            string title,
            Color backColor,
            Color textColor,
            Color borderColor)
        {
            Panel outer =
                new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = backColor,
                    BorderStyle =
                        BorderStyle.FixedSingle,
                    Padding = new Padding(1),
                    Margin =
                        new Padding(0, 0, 6, 0)
                };

            Label caption =
                new Label
                {
                    Text = title,
                    Dock = DockStyle.Top,
                    Height = 28,
                    Font =
                        new Font(
                            "Segoe UI Semibold",
                            9.25F,
                            FontStyle.Bold),
                    ForeColor = textColor,
                    BackColor = backColor,
                    TextAlign =
                        ContentAlignment.MiddleLeft,
                    Padding =
                        new Padding(8, 0, 0, 0),
                    Margin = new Padding(0)
                };

            Panel body =
                new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = backColor,
                    Padding = new Padding(3),
                    Margin = new Padding(0)
                };

            outer.Tag = body;
            outer.Controls.Add(body);
            outer.Controls.Add(caption);

            btwLibraryThemePanels.Add(outer);

            return outer;
        }

        private Button CreateBtwCompactButton(
            string text,
            Color backColor,
            Color foreColor,
            Font font,
            int width)
        {
            Button button =
                new Button
                {
                    Text = text,
                    Font = font,
                    BackColor = backColor,
                    ForeColor = foreColor,
                    FlatStyle = FlatStyle.Flat,
                    Width = width,
                    Height = 27,
                    Margin =
                        new Padding(2, 3, 2, 3),
                    Padding =
                        new Padding(3, 0, 3, 0),
                    AutoSize = false,
                    AutoEllipsis = false,
                    UseVisualStyleBackColor = false,
                    TabStop = false,
                    TextAlign = ContentAlignment.MiddleCenter,
                    UseMnemonic = false,
                    Cursor = Cursors.Hand,
                    MinimumSize = new Size(55, 27)
                };

            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor =
                activeTheme == null
                    ? SystemColors.ControlDark
                    : activeTheme.BorderColor;

            return button;
        }

        private ListBox CreateCompactBtwFolderList(
            Color backColor,
            Color foreColor)
        {
            return new ListBox
            {
                Dock = DockStyle.Fill,
                Font =
                    new Font(
                        "Segoe UI",
                        8.1F),
                IntegralHeight = false,
                HorizontalScrollbar = true,
                BackColor = backColor,
                ForeColor = foreColor,
                BorderStyle =
                    BorderStyle.FixedSingle,
                Margin = new Padding(2)
            };
        }

        private TableLayoutPanel CreateCompactBtwFolderEditor(
            string captionText,
            ListBox list,
            Color textColor,
            Color surface,
            Color accentColor,
            bool primaryAdd)
        {
            TableLayoutPanel area =
                new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 3,
                    BackColor = surface,
                    Margin = new Padding(3, 0, 3, 0),
                    Padding = new Padding(0)
                };

            area.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    100F));

            area.RowStyles.Add(
                new RowStyle(
                    SizeType.Absolute,
                    19F));
            area.RowStyles.Add(
                new RowStyle(
                    SizeType.Absolute,
                    50F));
            area.RowStyles.Add(
                new RowStyle(
                    SizeType.Absolute,
                    27F));

            Label caption =
                new Label
                {
                    Text = captionText,
                    Dock = DockStyle.Fill,
                    Font =
                        new Font(
                            "Segoe UI Semibold",
                            8.5F,
                            FontStyle.Bold),
                    ForeColor = textColor,
                    BackColor = Color.Transparent,
                    TextAlign =
                        ContentAlignment.MiddleLeft,
                    Padding = new Padding(1, 0, 0, 0),
                    Margin = new Padding(0)
                };

            list.Dock = DockStyle.Fill;
            list.Margin = new Padding(0);
            list.IntegralHeight = false;
            list.HorizontalScrollbar = true;

            TableLayoutPanel buttonRow =
                new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 2,
                    RowCount = 1,
                    BackColor = surface,
                    Margin = new Padding(0)
                };

            buttonRow.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Absolute,
                    82F));
            buttonRow.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Absolute,
                    82F));

            Button add =
                CreateBtwCompactButton(
                    "+ Add",
                    primaryAdd
                        ? accentColor
                        : surface,
                    primaryAdd
                        ? ThemeManager.GetContrastTextColor(
                            accentColor)
                        : textColor,
                    new Font(
                        "Segoe UI",
                        8F),
                    78);

            Button remove =
                CreateBtwCompactButton(
                    "Remove",
                    surface,
                    textColor,
                    new Font(
                        "Segoe UI",
                        8F),
                    78);

            add.Dock = DockStyle.Fill;
            remove.Dock = DockStyle.Fill;
            add.MinimumSize = new Size(70, 24);
            remove.MinimumSize = new Size(70, 24);
            add.Margin = new Padding(0, 1, 3, 1);
            remove.Margin = new Padding(0, 1, 3, 1);

            add.Click +=
                (s, e) =>
                {
                    AddBtwLibraryFolder(list);
                };

            remove.Click +=
                (s, e) =>
                {
                    RemoveSelectedFolder(list);
                };

            buttonRow.Controls.Add(add, 0, 0);
            buttonRow.Controls.Add(remove, 1, 0);

            area.Controls.Add(caption, 0, 0);
            area.Controls.Add(list, 0, 1);
            area.Controls.Add(buttonRow, 0, 2);

            return area;
        }

        private DataGridView CreateBtwGrid(
            Color pageBack,
            Color surface,
            Color inputBack,
            Color inputText,
            Color textColor,
            Color borderColor,
            Color accentColor,
            Font smallFont,
            Font boldFont)
        {
            DataGridView grid =
                new DataGridView
                {
                    Dock = DockStyle.Fill,
                    BackgroundColor = inputBack,
                    ForeColor = inputText,
                    GridColor = borderColor,
                    BorderStyle =
                        BorderStyle.FixedSingle,
                    AllowUserToAddRows = false,
                    AllowUserToDeleteRows = false,
                    AllowUserToResizeRows = false,
                    AllowUserToResizeColumns = true,
                    AutoGenerateColumns = false,
                    ReadOnly = true,
                    MultiSelect = false,
                    SelectionMode =
                        DataGridViewSelectionMode.FullRowSelect,
                    RowHeadersVisible = false,
                    AutoSizeRowsMode =
                        DataGridViewAutoSizeRowsMode.None,
                    AutoSizeColumnsMode =
                        DataGridViewAutoSizeColumnsMode.None,
                    Font = smallFont,
                    Margin = new Padding(3),
                    EnableHeadersVisualStyles = false,
                    ShowCellToolTips = true
                };

            grid.ColumnHeadersHeight = 27;
            grid.RowTemplate.Height = 28;

            grid.ColumnHeadersDefaultCellStyle =
                new DataGridViewCellStyle
                {
                    BackColor = surface,
                    ForeColor = textColor,
                    Font = boldFont,
                    Alignment =
                        DataGridViewContentAlignment.MiddleLeft,
                    SelectionBackColor = surface,
                    SelectionForeColor = textColor
                };

            grid.DefaultCellStyle =
                new DataGridViewCellStyle
                {
                    BackColor = inputBack,
                    ForeColor = inputText,
                    SelectionBackColor = accentColor,
                    SelectionForeColor =
                        ThemeManager.GetContrastTextColor(
                            accentColor),
                    Alignment =
                        DataGridViewContentAlignment.MiddleLeft,
                    Padding =
                        new Padding(3, 0, 3, 0)
                };

            grid.AlternatingRowsDefaultCellStyle =
                new DataGridViewCellStyle
                {
                    BackColor =
                        BlendBtwLibraryColor(
                            inputBack,
                            IsBtwLibraryDark(inputBack)
                                ? Color.White
                                : Color.Black,
                            0.025),
                    ForeColor = inputText
                };

            grid.Columns.Add(
                new DataGridViewTextBoxColumn
                {
                    Name = "Status",
                    HeaderText = "Status",
                    Width = 52,
                    SortMode =
                        DataGridViewColumnSortMode.NotSortable,
                    DefaultCellStyle =
                        new DataGridViewCellStyle
                        {
                            Alignment =
                                DataGridViewContentAlignment.MiddleCenter
                        }
                });

            grid.Columns.Add(
                new DataGridViewTextBoxColumn
                {
                    Name = "Barcode",
                    HeaderText = "Barcode",
                    Width = 125,
                    SortMode =
                        DataGridViewColumnSortMode.NotSortable
                });

            grid.Columns.Add(
                new DataGridViewTextBoxColumn
                {
                    Name = "Product",
                    HeaderText = "Product / BTW",
                    Width = 215,
                    SortMode =
                        DataGridViewColumnSortMode.NotSortable
                });

            grid.Columns.Add(
                new DataGridViewTextBoxColumn
                {
                    Name = "Location",
                    HeaderText = "Source Location",
                    AutoSizeMode =
                        DataGridViewAutoSizeColumnMode.Fill,
                    SortMode =
                        DataGridViewColumnSortMode.NotSortable
                });

            grid.SelectionChanged +=
                (sender, args) =>
                    ShowSelectedBtwLibraryRecord();

            grid.CellDoubleClick +=
                (sender, args) =>
                {
                    if (args.RowIndex >= 0)
                        OpenSelectedBtwLibrary();
                };

            grid.CellPainting +=
                DgvBtwLibrary_CellPainting;

            return grid;
        }

        private void DgvBtwLibrary_CellPainting(
            object sender,
            DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 ||
                e.ColumnIndex != 0)
                return;

            e.Handled = true;

            e.PaintBackground(
                e.CellBounds,
                true);

            e.Paint(
                e.CellBounds,
                DataGridViewPaintParts.Border);

            string status =
                e.Value == null
                    ? ""
                    : e.Value.ToString();

            Color statusColor =
                GetBtwLibraryStatusColor(status);

            int diameter =
                Math.Min(
                    13,
                    Math.Max(
                        8,
                        e.CellBounds.Height - 10));

            int x =
                e.CellBounds.X +
                ((e.CellBounds.Width - diameter) / 2);

            int y =
                e.CellBounds.Y +
                ((e.CellBounds.Height - diameter) / 2);

            using (SolidBrush brush =
                new SolidBrush(statusColor))
            using (Pen pen =
                new Pen(
                    IsBtwLibraryDark(statusColor)
                        ? Color.White
                        : Color.FromArgb(
                            80,
                            80,
                            80)))
            {
                e.Graphics.SmoothingMode =
                    System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                e.Graphics.FillEllipse(
                    brush,
                    new Rectangle(
                        x,
                        y,
                        diameter,
                        diameter));

                e.Graphics.DrawEllipse(
                    pen,
                    new Rectangle(
                        x,
                        y,
                        diameter - 1,
                        diameter - 1));
            }
        }

        private Color GetBtwLibraryStatusColor(
            string status)
        {
            if (string.Equals(
                status,
                "Ready",
                StringComparison.OrdinalIgnoreCase))
                return Color.FromArgb(
                    55, 185, 92);

            if (string.Equals(
                status,
                "Image Ready - Barcode Pending",
                StringComparison.OrdinalIgnoreCase))
                return Color.FromArgb(
                    245, 181, 52);

            if (string.Equals(
                status,
                "Ready - Barcode Not Detected",
                StringComparison.OrdinalIgnoreCase))
                return Color.FromArgb(
                    235, 132, 45);

            if (string.Equals(
                status,
                "Error",
                StringComparison.OrdinalIgnoreCase))
                return Color.FromArgb(
                    225, 75, 75);

            if (string.Equals(
                status,
                "Missing Image",
                StringComparison.OrdinalIgnoreCase))
                return Color.FromArgb(
                    145, 150, 156);

            if (string.Equals(
                status,
                "Duplicate Barcode",
                StringComparison.OrdinalIgnoreCase))
                return Color.FromArgb(
                    150, 91, 205);

            if (string.Equals(
                status,
                "New",
                StringComparison.OrdinalIgnoreCase))
                return Color.FromArgb(
                    65, 145, 230);

            return Color.FromArgb(
                125, 132, 138);
        }

        private void ApplyBtwLibraryTheme()
        {
            Color pageBack =
                GetBtwLibraryPageBack();
            Color surface =
                GetBtwLibrarySurface(pageBack);
            Color accent =
                activeTheme == null
                    ? Color.FromArgb(
                        72,
                        185,
                        112)
                    : activeTheme.ActiveColor;
            Color border =
                activeTheme == null
                    ? Color.FromArgb(
                        85,
                        95,
                        90)
                    : activeTheme.BorderColor;

            Color text =
                ThemeManager.GetContrastTextColor(
                    pageBack);

            Color inputBack =
                IsBtwLibraryDark(pageBack)
                    ? BlendBtwLibraryColor(
                        pageBack,
                        Color.White,
                        0.075)
                    : Color.White;

            Color inputText =
                ThemeManager.GetContrastTextColor(
                    inputBack);

            if (pageBtwLibrary != null)
                pageBtwLibrary.BackColor =
                    pageBack;

            ApplyBtwLibraryThemeRecursive(
                pageBtwLibrary,
                pageBack,
                surface,
                inputBack,
                inputText,
                text,
                border,
                accent);

            if (pageBtwLibrary != null &&
                pageBtwLibrary.Controls.Count > 0)
            {
                pageBtwLibrary.Controls[0].BackColor =
                    pageBack;
            }

            if (pageBtwLibrary != null)
            {
                foreach (Control child
                    in pageBtwLibrary.Controls)
                {
                    if (child == null)
                        continue;

                    foreach (Control nested
                        in child.Controls)
                    {
                        if (nested is Label &&
                            nested.Text ==
                            "BTW Image Library")
                        {
                            Control header =
                                nested.Parent;

                            header.BackColor =
                                pageBack;
                            break;
                        }
                    }
                }
            }

            // Section surfaces stay one level above the page.
            foreach (Panel section
                in btwLibraryThemePanels)
            {
                if (section == null ||
                    section.IsDisposed)
                    continue;

                section.BackColor = surface;
                section.BorderStyle =
                    BorderStyle.FixedSingle;

                Panel body =
                    section.Tag as Panel;

                if (body != null)
                    body.BackColor = surface;

                foreach (Control child
                    in section.Controls)
                {
                    Label label =
                        child as Label;

                    if (label != null)
                    {
                        label.BackColor =
                            surface;
                        label.ForeColor =
                            text;
                    }
                }
            }

            StyleBtwLibraryButton(
                btnBtwLibraryIndex,
                accent,
                ThemeManager.GetContrastTextColor(
                    accent),
                border);

            StyleBtwLibraryButton(
                btnBtwLibraryUseImage,
                accent,
                ThemeManager.GetContrastTextColor(
                    accent),
                border);

            StyleBtwLibraryButton(
                btnBtwLibraryCreateImages,
                surface,
                text,
                border);

            StyleBtwLibraryButton(
                btnBtwLibraryIndexBarcodes,
                surface,
                text,
                border);

            StyleBtwLibraryButton(
                btnBtwLibraryCancel,
                surface,
                text,
                border);

            StyleBtwLibraryButton(
                btnBtwLibraryOpen,
                surface,
                text,
                border);

            StyleBtwLibraryButton(
                btnBtwLibraryFolder,
                surface,
                text,
                border);

            if (pnlBtwLibraryProgress != null)
            {
                pnlBtwLibraryProgress.BackColor =
                    surface;
                pnlBtwLibraryProgress.Invalidate();
            }

            if (dgvBtwLibrary != null)
            {
                dgvBtwLibrary.BackgroundColor =
                    inputBack;
                dgvBtwLibrary.ForeColor =
                    inputText;
                dgvBtwLibrary.GridColor =
                    border;

                dgvBtwLibrary.ColumnHeadersDefaultCellStyle =
                    new DataGridViewCellStyle
                    {
                        BackColor = surface,
                        ForeColor = text,
                        Font =
                            new Font(
                                "Segoe UI Semibold",
                                8.75F,
                                FontStyle.Bold),
                        SelectionBackColor =
                            surface,
                        SelectionForeColor =
                            text
                    };

                dgvBtwLibrary.DefaultCellStyle =
                    new DataGridViewCellStyle
                    {
                        BackColor = inputBack,
                        ForeColor = inputText,
                        SelectionBackColor = accent,
                        SelectionForeColor =
                            ThemeManager.GetContrastTextColor(
                                accent),
                        Alignment =
                            DataGridViewContentAlignment.MiddleLeft,
                        Padding =
                            new Padding(
                                3,
                                0,
                                3,
                                0)
                    };

                dgvBtwLibrary.AlternatingRowsDefaultCellStyle =
                    new DataGridViewCellStyle
                    {
                        BackColor =
                            BlendBtwLibraryColor(
                                inputBack,
                                IsBtwLibraryDark(
                                    inputBack)
                                    ? Color.White
                                    : Color.Black,
                                0.025),
                        ForeColor = inputText
                    };

                dgvBtwLibrary.Invalidate();
            }

            if (txtBtwLibrarySearch != null)
            {
                txtBtwLibrarySearch.BackColor =
                    inputBack;
                txtBtwLibrarySearch.ForeColor =
                    inputText;
            }

            if (cmbBtwLibraryStatus != null)
            {
                cmbBtwLibraryStatus.BackColor =
                    inputBack;
                cmbBtwLibraryStatus.ForeColor =
                    inputText;
            }

            foreach (ListBox list in
                new[]
                {
                    lstBtwLibraryInclude,
                    lstBtwLibraryExclude
                })
            {
                if (list == null)
                    continue;

                list.BackColor =
                    inputBack;
                list.ForeColor =
                    inputText;
            }

            if (picBtwLibraryPreview != null)
            {
                picBtwLibraryPreview.BackColor =
                    inputBack;
                picBtwLibraryPreview.ForeColor =
                    inputText;
            }
        }

        private void ApplyBtwLibraryThemeRecursive(
            Control parent,
            Color pageBack,
            Color surface,
            Color inputBack,
            Color inputText,
            Color text,
            Color border,
            Color accent)
        {
            if (parent == null)
                return;

            foreach (Control child
                in parent.Controls)
            {
                if (child == null)
                    continue;

                if (child is TextBox ||
                    child is ComboBox ||
                    child is ListBox)
                {
                    child.BackColor =
                        inputBack;
                    child.ForeColor =
                        inputText;
                }
                else if (child is Button)
                {
                    Button button =
                        child as Button;

                    button.BackColor =
                        surface;
                    button.ForeColor =
                        text;
                    button.FlatStyle =
                        FlatStyle.Flat;
                    button.FlatAppearance.BorderColor =
                        border;
                    button.FlatAppearance.MouseOverBackColor =
                        accent;
                    button.FlatAppearance.MouseDownBackColor =
                        accent;
                }
                else if (child is PictureBox)
                {
                    child.BackColor =
                        inputBack;
                    child.ForeColor =
                        inputText;
                }
                else if (child is DataGridView)
                {
                    child.BackColor =
                        inputBack;
                    child.ForeColor =
                        inputText;
                }
                else if (child is Label)
                {
                    child.BackColor =
                        Color.Transparent;
                    child.ForeColor =
                        text;
                }
                else
                {
                    child.BackColor =
                        surface;
                }

                ApplyBtwLibraryThemeRecursive(
                    child,
                    pageBack,
                    surface,
                    inputBack,
                    inputText,
                    text,
                    border,
                    accent);
            }
        }

        private void StyleBtwLibraryButton(
            Button button,
            Color backColor,
            Color foreColor,
            Color borderColor)
        {
            if (button == null)
                return;

            button.BackColor =
                backColor;
            button.ForeColor =
                foreColor;
            button.FlatStyle =
                FlatStyle.Flat;
            button.UseVisualStyleBackColor =
                false;
            button.TextAlign =
                ContentAlignment.MiddleCenter;
            button.FlatAppearance.BorderSize =
                1;
            button.FlatAppearance.BorderColor =
                borderColor;
            button.FlatAppearance.MouseOverBackColor =
                backColor == button.BackColor
                    ? borderColor
                    : backColor;
            button.FlatAppearance.MouseDownBackColor =
                backColor;
            button.Invalidate();
        }

        private void CreateBtwLibraryContextMenu()
        {
            if (dgvBtwLibrary == null)
                return;

            ContextMenuStrip menu =
                new ContextMenuStrip();

            menu.Items.Add(
                "Copy BTW",
                null,
                (s, e) =>
                    CopySelectedBtwPath());

            menu.Items.Add(
                "Copy Image",
                null,
                (s, e) =>
                    CopySelectedBtwImagePath());

            menu.Items.Add(
                new ToolStripSeparator());

            menu.Items.Add(
                "Open BTW",
                null,
                (s, e) =>
                    OpenSelectedBtwLibrary());

            menu.Items.Add(
                "Open Image",
                null,
                (s, e) =>
                    OpenSelectedBtwImage());

            menu.Items.Add(
                new ToolStripSeparator());

            menu.Items.Add(
                "Open BTW File Location",
                null,
                (s, e) =>
                    OpenFileLocation(
                        GetSelectedBtwLibraryRecord(),
                        false));

            menu.Items.Add(
                "Open Image File Location",
                null,
                (s, e) =>
                    OpenFileLocation(
                        GetSelectedBtwLibraryRecord(),
                        true));

            menu.Items.Add(
                "Copy BTW Path",
                null,
                (s, e) =>
                    CopySelectedBtwPath());

            menu.Items.Add(
                "Copy Image Path",
                null,
                (s, e) =>
                    CopySelectedBtwImagePath());

            dgvBtwLibrary.ContextMenuStrip =
                menu;

            dgvBtwLibrary.CellMouseDown +=
                (s, e) =>
                {
                    if (e.RowIndex >= 0 &&
                        e.Button ==
                            MouseButtons.Right)
                    {
                        dgvBtwLibrary.ClearSelection();
                        dgvBtwLibrary.Rows[
                            e.RowIndex].Selected = true;
                    }
                };
        }

        private async void BtnBtwLibraryIndex_Click(object sender, EventArgs e)
        {
            await RunBtwLibraryStageAsync(
                "Index BTW",
                async token =>
                {
                    List<string> includes = GetBtwIncludeFolders();
                    List<string> excludes = GetBtwExcludeFolders();

                    SaveBtwLibraryFolderSettings();

                    return await Task.Run(
                        () => btwImageLibraryEngine.IndexBtwTemplates(
                            includes,
                            excludes,
                            ReportBtwLibraryProgress,
                            token),
                        token);
                });
        }

        private async void BtnBtwLibraryCreateImages_Click(object sender, EventArgs e)
        {
            await RunBtwLibraryStageAsync(
                "Create Images",
                async token =>
                {
                    EnsureBtwLibrarySettings();

                    if (btwLibraryModuleSettings == null)
                    {
                        btwLibraryModuleSettings =
                            BtwImageLibraryModuleSettings.Load(
                                btwLibrarySettingsFile,
                                currentSettings.DefaultDpi);
                    }

                    int dpi = btwLibraryModuleSettings.Dpi;
                    string extension =
                        btwLibraryModuleSettings.ImageExtension;

                    return await Task.Run(
                        () => btwImageLibraryEngine.CreateImages(
                            dpi,
                            extension,
                            ReportBtwLibraryProgress,
                            token),
                        token);
                });
        }

        private async void BtnBtwLibraryIndexBarcodes_Click(object sender, EventArgs e)
        {
            await RunBtwLibraryStageAsync(
                "Index Barcodes",
                async token =>
                {
                    return await Task.Run(
                        () => btwImageLibraryEngine.IndexBarcodes(
                            ReportBtwLibraryProgress,
                            token),
                        token);
                });
        }

        private async Task RunBtwLibraryStageAsync(
            string stage,
            Func<CancellationToken, Task<BtwImageLibraryRefreshResult>> action)
        {
            if (btwLibraryCts != null)
                return;

            if (stage == "Index BTW" &&
                GetBtwIncludeFolders().Count == 0)
            {
                MessageBox.Show(
                    "Add at least one BTW Include Folder first.",
                    "BTW Image Library",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            btwLibraryCts = new CancellationTokenSource();

            SetBtwLibraryButtonsEnabled(false);
            SetBtwLibraryStage(
                stage,
                0,
                0,
                "Starting...");

            try
            {
                BtwImageLibraryRefreshResult result =
                    await action(btwLibraryCts.Token);

                RefreshBtwLibraryStats();
                RefreshBtwLibraryResults();

                SetBtwLibraryStage(
                    stage + " complete",
                    1,
                    1,
                    BuildStageSummary(stage, result));

                MessageBox.Show(
                    BuildStageSummary(stage, result),
                    "BTW Image Library",
                    MessageBoxButtons.OK,
                    result.Errors == 0
                        ? MessageBoxIcon.Information
                        : MessageBoxIcon.Warning);
            }
            catch (OperationCanceledException)
            {
                SetBtwLibraryStage(
                    stage + " cancelled",
                    0,
                    0,
                    "The operation was cancelled.");
            }
            catch (Exception ex)
            {
                SetBtwLibraryStage(
                    stage + " failed",
                    0,
                    0,
                    ex.Message);

                MessageBox.Show(
                    "BTW Image Library " + stage + " failed.\n\n" + ex.Message,
                    "BTW Image Library",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                if (btwLibraryCts != null)
                {
                    btwLibraryCts.Dispose();
                    btwLibraryCts = null;
                }

                SetBtwLibraryButtonsEnabled(true);
            }
        }

        private void ReportBtwLibraryProgress(BtwImageLibraryProgress p)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() =>
                    ReportBtwLibraryProgress(p)));
                return;
            }

            int total = Math.Max(0, p.Total);
            int processed = Math.Max(0, p.Processed);

            int percent = total <= 0
                ? (p.Completed ? 100 : 0)
                : (int)Math.Round(
                    Math.Max(0, Math.Min(100, processed * 100.0 / total)));

            pnlBtwLibraryProgress.Tag =
                new Point(processed, total);
            pnlBtwLibraryProgress.Invalidate();

            lblBtwLibraryStage.Text =
                string.IsNullOrWhiteSpace(p.Operation)
                    ? "Working..."
                    : p.Operation;

            lblBtwLibraryProgressText.Text =
                total <= 0
                    ? "Ready"
                    : string.Format(
                        "{0:N0} / {1:N0}",
                        processed,
                        total);

            lblBtwLibraryProgressPercent.Text =
                percent.ToString("0") + "%";

            lblBtwLibraryProgressDetails.Text =
                string.Format(
                    "Found: {0:N0}    Indexed: {1:N0}    Created: {2:N0}    Detected: {3:N0}    Not detected: {4:N0}    Errors: {5:N0}    Duplicates: {6:N0}\r\nCurrent: {7}",
                    p.Found,
                    p.Indexed,
                    p.Created,
                    p.Detected,
                    p.NotDetected,
                    p.Errors,
                    p.Duplicates,
                    string.IsNullOrWhiteSpace(p.CurrentFile)
                        ? "—"
                        : Path.GetFileName(p.CurrentFile));

            RefreshBtwLibraryStats();
        }

        private void PnlBtwLibraryProgress_Paint(
            object sender,
            PaintEventArgs e)
        {
            Point progress =
                pnlBtwLibraryProgress.Tag is Point
                    ? (Point)pnlBtwLibraryProgress.Tag
                    : new Point(0, 0);

            int processed = progress.X;
            int total = progress.Y;

            Color pageBack =
                GetBtwLibraryPageBack();
            Color accent =
                activeTheme == null
                    ? Color.FromArgb(
                        72,
                        185,
                        112)
                    : activeTheme.ActiveColor;
            Color border =
                activeTheme == null
                    ? Color.FromArgb(
                        85,
                        95,
                        90)
                    : activeTheme.BorderColor;

            Rectangle bar =
                new Rectangle(
                    4,
                    4,
                    Math.Max(
                        10,
                        pnlBtwLibraryProgress.ClientSize.Width - 9),
                    Math.Max(
                        12,
                        pnlBtwLibraryProgress.ClientSize.Height - 9));

            double ratio =
                total <= 0
                    ? 0
                    : Math.Max(
                        0,
                        Math.Min(
                            1,
                            processed /
                            (double)total));

            int fillWidth =
                (int)Math.Round(
                    bar.Width * ratio);

            using (SolidBrush background =
                new SolidBrush(
                    IsBtwLibraryDark(pageBack)
                        ? BlendBtwLibraryColor(
                            pageBack,
                            Color.Black,
                            0.08)
                        : BlendBtwLibraryColor(
                            pageBack,
                            Color.Black,
                            0.04)))
            using (SolidBrush fill =
                new SolidBrush(accent))
            using (Pen outline =
                new Pen(border))
            using (StringFormat format =
                new StringFormat
                {
                    Alignment =
                        StringAlignment.Center,
                    LineAlignment =
                        StringAlignment.Center
                })
            {
                e.Graphics.SmoothingMode =
                    System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                e.Graphics.FillRectangle(
                    background,
                    bar);

                if (fillWidth > 0)
                {
                    e.Graphics.FillRectangle(
                        fill,
                        new Rectangle(
                            bar.X,
                            bar.Y,
                            fillWidth,
                            bar.Height));
                }

                e.Graphics.DrawRectangle(
                    outline,
                    bar);

                string text =
                    total <= 0
                        ? "Ready"
                        : string.Format(
                            "{0:N0} / {1:N0} BTW",
                            processed,
                            total);

                Color textColor =
                    ThemeManager.GetContrastTextColor(
                        accent);

                using (SolidBrush brush =
                    new SolidBrush(textColor))
                using (Font font =
                    new Font(
                        "Segoe UI",
                        8.5F,
                        FontStyle.Bold))
                {
                    e.Graphics.DrawString(
                        text,
                        font,
                        brush,
                        bar,
                        format);
                }
            }
        }

        private void SetBtwLibraryStage(
            string stage,
            int processed,
            int total,
            string details)
        {
            lblBtwLibraryStage.Text = stage;
            lblBtwLibraryProgressText.Text =
                total <= 0
                    ? "Ready"
                    : string.Format("{0:N0} / {1:N0}", processed, total);

            int percent = total <= 0
                ? 0
                : (int)Math.Round(
                    Math.Max(0, Math.Min(100, processed * 100.0 / total)));

            pnlBtwLibraryProgress.Tag =
                new Point(processed, total);
            pnlBtwLibraryProgress.Invalidate();

            lblBtwLibraryProgressPercent.Text =
                percent + "%";
            lblBtwLibraryProgressDetails.Text = details ?? "";
        }

        private void SetBtwLibraryStageIdle()
        {
            SetBtwLibraryStage(
                "Ready — choose a manual stage above.",
                0,
                0,
                "Index BTW → Create Images → Index Barcodes");
        }

        private void SetBtwLibraryButtonsEnabled(bool enabled)
        {
            btnBtwLibraryIndex.Enabled = enabled;
            btnBtwLibraryCreateImages.Enabled = enabled;
            btnBtwLibraryIndexBarcodes.Enabled = enabled;
            btnBtwLibraryCancel.Enabled = !enabled;
            btnBtwLibrarySettings.Enabled = enabled;
        }

        private string BuildStageSummary(
            string stage,
            BtwImageLibraryRefreshResult result)
        {
            if (result == null)
                return stage + " completed.";

            if (stage == "Index BTW")
            {
                return string.Format(
                    "Index BTW completed.\n\n" +
                    "BTW found: {0:N0}\n" +
                    "Indexed: {1:N0}\n" +
                    "Errors: {2:N0}",
                    result.TotalFiles,
                    result.IndexedFiles,
                    result.Errors);
            }

            if (stage == "Create Images")
            {
                return string.Format(
                    "Create Images completed.\n\n" +
                    "Templates processed: {0:N0}\n" +
                    "Images created: {1:N0}\n" +
                    "Image failures: {2:N0}\n" +
                    "Errors: {3:N0}",
                    result.TotalFiles,
                    result.ImagesCreated,
                    result.ImagesFailed,
                    result.Errors);
            }

            return string.Format(
                "Index Barcodes completed.\n\n" +
                "Images processed: {0:N0}\n" +
                "Barcodes detected: {1:N0}\n" +
                "Barcodes not detected: {2:N0}\n" +
                "Duplicate barcodes: {3:N0}\n" +
                "Errors: {4:N0}",
                result.TotalFiles,
                result.BarcodeDetected,
                result.BarcodeNotDetected,
                result.DuplicateBarcodes,
                result.Errors);
        }

        private List<string> GetBtwIncludeFolders()
        {
            return lstBtwLibraryInclude.Items
                .Cast<object>()
                .Select(x => x.ToString())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
        }

        private List<string> GetBtwExcludeFolders()
        {
            return lstBtwLibraryExclude.Items
                .Cast<object>()
                .Select(x => x.ToString())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
        }

        private void LoadBtwLibraryFolders()
        {
            EnsureBtwLibrarySettings();
            lstBtwLibraryInclude.Items.Clear();
            lstBtwLibraryExclude.Items.Clear();

            foreach (string folder in SplitBtwLibraryFolders(
                currentSettings.BtwLibraryIncludeFolders))
            {
                lstBtwLibraryInclude.Items.Add(folder);
            }

            foreach (string folder in SplitBtwLibraryFolders(
                currentSettings.BtwLibraryExcludeFolders))
            {
                lstBtwLibraryExclude.Items.Add(folder);
            }
        }

        private static IEnumerable<string> SplitBtwLibraryFolders(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Enumerable.Empty<string>();

            // Accept the current pipe-delimited format and lists saved by
            // earlier revisions using semicolons or line breaks.
            return value
                .Split(
                    new[] { '|', ';', '\r', '\n' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim().Trim('"'))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private void AddBtwLibraryFolder(ListBox target)
        {
            string folder = SelectFolderModern("BTWImageLibrary.Folder");

            if (string.IsNullOrWhiteSpace(folder))
                return;

            try
            {
                folder = Path.GetFullPath(folder);
            }
            catch
            {
            }

            bool exists = target.Items.Cast<object>()
                .Any(x => string.Equals(
                    x.ToString(),
                    folder,
                    StringComparison.OrdinalIgnoreCase));

            if (!exists)
                target.Items.Add(folder);

            SaveBtwLibraryFolderSettings();
        }

        private void RemoveSelectedFolder(ListBox target)
        {
            if (target.SelectedIndex >= 0)
                target.Items.RemoveAt(target.SelectedIndex);

            SaveBtwLibraryFolderSettings();
        }

        private void SaveBtwLibraryFolderSettings()
        {
            EnsureBtwLibrarySettings();
            currentSettings.BtwLibraryIncludeFolders =
                string.Join("|", GetBtwIncludeFolders());

            currentSettings.BtwLibraryExcludeFolders =
                string.Join("|", GetBtwExcludeFolders());

            SettingsManager.Save(currentSettings);
        }

        private void RestoreBtwLibraryFoldersFromIndexIfMissing()
        {
            if (lstBtwLibraryInclude == null ||
                lstBtwLibraryInclude.Items.Count > 0 ||
                dgvBtwLibrary == null)
                return;

            HashSet<string> folders =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            foreach (DataGridViewRow row in dgvBtwLibrary.Rows)
            {
                BtwImageLibraryRecord record =
                    row.Tag as BtwImageLibraryRecord;

                if (record == null ||
                    string.IsNullOrWhiteSpace(record.FilePath))
                    continue;

                try
                {
                    string folder =
                        Path.GetDirectoryName(record.FilePath);

                    if (!string.IsNullOrWhiteSpace(folder))
                        folders.Add(
                            Path.GetFullPath(folder));
                }
                catch
                {
                }
            }

            if (folders.Count == 0)
                return;

            // Recover source folders from already indexed BTW files when
            // the settings file was cleared by an earlier UI revision.
            foreach (string folder in
                folders.OrderBy(
                    x => x,
                    StringComparer.OrdinalIgnoreCase))
            {
                lstBtwLibraryInclude.Items.Add(folder);
            }

            SaveBtwLibraryFolderSettings();
        }

        private void RefreshBtwLibraryStats()
        {
            if (btwImageLibraryEngine == null ||
                lblBtwLibraryStats == null)
                return;

            try
            {
                lblBtwLibraryStats.Text = string.Format(
                    "Templates: {0:N0}    Images: {1:N0}    Missing: {2:N0}    Errors: {3:N0}    Duplicate barcodes: {4:N0}",
                    btwImageLibraryEngine.Count,
                    btwImageLibraryEngine.ImageCount,
                    btwImageLibraryEngine.MissingImageCount,
                    btwImageLibraryEngine.ErrorCount,
                    btwImageLibraryEngine.DuplicateBarcodeCount);
            }
            catch
            {
            }
        }

        private void RefreshBtwLibraryResults()
        {
            if (dgvBtwLibrary == null ||
                btwImageLibraryEngine == null)
                return;

            try
            {
                string query = txtBtwLibrarySearch == null
                    ? ""
                    : txtBtwLibrarySearch.Text;

                string status = cmbBtwLibraryStatus == null ||
                                cmbBtwLibraryStatus.SelectedItem == null
                    ? "All"
                    : cmbBtwLibraryStatus.SelectedItem.ToString();

                List<BtwImageLibraryRecord> records =
                    btwImageLibraryEngine.Search(query, status);

                dgvBtwLibrary.Rows.Clear();

                foreach (BtwImageLibraryRecord r in records)
                {
                    int row = dgvBtwLibrary.Rows.Add(
                        r.Status,
                        r.Barcode,
                        r.ProductName,
                        r.FilePath);

                    dgvBtwLibrary.Rows[row].Tag = r;
                    dgvBtwLibrary.Rows[row]
                        .Cells[0]
                        .ToolTipText =
                        r.Status ?? "";
                }

                if (dgvBtwLibrary.Rows.Count > 0)
                {
                    dgvBtwLibrary.Rows[0].Selected = true;
                    ShowSelectedBtwLibraryRecord();
                }
                else
                {
                    ClearBtwLibraryDetails();
                }
            }
            catch
            {
                ClearBtwLibraryDetails();
            }
        }

        private BtwImageLibraryRecord GetSelectedBtwLibraryRecord()
        {
            if (dgvBtwLibrary == null ||
                dgvBtwLibrary.SelectedRows.Count == 0)
                return null;

            return dgvBtwLibrary.SelectedRows[0].Tag
                as BtwImageLibraryRecord;
        }

        private void ShowSelectedBtwLibraryRecord()
        {
            BtwImageLibraryRecord r = GetSelectedBtwLibraryRecord();

            if (r == null)
            {
                ClearBtwLibraryDetails();
                return;
            }

            selectedBtwLibraryImage = r.ImagePath ?? "";

            LoadBtwLibraryPreview(r.ImagePath);

            bool imageReady =
                !string.IsNullOrWhiteSpace(r.ImagePath) &&
                File.Exists(r.ImagePath);

            btnBtwLibraryUseImage.Enabled = imageReady;
            btnBtwLibraryOpen.Enabled =
                !string.IsNullOrWhiteSpace(r.FilePath) &&
                File.Exists(r.FilePath);
            btnBtwLibraryFolder.Enabled =
                !string.IsNullOrWhiteSpace(r.FilePath) &&
                File.Exists(r.FilePath);
        }

        private void ClearBtwLibraryDetails()
        {
            selectedBtwLibraryImage = "";

            if (picBtwLibraryPreview != null)
            {
                Image old = picBtwLibraryPreview.Image;
                picBtwLibraryPreview.Image = null;
                if (old != null)
                    old.Dispose();
            }

            if (btnBtwLibraryUseImage != null)
                btnBtwLibraryUseImage.Enabled = false;

            if (btnBtwLibraryOpen != null)
                btnBtwLibraryOpen.Enabled = false;

            if (btnBtwLibraryFolder != null)
                btnBtwLibraryFolder.Enabled = false;
        }

        private void LoadBtwLibraryPreview(string imagePath)
        {
            if (picBtwLibraryPreview == null)
                return;

            Image old = picBtwLibraryPreview.Image;
            picBtwLibraryPreview.Image = null;

            if (old != null)
                old.Dispose();

            if (string.IsNullOrWhiteSpace(imagePath) ||
                !File.Exists(imagePath))
                return;

            try
            {
                using (Image source = Image.FromFile(imagePath))
                {
                    picBtwLibraryPreview.Image =
                        new Bitmap(source);
                }
            }
            catch
            {
            }
        }

        private void ShowLargeBtwLibraryPreview()
        {
            if (string.IsNullOrWhiteSpace(selectedBtwLibraryImage) ||
                !File.Exists(selectedBtwLibraryImage))
                return;

            Form preview = new Form
            {
                Text = "BTW Image Preview",
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(1000, 760),
                MinimumSize = new Size(600, 450),
                BackColor = Color.FromArgb(35, 35, 35)
            };

            PictureBox picture = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(35, 35, 35)
            };

            try
            {
                using (Image source = Image.FromFile(selectedBtwLibraryImage))
                    picture.Image = new Bitmap(source);
            }
            catch
            {
                preview.Dispose();
                return;
            }

            preview.Controls.Add(picture);

            preview.FormClosed += (s, e) =>
            {
                if (picture.Image != null)
                    picture.Image.Dispose();
            };

            preview.ShowDialog(this);
        }

        private void OpenSelectedBtwLibrary()
        {
            BtwImageLibraryRecord r = GetSelectedBtwLibraryRecord();

            if (r == null ||
                string.IsNullOrWhiteSpace(r.FilePath) ||
                !File.Exists(r.FilePath))
                return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = r.FilePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Unable to open the BTW file.\n\n" + ex.Message,
                    "BTW Image Library",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void OpenSelectedBtwLibraryFolder()
        {
            BtwImageLibraryRecord r = GetSelectedBtwLibraryRecord();

            if (r == null ||
                string.IsNullOrWhiteSpace(r.FilePath) ||
                !File.Exists(r.FilePath))
                return;

            OpenFileLocation(r, false);
        }

        private void OpenSelectedBtwImage()
        {
            BtwImageLibraryRecord r = GetSelectedBtwLibraryRecord();

            if (r == null ||
                string.IsNullOrWhiteSpace(r.ImagePath) ||
                !File.Exists(r.ImagePath))
                return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = r.ImagePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Unable to open the generated image.\n\n" + ex.Message,
                    "BTW Image Library",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void OpenFileLocation(
            BtwImageLibraryRecord r,
            bool image)
        {
            if (r == null)
                return;

            string path = image
                ? r.ImagePath
                : r.FilePath;

            if (string.IsNullOrWhiteSpace(path) ||
                !File.Exists(path))
                return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "/select,\"" + path + "\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Unable to open the file location.\n\n" + ex.Message,
                    "BTW Image Library",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void CopySelectedBtwPath()
        {
            BtwImageLibraryRecord r = GetSelectedBtwLibraryRecord();

            if (r == null ||
                string.IsNullOrWhiteSpace(r.FilePath))
                return;

            Clipboard.SetText(r.FilePath);
        }

        private void CopySelectedBtwImagePath()
        {
            BtwImageLibraryRecord r = GetSelectedBtwLibraryRecord();

            if (r == null ||
                string.IsNullOrWhiteSpace(r.ImagePath))
                return;

            Clipboard.SetText(r.ImagePath);
        }

        private void CopySelectedBtwLibraryPath()
        {
            CopySelectedBtwPath();
        }

        private void UseSelectedBtwLibraryImage()
        {
            BtwImageLibraryRecord r = GetSelectedBtwLibraryRecord();

            if (r == null ||
                string.IsNullOrWhiteSpace(r.ImagePath) ||
                !File.Exists(r.ImagePath))
                return;

            selectedCollageFiles = new[] { r.ImagePath };
            selectedCollageFolder = "";

            if (txtCollageSource != null)
                txtCollageSource.Text = r.ImagePath;

            if (cmbCollageOutputType != null)
                cmbCollageOutputType.SelectedItem = "PDF";

            if (cmbCollageGrid != null)
                cmbCollageGrid.SelectedIndex = 0;

            manualCollageSlots = null;
            manualCollageCaptions = null;
            manualCollageCustomNames = null;

            UpdateCollageArrangementButtons();

            if (navButtons.Count > 2)
                SwitchPage(
                    pageFormat,
                    navButtons[2],
                    "Step 2: Image Formatting");

            MessageBox.Show(
                "The clean BTW image has been loaded into Image Collage Builder.\n\n" +
                "The master image remains borderless. Apply the required border only in the collage workflow.",
                "BTW Image Library",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        private void ShowBtwLibrarySettingsDialog()
        {
            EnsureBtwLibrarySettings();

            if (btwLibraryModuleSettings == null)
            {
                btwLibraryModuleSettings =
                    BtwImageLibraryModuleSettings.Load(
                        btwLibrarySettingsFile,
                        currentSettings.DefaultDpi);
            }

            Color pageBack = GetBtwLibraryPageBack();
            Color surface = GetBtwLibrarySurface(pageBack);
            Color textColor =
                ThemeManager.GetContrastTextColor(pageBack);
            Color accentColor =
                activeTheme == null
                    ? SystemColors.Highlight
                    : activeTheme.ActiveColor;
            Color inputBack =
                IsBtwLibraryDark(pageBack)
                    ? BlendBtwLibraryColor(
                        pageBack,
                        Color.White,
                        0.075)
                    : Color.White;
            Color inputText =
                ThemeManager.GetContrastTextColor(inputBack);

            using (Form dialog = new Form())
            {
                dialog.Text = "BTW Image Library Settings";
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;
                dialog.ShowInTaskbar = false;
                dialog.ClientSize = new Size(500, 310);
                dialog.BackColor = pageBack;

                TableLayoutPanel root =
                    new TableLayoutPanel
                    {
                        Dock = DockStyle.Fill,
                        ColumnCount = 1,
                        RowCount = 4,
                        BackColor = pageBack,
                        Padding = new Padding(18)
                    };

                root.RowStyles.Add(
                    new RowStyle(SizeType.Absolute, 34F));
                root.RowStyles.Add(
                    new RowStyle(SizeType.Absolute, 96F));
                root.RowStyles.Add(
                    new RowStyle(SizeType.Percent, 100F));
                root.RowStyles.Add(
                    new RowStyle(SizeType.Absolute, 42F));

                Label title =
                    new Label
                    {
                        Text = "BTW Image Library Settings",
                        Dock = DockStyle.Fill,
                        Font =
                            new Font(
                                "Segoe UI Semibold",
                                13F,
                                FontStyle.Bold),
                        ForeColor = textColor,
                        BackColor = Color.Transparent,
                        TextAlign = ContentAlignment.MiddleLeft
                    };

                TableLayoutPanel fields =
                    new TableLayoutPanel
                    {
                        Dock = DockStyle.Fill,
                        ColumnCount = 2,
                        RowCount = 2,
                        BackColor = surface,
                        Padding = new Padding(12, 10, 12, 8)
                    };

                fields.ColumnStyles.Add(
                    new ColumnStyle(
                        SizeType.Absolute,
                        175F));
                fields.ColumnStyles.Add(
                    new ColumnStyle(
                        SizeType.Percent,
                        100F));

                fields.RowStyles.Add(
                    new RowStyle(
                        SizeType.Absolute,
                        34F));
                fields.RowStyles.Add(
                    new RowStyle(
                        SizeType.Absolute,
                        34F));

                Label dpiLabel =
                    new Label
                    {
                        Text = "Image DPI (Custom)",
                        Dock = DockStyle.Fill,
                        Font = new Font("Segoe UI", 9F),
                        ForeColor = textColor,
                        TextAlign = ContentAlignment.MiddleLeft
                    };

                ComboBox dpiCombo =
                    new ComboBox
                    {
                        Dock = DockStyle.Fill,
                        DropDownStyle =
                            ComboBoxStyle.DropDown,
                        Font = new Font("Segoe UI", 9F),
                        BackColor = inputBack,
                        ForeColor = inputText,
                        IntegralHeight = true,
                        MaxLength = 4
                    };

                dpiCombo.Items.AddRange(
                    new object[]
                    {
                        "72",
                        "96",
                        "150",
                        "200",
                        "300",
                        "400",
                        "600",
                        "1200"
                    });

                dpiCombo.Text =
                    (btwLibraryModuleSettings.Dpi > 0
                        ? btwLibraryModuleSettings.Dpi
                        : 300).ToString();

                dpiCombo.KeyPress +=
                    (s, e) =>
                    {
                        if (!char.IsControl(e.KeyChar) &&
                            !char.IsDigit(e.KeyChar))
                        {
                            e.Handled = true;
                        }
                    };

                Label extensionLabel =
                    new Label
                    {
                        Text = "Image Extension",
                        Dock = DockStyle.Fill,
                        Font = new Font("Segoe UI", 9F),
                        ForeColor = textColor,
                        TextAlign = ContentAlignment.MiddleLeft
                    };

                ComboBox extensionCombo =
                    new ComboBox
                    {
                        Dock = DockStyle.Fill,
                        DropDownStyle =
                            ComboBoxStyle.DropDownList,
                        Font = new Font("Segoe UI", 9F),
                        BackColor = inputBack,
                        ForeColor = inputText
                    };

                extensionCombo.Items.AddRange(
                    new object[]
                    {
                        "PNG (.png)",
                        "JPG (.jpg)",
                        "BMP (.bmp)",
                        "TIFF (.tif)"
                    });

                string selectedExtension =
                    BtwImageLibraryModuleSettings.NormalizeExtension(
                        btwLibraryModuleSettings.ImageExtension);

                int extensionIndex =
                    selectedExtension == "JPG" ? 1 :
                    selectedExtension == "BMP" ? 2 :
                    selectedExtension == "TIFF" ? 3 : 0;

                extensionCombo.SelectedIndex =
                    extensionIndex;

                fields.Controls.Add(dpiLabel, 0, 0);
                fields.Controls.Add(dpiCombo, 1, 0);
                fields.Controls.Add(extensionLabel, 0, 1);
                fields.Controls.Add(extensionCombo, 1, 1);

                Label note =
                    new Label
                    {
                        Text =
                            "These settings are used by Create Images. " +
                            "Existing generated files are not deleted automatically.",
                        Dock = DockStyle.Fill,
                        Font = new Font("Segoe UI", 8.5F),
                        ForeColor = textColor,
                        BackColor = Color.Transparent,
                        AutoEllipsis = true,
                        TextAlign = ContentAlignment.TopLeft,
                        Padding = new Padding(2, 8, 2, 0)
                    };

                TableLayoutPanel buttons =
                    new TableLayoutPanel
                    {
                        Dock = DockStyle.Fill,
                        ColumnCount = 3,
                        RowCount = 1,
                        BackColor = pageBack
                    };

                buttons.ColumnStyles.Add(
                    new ColumnStyle(SizeType.Percent, 100F));
                buttons.ColumnStyles.Add(
                    new ColumnStyle(SizeType.Absolute, 90F));
                buttons.ColumnStyles.Add(
                    new ColumnStyle(SizeType.Absolute, 90F));

                Button save =
                    CreateBtwCompactButton(
                        "Save",
                        accentColor,
                        ThemeManager.GetContrastTextColor(
                            accentColor),
                        new Font(
                            "Segoe UI Semibold",
                            9F,
                            FontStyle.Bold),
                        84);

                Button cancel =
                    CreateBtwCompactButton(
                        "Cancel",
                        surface,
                        textColor,
                        new Font("Segoe UI", 9F),
                        84);

                save.DialogResult = DialogResult.OK;
                cancel.DialogResult = DialogResult.Cancel;

                buttons.Controls.Add(
                    new Panel
                    {
                        Dock = DockStyle.Fill,
                        BackColor = pageBack
                    },
                    0,
                    0);

                buttons.Controls.Add(save, 1, 0);
                buttons.Controls.Add(cancel, 2, 0);

                root.Controls.Add(title, 0, 0);
                root.Controls.Add(fields, 0, 1);
                root.Controls.Add(note, 0, 2);
                root.Controls.Add(buttons, 0, 3);

                dialog.AcceptButton = save;
                dialog.CancelButton = cancel;
                dialog.Controls.Add(root);

                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                int selectedDpi;
                if (!int.TryParse(
                    dpiCombo.Text.Trim(),
                    out selectedDpi) ||
                    selectedDpi < 1 ||
                    selectedDpi > 2400)
                {
                    MessageBox.Show(
                        "Enter a custom DPI between 1 and 2400.",
                        "BTW Image Library Settings",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);

                    return;
                }

                string extension =
                    extensionCombo.SelectedIndex == 1 ? "JPG" :
                    extensionCombo.SelectedIndex == 2 ? "BMP" :
                    extensionCombo.SelectedIndex == 3 ? "TIFF" :
                    "PNG";

                btwLibraryModuleSettings.Dpi = selectedDpi;
                btwLibraryModuleSettings.ImageExtension = extension;

                btwLibraryModuleSettings.Save(
                    btwLibrarySettingsFile);

                SetBtwLibraryStage(
                    "Settings saved",
                    0,
                    0,
                    string.Format(
                        "Create Images: {0} DPI • {1} output",
                        selectedDpi,
                        extension));
            }
        }

    }
}

namespace NPPLPrintMaster
{
    internal sealed class BtwImageLibraryModuleSettings
    {
        public int Dpi { get; set; }

        public string ImageExtension { get; set; }

        public static BtwImageLibraryModuleSettings Load(
            string filePath,
            int fallbackDpi)
        {
            BtwImageLibraryModuleSettings settings =
                new BtwImageLibraryModuleSettings
                {
                    Dpi = IsValidDpi(fallbackDpi)
                        ? fallbackDpi
                        : 300,
                    ImageExtension = "PNG"
                };

            try
            {
                if (!File.Exists(filePath))
                    return settings;

                foreach (string rawLine in File.ReadAllLines(filePath))
                {
                    string line = (rawLine ?? "").Trim();

                    if (line.Length == 0 ||
                        line.StartsWith(";") ||
                        line.StartsWith("#"))
                        continue;

                    int separator = line.IndexOf('=');
                    if (separator <= 0)
                        continue;

                    string key =
                        line.Substring(0, separator).Trim();

                    string value =
                        line.Substring(separator + 1).Trim();

                    if (key.Equals(
                        "Dpi",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        int dpi;
                        if (int.TryParse(value, out dpi) &&
                            IsValidDpi(dpi))
                        {
                            settings.Dpi = dpi;
                        }
                    }
                    else if (key.Equals(
                        "ImageExtension",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        settings.ImageExtension =
                            NormalizeExtension(value);
                    }
                }
            }
            catch
            {
                // Keep safe defaults if the module settings file
                // cannot be read.
            }

            settings.ImageExtension =
                NormalizeExtension(settings.ImageExtension);

            if (!IsValidDpi(settings.Dpi))
                settings.Dpi = 300;

            return settings;
        }

        public void Save(string filePath)
        {
            try
            {
                string directory =
                    Path.GetDirectoryName(filePath);

                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                StringBuilder content =
                    new StringBuilder();

                content.AppendLine(
                    "; NPPLPrintMaster BTW Image Library settings");

                content.AppendLine(
                    "Dpi=" +
                    (IsValidDpi(Dpi) ? Dpi : 300));

                content.AppendLine(
                    "ImageExtension=" +
                    NormalizeExtension(ImageExtension));

                File.WriteAllText(
                    filePath,
                    content.ToString(),
                    Encoding.UTF8);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Unable to save BTW Image Library settings.",
                    ex);
            }
        }

        public static string NormalizeExtension(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "PNG";

            string normalized =
                value.Trim()
                    .TrimStart('.')
                    .ToUpperInvariant();

            if (normalized.Contains("("))
            {
                normalized =
                    normalized.Substring(
                        0,
                        normalized.IndexOf('('))
                    .Trim();
            }

            if (normalized == "JPEG")
                normalized = "JPG";

            if (normalized == "TIF")
                normalized = "TIFF";

            switch (normalized)
            {
                case "PNG":
                case "JPG":
                case "BMP":
                case "TIFF":
                    return normalized;

                default:
                    return "PNG";
            }
        }

        private static bool IsValidDpi(int dpi)
        {
            return dpi >= 72 && dpi <= 1200;
        }
    }
}
