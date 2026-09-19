using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Threading.Tasks;

namespace NPPLPrintMaster
{
    // ==========================================
    // STANDALONE JOB CARD PREVIEW WINDOW
    // ==========================================
    public class JobCardPreviewForm : Form
    {
        private PictureBox pbPreview;
        private Button btnSave, btnCustomize, btnClose;
        private Bitmap previewBmp;
        private List<CanvasItem> rawItems;

        public JobCardPreviewForm(
            Bitmap generatedBmp,
            List<CanvasItem> itemsForBuilder,
            WorkspaceData workspaceData,
            AppSettings appSettings,
            string initialSaveDirectory)
        {
            this.Text = "Job Card Preview - NPPL PrintMaster";
            this.Size = new Size(900, 750);
            this.StartPosition = FormStartPosition.CenterParent;
            bool useBarTender10Classic =
                appSettings != null &&
                ThemeManager.IsBarTender10Classic(appSettings.Theme);

            ThemeColors previewTheme =
                useBarTender10Classic
                    ? ThemeManager.GetTheme("BarTender 10 Classic")
                    : null;

            this.BackColor = useBarTender10Classic
                ? previewTheme.BgLight
                : Color.FromArgb(38, 40, 46);
            this.ForeColor = useBarTender10Classic
                ? previewTheme.TextColor
                : Color.White;
            previewBmp = generatedBmp;
            rawItems = itemsForBuilder;

            pbPreview = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = useBarTender10Classic
                    ? Color.FromArgb(96, 127, 167)
                    : Color.FromArgb(50, 52, 59),
                Image = previewBmp
            };

            Panel pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = useBarTender10Classic
                    ? previewTheme.ContentBg
                    : Color.FromArgb(28, 29, 33),
                Padding = new Padding(10)
            };

