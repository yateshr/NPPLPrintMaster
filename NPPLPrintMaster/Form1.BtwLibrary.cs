using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        private Label lblBtwLibraryProduct;
        private Label lblBtwLibraryBarcode;
        private Label lblBtwLibraryFile;
        private Label lblBtwLibraryLocation;
        private Label lblBtwLibraryStats;
        private Label lblBtwLibraryProgressPercent;
        private Label lblBtwLibraryProgressText;
        private Label lblBtwLibraryCurrent;
        private Panel pnlBtwLibraryProgress;
        private Button btnBtwLibraryRefresh;
        private Button btnBtwLibraryCancel;
        private Button btnBtwLibraryClearIndex;
        private Button btnBtwLibraryUseImage;
        private Button btnBtwLibraryOpen;
        private Button btnBtwLibraryFolder;
        private Button btnBtwLibraryCopyPath;
        private ListBox lstBtwLibraryInclude;
        private ListBox lstBtwLibraryExclude;
        private CancellationTokenSource btwLibraryCts;
        private string selectedBtwLibraryImage = "";

        private void BuildBtwImageLibrary()
        {
            string dataRoot = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "NPPLPrintMaster",
                "BTWImageLibrary");

            btwImageLibraryEngine =
                new BtwImageLibraryEngine(
                    Path.Combine(dataRoot, "Library.xml"),
                    Path.Combine(dataRoot, "Images"));

            Color pageBack = activeTheme == null
                ? SystemColors.Control
                : activeTheme.ControlBg;

            Color textColor = activeTheme == null
                ? SystemColors.ControlText
                : ThemeManager.GetContrastTextColor(
                    activeTheme.ControlBg);

            Color borderColor = activeTheme == null
                ? SystemColors.ControlDark
                : activeTheme.BorderColor;

            Color accentColor = activeTheme == null
                ? SystemColors.Highlight
                : activeTheme.ActiveColor;

            Font normalFont = new Font("Segoe UI", 9F);
            Font smallFont = new Font("Segoe UI", 8.5F);
            Font boldFont = new Font("Segoe UI", 9F, FontStyle.Bold);

            Panel root = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = pageBack,
                Padding = new Padding(14)
            };

            TableLayoutPanel main =
                new TableLayoutPanel
                {
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    AutoSizeMode =
                        AutoSizeMode.GrowAndShrink,
                    ColumnCount = 1,
                    RowCount = 5,
                    BackColor = pageBack
                };

            main.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    100F));

            // -----------------------------------------------------------------
            // HEADER
            // -----------------------------------------------------------------
            Panel header = new Panel
            {
                Height = 70,
                Dock = DockStyle.Fill,
                BackColor = pageBack,
                Margin = new Padding(0, 0, 0, 8)
            };

            Label title = new Label
            {
                Text = "BTW Image Library",
                Dock = DockStyle.Top,
                Height = 32,
                Font = new Font(
                    "Segoe UI Semibold",
                    18F,
                    FontStyle.Bold),
                ForeColor = textColor,
                BackColor = Color.Transparent
            };

            Label subtitle = new Label
            {
                Text =
                    "Central BTW template library • barcode search • clean image generation",
                Dock = DockStyle.Top,
                Height = 27,
                Font = normalFont,
                ForeColor = textColor,
                BackColor = Color.Transparent
            };

            header.Controls.Add(subtitle);
            header.Controls.Add(title);

            // -----------------------------------------------------------------
            // SEARCH / COMMAND BAR
            // -----------------------------------------------------------------
            Panel commandPanel = CreateBtwSectionPanel(
                "Search & Library Controls",
                pageBack,
                textColor,
                borderColor);

            TableLayoutPanel command =
                new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 6,
                    RowCount = 2,
                    Padding = new Padding(10),
                    BackColor = pageBack
                };

            command.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 42F));
            command.ColumnStyles.Add(
                new ColumnStyle(SizeType.Absolute, 145F));
            command.ColumnStyles.Add(
                new ColumnStyle(SizeType.Absolute, 145F));
            command.ColumnStyles.Add(
                new ColumnStyle(SizeType.Absolute, 80F));
            command.ColumnStyles.Add(
                new ColumnStyle(SizeType.Absolute, 105F));
            command.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 58F));

            command.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 34F));
            command.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 28F));

            txtBtwLibrarySearch = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10F),
                Margin = new Padding(3),
                ForeColor = SystemColors.WindowText,
                BackColor = SystemColors.Window
            };
            txtBtwLibrarySearch.TextChanged +=
                (sender, args) =>
                    RefreshBtwLibraryResults();

            cmbBtwLibraryStatus = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle =
                    ComboBoxStyle.DropDownList,
                Font = normalFont,
                Margin = new Padding(3)
            };

            cmbBtwLibraryStatus.Items.AddRange(
                new object[]
                {
                    "All",
                    "Ready",
                    "Ready - Barcode Not Detected",
                    "Processing",
                    "Error",
                    "Missing Image",
                    "Duplicate Barcode"
                });

            cmbBtwLibraryStatus.SelectedIndex = 0;
            cmbBtwLibraryStatus.SelectedIndexChanged +=
                (sender, args) =>
                    RefreshBtwLibraryResults();

            btnBtwLibraryRefresh = CreateBtwButton(
                "Refresh Library",
                accentColor,
                textColor,
                normalFont);
            btnBtwLibraryRefresh.Click +=
                BtnBtwLibraryRefresh_Click;

            btnBtwLibraryCancel = CreateBtwButton(
                "Cancel",
                pageBack,
                textColor,
                normalFont);
            btnBtwLibraryCancel.Enabled = false;
            btnBtwLibraryCancel.Click +=
                (sender, args) =>
                {
                    if (btwLibraryCts != null)
                        btwLibraryCts.Cancel();
                };

            btnBtwLibraryClearIndex = CreateBtwButton(
                "Clear Index",
                pageBack,
                textColor,
                normalFont);
            btnBtwLibraryClearIndex.Click +=
                BtnBtwLibraryClearIndex_Click;

            Label searchHelp = new Label
            {
                Text =
                    "Search by barcode, product, BTW filename or folder path.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = smallFont,
                ForeColor = textColor,
                BackColor = Color.Transparent,
                Margin = new Padding(5, 0, 0, 0)
            };

            lblBtwLibraryStats = new Label
            {
                Text =
                    "Templates: 0    Images: 0    Missing: 0    Errors: 0    Duplicates: 0",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = boldFont,
                ForeColor = textColor,
                BackColor = Color.Transparent,
                Margin = new Padding(5, 0, 0, 0),
                AutoEllipsis = true
            };

            command.Controls.Add(txtBtwLibrarySearch, 0, 0);
            command.Controls.Add(cmbBtwLibraryStatus, 1, 0);
            command.Controls.Add(btnBtwLibraryRefresh, 2, 0);
            command.Controls.Add(btnBtwLibraryCancel, 3, 0);
            command.Controls.Add(btnBtwLibraryClearIndex, 4, 0);
            command.Controls.Add(searchHelp, 0, 1);
            command.SetColumnSpan(searchHelp, 3);
            command.Controls.Add(lblBtwLibraryStats, 3, 1);
            command.SetColumnSpan(lblBtwLibraryStats, 3);

            ((Panel)commandPanel.Tag).Controls.Add(command);

            // -----------------------------------------------------------------
            // SOURCE FOLDERS + PROGRESS
            // -----------------------------------------------------------------
            TableLayoutPanel sourceRow =
                new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 2,
                    RowCount = 1,
                    BackColor = pageBack,
                    Margin = new Padding(0, 8, 0, 8)
                };

            sourceRow.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    48F));
            sourceRow.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    52F));

            Panel foldersPanel = CreateBtwSectionPanel(
                "BTW Source Folders",
                pageBack,
                textColor,
                borderColor);

            TableLayoutPanel folders =
                new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 2,
                    RowCount = 5,
                    Padding = new Padding(10),
                    BackColor = pageBack
                };

            folders.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 50F));
            folders.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 50F));

            folders.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 25F));
            folders.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 82F));
            folders.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 35F));
            folders.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 25F));
            folders.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 82F));

            Label includeLabel = CreateBtwLabel(
                "Include folders",
                boldFont,
                textColor);

            Label excludeLabel = CreateBtwLabel(
                "Exclude folders",
                boldFont,
                textColor);

            lstBtwLibraryInclude = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = smallFont,
                IntegralHeight = false,
                HorizontalScrollbar = true,
                BackColor = SystemColors.Window,
                ForeColor = SystemColors.WindowText,
                Margin = new Padding(2)
            };

            lstBtwLibraryExclude = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = smallFont,
                IntegralHeight = false,
                HorizontalScrollbar = true,
                BackColor = SystemColors.Window,
                ForeColor = SystemColors.WindowText,
                Margin = new Padding(2)
            };

            Button addInclude = CreateBtwButton(
                "+ Add Include",
                accentColor,
                textColor,
                normalFont);
            addInclude.Click +=
                (sender, args) =>
                    AddBtwLibraryFolder(
                        lstBtwLibraryInclude);

            Button removeInclude = CreateBtwButton(
                "Remove",
                pageBack,
                textColor,
                normalFont);
            removeInclude.Click +=
                (sender, args) =>
                    RemoveSelectedFolder(
                        lstBtwLibraryInclude);

            Button addExclude = CreateBtwButton(
                "+ Add Exclude",
                pageBack,
                textColor,
                normalFont);
            addExclude.Click +=
                (sender, args) =>
                    AddBtwLibraryFolder(
                        lstBtwLibraryExclude);

            Button removeExclude = CreateBtwButton(
                "Remove",
                pageBack,
                textColor,
                normalFont);
            removeExclude.Click +=
                (sender, args) =>
                    RemoveSelectedFolder(
                        lstBtwLibraryExclude);

            FlowLayoutPanel includeButtons =
                new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    FlowDirection =
                        FlowDirection.LeftToRight,
                    WrapContents = false,
                    BackColor = pageBack,
                    Margin = new Padding(0)
                };
            includeButtons.Controls.Add(addInclude);
            includeButtons.Controls.Add(removeInclude);

            FlowLayoutPanel excludeButtons =
                new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    FlowDirection =
                        FlowDirection.LeftToRight,
                    WrapContents = false,
                    BackColor = pageBack,
                    Margin = new Padding(0)
                };
            excludeButtons.Controls.Add(addExclude);
            excludeButtons.Controls.Add(removeExclude);

            folders.Controls.Add(includeLabel, 0, 0);
            folders.Controls.Add(excludeLabel, 1, 0);
            folders.Controls.Add(lstBtwLibraryInclude, 0, 1);
            folders.Controls.Add(lstBtwLibraryExclude, 1, 1);
            folders.Controls.Add(includeButtons, 0, 2);
            folders.Controls.Add(excludeButtons, 1, 2);

            Label folderInfo = CreateBtwLabel(
                "Folders are saved automatically. Unavailable network/USB folders remain visible.",
                smallFont,
                textColor);
            folderInfo.Dock = DockStyle.Fill;
            folderInfo.TextAlign =
                ContentAlignment.MiddleLeft;
            folders.Controls.Add(folderInfo, 0, 3);
            folders.SetColumnSpan(folderInfo, 2);

            Label folderInfo2 = CreateBtwLabel(
                "Refresh scans Include folders recursively; Exclude folders are skipped.",
                smallFont,
                textColor);
            folderInfo2.Dock = DockStyle.Fill;
            folderInfo2.TextAlign =
                ContentAlignment.MiddleLeft;
            folders.Controls.Add(folderInfo2, 0, 4);
            folders.SetColumnSpan(folderInfo2, 2);

            ((Panel)foldersPanel.Tag).Controls.Add(folders);

            Panel progressPanel = CreateBtwSectionPanel(
                "Library Refresh Progress",
                pageBack,
                textColor,
                borderColor);

            TableLayoutPanel progressLayout =
                new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 4,
                    Padding = new Padding(12),
                    BackColor = pageBack
                };

            progressLayout.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 38F));
            progressLayout.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 30F));
            progressLayout.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 25F));
            progressLayout.RowStyles.Add(
                new RowStyle(SizeType.Percent, 100F));

            pnlBtwLibraryProgress = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(
                    Math.Max(0, pageBack.R - 10),
                    Math.Max(0, pageBack.G - 10),
                    Math.Max(0, pageBack.B - 10)),
                BorderStyle = BorderStyle.FixedSingle
            };
            pnlBtwLibraryProgress.Paint +=
                PnlBtwLibraryProgress_Paint;

            lblBtwLibraryProgressText = CreateBtwLabel(
                "Ready",
                boldFont,
                textColor);
            lblBtwLibraryProgressText.Dock = DockStyle.Fill;
            lblBtwLibraryProgressText.TextAlign =
                ContentAlignment.MiddleLeft;

            lblBtwLibraryProgressPercent = CreateBtwLabel(
                "0%",
                boldFont,
                textColor);
            lblBtwLibraryProgressPercent.Dock =
                DockStyle.Fill;
            lblBtwLibraryProgressPercent.TextAlign =
                ContentAlignment.MiddleLeft;

            lblBtwLibraryCurrent = CreateBtwLabel(
                "",
                smallFont,
                textColor);
            lblBtwLibraryCurrent.Dock = DockStyle.Fill;
            lblBtwLibraryCurrent.AutoEllipsis = true;

            progressLayout.Controls.Add(
                pnlBtwLibraryProgress, 0, 0);
            progressLayout.Controls.Add(
                lblBtwLibraryProgressText, 0, 1);
            progressLayout.Controls.Add(
                lblBtwLibraryProgressPercent, 0, 2);
            progressLayout.Controls.Add(
                lblBtwLibraryCurrent, 0, 3);

            ((Panel)progressPanel.Tag).Controls.Add(progressLayout);

            sourceRow.Controls.Add(
                foldersPanel,
                0,
                0);
            sourceRow.Controls.Add(
                progressPanel,
                1,
                0);

            // -----------------------------------------------------------------
            // RESULTS + DETAILS
            // -----------------------------------------------------------------
            TableLayoutPanel resultsRow =
                new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 2,
                    RowCount = 1,
                    BackColor = pageBack,
                    Margin = new Padding(0, 0, 0, 8)
                };

            resultsRow.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    65F));
            resultsRow.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    35F));

            Panel resultsPanel = CreateBtwSectionPanel(
                "BTW Templates",
                pageBack,
                textColor,
                borderColor);

            dgvBtwLibrary = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = SystemColors.Window,
                ForeColor = SystemColors.WindowText,
                GridColor = borderColor,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoGenerateColumns = false,
                ReadOnly = true,
                MultiSelect = false,
                SelectionMode =
                    DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                AutoSizeRowsMode =
                    DataGridViewAutoSizeRowsMode.None,
                Font = smallFont,
                Margin = new Padding(8)
            };

            dgvBtwLibrary.ColumnHeadersDefaultCellStyle =
                new DataGridViewCellStyle
                {
                    BackColor = pageBack,
                    ForeColor = textColor,
                    Font = boldFont,
                    SelectionBackColor = accentColor,
                    SelectionForeColor =
                        ThemeManager.GetContrastTextColor(
                            accentColor)
                };

            dgvBtwLibrary.DefaultCellStyle =
                new DataGridViewCellStyle
                {
                    BackColor = SystemColors.Window,
                    ForeColor = SystemColors.WindowText,
                    SelectionBackColor = accentColor,
                    SelectionForeColor =
                        ThemeManager.GetContrastTextColor(
                            accentColor)
                };

            dgvBtwLibrary.AlternatingRowsDefaultCellStyle =
                new DataGridViewCellStyle
                {
                    BackColor = SystemColors.ControlLightLight,
                    ForeColor = SystemColors.WindowText
                };

            dgvBtwLibrary.Columns.Add(
                new DataGridViewTextBoxColumn
                {
                    Name = "Status",
                    HeaderText = "Status",
                    Width = 145,
                    SortMode =
                        DataGridViewColumnSortMode.NotSortable
                });

            dgvBtwLibrary.Columns.Add(
                new DataGridViewTextBoxColumn
                {
                    Name = "Barcode",
                    HeaderText = "Barcode",
                    Width = 125,
                    SortMode =
                        DataGridViewColumnSortMode.NotSortable
                });

            dgvBtwLibrary.Columns.Add(
                new DataGridViewTextBoxColumn
                {
                    Name = "Product",
                    HeaderText = "Product / BTW",
                    Width = 210,
                    SortMode =
                        DataGridViewColumnSortMode.NotSortable
                });

            dgvBtwLibrary.Columns.Add(
                new DataGridViewTextBoxColumn
                {
                    Name = "Location",
                    HeaderText = "Source Location",
                    AutoSizeMode =
                        DataGridViewAutoSizeColumnMode.Fill,
                    SortMode =
                        DataGridViewColumnSortMode.NotSortable
                });

            dgvBtwLibrary.SelectionChanged +=
                (sender, args) =>
                    ShowSelectedBtwLibraryRecord();

            dgvBtwLibrary.CellDoubleClick +=
                (sender, args) =>
                {
                    if (args.RowIndex >= 0)
                        OpenSelectedBtwLibrary();
                };

            ((Panel)resultsPanel.Tag).Controls.Add(dgvBtwLibrary);

            Panel detailPanel = CreateBtwSectionPanel(
                "Selected BTW",
                pageBack,
                textColor,
                borderColor);

            TableLayoutPanel detail =
                new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 7,
                    Padding = new Padding(10),
                    BackColor = pageBack
                };

            detail.RowStyles.Add(
                new RowStyle(SizeType.Percent, 52F));
            detail.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 30F));
            detail.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 30F));
            detail.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 42F));
            detail.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 52F));
            detail.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 38F));
            detail.RowStyles.Add(
                new RowStyle(SizeType.Percent, 48F));

            picBtwLibraryPreview = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = SystemColors.Window,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(2)
            };

            lblBtwLibraryProduct =
                CreateBtwDetailLabel(
                    "Product: —",
                    0, 0, 0, 0);
            lblBtwLibraryBarcode =
                CreateBtwDetailLabel(
                    "Barcode: —",
                    0, 0, 0, 0);
            lblBtwLibraryFile =
                CreateBtwDetailLabel(
                    "BTW: —",
                    0, 0, 0, 0);
            lblBtwLibraryLocation =
                CreateBtwDetailLabel(
                    "Location: —",
                    0, 0, 0, 0);

            ConfigureBtwDetailLabel(
                lblBtwLibraryProduct,
                textColor,
                normalFont);
            ConfigureBtwDetailLabel(
                lblBtwLibraryBarcode,
                textColor,
                normalFont);
            ConfigureBtwDetailLabel(
                lblBtwLibraryFile,
                textColor,
                normalFont);
            ConfigureBtwDetailLabel(
                lblBtwLibraryLocation,
                textColor,
                smallFont);

            detail.Controls.Add(
                picBtwLibraryPreview,
                0,
                0);
            detail.Controls.Add(
                lblBtwLibraryProduct,
                0,
                1);
            detail.Controls.Add(
                lblBtwLibraryBarcode,
                0,
                2);
            detail.Controls.Add(
                lblBtwLibraryFile,
                0,
                3);
            detail.Controls.Add(
                lblBtwLibraryLocation,
                0,
                4);

            FlowLayoutPanel actions =
                new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    FlowDirection =
                        FlowDirection.LeftToRight,
                    WrapContents = false,
                    BackColor = pageBack,
                    Margin = new Padding(0)
                };

            btnBtwLibraryUseImage =
                CreateBtwButton(
                    "Use Image",
                    accentColor,
                    textColor,
                    smallFont);
            btnBtwLibraryUseImage.Width = 92;
            btnBtwLibraryUseImage.Click +=
                (sender, args) =>
                    UseSelectedBtwLibraryImage();

            btnBtwLibraryOpen =
                CreateBtwButton(
                    "Open BTW",
                    pageBack,
                    textColor,
                    smallFont);
            btnBtwLibraryOpen.Width = 92;
            btnBtwLibraryOpen.Click +=
                (sender, args) =>
                    OpenSelectedBtwLibrary();

            btnBtwLibraryFolder =
                CreateBtwButton(
                    "Open Folder",
                    pageBack,
                    textColor,
                    smallFont);
            btnBtwLibraryFolder.Width = 98;
            btnBtwLibraryFolder.Click +=
                (sender, args) =>
                    OpenSelectedBtwLibraryFolder();

            btnBtwLibraryCopyPath =
                CreateBtwButton(
                    "Copy Path",
                    pageBack,
                    textColor,
                    smallFont);
            btnBtwLibraryCopyPath.Width = 92;
            btnBtwLibraryCopyPath.Click +=
                (sender, args) =>
                    CopySelectedBtwLibraryPath();

            actions.Controls.Add(btnBtwLibraryUseImage);
            actions.Controls.Add(btnBtwLibraryOpen);
            actions.Controls.Add(btnBtwLibraryFolder);
            actions.Controls.Add(btnBtwLibraryCopyPath);

            detail.Controls.Add(actions, 0, 5);

            Label detailHint = CreateBtwLabel(
                "Select a template in the grid. Double-click opens the BTW file.",
                smallFont,
                textColor);
            detailHint.Dock = DockStyle.Fill;
            detailHint.TextAlign =
                ContentAlignment.TopLeft;
            detail.Controls.Add(detailHint, 0, 6);

            ((Panel)detailPanel.Tag).Controls.Add(detail);

            resultsRow.Controls.Add(
                resultsPanel,
                0,
                0);
            resultsRow.Controls.Add(
                detailPanel,
                1,
                0);

            // -----------------------------------------------------------------
            // Assemble
            // -----------------------------------------------------------------
            main.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 70F));
            main.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 92F));
            main.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 250F));
            main.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 480F));

            main.Controls.Add(header, 0, 0);
            main.Controls.Add(commandPanel, 0, 1);
            main.Controls.Add(sourceRow, 0, 2);
            main.Controls.Add(resultsRow, 0, 3);

            root.Controls.Add(main);
            pageBtwLibrary.Controls.Clear();
            pageBtwLibrary.Controls.Add(root);

            pageBtwLibrary.Enter +=
                (sender, args) =>
                {
                    if (txtBtwLibrarySearch != null)
                        txtBtwLibrarySearch.Focus();
                };

            LoadBtwLibraryFolders();
            RefreshBtwLibraryStats();
            RefreshBtwLibraryResults();
        }

        private Panel CreateBtwSectionPanel(
            string title,
            Color backColor,
            Color textColor,
            Color borderColor)
        {
            Panel outer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = backColor,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(1),
                Margin = new Padding(0, 0, 8, 0)
            };

            Label caption = new Label
            {
                Text = title,
                Dock = DockStyle.Top,
                Height = 31,
                Font = new Font(
                    "Segoe UI Semibold",
                    10F,
                    FontStyle.Bold),
                ForeColor = textColor,
                BackColor = backColor,
                TextAlign =
                    ContentAlignment.MiddleLeft,
                Padding = new Padding(9, 0, 0, 0)
            };

            Panel body = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = backColor,
                Padding = new Padding(2)
            };

            // Keep the section caption and content in separate panels.
            // Store the body in Tag so the caller can add its controls
            // to the correct area instead of covering the caption/body.
            outer.Tag = body;

            outer.Controls.Add(body);
            outer.Controls.Add(caption);

            return outer;
        }

        private Label CreateBtwLabel(
            string text,
            Font font,
            Color foreColor)
        {
            return new Label
            {
                Text = text,
                Font = font,
                ForeColor = foreColor,
                BackColor = Color.Transparent,
                AutoEllipsis = true
            };
        }

        private Button CreateBtwButton(
            string text,
            Color backColor,
            Color foreColor,
            Font font)
        {
            Button button = new Button
            {
                Text = text,
                Font = font,
                BackColor = backColor,
                ForeColor = foreColor,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance =
                {
                    BorderColor =
                        activeTheme == null
                            ? SystemColors.ControlDark
                            : activeTheme.BorderColor
                },
                Margin = new Padding(2),
                Height = 29,
                AutoSize = false
            };

            return button;
        }

        private void ConfigureBtwDetailLabel(
            Label label,
            Color foreColor,
            Font font)
        {
            label.Dock = DockStyle.Fill;
            label.Font = font;
            label.ForeColor = foreColor;
            label.BackColor = Color.Transparent;
            label.TextAlign =
                ContentAlignment.MiddleLeft;
            label.AutoEllipsis = true;
            label.Margin = new Padding(2);
        }

        private Label CreateBtwDetailLabel(
            string text,
            int x,
            int y,
            int w,
            int h)
        {
            return new Label
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(w, h),
                Font = new Font("Segoe UI", 9),
                AutoEllipsis = true
            };
        }

        private Button CreateBtwActionButton(
            string text,
            int x,
            int y,
            int w)
        {
            return new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(w, 30),
                Font = new Font(
                    "Segoe UI",
                    8.5f,
                    FontStyle.Bold),
                FlatStyle = FlatStyle.Flat
            };
        }

        private void LoadBtwLibraryFolders()
        {
            lstBtwLibraryInclude.Items.Clear();
            lstBtwLibraryExclude.Items.Clear();

            foreach (string folder in
                SplitBtwLibraryFolders(
                    currentSettings.BtwLibraryIncludeFolders))
            {
                lstBtwLibraryInclude.Items.Add(folder);
            }

            foreach (string folder in
                SplitBtwLibraryFolders(
                    currentSettings.BtwLibraryExcludeFolders))
            {
                lstBtwLibraryExclude.Items.Add(folder);
            }
        }

        private static IEnumerable<string>
            SplitBtwLibraryFolders(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Enumerable.Empty<string>();

            // IMPORTANT: Do not call Directory.Exists here.
            // A configured network/USB folder must remain visible
            // even while it is temporarily unavailable.
            return value
                .Split(
                    new[] { '|' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase);
        }

        private void AddBtwLibraryFolder(ListBox target)
        {
            string folder =
                SelectFolderModern(
                    "BTWImageLibrary.Folder");

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

            target.SelectedItem = folder;

            RefreshBtwLibraryFolderVisualState();
        }

        private void RemoveSelectedFolder(
            ListBox target)
        {
            if (target.SelectedIndex >= 0)
                target.Items.RemoveAt(
                    target.SelectedIndex);

            SaveBtwLibraryFolderSettings();
            RefreshBtwLibraryFolderVisualState();
        }

        private void RefreshBtwLibraryFolderVisualState()
        {
            if (lstBtwLibraryInclude == null)
                return;

            for (int i = 0;
                i < lstBtwLibraryInclude.Items.Count;
                i++)
            {
                // Keep the full path visible. Availability is checked
                // when Refresh is pressed.
                lstBtwLibraryInclude.SetSelected(
                    i,
                    lstBtwLibraryInclude.SelectedIndex == i);
            }
        }

        private void SaveBtwLibraryFolderSettings()
        {
            currentSettings.BtwLibraryIncludeFolders =
                string.Join(
                    "|",
                    lstBtwLibraryInclude.Items
                        .Cast<object>()
                        .Select(x => x.ToString()));

            currentSettings.BtwLibraryExcludeFolders =
                string.Join(
                    "|",
                    lstBtwLibraryExclude.Items
                        .Cast<object>()
                        .Select(x => x.ToString()));

            SettingsManager.Save(currentSettings);
        }

        private async void BtnBtwLibraryRefresh_Click(
            object sender,
            EventArgs e)
        {
            if (btwLibraryCts != null)
                return;

            List<string> includes =
                lstBtwLibraryInclude.Items
                    .Cast<object>()
                    .Select(x => x.ToString())
                    .ToList();

            List<string> excludes =
                lstBtwLibraryExclude.Items
                    .Cast<object>()
                    .Select(x => x.ToString())
                    .ToList();

            if (includes.Count == 0)
            {
                MessageBox.Show(
                    "Add at least one BTW Include Folder first.",
                    "BTW Image Library",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            SaveBtwLibraryFolderSettings();

            btwLibraryCts =
                new CancellationTokenSource();

            btnBtwLibraryRefresh.Enabled = false;
            btnBtwLibraryClearIndex.Enabled = false;
            btnBtwLibraryCancel.Enabled = true;

            SetBtwLibraryProgress(
                0,
                0,
                "Starting BTW scan...");

            try
            {
                IProgress<BtwImageLibraryProgress>
                    progress =
                        new Progress<BtwImageLibraryProgress>(
                            p =>
                            {
                                SetBtwLibraryProgress(
                                    p.Processed,
                                    p.Total,
                                    string.IsNullOrWhiteSpace(
                                        p.CurrentFile)
                                        ? ""
                                        : Path.GetFileName(
                                            p.CurrentFile));

                                lblBtwLibraryStats.Text =
                                    string.Format(
                                        "Templates: {0:N0}    Images: {1:N0}    New: {2:N0}    Modified: {3:N0}    Errors: {4:N0}",
                                        p.Total,
                                        btwImageLibraryEngine.ImageCount,
                                        p.NewFiles,
                                        p.ChangedFiles,
                                        p.Errors);
                            });

                BtwImageLibraryRefreshResult result =
                    await Task.Run(
                        () =>
                            btwImageLibraryEngine.Refresh(
                                includes,
                                excludes,
                                currentSettings.DefaultDpi,
                                progress.Report,
                                btwLibraryCts.Token),
                        btwLibraryCts.Token);

                RefreshBtwLibraryStats();
                RefreshBtwLibraryResults();

                MessageBox.Show(
                    string.Format(
                        "BTW Image Library refresh completed.\n\n" +
                        "BTW found: {0:N0}\n" +
                        "New: {1:N0}\n" +
                        "Modified: {2:N0}\n" +
                        "Unchanged: {3:N0}\n" +
                        "Removed: {4:N0}\n" +
                        "Images created: {5:N0}\n" +
                        "Errors: {6:N0}\n" +
                        "Duplicate barcodes: {7:N0}",
                        result.TotalFiles,
                        result.NewFiles,
                        result.ChangedFiles,
                        result.UnchangedFiles,
                        result.RemovedFiles,
                        result.ImagesCreated,
                        result.Errors,
                        result.DuplicateBarcodes),
                    "BTW Image Library",
                    MessageBoxButtons.OK,
                    result.Errors == 0
                        ? MessageBoxIcon.Information
                        : MessageBoxIcon.Warning);
            }
            catch (OperationCanceledException)
            {
                RefreshBtwLibraryStats();
                RefreshBtwLibraryResults();

                SetBtwLibraryProgress(
                    0,
                    0,
                    "Refresh cancelled.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "BTW Image Library refresh failed.\n\n" +
                    ex.Message,
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

                btnBtwLibraryRefresh.Enabled = true;
                btnBtwLibraryClearIndex.Enabled = true;
                btnBtwLibraryCancel.Enabled = false;
            }
        }

        private void BtnBtwLibraryClearIndex_Click(
            object sender,
            EventArgs e)
        {
            if (btwLibraryCts != null)
                return;

            DialogResult answer =
                MessageBox.Show(
                    "This will clear the cached BTW template index.\n\n" +
                    "The generated PNG image cache will NOT be deleted.\n\n" +
                    "The next Refresh will scan the selected source folders from scratch.\n\n" +
                    "Continue?",
                    "Clear BTW Image Library Index",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

            if (answer != DialogResult.Yes)
                return;

            try
            {
                btwImageLibraryEngine.ClearIndex();

                RefreshBtwLibraryStats();
                RefreshBtwLibraryResults();

                SetBtwLibraryProgress(
                    0,
                    0,
                    "Index cleared. Add/verify source folders, then Refresh.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Unable to clear the BTW Image Library index.\n\n" +
                    ex.Message,
                    "BTW Image Library",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void SetBtwLibraryProgress(
            int processed,
            int total,
            string current)
        {
            lblBtwLibraryProgressText.Text =
                total <= 0
                    ? "Ready"
                    : string.Format(
                        "{0:N0} / {1:N0} BTW templates",
                        processed,
                        total);

            lblBtwLibraryProgressPercent.Text =
                total <= 0
                    ? "0%"
                    : string.Format(
                        "{0:0.0}%",
                        processed * 100.0 / total);

            lblBtwLibraryCurrent.Text =
                string.IsNullOrWhiteSpace(current)
                    ? ""
                    : "Current: " + current;

            pnlBtwLibraryProgress.Tag =
                new Point(processed, total);

            pnlBtwLibraryProgress.Invalidate();
        }

        private void PnlBtwLibraryProgress_Paint(
            object sender,
            PaintEventArgs e)
        {
            Point p =
                pnlBtwLibraryProgress.Tag is Point
                    ? (Point)pnlBtwLibraryProgress.Tag
                    : new Point(0, 0);

            int processed = p.X;
            int total = p.Y;

            Rectangle bar =
                new Rectangle(
                    6,
                    6,
                    Math.Max(
                        10,
                        pnlBtwLibraryProgress.ClientSize.Width - 12),
                    Math.Max(
                        10,
                        pnlBtwLibraryProgress.ClientSize.Height - 12));

            using (SolidBrush background =
                new SolidBrush(
                    activeTheme == null
                        ? Color.FromArgb(45, 55, 65)
                        : activeTheme.ControlBg))
            using (SolidBrush fill =
                new SolidBrush(
                    activeTheme == null
                        ? Color.FromArgb(48, 145, 255)
                        : activeTheme.ActiveColor))
            using (SolidBrush text =
                new SolidBrush(
                    activeTheme == null
                        ? Color.White
                        : ThemeManager.GetContrastTextColor(
                            activeTheme.ActiveColor)))
            using (Pen border =
                new Pen(
                    activeTheme == null
                        ? Color.Gray
                        : activeTheme.BorderColor))
            using (StringFormat sf =
                new StringFormat
                {
                    Alignment =
                        StringAlignment.Center,
                    LineAlignment =
                        StringAlignment.Center
                })
            {
                e.Graphics.FillRectangle(
                    background,
                    bar);

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
                    border,
                    bar);

                string label =
                    total <= 0
                        ? "Ready"
                        : string.Format(
                            "{0:N0} / {1:N0} BTW",
                            processed,
                            total);

                using (Font f =
                    new Font(
                        "Segoe UI",
                        9,
                        FontStyle.Bold))
                {
                    e.Graphics.DrawString(
                        label,
                        f,
                        text,
                        bar,
                        sf);
                }
            }
        }

        private void RefreshBtwLibraryStats()
        {
            if (btwImageLibraryEngine == null ||
                lblBtwLibraryStats == null)
                return;

            lblBtwLibraryStats.Text =
                string.Format(
                    "Templates: {0:N0}    Images: {1:N0}    Missing: {2:N0}    Errors: {3:N0}    Duplicates: {4:N0}",
                    btwImageLibraryEngine.Count,
                    btwImageLibraryEngine.ImageCount,
                    btwImageLibraryEngine.MissingImageCount,
                    btwImageLibraryEngine.ErrorCount,
                    btwImageLibraryEngine.DuplicateBarcodeCount);
        }

        private void RefreshBtwLibraryResults()
        {
            if (dgvBtwLibrary == null ||
                btwImageLibraryEngine == null)
                return;

            string query =
                txtBtwLibrarySearch == null
                    ? ""
                    : txtBtwLibrarySearch.Text;

            string status =
                cmbBtwLibraryStatus == null ||
                cmbBtwLibraryStatus.SelectedItem == null
                    ? "All"
                    : cmbBtwLibraryStatus.SelectedItem.ToString();

            List<BtwImageLibraryRecord> results =
                btwImageLibraryEngine.Search(
                    query,
                    status);

            dgvBtwLibrary.Rows.Clear();

            foreach (BtwImageLibraryRecord r in results)
            {
                int row =
                    dgvBtwLibrary.Rows.Add(
                        r.Status,
                        r.Barcode,
                        r.ProductName,
                        r.FilePath);

                dgvBtwLibrary.Rows[row].Tag = r;
            }

            if (dgvBtwLibrary.Rows.Count > 0)
                dgvBtwLibrary.Rows[0].Selected = true;
            else
                ClearBtwLibraryDetails();
        }

        private BtwImageLibraryRecord
            GetSelectedBtwLibraryRecord()
        {
            if (dgvBtwLibrary == null ||
                dgvBtwLibrary.SelectedRows.Count == 0)
                return null;

            return dgvBtwLibrary
                .SelectedRows[0]
                .Tag as BtwImageLibraryRecord;
        }

        private void ShowSelectedBtwLibraryRecord()
        {
            BtwImageLibraryRecord r =
                GetSelectedBtwLibraryRecord();

            if (r == null)
            {
                ClearBtwLibraryDetails();
                return;
            }

            selectedBtwLibraryImage =
                r.ImagePath ?? "";

            lblBtwLibraryProduct.Text =
                "Product: " +
                (r.ProductName ?? "—");

            lblBtwLibraryBarcode.Text =
                "Barcode: " +
                (string.IsNullOrWhiteSpace(r.Barcode)
                    ? "Not detected"
                    : r.Barcode);

            lblBtwLibraryFile.Text =
                "BTW: " +
                (r.FileName ?? "—");

            lblBtwLibraryLocation.Text =
                "Location: " +
                (r.FilePath ?? "—");

            LoadBtwLibraryPreview(
                r.ImagePath);

            bool imageReady =
                !string.IsNullOrWhiteSpace(r.ImagePath) &&
                File.Exists(r.ImagePath);

            btnBtwLibraryUseImage.Enabled =
                imageReady;

            btnBtwLibraryOpen.Enabled =
                File.Exists(r.FilePath);

            btnBtwLibraryFolder.Enabled =
                File.Exists(r.FilePath);

            btnBtwLibraryCopyPath.Enabled =
                !string.IsNullOrWhiteSpace(
                    r.FilePath);
        }

        private void ClearBtwLibraryDetails()
        {
            selectedBtwLibraryImage = "";

            lblBtwLibraryProduct.Text =
                "Product: —";
            lblBtwLibraryBarcode.Text =
                "Barcode: —";
            lblBtwLibraryFile.Text =
                "BTW: —";
            lblBtwLibraryLocation.Text =
                "Location: —";

            if (picBtwLibraryPreview.Image != null)
            {
                Image old =
                    picBtwLibraryPreview.Image;

                picBtwLibraryPreview.Image = null;
                old.Dispose();
            }
        }

        private void LoadBtwLibraryPreview(
            string imagePath)
        {
            if (picBtwLibraryPreview.Image != null)
            {
                Image old =
                    picBtwLibraryPreview.Image;

                picBtwLibraryPreview.Image = null;
                old.Dispose();
            }

            if (string.IsNullOrWhiteSpace(imagePath) ||
                !File.Exists(imagePath))
                return;

            try
            {
                using (Image source =
                    Image.FromFile(imagePath))
                {
                    picBtwLibraryPreview.Image =
                        new Bitmap(source);
                }
            }
            catch
            {
            }
        }

        private void OpenSelectedBtwLibrary()
        {
            BtwImageLibraryRecord r =
                GetSelectedBtwLibraryRecord();

            if (r == null ||
                !File.Exists(r.FilePath))
                return;

            try
            {
                Process.Start(
                    new ProcessStartInfo
                    {
                        FileName = r.FilePath,
                        UseShellExecute = true
                    });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Unable to open the BTW file.\n\n" +
                    ex.Message);
            }
        }

        private void OpenSelectedBtwLibraryFolder()
        {
            BtwImageLibraryRecord r =
                GetSelectedBtwLibraryRecord();

            if (r == null ||
                !File.Exists(r.FilePath))
                return;

            try
            {
                Process.Start(
                    new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments =
                            "/select,\"" +
                            r.FilePath +
                            "\"",
                        UseShellExecute = true
                    });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Unable to open the BTW folder.\n\n" +
                    ex.Message);
            }
        }

        private void CopySelectedBtwLibraryPath()
        {
            BtwImageLibraryRecord r =
                GetSelectedBtwLibraryRecord();

            if (r == null ||
                string.IsNullOrWhiteSpace(
                    r.FilePath))
                return;

            Clipboard.SetText(
                r.FilePath);
        }

        private void UseSelectedBtwLibraryImage()
        {
            BtwImageLibraryRecord r =
                GetSelectedBtwLibraryRecord();

            if (r == null ||
                string.IsNullOrWhiteSpace(
                    r.ImagePath) ||
                !File.Exists(r.ImagePath))
                return;

            selectedCollageFiles =
                new[] { r.ImagePath };

            selectedCollageFolder = "";

            if (txtCollageSource != null)
                txtCollageSource.Text =
                    r.ImagePath;

            if (cmbCollageOutputType != null)
                cmbCollageOutputType.SelectedItem =
                    "PDF";

            if (cmbCollageGrid != null)
                cmbCollageGrid.SelectedIndex = 0;

            manualCollageSlots = null;
            manualCollageCaptions = null;

            UpdateCollageArrangementButtons();

            manualCollageCustomNames = null;

            if (navButtons.Count > 2)
            {
                SwitchPage(
                    pageFormat,
                    navButtons[2],
                    "Step 2: Image Formatting");
            }

            MessageBox.Show(
                "The clean BTW image has been loaded into Image Collage Builder.\n\n" +
                "You can now use the existing Arrange Images workflow and apply the required border there.",
                "BTW Image Library",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }
}