            btnSave = new Button
            {
                Text = "💾 Save Job Card",
                Size = new Size(160, 40),
                Location = new Point(15, 10),
                BackColor = Color.FromArgb(252, 213, 53),
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += (s, e) =>
            {
                using (SaveFileDialog sfd = new SaveFileDialog
                {
                    Filter = "BMP Image|*.bmp",
                    FileName = "JobCard_Automated.bmp",
                    AddExtension = true,
                    DefaultExt = "bmp"
                })
                {
                    if (!string.IsNullOrWhiteSpace(initialSaveDirectory) &&
                        Directory.Exists(initialSaveDirectory))
                    {
                        sfd.InitialDirectory = initialSaveDirectory;
                    }

                    if (sfd.ShowDialog() != DialogResult.OK)
                        return;

                    string savedBmpPath = sfd.FileName;
                    bool bmpSaved = false;

                    try
                    {
                        // 1. Save the final Job Card BMP exactly where the user selected.
                        previewBmp.Save(
                            savedBmpPath,
                            System.Drawing.Imaging.ImageFormat.Bmp);

                        bmpSaved = true;

                        // 2. Remember the selected BMP folder for the next Save Job Card.
                        string bmpFolder =
                            Path.GetDirectoryName(savedBmpPath);

                        if (appSettings != null &&
                            !string.IsNullOrWhiteSpace(bmpFolder) &&
                            Directory.Exists(bmpFolder))
                        {
                            appSettings.LastJobCardSaveFolder = bmpFolder;
                            SettingsManager.Save(appSettings);
                        }

                        // 3. Build the exact requested NPPL storage location:
                        //    C:\Users\<user>\Documents\NPPLPrintMaster
                        string userProfile =
                            Environment.GetFolderPath(
                                Environment.SpecialFolder.UserProfile);

                        if (string.IsNullOrWhiteSpace(userProfile))
                            throw new InvalidOperationException(
                                "Windows user profile folder could not be determined.");

                        string npplFolder =
                            Path.Combine(
                                userProfile,
                                "Documents",
                                "NPPLPrintMaster");

                        Directory.CreateDirectory(npplFolder);

                        string baseName =
                            Path.GetFileNameWithoutExtension(savedBmpPath);

                        if (string.IsNullOrWhiteSpace(baseName))
                            throw new InvalidOperationException(
                                "The saved Job Card filename could not be determined.");

                        string npplPath =
                            Path.Combine(
                                npplFolder,
                                baseName + ".nppl");

                        // 4. Save the actual workspace directly from this same button.
                        //    No callback is used in V4.
                        if (workspaceData == null)
                            throw new InvalidOperationException(
                                "Workspace data was not supplied to the Job Card preview.");

                        WorkspaceEngine.SaveToFile(
                            npplPath,
                            workspaceData);

                        // 5. Verify Windows actually created the file.
                        if (!File.Exists(npplPath))
                        {
                            throw new IOException(
                                "WorkspaceEngine completed, but the NPPL file does not exist.\n\n" +
                                "Expected file:\n" +
                                npplPath);
                        }

                        FileInfo npplInfo =
                            new FileInfo(npplPath);

                        if (npplInfo.Length <= 0)
                        {
                            throw new IOException(
                                "The NPPL file was created but is empty.\n\n" +
                                npplPath);
                        }

                        Logger.LogAction(
                            "WORKSPACE_AUTO_SAVE",
                            "BMP: " + savedBmpPath +
                            " | NPPL: " + npplPath);

                        MessageBox.Show(
                            "Job card saved successfully.\n\n" +
                            "BMP:\n" +
                            savedBmpPath +
                            "\n\n" +
                            "NPPL workspace:\n" +
                            npplPath,
                            "Job Card + NPPL Saved",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        string message =
                            bmpSaved
                                ? "The BMP was saved successfully, but the matching NPPL workspace could not be saved."
                                : "The Job Card BMP could not be saved.";

                        MessageBox.Show(
                            message +
                            "\n\n" +
                            "BMP:\n" +
                            savedBmpPath +
                            "\n\n" +
                            "Error:\n" +
                            ex.ToString(),
                            "Save Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                }
            };

            btnCustomize = new Button
            {
                Text = "✏️ Customize / Align Manually",
                Size = new Size(200, 40),
                Location = new Point(190, 10),
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            btnCustomize.FlatAppearance.BorderSize = 0;
            btnCustomize.Click += (s, e) => {
                this.Hide();
                FreeformBuilderForm builder = new FreeformBuilderForm(rawItems, workspaceData, appSettings, initialSaveDirectory);
                builder.WindowState = FormWindowState.Maximized;
                builder.ShowDialog();
                this.Close();
            };

            btnClose = new Button
            {
                Text = "Close",
                Size = new Size(100, 40),
                Location = new Point(765, 10),
                BackColor = Color.FromArgb(70, 72, 79),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => this.Close();

            if (useBarTender10Classic)
            {
                foreach (Button previewButton in new[] { btnSave, btnCustomize, btnClose })
                {
                    previewButton.FlatStyle = FlatStyle.Flat;
                    previewButton.FlatAppearance.BorderSize = 1;
                    previewButton.FlatAppearance.BorderColor = previewTheme.BorderColor;
                }

                btnSave.BackColor = previewTheme.ControlBg;
                btnSave.ForeColor = previewTheme.TextColor;

                btnCustomize.BackColor = previewTheme.ActiveColor;
                btnCustomize.ForeColor =
                    ThemeManager.GetContrastTextColor(previewTheme.ActiveColor);

                btnClose.BackColor = previewTheme.ControlBg;
                btnClose.ForeColor = previewTheme.TextColor;
            }

            pnlBottom.Controls.AddRange(new Control[] { btnSave, btnCustomize, btnClose });
            this.Controls.Add(pbPreview);
            this.Controls.Add(pnlBottom);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (previewBmp != null) previewBmp.Dispose();
            base.OnFormClosed(e);
        }
    }

    public partial class Form1 : Form
    {
        private TextBox txtSource1, txtExport1;
        private NumericUpDown numDpi;
        private ComboBox cmbFormat;
        private CheckBox chkSubDirs1, chkPreserveFolders1, chkExtractAndFormat;
        private string[] selectedBtwFiles = null;
        private string selectedBtwFolder = "";

        // Find & Extract BTW Files
        private TextBox txtFindMasterFolder, txtFindOutput;
        private RichTextBox rtbFindNames, rtbFindResults;
        private ComboBox cmbFindFormat;
        private NumericUpDown numFindDpi;
        private CheckBox chkFindPreserve;
        private Button btnFindSearch, btnFindExtract;
        private Dictionary<string, List<string>> btwSearchIndex =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private string indexedBtwRoot = "";
        private List<string> lastFindMatches = new List<string>();

        private TextBox txtSource2, txtExport2;
        private NumericUpDown numThickness;
        private ComboBox cmbFormattingOutputFormat;
        private CheckBox chkPadding, chkSubDirs2, chkPreserveFolders2;
        private Button btnColor;
        private string[] selectedRawFiles = null;
        private string selectedRawFolder = "";
        private Color selectedBorderColor = Color.FromArgb(255, 60, 70, 90);

        private RichTextBox rtbFormatConsole;

        // Image Collage Builder
        private TextBox txtCollageSource, txtCollageOutput, txtCollageFileName;
        private ComboBox cmbCollageOutputType, cmbCollageGrid, cmbCollageOrientation;
        private CheckBox chkCollageSubDirs, chkCollageShowFilename, chkCollageBorders, chkCollageAutoFileName;
        private Button btnGenerateCollage;
        private Button btnArrangeCollage;
        private Button btnClearCollageArrangement;
        private string[] selectedCollageFiles = null;
        private string selectedCollageFolder = "";
        private List<string> manualCollageSlots = null;
        private Dictionary<string, string> manualCollageCaptions = null;
        private Dictionary<string, string> manualCollageCustomNames = null;
        private string manualCollageDisplayMode = "Both";

        private FlowLayoutPanel pnlProducts, pnlCartons, pnlOthers;
        private TextBox txtLane1Text, txtLane2Text;

        // Smart Image Finder V1.1
        private TextBox txtSmartProductFolder, txtSmartCartonFolder;
        private ToolTip smartFolderToolTip;
        private TextBox txtSmartProductCode, txtSmartCartonCode;
        private ListBox lstSmartProductResults, lstSmartCartonResults;
        private PictureBox pbSmartProductPreview, pbSmartCartonPreview;
        private Label lblSmartStatus, lblSmartHealth;
        private Button btnSmartRefresh;
        private CheckBox chkSmartMonitor;
        private FileSystemWatcher smartProductWatcher, smartCartonWatcher;
        private System.Windows.Forms.Timer smartMonitorTimer;
        private System.Windows.Forms.Timer smartSafetyTimer;
        private System.Windows.Forms.Timer smartProductSearchTimer;
        private System.Windows.Forms.Timer smartCartonSearchTimer;
        private bool smartRefreshRunning;
        private SmartImageFinderEngine productImageIndex = new SmartImageFinderEngine("ProductBarcodes");
        private SmartImageFinderEngine cartonImageIndex = new SmartImageFinderEngine("CartonBarcodes");

        private CheckBox chkShowText1, chkShowText2, chkOpenDirectFreeform;
        private Font fontLane1 = new Font("Calibri", 14, FontStyle.Bold);
        private Font fontLane2 = new Font("Calibri", 14, FontStyle.Bold);

        private List<string> customLayoutMemory = new List<string>();

        private Panel pnlContent, pnlSidebar, pnlHeader, pnlLogo;
        private Label lblHeaderTitle, lblAppTitle, lblAppSub;
        private Panel pageHome, pageExtract, pageFormat, pageCompose, pageSettings;
        private Button activeNavButton;
        private List<Button> navButtons = new List<Button>();
        private Splitter sidebarSplitter;
        private MenuStrip menuBar;

        private ComboBox cmbTheme, cmbTypography, cmbLayout;
        private Label lblAppearanceResolved;
        private TextBox txtDefExport1, txtDefExport2;
        private NumericUpDown numDefDpi;
        private Button btnRun1;
        private Button btnSaveSettings;

        private AppSettings currentSettings;
        private ThemeColors activeTheme;
        private Color sidebarColor, sidebarHover, activeColor, bgLight, contentBg, textColor, controlBg;

        public Form1()
        {
            currentSettings = SettingsManager.Load();
            BuildInterface();
            this.Load += Form1_Load;
            this.FormClosing += Form1_FormClosing;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            currentSettings.Theme =
                AppearanceManager.NormalizeTheme(currentSettings.Theme);
            currentSettings.TypographyStyle =
                AppearanceManager.NormalizeTypographyChoice(
                    currentSettings.TypographyStyle);
            currentSettings.LayoutStyle =
                AppearanceManager.NormalizeLayoutChoice(
                    currentSettings.LayoutStyle);

            ApplyTheme(currentSettings.Theme);
            Logger.LogAction("APP_START", "NPPLPrintMaster Enterprise Edition launched.");
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (smartMonitorTimer != null) smartMonitorTimer.Stop();
            if (smartSafetyTimer != null) smartSafetyTimer.Stop();
            if (smartProductSearchTimer != null) smartProductSearchTimer.Stop();
            if (smartCartonSearchTimer != null) smartCartonSearchTimer.Stop();
            DisposeWatcher(ref smartProductWatcher);
            DisposeWatcher(ref smartCartonWatcher);
            Logger.LogAction("APP_EXIT", "NPPLPrintMaster closed safely.");
        }

        private void BuildInterface()
        {
            this.Text = "NPPLPrintMaster - Enterprise Edition";
            this.Size = new Size(1200, 800);
            this.MinimumSize = new Size(900, 600);
            this.StartPosition = FormStartPosition.CenterScreen;

            menuBar = new MenuStrip { Padding = new Padding(5) };
            ToolStripMenuItem fileMenu = new ToolStripMenuItem("File");
            ToolStripMenuItem saveItem = new ToolStripMenuItem("💾 Save Workspace...");
            saveItem.Click += (s, e) => SaveWorkspace();
            ToolStripMenuItem loadItem = new ToolStripMenuItem("📂 Load Workspace...");
            loadItem.Click += (s, e) => LoadWorkspace();
            ToolStripMenuItem exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += (s, e) => Application.Exit();
            fileMenu.DropDownItems.AddRange(new ToolStripItem[] { saveItem, loadItem, new ToolStripSeparator(), exitItem });

            ToolStripMenuItem editMenu = new ToolStripMenuItem("Edit");
            ToolStripMenuItem clearItem = new ToolStripMenuItem("Clear All Images");
            clearItem.Click += (s, e) => { ClearAllLanesAndSmartFinder(); };
            editMenu.DropDownItems.Add(clearItem);

            ToolStripMenuItem toolsMenu = new ToolStripMenuItem("Tools");
            ToolStripMenuItem standaloneBuilderItem = new ToolStripMenuItem("🎨 Pro Freeform Builder");
            standaloneBuilderItem.Click += (s, e) => { FreeformBuilderForm builder = new FreeformBuilderForm(); builder.WindowState = FormWindowState.Maximized; builder.Show(); };
            toolsMenu.DropDownItems.Add(standaloneBuilderItem);

            ToolStripMenuItem helpMenu = new ToolStripMenuItem("Help");
            var myDocuments = new Dictionary<string, string> {
                { "FAQ & How to Use", "faq.md" }, { "License Agreement", "license.md" },
                { "About & Version Info", "about.md" }, { "System Requirements", "requirements.md" },
                { "Troubleshooting Guide", "troubleshooting.md" }, { "Release Notes", "releasenotes.md" }
            };
            foreach (var doc in myDocuments)
            {
                ToolStripMenuItem item = new ToolStripMenuItem(doc.Key);
                item.Click += (s, e) => HelpViewer.ShowDocument(doc.Key, doc.Value);
                helpMenu.DropDownItems.Add(item);
            }
            menuBar.Items.AddRange(new ToolStripItem[] { fileMenu, editMenu, toolsMenu, helpMenu });
            this.MainMenuStrip = menuBar;

            pnlSidebar = new Panel { Dock = DockStyle.Left, Width = 240, MinimumSize = new Size(60, 0) };
            sidebarSplitter = new Splitter { Dock = DockStyle.Left, Width = 3, Cursor = Cursors.VSplit };

            pnlLogo = new Panel { Dock = DockStyle.Top, Height = 80 };
            string logoPath = Path.Combine(Application.StartupPath, "logo.png");
            if (File.Exists(logoPath))
            {
                PictureBox picLogo = new PictureBox { Image = Image.FromFile(logoPath), SizeMode = PictureBoxSizeMode.Zoom, Dock = DockStyle.Fill };
                pnlLogo.Controls.Add(picLogo);
            }
            else
            {
                lblAppTitle = new Label
                {
                    Text = "NPPL",
                    Font = new Font("Segoe UI Black", 22, FontStyle.Bold),
                    Location = new Point(15, 15),
                    AutoSize = true
                };

                lblAppSub = new Label
                {
                    Text = "PRINT MASTER",
                    Font = new Font("Segoe UI", 9, FontStyle.Regular),
                    Location = new Point(18, 55),
                    AutoSize = true
                };

                pnlLogo.Controls.AddRange(new Control[] { lblAppTitle, lblAppSub });
            }
            pnlSidebar.Controls.Add(pnlLogo);

            Label lblCredit = new Label
            {
                Text = "Built by Yatesh Rohit",
                ForeColor = Color.FromArgb(120, 120, 125),
                Font = new Font("Segoe UI", 9F, FontStyle.Italic),
                AutoSize = false,
                Width = pnlSidebar.Width,
                Height = 40,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(15, 0, 0, 0),
                Dock = DockStyle.Bottom
            };

            pnlSidebar.Controls.Add(lblCredit);
            lblCredit.BringToFront();

            Panel pnlMain = new Panel { Dock = DockStyle.Fill };
            pnlHeader = new Panel { Dock = DockStyle.Top, Height = 80 };
            lblHeaderTitle = new Label { Text = "Welcome to PrintMaster", Font = new Font("Segoe UI Semibold", 18, FontStyle.Bold), Location = new Point(30, 25), AutoSize = true };
            pnlHeader.Controls.Add(lblHeaderTitle);
            Panel borderLine = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Color.LightGray };
            pnlHeader.Controls.Add(borderLine);

            pnlContent = new Panel { Dock = DockStyle.Fill, Padding = new Padding(30) };
            pnlMain.Controls.Add(pnlContent);
            pnlMain.Controls.Add(pnlHeader);

            this.Controls.Add(pnlMain);
            this.Controls.Add(sidebarSplitter);
            this.Controls.Add(pnlSidebar);
            this.Controls.Add(menuBar);

            pageHome = new Panel { Size = new Size(1000, 800), Dock = DockStyle.Fill, Visible = false };
            pageExtract = new Panel { Size = new Size(1000, 800), Dock = DockStyle.Fill, Visible = false, AutoScroll = true };
            pageFormat = new Panel { Size = new Size(1000, 800), Dock = DockStyle.Fill, Visible = false, AutoScroll = true };
            pageCompose = new Panel { Size = new Size(1000, 800), Dock = DockStyle.Fill, Visible = false, AutoScroll = true };
            pageSettings = new Panel { Size = new Size(1000, 800), Dock = DockStyle.Fill, Visible = false };
            pnlContent.Controls.AddRange(new Control[] { pageHome, pageExtract, pageFormat, pageCompose, pageSettings });

            Label lblWelcome = new Label { Text = "Workflow Automation Suite", Font = new Font("Segoe UI", 14), Location = new Point(0, 0), AutoSize = true };
            Label lblInstr = new Label { Text = "Use the sidebar on the left to navigate between extraction, formatting, and job card building.\n\nYou can drag the line next to the sidebar to shrink or expand it.", Font = new Font("Segoe UI", 11), Location = new Point(0, 40), AutoSize = true };
            Button btnSaveMem = new Button { Text = "💾 Save Current Workspace", Location = new Point(0, 120), Size = new Size(250, 45), Font = new Font("Segoe UI", 10, FontStyle.Bold), FlatStyle = FlatStyle.Flat };
            btnSaveMem.Click += (s, e) => SaveWorkspace();
            Button btnLoadMem = new Button { Text = "📂 Load Previous Workspace", Location = new Point(270, 120), Size = new Size(250, 45), Font = new Font("Segoe UI", 10, FontStyle.Bold), FlatStyle = FlatStyle.Flat };
            btnLoadMem.Click += (s, e) => LoadWorkspace();
            Button btnDocs = new Button { Text = "📖 View Documentation / Help", Location = new Point(0, 180), Size = new Size(520, 45), Font = new Font("Segoe UI", 10, FontStyle.Bold), FlatStyle = FlatStyle.Flat };
            btnDocs.Click += (s, e) => HelpViewer.ShowDocument("FAQ & How to Use", "faq.md");
            pageHome.Controls.AddRange(new Control[] { lblWelcome, lblInstr, btnSaveMem, btnLoadMem, btnDocs });

            GroupBox grpStep1 = new GroupBox { Text = "BarTender Auto-Extractor", Font = new Font("Segoe UI", 11, FontStyle.Bold), Location = new Point(0, 0), Size = new Size(850, 175) };
            txtSource1 = new TextBox { Location = new Point(20, 40), Size = new Size(245, 27), ReadOnly = true, Text = "Select .btw Files or Folder..." };
            Button btnSource1Files = new Button { Text = "Files", Location = new Point(270, 39), Size = new Size(60, 29), FlatStyle = FlatStyle.Flat };
            btnSource1Files.Click += (s, e) =>
            {
                using (OpenFileDialog ofd = new OpenFileDialog
                {
                    Multiselect = true,
                    Filter = "BarTender Files (*.btw)|*.btw"
                })
                {
                    if (DialogMemoryManager.ShowOpenDialog(
                        ofd,
                        "Extraction.SourceFiles") == DialogResult.OK)
                    {
                        selectedBtwFiles = ofd.FileNames;
                        selectedBtwFolder = "";
                        txtSource1.Text =
                            $"{selectedBtwFiles.Length} .btw file(s) selected";
                    }
                }
            };
            Button btnSource1Folder = new Button { Text = "Folder", Location = new Point(335, 39), Size = new Size(70, 29), FlatStyle = FlatStyle.Flat };
            btnSource1Folder.Click += (s, e) => { string folder = SelectFolderModern("Extraction.SourceFolder"); if (!string.IsNullOrEmpty(folder)) { selectedBtwFolder = folder; selectedBtwFiles = null; txtSource1.Text = folder; } };
            chkSubDirs1 = new CheckBox { Text = "Include Sub-directories", Location = new Point(20, 75), AutoSize = true, Font = new Font("Segoe UI", 10) };
            chkPreserveFolders1 = new CheckBox
            {
                Text = "Preserve Source Folder Structure",
                Location = new Point(20, 98),
                AutoSize = true,
                Font = new Font("Segoe UI", 10),
                Checked = false
            };
            txtExport1 = new TextBox { Location = new Point(430, 40), Size = new Size(250, 27), ReadOnly = true, Text = string.IsNullOrEmpty(currentSettings.DefaultExport1) ? "Select Output Folder..." : currentSettings.DefaultExport1 };
            Button btnExport1 = new Button { Text = "Browse", Location = new Point(690, 39), Size = new Size(70, 29), FlatStyle = FlatStyle.Flat };
            btnExport1.Click += (s, e) => { string f = SelectFolderModern("Extraction.OutputFolder"); if (!string.IsNullOrEmpty(f)) txtExport1.Text = f; };
            Label lblFormat = new Label { Text = "Format:", Location = new Point(20, 130), AutoSize = true, Font = new Font("Segoe UI", 10) };
            cmbFormat = new ComboBox { Location = new Point(85, 127), Size = new Size(80, 28), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbFormat.Items.AddRange(new object[] { "jpg", "png", "bmp" }); cmbFormat.SelectedIndex = 0;
            Label lblDpi = new Label { Text = "DPI:", Location = new Point(190, 130), AutoSize = true, Font = new Font("Segoe UI", 10) };
            numDpi = new NumericUpDown { Location = new Point(235, 127), Size = new Size(70, 27), Minimum = 96, Maximum = 2400, Value = currentSettings.DefaultDpi };
            btnRun1 = new Button { Text = "▶ Extract Images", Location = new Point(430, 90), Size = new Size(330, 50), Font = new Font("Segoe UI", 11, FontStyle.Bold), FlatStyle = FlatStyle.Flat };
            btnRun1.FlatAppearance.BorderSize = 0; btnRun1.Click += BtnRunBlock1_Click;
            chkExtractAndFormat = new CheckBox
            {
                Text = "Extract + Format in one task (uses Formatting tab border/output settings)",
                Location = new Point(315, 145),
                Size = new Size(445, 24),
                Font = new Font("Segoe UI", 8.5f),
                Checked = false
            };
            grpStep1.Controls.AddRange(new Control[] { txtSource1, btnSource1Files, btnSource1Folder, chkSubDirs1, chkPreserveFolders1, txtExport1, btnExport1, lblFormat, cmbFormat, lblDpi, numDpi, btnRun1, chkExtractAndFormat });
            pageExtract.Controls.Add(grpStep1);

            // ====================================================
            // FIND & EXTRACT BTW FILES
            // ====================================================
            GroupBox grpFindExtract = new GroupBox
            {
                Text = "Find & Extract BTW Files",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Location = new Point(0, 195),
                Size = new Size(850, 475)
            };

            Label lblFindMaster = new Label
            {
                Text = "Master BTW Directory:",
                Location = new Point(20, 35),
                AutoSize = true,
                Font = new Font("Segoe UI", 10)
            };

            txtFindMasterFolder = new TextBox
            {
                Location = new Point(170, 32),
                Size = new Size(510, 27),
                ReadOnly = true,
                Text = "Select master BTW directory..."
            };

            Button btnFindMasterBrowse = new Button
            {
                Text = "Browse",
                Location = new Point(690, 31),
                Size = new Size(70, 29),
                FlatStyle = FlatStyle.Flat
            };
            btnFindMasterBrowse.Click += (s, e) =>
            {
                string folder = SelectFolderModern("FindExtract.MasterBtwFolder");
                if (!string.IsNullOrWhiteSpace(folder))
                {
                    txtFindMasterFolder.Text = folder;
                    indexedBtwRoot = "";
                    btwSearchIndex.Clear();
                    lastFindMatches.Clear();
                    rtbFindResults.Clear();
                }
            };

            CheckBox chkFindSubDirs = new CheckBox
            {
                Text = "Include Sub-directories",
                Location = new Point(20, 67),
                AutoSize = true,
                Font = new Font("Segoe UI", 10),
                Checked = true,
                Enabled = false
            };

            Label lblFindNames = new Label
            {
                Text = "BTW names to find (one per line):",
                Location = new Point(20, 98),
                AutoSize = true,
                Font = new Font("Segoe UI", 10)
            };

            rtbFindNames = new RichTextBox
            {
                Location = new Point(20, 122),
                Size = new Size(390, 130),
                Font = new Font("Consolas", 9.5f),
                WordWrap = false
            };

            Button btnLoadFindList = new Button
            {
                Text = "Load TXT / Excel",
                Location = new Point(20, 260),
                Size = new Size(125, 32),
                FlatStyle = FlatStyle.Flat
            };
            btnLoadFindList.Click += BtnLoadFindList_Click;

            Button btnClearFindList = new Button
            {
                Text = "Clear List",
                Location = new Point(155, 260),
                Size = new Size(90, 32),
                FlatStyle = FlatStyle.Flat
            };
            btnClearFindList.Click += (s, e) =>
            {
                rtbFindNames.Clear();
                rtbFindResults.Clear();
                lastFindMatches.Clear();
            };

            btnFindSearch = new Button
            {
                Text = "🔎 Find BTW Files",
                Location = new Point(255, 260),
                Size = new Size(155, 32),
                FlatStyle = FlatStyle.Flat
            };
            btnFindSearch.Click += BtnFindBtwFiles_Click;

            Label lblFindResults = new Label
            {
                Text = "Search Results:",
                Location = new Point(430, 98),
                AutoSize = true,
                Font = new Font("Segoe UI", 10)
            };

            rtbFindResults = new RichTextBox
            {
                Location = new Point(430, 122),
                Size = new Size(380, 170),
                ReadOnly = true,
                Font = new Font("Consolas", 9.0f),
                WordWrap = false
            };

            Label lblFindOutput = new Label
            {
                Text = "Output Folder:",
                Location = new Point(20, 315),
                AutoSize = true,
                Font = new Font("Segoe UI", 10)
            };

            txtFindOutput = new TextBox
            {
                Location = new Point(125, 312),
                Size = new Size(555, 27),
                ReadOnly = true,
                Text = string.IsNullOrEmpty(currentSettings.DefaultExport1)
                    ? "Select Output Folder..."
                    : currentSettings.DefaultExport1
            };

            Button btnFindOutputBrowse = new Button
            {
                Text = "Browse",
                Location = new Point(690, 311),
                Size = new Size(70, 29),
                FlatStyle = FlatStyle.Flat
            };
            btnFindOutputBrowse.Click += (s, e) =>
            {
                string folder = SelectFolderModern("FindExtract.OutputFolder");
                if (!string.IsNullOrWhiteSpace(folder))
                    txtFindOutput.Text = folder;
            };

            Label lblFindFormat = new Label
            {
                Text = "Format:",
                Location = new Point(20, 355),
                AutoSize = true,
                Font = new Font("Segoe UI", 10)
            };

            cmbFindFormat = new ComboBox
            {
                Location = new Point(85, 352),
                Size = new Size(80, 28),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbFindFormat.Items.AddRange(new object[] { "jpg", "png", "bmp" });
            cmbFindFormat.SelectedIndex = cmbFormat.SelectedIndex >= 0
                ? cmbFormat.SelectedIndex
                : 0;

            Label lblFindDpi = new Label
            {
                Text = "DPI:",
                Location = new Point(190, 355),
                AutoSize = true,
                Font = new Font("Segoe UI", 10)
            };

            numFindDpi = new NumericUpDown
            {
                Location = new Point(235, 352),
                Size = new Size(70, 27),
                Minimum = 96,
                Maximum = 2400,
                Value = numDpi.Value
            };

            chkFindPreserve = new CheckBox
            {
                Text = "Preserve Source Folder Structure",
                Location = new Point(20, 388),
                AutoSize = true,
                Font = new Font("Segoe UI", 10),
                Checked = false
            };

            btnFindExtract = new Button
            {
                Text = "▶ Find & Extract",
                Location = new Point(430, 352),
                Size = new Size(330, 55),
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat
            };
            btnFindExtract.FlatAppearance.BorderSize = 0;
            btnFindExtract.Click += BtnFindAndExtract_Click;

            Label lblFindHint = new Label
            {
                Text = "Exact filename match, case-insensitive. .btw extension is optional. Duplicate names extract all matches.",
                Location = new Point(20, 425),
                Size = new Size(790, 35),
                Font = new Font("Segoe UI", 9)
            };

            grpFindExtract.Controls.AddRange(new Control[]
            {
                lblFindMaster, txtFindMasterFolder, btnFindMasterBrowse, chkFindSubDirs,
                lblFindNames, rtbFindNames, btnLoadFindList, btnClearFindList, btnFindSearch,
                lblFindResults, rtbFindResults,
                lblFindOutput, txtFindOutput, btnFindOutputBrowse,
                lblFindFormat, cmbFindFormat, lblFindDpi, numFindDpi,
                chkFindPreserve, btnFindExtract, lblFindHint
            });
            pageExtract.Controls.Add(grpFindExtract);

            GroupBox grpStep2 = new GroupBox { Text = "Image Formatting Engine", Font = new Font("Segoe UI", 11, FontStyle.Bold), Location = new Point(0, 0), Size = new Size(850, 205) };
            txtSource2 = new TextBox { Location = new Point(20, 40), Size = new Size(245, 27), ReadOnly = true, Text = "Select Images or Folder..." };
            Button btnSource2Files = new Button { Text = "Files", Location = new Point(270, 39), Size = new Size(60, 29), FlatStyle = FlatStyle.Flat };
            btnSource2Files.Click += (s, e) =>
            {
                using (OpenFileDialog ofd = new OpenFileDialog
                {
                    Multiselect = true,
                    Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp"
                })
                {
                    if (DialogMemoryManager.ShowOpenDialog(
                        ofd,
                        "Formatting.SourceFiles") == DialogResult.OK)
                    {
                        selectedRawFiles = ofd.FileNames;
                        selectedRawFolder = "";
                        txtSource2.Text =
                            $"{selectedRawFiles.Length} image(s) selected";
                    }
                }
            };
            Button btnSource2Folder = new Button { Text = "Folder", Location = new Point(335, 39), Size = new Size(70, 29), FlatStyle = FlatStyle.Flat };
            btnSource2Folder.Click += (s, e) => { string folder = SelectFolderModern("Formatting.SourceFolder"); if (!string.IsNullOrEmpty(folder)) { selectedRawFolder = folder; selectedRawFiles = null; txtSource2.Text = folder; } };
            chkSubDirs2 = new CheckBox { Text = "Include Sub-directories", Location = new Point(20, 75), AutoSize = true, Font = new Font("Segoe UI", 10) };
            chkPreserveFolders2 = new CheckBox
            {
                Text = "Preserve Source Folder Structure",
                Location = new Point(20, 98),
                AutoSize = true,
                Font = new Font("Segoe UI", 10),
                Checked = false
            };
            txtExport2 = new TextBox { Location = new Point(430, 40), Size = new Size(250, 27), ReadOnly = true, Text = string.IsNullOrEmpty(currentSettings.DefaultExport2) ? "Select Formatted Output..." : currentSettings.DefaultExport2 };
            Button btnExport2 = new Button { Text = "Browse", Location = new Point(690, 39), Size = new Size(70, 29), FlatStyle = FlatStyle.Flat };
            btnExport2.Click += (s, e) => { string f = SelectFolderModern("Formatting.OutputFolder"); if (!string.IsNullOrEmpty(f)) txtExport2.Text = f; };
            Label lblThick = new Label { Text = "Border Width:", Location = new Point(20, 135), AutoSize = true, Font = new Font("Segoe UI", 10) };
            numThickness = new NumericUpDown { Location = new Point(125, 132), Size = new Size(50, 27), Minimum = 0, Maximum = 50, Value = 4 };
            btnColor = new Button { Text = "Pick Color", Location = new Point(190, 130), Size = new Size(90, 29), BackColor = selectedBorderColor, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnColor.Click += (s, e) => { using (ColorDialog cd = new ColorDialog()) { if (cd.ShowDialog() == DialogResult.OK) { btnColor.BackColor = cd.Color; selectedBorderColor = cd.Color; } } };
            chkPadding = new CheckBox { Text = "Add Padding", Location = new Point(300, 135), AutoSize = true, Font = new Font("Segoe UI", 10) };

            Label lblFormattingOutputFormat = new Label
            {
                Text = "Output Format:",
                Location = new Point(430, 157),
                AutoSize = true,
                Font = new Font("Segoe UI", 10)
            };

            cmbFormattingOutputFormat = new ComboBox
            {
                Location = new Point(535, 153),
                Size = new Size(180, 28),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbFormattingOutputFormat.Items.AddRange(
                new object[] { "Same as Source", "PNG (Lossless)" });
            cmbFormattingOutputFormat.SelectedIndex = 0;

            Button btnRun2 = new Button { Text = "▶ Format Images", Location = new Point(430, 90), Size = new Size(330, 50), BackColor = Color.MediumPurple, ForeColor = Color.White, Font = new Font("Segoe UI", 11, FontStyle.Bold), FlatStyle = FlatStyle.Flat };
            btnRun2.FlatAppearance.BorderSize = 0; btnRun2.Click += BtnRunBlock2_Click;

            grpStep2.Controls.AddRange(new Control[] { txtSource2, btnSource2Files, btnSource2Folder, chkSubDirs2, chkPreserveFolders2, txtExport2, btnExport2, lblThick, numThickness, btnColor, chkPadding, lblFormattingOutputFormat, cmbFormattingOutputFormat, btnRun2 });
            pageFormat.Controls.Add(grpStep2);

            GroupBox grpConsole = new GroupBox { Text = "Live Execution Console", Font = new Font("Segoe UI", 11, FontStyle.Bold), Location = new Point(0, 225), Size = new Size(850, 260) };

            rtbFormatConsole = new RichTextBox
            {
                Location = new Point(20, 35),
                Size = new Size(810, 205),
                BackColor = Color.FromArgb(10, 10, 10),
                ForeColor = Color.LimeGreen,
                Font = new Font("Calibri", 11, FontStyle.Bold),
                ReadOnly = true,
                BorderStyle = BorderStyle.None
            };

            grpConsole.Controls.Add(rtbFormatConsole);
            pageFormat.Controls.Add(grpConsole);

            // ====================================================
            // IMAGE COLLAGE BUILDER
            // Temporary container name - can be renamed later.
            // ====================================================
            GroupBox grpCollage = new GroupBox
            {
                Text = "Image Collage Builder",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Location = new Point(0, 505),
                Size = new Size(850, 400)
            };

            // --------------------------------------------------------
            // Source
            // --------------------------------------------------------
            Label lblCollageSource = new Label
            {
                Text = "Image Source",
                Location = new Point(20, 34),
                AutoSize = true,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };

            txtCollageSource = new TextBox
            {
                Location = new Point(20, 56),
                Size = new Size(350, 27),
                ReadOnly = true,
                Text = "Select image files or a folder..."
            };

            Button btnCollageFiles = new Button
            {
                Text = "Files",
                Location = new Point(380, 55),
                Size = new Size(90, 29),
                FlatStyle = FlatStyle.Flat
            };
            btnCollageFiles.Click += (s, e) =>
            {
                using (OpenFileDialog ofd = new OpenFileDialog
                {
                    Multiselect = true,
                    Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff"
                })
                {
                    if (DialogMemoryManager.ShowOpenDialog(
                        ofd,
                        "Collage.SourceFiles") == DialogResult.OK)
                    {
                        selectedCollageFiles = ofd.FileNames;
                        selectedCollageFolder = "";
                        manualCollageSlots = null;
                        manualCollageCaptions = null;
                manualCollageCustomNames = null;
                        UpdateCollageArrangementButtons();
                        txtCollageSource.Text = selectedCollageFiles.Length + " image(s) selected";
                    }
                }
            };

            Button btnCollageFolder = new Button
            {
                Text = "Folder",
                Location = new Point(478, 55),
                Size = new Size(95, 29),
                FlatStyle = FlatStyle.Flat
            };
            btnCollageFolder.Click += (s, e) =>
            {
                string folder = SelectFolderModern("Collage.SourceFolder");
                if (!string.IsNullOrWhiteSpace(folder))
                {
                    selectedCollageFolder = folder;
                    selectedCollageFiles = null;
                    manualCollageSlots = null;
                    manualCollageCaptions = null;
                manualCollageCustomNames = null;
                    UpdateCollageArrangementButtons();
                    txtCollageSource.Text = folder;
                }
            };

            chkCollageSubDirs = new CheckBox
            {
                Text = "Include sub-folders",
                Location = new Point(20, 89),
                AutoSize = true,
                Font = new Font("Segoe UI", 9)
            };

            chkCollageSubDirs.CheckedChanged += (s, e) =>
            {
                manualCollageSlots = null;
                manualCollageCaptions = null;
                manualCollageCustomNames = null;
                UpdateCollageArrangementButtons();
            };

            // --------------------------------------------------------
            // Destination
            // --------------------------------------------------------
            Label lblCollageOutput = new Label
            {
                Text = "Output Folder",
                Location = new Point(595, 34),
                AutoSize = true,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };

            txtCollageOutput = new TextBox
            {
                Location = new Point(595, 56),
                Size = new Size(155, 27),
                ReadOnly = true,
                Text = string.IsNullOrEmpty(currentSettings.DefaultExport2)
                    ? "Select..."
                    : currentSettings.DefaultExport2
            };

            Button btnCollageOutput = new Button
            {
                Text = "Browse",
                Location = new Point(758, 55),
                Size = new Size(70, 29),
                FlatStyle = FlatStyle.Flat
            };
            btnCollageOutput.Click += (s, e) =>
            {
                string folder = SelectFolderModern("Collage.OutputFolder");
                if (!string.IsNullOrWhiteSpace(folder))
                    txtCollageOutput.Text = folder;
            };

            chkCollageAutoFileName = new CheckBox
            {
                Text = "Generate file name automatically",
                Location = new Point(595, 89),
                AutoSize = true,
                Font = new Font("Segoe UI", 9),
                Checked = true
            };

            Label lblCollageFileName = new Label
            {
                Text = "Custom File Name",
                Location = new Point(595, 116),
                AutoSize = true,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };

            txtCollageFileName = new TextBox
            {
                Location = new Point(595, 138),
                Size = new Size(233, 27),
                Text = "ImageCollage",
                Enabled = false
            };

            chkCollageAutoFileName.CheckedChanged += (s, e) =>
            {
                txtCollageFileName.Enabled = !chkCollageAutoFileName.Checked;
                lblCollageFileName.Enabled = !chkCollageAutoFileName.Checked;
            };

            lblCollageFileName.Enabled = false;

            // --------------------------------------------------------
            // Output settings
            // --------------------------------------------------------
            Label lblOutputSettings = new Label
            {
                Text = "Output Settings",
                Location = new Point(20, 135),
                AutoSize = true,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };

            Label lblCollageType = new Label
            {
                Text = "Format",
                Location = new Point(20, 161),
                AutoSize = true,
                Font = new Font("Segoe UI", 9)
            };

            cmbCollageOutputType = new ComboBox
            {
                Location = new Point(20, 182),
                Size = new Size(150, 28),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbCollageOutputType.Items.AddRange(new object[] { "PDF", "Word", "Excel" });
            cmbCollageOutputType.SelectedIndex = 0;
            cmbCollageOutputType.SelectedIndexChanged += (s, e) =>
            {
                UpdateCollageArrangementButtons();
            };

            Label lblCollageGrid = new Label
            {
                Text = "Grid Layout",
                Location = new Point(190, 161),
                AutoSize = true,
                Font = new Font("Segoe UI", 9)
            };

            cmbCollageGrid = new ComboBox
            {
                Location = new Point(190, 182),
                Size = new Size(150, 28),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbCollageGrid.Items.AddRange(new object[] { "1 x 1", "2 x 2", "3 x 3", "4 x 4" });
            cmbCollageGrid.SelectedIndex = 1;
            cmbCollageGrid.SelectedIndexChanged += (s, e) =>
            {
                manualCollageSlots = null;
                manualCollageCaptions = null;
                manualCollageCustomNames = null;
                UpdateCollageArrangementButtons();
            };

            Label lblCollageOrientation = new Label
            {
                Text = "Page Orientation",
                Location = new Point(360, 161),
                AutoSize = true,
                Font = new Font("Segoe UI", 9)
            };

            cmbCollageOrientation = new ComboBox
            {
                Location = new Point(360, 182),
                Size = new Size(150, 28),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbCollageOrientation.Items.AddRange(new object[] { "Portrait", "Landscape" });
            cmbCollageOrientation.SelectedIndex = 0;

            // --------------------------------------------------------
            // Display options
            // --------------------------------------------------------
            Label lblDisplayOptions = new Label
            {
                Text = "Display Options",
                Location = new Point(20, 229),
                AutoSize = true,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };

            chkCollageShowFilename = new CheckBox
            {
                Text = "Show file name / caption below image",
                Location = new Point(20, 252),
                AutoSize = true,
                Font = new Font("Segoe UI", 9),
                Checked = false
            };

            chkCollageBorders = new CheckBox
            {
                Text = "Draw grid cell borders",
                Location = new Point(245, 252),
                AutoSize = true,
                Font = new Font("Segoe UI", 9),
                Checked = false
            };

            Label lblCollageHint = new Label
            {
                Text =
                    "Most common image sizes are placed first for a cleaner collage. " +
                    "Manual arrangement keeps the selected grid fixed and lets you rearrange images.",
                Location = new Point(20, 289),
                Size = new Size(505, 42),
                Font = new Font("Segoe UI", 8)
            };

            btnArrangeCollage = new Button
            {
                Text = "🖼  Arrange Images...",
                Location = new Point(540, 226),
                Size = new Size(138, 62),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat
            };
            btnArrangeCollage.FlatAppearance.BorderSize = 0;
            btnArrangeCollage.Click += BtnArrangeCollage_Click;

            btnClearCollageArrangement = new Button
            {
                Text = "↺  Automatic",
                Location = new Point(686, 226),
                Size = new Size(142, 62),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat
            };
            btnClearCollageArrangement.FlatAppearance.BorderSize = 0;
            btnClearCollageArrangement.Click += (s, e) =>
            {
                manualCollageSlots = null;
                manualCollageCaptions = null;
                manualCollageCustomNames = null;
                UpdateCollageArrangementButtons();
            };

            btnGenerateCollage = new Button
            {
                Text = "▶  Generate Collage",
                Location = new Point(540, 294),
                Size = new Size(288, 52),
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat
            };
            btnGenerateCollage.FlatAppearance.BorderSize = 0;
            btnGenerateCollage.Click += BtnGenerateCollage_Click;

            grpCollage.Controls.AddRange(new Control[]
            {
                lblCollageSource, txtCollageSource, btnCollageFiles, btnCollageFolder,
                chkCollageSubDirs,
                lblCollageOutput, txtCollageOutput, btnCollageOutput,
                chkCollageAutoFileName, lblCollageFileName, txtCollageFileName,
                lblOutputSettings,
                lblCollageType, cmbCollageOutputType,
                lblCollageGrid, cmbCollageGrid,
                lblCollageOrientation, cmbCollageOrientation,
                lblDisplayOptions,
                chkCollageShowFilename, chkCollageBorders,
                lblCollageHint,
                btnArrangeCollage, btnClearCollageArrangement,
                btnGenerateCollage
            });

            pageFormat.Controls.Add(grpCollage);
            UpdateCollageArrangementButtons();

            pnlProducts = CreateLane("Lane 1: Products", 20, pageCompose);
            chkShowText1 = new CheckBox { Text = "Show Text Box Below", Checked = true, Location = new Point(30, 355), AutoSize = true, Font = new Font("Segoe UI", 10) };
            txtLane1Text = new TextBox { Location = new Point(30, 385), Size = new Size(210, 27), Text = "PRODUCT BARCODE {W} X {H} MM", BackColor = Color.FromArgb(50, 52, 59), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            Button btnFont1 = new Button { Text = "Aa Font...", Location = new Point(250, 384), Size = new Size(80, 29), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(50, 52, 59) };
            btnFont1.Click += (s, e) => { using (FontDialog fd = new FontDialog { Font = fontLane1 }) { if (fd.ShowDialog() == DialogResult.OK) { fontLane1 = fd.Font; } } };

            pnlCartons = CreateLane("Lane 2: Cartons", 350, pageCompose);
            chkShowText2 = new CheckBox { Text = "Show Text Box Below", Checked = true, Location = new Point(360, 355), AutoSize = true, Font = new Font("Segoe UI", 10) };
            txtLane2Text = new TextBox { Location = new Point(360, 385), Size = new Size(210, 27), Text = "CARTON BARCODE {W} X {H} MM", BackColor = Color.FromArgb(50, 52, 59), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            Button btnFont2 = new Button { Text = "Aa Font...", Location = new Point(580, 384), Size = new Size(80, 29), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(50, 52, 59) };
            btnFont2.Click += (s, e) => { using (FontDialog fd = new FontDialog { Font = fontLane2 }) { if (fd.ShowDialog() == DialogResult.OK) { fontLane2 = fd.Font; } } };

            pnlOthers = CreateLane("Lane 3: Others", 680, pageCompose);

            Button btnRun3 = new Button { Text = "▶ Generate Job Card", Font = new Font("Segoe UI", 12, FontStyle.Bold), Location = new Point(20, 440), Size = new Size(650, 50), BackColor = Color.FromArgb(252, 213, 53), ForeColor = Color.Black, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnRun3.FlatAppearance.BorderSize = 0; btnRun3.Click += BtnRunBlock3_Click;

            Button btnClear = new Button { Text = "🗑️ Clear All Lanes", Font = new Font("Segoe UI", 10, FontStyle.Bold), Location = new Point(690, 440), Size = new Size(300, 50), BackColor = Color.FromArgb(28, 29, 33), ForeColor = Color.IndianRed, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnClear.FlatAppearance.BorderSize = 1; btnClear.FlatAppearance.BorderColor = Color.IndianRed;
            btnClear.Click += (s, e) => { ClearAllLanesAndSmartFinder(); };

            chkOpenDirectFreeform = new CheckBox
            {
                Text = "Open directly in Freeform Builder (skip Job Card preview)",
                Location = new Point(20, 498),
                AutoSize = true,
                Checked = currentSettings.OpenJobCardDirectlyInFreeform,
                Font = new Font("Segoe UI", 9)
            };
            chkOpenDirectFreeform.CheckedChanged += (s, e) =>
            {
                currentSettings.OpenJobCardDirectlyInFreeform =
                    chkOpenDirectFreeform.Checked;
                SettingsManager.Save(currentSettings);
            };

            pageCompose.Controls.AddRange(new Control[]
            {
                chkShowText1, txtLane1Text, btnFont1,
                chkShowText2, txtLane2Text, btnFont2,
                btnRun3, btnClear, chkOpenDirectFreeform
            });

            // Optional Smart Image Finder. Existing manual lanes above stay
            // exactly as they are and remain available at all times.
            BuildSmartImageFinder();

            GroupBox grpTheme = new GroupBox
            {
                Text = "Appearance",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Location = new Point(0, 0),
                Size = new Size(600, 180)
            };

            Label lblTheme = new Label
            {
                Text = "Color Theme:",
                Location = new Point(20, 35),
                AutoSize = true,
                Font = new Font("Segoe UI", 10)
            };

            cmbTheme = new ComboBox
            {
                Location = new Point(180, 32),
                Size = new Size(240, 28),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbTheme.Items.AddRange(ThemeManager.ThemeNames);

            currentSettings.Theme =
                AppearanceManager.NormalizeTheme(currentSettings.Theme);
            cmbTheme.SelectedItem = currentSettings.Theme;

            Label lblTypography = new Label
            {
                Text = "Typography / Text:",
                Location = new Point(20, 73),
                AutoSize = true,
                Font = new Font("Segoe UI", 10)
            };

            cmbTypography = new ComboBox
            {
                Location = new Point(180, 70),
                Size = new Size(240, 28),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbTypography.Items.AddRange(AppearanceManager.TypographyNames);
            currentSettings.TypographyStyle =
                AppearanceManager.NormalizeTypographyChoice(
                    currentSettings.TypographyStyle);
            cmbTypography.SelectedItem = currentSettings.TypographyStyle;

            Label lblLayout = new Label
            {
                Text = "Layout Style:",
                Location = new Point(20, 111),
                AutoSize = true,
                Font = new Font("Segoe UI", 10)
            };

            cmbLayout = new ComboBox
            {
                Location = new Point(180, 108),
                Size = new Size(240, 28),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbLayout.Items.AddRange(AppearanceManager.LayoutNames);
            currentSettings.LayoutStyle =
                AppearanceManager.NormalizeLayoutChoice(
                    currentSettings.LayoutStyle);
            cmbLayout.SelectedItem = currentSettings.LayoutStyle;

            Button btnResetAppearance = new Button
            {
                Text = "Reset to NPPL Default",
                Location = new Point(430, 32),
                Size = new Size(145, 28),
                FlatStyle = FlatStyle.Flat
            };

            lblAppearanceResolved = new Label
            {
                Text = "",
                Location = new Point(20, 145),
                Size = new Size(550, 24),
                AutoEllipsis = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic)
            };

            EventHandler appearanceChanged = (s, e) =>
            {
                string selectedTheme =
                    cmbTheme.SelectedItem == null
                        ? "Forest Graphite"
                        : cmbTheme.SelectedItem.ToString();

                ApplyTheme(selectedTheme);
            };

            cmbTheme.SelectedIndexChanged += appearanceChanged;
            cmbTypography.SelectedIndexChanged += appearanceChanged;
            cmbLayout.SelectedIndexChanged += appearanceChanged;

            btnResetAppearance.Click += (s, e) =>
            {
                cmbTheme.SelectedItem = "Forest Graphite";
                cmbTypography.SelectedItem =
                    AppearanceManager.UseThemeDefault;
                cmbLayout.SelectedItem =
                    AppearanceManager.UseThemeDefault;
                ApplyTheme("Forest Graphite");
            };

            grpTheme.Controls.AddRange(
                new Control[]
                {
                    lblTheme,
                    cmbTheme,
                    lblTypography,
                    cmbTypography,
                    lblLayout,
                    cmbLayout,
                    btnResetAppearance,
                    lblAppearanceResolved
                });

            GroupBox grpDefs = new GroupBox { Text = "Application Defaults", Font = new Font("Segoe UI", 11, FontStyle.Bold), Location = new Point(0, 200), Size = new Size(600, 200) };
            Label lblDefDpi = new Label { Text = "Default Extraction DPI:", Location = new Point(20, 40), AutoSize = true, Font = new Font("Segoe UI", 10) };
            numDefDpi = new NumericUpDown { Location = new Point(200, 37), Size = new Size(80, 27), Minimum = 96, Maximum = 2400, Value = currentSettings.DefaultDpi };
            Label lblDefEx1 = new Label { Text = "Default Extraction Output:", Location = new Point(20, 85), AutoSize = true, Font = new Font("Segoe UI", 10) };
            txtDefExport1 = new TextBox { Location = new Point(200, 82), Size = new Size(280, 27), Text = currentSettings.DefaultExport1 };
            Button btnDefEx1 = new Button { Text = "Browse", Location = new Point(490, 81), Size = new Size(80, 29), FlatStyle = FlatStyle.Flat };
            btnDefEx1.Click += (s, e) => { string f = SelectFolderModern("Settings.DefaultExtractionOutput"); if (!string.IsNullOrEmpty(f)) txtDefExport1.Text = f; };
            Label lblDefEx2 = new Label { Text = "Default Formatting Output:", Location = new Point(20, 130), AutoSize = true, Font = new Font("Segoe UI", 10) };
            txtDefExport2 = new TextBox { Location = new Point(200, 127), Size = new Size(280, 27), Text = currentSettings.DefaultExport2 };
            Button btnDefEx2 = new Button { Text = "Browse", Location = new Point(490, 126), Size = new Size(80, 29), FlatStyle = FlatStyle.Flat };
            btnDefEx2.Click += (s, e) => { string f = SelectFolderModern("Settings.DefaultFormattingOutput"); if (!string.IsNullOrEmpty(f)) txtDefExport2.Text = f; };
            grpDefs.Controls.AddRange(new Control[] { lblDefDpi, numDefDpi, lblDefEx1, txtDefExport1, btnDefEx1, lblDefEx2, txtDefExport2, btnDefEx2 });

            btnSaveSettings = new Button { Text = "💾 Save Preferences", Location = new Point(0, 420), Size = new Size(200, 45), Font = new Font("Segoe UI", 10, FontStyle.Bold), FlatStyle = FlatStyle.Flat };
            btnSaveSettings.Click += (s, e) => {
                currentSettings.Theme =
                    cmbTheme.SelectedItem == null
                        ? "Forest Graphite"
                        : cmbTheme.SelectedItem.ToString();

                currentSettings.TypographyStyle =
                    cmbTypography.SelectedItem == null
                        ? AppearanceManager.UseThemeDefault
                        : cmbTypography.SelectedItem.ToString();

                currentSettings.LayoutStyle =
                    cmbLayout.SelectedItem == null
                        ? AppearanceManager.UseThemeDefault
                        : cmbLayout.SelectedItem.ToString();

                currentSettings.DefaultDpi = (int)numDefDpi.Value;
                currentSettings.DefaultExport1 = txtDefExport1.Text;
                currentSettings.DefaultExport2 = txtDefExport2.Text;
                SettingsManager.Save(currentSettings);
                MessageBox.Show("Settings permanently saved!", "Success");
            };

            pageSettings.Controls.AddRange(new Control[] { grpTheme, grpDefs, btnSaveSettings });

            int navY = 90;
            navButtons.Add(CreateNavButton("🏠 Dashboard Home", navY)); navY += 50;
            navButtons.Add(CreateNavButton("📂 1. Extraction", navY)); navY += 50;
            navButtons.Add(CreateNavButton("🎨 2. Formatting", navY)); navY += 50;
            navButtons.Add(CreateNavButton("🖨️ 3. Quick Job Card", navY)); navY += 70;
            Button btnNavFreeform = CreateNavButton("🚀 Pro Freeform Builder", navY); navButtons.Add(btnNavFreeform); navY += 70;
            Button btnNavSettings = CreateNavButton("⚙️ Settings", navY); navButtons.Add(btnNavSettings);
            pnlSidebar.Controls.AddRange(navButtons.ToArray());

            navButtons[0].Click += (s, e) => SwitchPage(pageHome, navButtons[0], "Dashboard Home");
            navButtons[1].Click += (s, e) => SwitchPage(pageExtract, navButtons[1], "Step 1: BarTender Extraction");
            navButtons[2].Click += (s, e) => SwitchPage(pageFormat, navButtons[2], "Step 2: Image Formatting");
            navButtons[3].Click += (s, e) => SwitchPage(pageCompose, navButtons[3], "Step 3: Quick Job Card");
            btnNavFreeform.Click += (s, e) => { FreeformBuilderForm builder = new FreeformBuilderForm(); builder.WindowState = FormWindowState.Maximized; builder.Show(); };
            btnNavSettings.Click += (s, e) => SwitchPage(pageSettings, btnNavSettings, "Application Settings");

            CaptureOriginalAppearance();
            SwitchPage(pageHome, navButtons[0], "Dashboard Home");
        }

        private Button CreateNavButton(string text, int yPos)
        {
            Button btn = new Button
            {
                Text = "  " + text,
                Location = new Point(0, yPos),
                Size = new Size(pnlSidebar.Width, 50),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat, // Fixed from FlatModel
                Font = new Font("Segoe UI Semibold", 10),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(15, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.MouseEnter += (s, e) => { if (btn != activeNavButton) btn.BackColor = sidebarHover; };
            btn.MouseLeave += (s, e) => { if (btn != activeNavButton) btn.BackColor = sidebarColor; };
            return btn;
        }

        private void SwitchPage(Panel targetPage, Button clickedButton, string headerTitle)
        {
            pageHome.Visible = false; pageExtract.Visible = false; pageFormat.Visible = false; pageCompose.Visible = false; pageSettings.Visible = false;
            if (activeNavButton != null) { activeNavButton.BackColor = sidebarColor; activeNavButton.ForeColor = (sidebarColor.R > 200) ? Color.Black : Color.LightGray; }
            activeNavButton = clickedButton; activeNavButton.BackColor = activeColor; activeNavButton.ForeColor = Color.White;
            targetPage.Visible = true; targetPage.BringToFront(); lblHeaderTitle.Text = headerTitle;
        }













































        private sealed class SmartImageResultItem
        {
            public SmartImageResultItem(string path, bool newest) { FilePath = path; NewestDuplicate = newest; }
            public string FilePath { get; private set; }
            public bool NewestDuplicate { get; private set; }
            public override string ToString()
            {
                string d; try { d = File.GetLastWriteTime(FilePath).ToString("yyyy-MM-dd HH:mm"); } catch { d = "unknown date"; }
                return (NewestDuplicate ? "[NEWEST] " : "") + d + " | " + Path.GetFileName(FilePath);
            }
        }


























        private static Dictionary<string, List<string>>
            BuildBtwSearchIndex(
                string masterRoot)
        {
            Dictionary<string, List<string>> index =
                new Dictionary<string, List<string>>(
                    StringComparer.OrdinalIgnoreCase);

            foreach (string file in
                EnumerateBtwFilesSafe(masterRoot))
            {
                string key =
                    Path.GetFileNameWithoutExtension(file);

                List<string> paths;

                if (!index.TryGetValue(key, out paths))
                {
                    paths = new List<string>();
                    index[key] = paths;
                }

                paths.Add(file);
            }

            return index;
        }













        // ==========================================
        // STEP 3: PREVIEW GENERATOR WORKFLOW
        // ==========================================













    }
}