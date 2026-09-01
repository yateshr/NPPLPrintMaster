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
            this.BackColor = Color.FromArgb(38, 40, 46);
            this.ForeColor = Color.White;

            previewBmp = generatedBmp;
            rawItems = itemsForBuilder;

            pbPreview = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(50, 52, 59),
                Image = previewBmp
            };

            Panel pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = Color.FromArgb(28, 29, 33),
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
        private CheckBox chkSubDirs1, chkPreserveFolders1;
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
        private string[] selectedCollageFiles = null;
        private string selectedCollageFolder = "";

        private FlowLayoutPanel pnlProducts, pnlCartons, pnlOthers;
        private TextBox txtLane1Text, txtLane2Text;

        // Smart Image Finder V1.1
        private TextBox txtSmartProductFolder, txtSmartCartonFolder;
        private TextBox txtSmartProductCode, txtSmartCartonCode;
        private ListBox lstSmartProductResults, lstSmartCartonResults;
        private PictureBox pbSmartProductPreview, pbSmartCartonPreview;
        private Label lblSmartStatus, lblSmartHealth;
        private Button btnSmartRefresh;
        private CheckBox chkSmartMonitor;
        private FileSystemWatcher smartProductWatcher, smartCartonWatcher;
        private System.Windows.Forms.Timer smartMonitorTimer;
        private bool smartRefreshRunning;
        private SmartImageFinderEngine productImageIndex = new SmartImageFinderEngine("ProductBarcodes");
        private SmartImageFinderEngine cartonImageIndex = new SmartImageFinderEngine("CartonBarcodes");

        private CheckBox chkShowText1, chkShowText2;
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

        private ComboBox cmbTheme;
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
            ApplyTheme(currentSettings.Theme);
            Logger.LogAction("APP_START", "NPPLPrintMaster Enterprise Edition launched.");
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
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
            clearItem.Click += (s, e) => { ClearLane(pnlProducts); ClearLane(pnlCartons); ClearLane(pnlOthers); };
            editMenu.DropDownItems.Add(clearItem);

            ToolStripMenuItem toolsMenu = new ToolStripMenuItem("Tools");
            ToolStripMenuItem standaloneBuilderItem = new ToolStripMenuItem("🎨 Pro Freeform Builder");
            standaloneBuilderItem.Click += (s, e) => { FreeformBuilderForm builder = new FreeformBuilderForm(); builder.Show(); };
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
            btnSource1Files.Click += (s, e) => { using (OpenFileDialog ofd = new OpenFileDialog { Multiselect = true, Filter = "BarTender Files (*.btw)|*.btw" }) { if (ofd.ShowDialog() == DialogResult.OK) { selectedBtwFiles = ofd.FileNames; selectedBtwFolder = ""; txtSource1.Text = $"{selectedBtwFiles.Length} .btw file(s) selected"; } } };
            Button btnSource1Folder = new Button { Text = "Folder", Location = new Point(335, 39), Size = new Size(70, 29), FlatStyle = FlatStyle.Flat };
            btnSource1Folder.Click += (s, e) => { string folder = SelectFolderModern(); if (!string.IsNullOrEmpty(folder)) { selectedBtwFolder = folder; selectedBtwFiles = null; txtSource1.Text = folder; } };
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
            btnExport1.Click += (s, e) => { string f = SelectFolderModern(); if (!string.IsNullOrEmpty(f)) txtExport1.Text = f; };
            Label lblFormat = new Label { Text = "Format:", Location = new Point(20, 130), AutoSize = true, Font = new Font("Segoe UI", 10) };
            cmbFormat = new ComboBox { Location = new Point(85, 127), Size = new Size(80, 28), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbFormat.Items.AddRange(new object[] { "jpg", "png", "bmp" }); cmbFormat.SelectedIndex = 0;
            Label lblDpi = new Label { Text = "DPI:", Location = new Point(190, 130), AutoSize = true, Font = new Font("Segoe UI", 10) };
            numDpi = new NumericUpDown { Location = new Point(235, 127), Size = new Size(70, 27), Minimum = 96, Maximum = 2400, Value = currentSettings.DefaultDpi };
            btnRun1 = new Button { Text = "▶ Extract Images", Location = new Point(430, 90), Size = new Size(330, 50), Font = new Font("Segoe UI", 11, FontStyle.Bold), FlatStyle = FlatStyle.Flat };
            btnRun1.FlatAppearance.BorderSize = 0; btnRun1.Click += BtnRunBlock1_Click;
            grpStep1.Controls.AddRange(new Control[] { txtSource1, btnSource1Files, btnSource1Folder, chkSubDirs1, chkPreserveFolders1, txtExport1, btnExport1, lblFormat, cmbFormat, lblDpi, numDpi, btnRun1 });
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
                string folder = SelectFolderModern();
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
                string folder = SelectFolderModern();
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
            btnSource2Files.Click += (s, e) => { using (OpenFileDialog ofd = new OpenFileDialog { Multiselect = true, Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp" }) { if (ofd.ShowDialog() == DialogResult.OK) { selectedRawFiles = ofd.FileNames; selectedRawFolder = ""; txtSource2.Text = $"{selectedRawFiles.Length} image(s) selected"; } } };
            Button btnSource2Folder = new Button { Text = "Folder", Location = new Point(335, 39), Size = new Size(70, 29), FlatStyle = FlatStyle.Flat };
            btnSource2Folder.Click += (s, e) => { string folder = SelectFolderModern(); if (!string.IsNullOrEmpty(folder)) { selectedRawFolder = folder; selectedRawFiles = null; txtSource2.Text = folder; } };
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
            btnExport2.Click += (s, e) => { string f = SelectFolderModern(); if (!string.IsNullOrEmpty(f)) txtExport2.Text = f; };
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
                Size = new Size(850, 355)
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
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        selectedCollageFiles = ofd.FileNames;
                        selectedCollageFolder = "";
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
                string folder = SelectFolderModern();
                if (!string.IsNullOrWhiteSpace(folder))
                {
                    selectedCollageFolder = folder;
                    selectedCollageFiles = null;
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
                string folder = SelectFolderModern();
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
                Text = "Show file name below image",
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
                Text = "Most common image sizes are placed first for a cleaner collage.",
                Location = new Point(20, 289),
                Size = new Size(440, 25),
                Font = new Font("Segoe UI", 8)
            };

            btnGenerateCollage = new Button
            {
                Text = "▶  Generate Collage",
                Location = new Point(540, 226),
                Size = new Size(288, 62),
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
                lblCollageHint, btnGenerateCollage
            });

            pageFormat.Controls.Add(grpCollage);

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
            btnClear.Click += (s, e) => { ClearLane(pnlProducts); ClearLane(pnlCartons); ClearLane(pnlOthers); };

            pageCompose.Controls.AddRange(new Control[] { chkShowText1, txtLane1Text, btnFont1, chkShowText2, txtLane2Text, btnFont2, btnRun3, btnClear });

            // Optional Smart Image Finder. Existing manual lanes above stay
            // exactly as they are and remain available at all times.
            BuildSmartImageFinder();

            GroupBox grpTheme = new GroupBox { Text = "Appearance", Font = new Font("Segoe UI", 11, FontStyle.Bold), Location = new Point(0, 0), Size = new Size(600, 100) };
            Label lblTheme = new Label { Text = "Global Theme:", Location = new Point(20, 40), AutoSize = true, Font = new Font("Segoe UI", 10) };
            cmbTheme = new ComboBox { Location = new Point(140, 37), Size = new Size(200, 28), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbTheme.Items.AddRange(ThemeManager.ThemeNames);

            if (!cmbTheme.Items.Contains(currentSettings.Theme))
                currentSettings.Theme = "Midnight Dark";

            cmbTheme.SelectedItem = currentSettings.Theme;
            cmbTheme.SelectedIndexChanged += (s, e) =>
            {
                if (cmbTheme.SelectedItem != null)
                    ApplyTheme(cmbTheme.SelectedItem.ToString());
            };
            grpTheme.Controls.AddRange(new Control[] { lblTheme, cmbTheme });

            GroupBox grpDefs = new GroupBox { Text = "Application Defaults", Font = new Font("Segoe UI", 11, FontStyle.Bold), Location = new Point(0, 120), Size = new Size(600, 200) };
            Label lblDefDpi = new Label { Text = "Default Extraction DPI:", Location = new Point(20, 40), AutoSize = true, Font = new Font("Segoe UI", 10) };
            numDefDpi = new NumericUpDown { Location = new Point(200, 37), Size = new Size(80, 27), Minimum = 96, Maximum = 2400, Value = currentSettings.DefaultDpi };
            Label lblDefEx1 = new Label { Text = "Default Extraction Output:", Location = new Point(20, 85), AutoSize = true, Font = new Font("Segoe UI", 10) };
            txtDefExport1 = new TextBox { Location = new Point(200, 82), Size = new Size(280, 27), Text = currentSettings.DefaultExport1 };
            Button btnDefEx1 = new Button { Text = "Browse", Location = new Point(490, 81), Size = new Size(80, 29), FlatStyle = FlatStyle.Flat };
            btnDefEx1.Click += (s, e) => { string f = SelectFolderModern(); if (!string.IsNullOrEmpty(f)) txtDefExport1.Text = f; };
            Label lblDefEx2 = new Label { Text = "Default Formatting Output:", Location = new Point(20, 130), AutoSize = true, Font = new Font("Segoe UI", 10) };
            txtDefExport2 = new TextBox { Location = new Point(200, 127), Size = new Size(280, 27), Text = currentSettings.DefaultExport2 };
            Button btnDefEx2 = new Button { Text = "Browse", Location = new Point(490, 126), Size = new Size(80, 29), FlatStyle = FlatStyle.Flat };
            btnDefEx2.Click += (s, e) => { string f = SelectFolderModern(); if (!string.IsNullOrEmpty(f)) txtDefExport2.Text = f; };
            grpDefs.Controls.AddRange(new Control[] { lblDefDpi, numDefDpi, lblDefEx1, txtDefExport1, btnDefEx1, lblDefEx2, txtDefExport2, btnDefEx2 });

            btnSaveSettings = new Button { Text = "💾 Save Preferences", Location = new Point(0, 340), Size = new Size(200, 45), Font = new Font("Segoe UI", 10, FontStyle.Bold), FlatStyle = FlatStyle.Flat };
            btnSaveSettings.Click += (s, e) => {
                currentSettings.Theme = cmbTheme.SelectedItem.ToString();
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
            btnNavFreeform.Click += (s, e) => { FreeformBuilderForm builder = new FreeformBuilderForm(); builder.Show(); };
            btnNavSettings.Click += (s, e) => SwitchPage(pageSettings, btnNavSettings, "Application Settings");

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

        private void ApplyTheme(string themeName)
        {
            activeTheme = ThemeManager.GetTheme(themeName);
            sidebarColor = activeTheme.SidebarColor; sidebarHover = activeTheme.SidebarHover;
            bgLight = activeTheme.BgLight; contentBg = activeTheme.ContentBg;
            textColor = activeTheme.TextColor; controlBg = activeTheme.ControlBg; activeColor = activeTheme.ActiveColor;

            this.BackColor = bgLight; pnlContent.BackColor = bgLight; pnlHeader.BackColor = contentBg;
            lblHeaderTitle.ForeColor = textColor; pnlSidebar.BackColor = sidebarColor; sidebarSplitter.BackColor = activeColor;

            pnlLogo.BackColor = (sidebarColor.R > 200) ? Color.FromArgb(220, 220, 220) : Color.FromArgb(20, 20, 24);
            if (lblAppTitle != null) lblAppTitle.ForeColor = (sidebarColor.R > 200) ? Color.Black : Color.White;
            if (lblAppSub != null) lblAppSub.ForeColor = activeColor;

            menuBar.BackColor = contentBg; menuBar.ForeColor = textColor;
            foreach (ToolStripItem item in menuBar.Items)
            {
                item.ForeColor = textColor;
                if (item is ToolStripMenuItem dropDownItem) { foreach (ToolStripItem subItem in dropDownItem.DropDownItems) { subItem.BackColor = contentBg; subItem.ForeColor = textColor; } }
            }

            foreach (Button btn in navButtons)
            {
                btn.BackColor = (btn == activeNavButton) ? activeColor : sidebarColor;
                btn.ForeColor = (btn == activeNavButton)
                    ? ThemeManager.GetContrastTextColor(activeColor)
                    : ThemeManager.GetContrastTextColor(sidebarColor);
                if (btn.Text.Contains("Pro Freeform")) btn.ForeColor = (sidebarColor.R > 200 && btn != activeNavButton) ? Color.DarkGoldenrod : Color.Gold;
            }

            ThemeManager.ApplyColorsToControls(pageHome.Controls, activeTheme);
            ThemeManager.ApplyColorsToControls(pageExtract.Controls, activeTheme);
            ThemeManager.ApplyColorsToControls(pageFormat.Controls, activeTheme);
            ThemeManager.ApplyColorsToControls(pageCompose.Controls, activeTheme);
            ThemeManager.ApplyColorsToControls(pageSettings.Controls, activeTheme);

            if (btnRun1 != null)
            {
                btnRun1.BackColor = activeColor;
                btnRun1.ForeColor = ThemeManager.GetContrastTextColor(activeColor);
            }

            if (btnSaveSettings != null)
            {
                btnSaveSettings.BackColor = activeColor;
                btnSaveSettings.ForeColor = ThemeManager.GetContrastTextColor(activeColor);
            }

            // Determine workspace brightness from the actual content surface,
            // not from the sidebar. This is important for themes with a dark
            // blue sidebar and a light workspace.
            bool isLightTheme = ThemeManager.IsLight(contentBg);

            foreach (Control c in pageCompose.Controls)
            {
                if (c is Panel wrapper && wrapper.Size.Width == 310)
                {
                    wrapper.BackColor = contentBg;

                    foreach (Control child in wrapper.Controls)
                    {
                        if (child is Label lbl)
                            lbl.ForeColor = textColor;

                        if (child is FlowLayoutPanel flp)
                            flp.BackColor = controlBg;

                        if (child is Button btnBrowse)
                        {
                            btnBrowse.BackColor = controlBg;
                            btnBrowse.ForeColor = textColor;
                            btnBrowse.FlatAppearance.BorderColor = activeTheme.BorderColor;
                        }
                    }
                }
                else if (c is TextBox txt)
                {
                    txt.BackColor = controlBg;
                    txt.ForeColor = textColor;
                }
                else if (c is Button btnAction)
                {
                    if (btnAction.Text.Contains("Generate"))
                    {
                        btnAction.BackColor = activeColor;
                        btnAction.ForeColor = ThemeManager.GetContrastTextColor(activeColor);
                    }
                    else if (btnAction.Text.Contains("Clear"))
                    {
                        btnAction.BackColor = controlBg;
                        btnAction.ForeColor = isLightTheme
                            ? Color.FromArgb(176, 45, 45)
                            : Color.FromArgb(255, 130, 130);
                    }
                    else if (btnAction.Text.Contains("Aa Font"))
                    {
                        btnAction.BackColor = controlBg;
                        btnAction.ForeColor = textColor;
                    }
                }
            }
        }

        private void BuildSmartImageFinder()
        {
            GroupBox grp = new GroupBox
            {
                Text = "Smart Image Finder V1.1 (Optional)",
                Location = new Point(20, 520),
                Size = new Size(970, 545),
                Font = new Font("Segoe UI", 11, FontStyle.Bold)
            };

            Label hint = new Label
            {
                Text = "Scanner-ready: scan/type a code and press Enter. Click a preview for the large Image Inspector.",
                Location = new Point(20, 30), Size = new Size(920, 22), Font = new Font("Segoe UI", 9)
            };

            Label lp = new Label { Text = "Product Barcode Directory:", Location = new Point(20, 65), AutoSize = true };
            Label lc = new Label { Text = "Carton Barcode Directory:", Location = new Point(20, 100), AutoSize = true };
            txtSmartProductFolder = new TextBox { Location = new Point(180,62), Size = new Size(520,27), ReadOnly = true, Text = currentSettings.SmartProductImageFolder ?? "" };
            txtSmartCartonFolder = new TextBox { Location = new Point(180,97), Size = new Size(520,27), ReadOnly = true, Text = currentSettings.SmartCartonImageFolder ?? "" };
            Button bp = new Button { Text = "Folder", Location = new Point(710,61), Size = new Size(75,29), FlatStyle = FlatStyle.Flat };
            Button bc = new Button { Text = "Folder", Location = new Point(710,96), Size = new Size(75,29), FlatStyle = FlatStyle.Flat };
            btnSmartRefresh = new Button { Text = "↻ Incremental Refresh", Location = new Point(795,61), Size = new Size(155,29), FlatStyle = FlatStyle.Flat };
            chkSmartMonitor = new CheckBox { Text = "Monitor folders automatically", Location = new Point(795,99), Size = new Size(170,24), Checked = true, Font = new Font("Segoe UI", 8) };
            lblSmartStatus = new Label { Text = "Scanner ready. " + GetSmartIndexSummary(), Location = new Point(20,132), Size = new Size(930,20), Font = new Font("Segoe UI",8) };
            lblSmartHealth = new Label { Text = GetSmartHealthSummary(), Location = new Point(20,154), Size = new Size(720,22), Font = new Font("Segoe UI",8,FontStyle.Bold) };
            Button bfp = new Button { Text = "Product Failures", Location = new Point(755,150), Size = new Size(95,28), FlatStyle = FlatStyle.Flat };
            Button bfc = new Button { Text = "Carton Failures", Location = new Point(855,150), Size = new Size(95,28), FlatStyle = FlatStyle.Flat };

            GroupBox gp = CreateSmartSearchGroup("Product Barcode / EAN", new Point(20,190), false);
            GroupBox gc = CreateSmartSearchGroup("Carton SAP Code", new Point(490,190), true);

            bp.Click += (s,e) => SelectSmartFolder(false);
            bc.Click += (s,e) => SelectSmartFolder(true);
            btnSmartRefresh.Click += async (s,e) => await RefreshSmartIndexesAsync(false, false);
            chkSmartMonitor.CheckedChanged += (s,e) => SetupSmartWatchers();
            bfp.Click += (s,e) => ShowFailures("Product", productImageIndex);
            bfc.Click += (s,e) => ShowFailures("Carton", cartonImageIndex);

            grp.Controls.AddRange(new Control[] { hint,lp,lc,txtSmartProductFolder,txtSmartCartonFolder,bp,bc,btnSmartRefresh,chkSmartMonitor,lblSmartStatus,lblSmartHealth,bfp,bfc,gp,gc });
            pageCompose.Controls.Add(grp);
            pageCompose.AutoScrollMinSize = new Size(0, 1095);

            smartMonitorTimer = new System.Windows.Forms.Timer { Interval = 1800 };
            smartMonitorTimer.Tick += async (s,e) =>
            {
                smartMonitorTimer.Stop();
                if (!smartRefreshRunning && chkSmartMonitor.Checked)
                    await RefreshSmartIndexesAsync(false, true);
            };
            SetupSmartWatchers();
        }

        private GroupBox CreateSmartSearchGroup(string title, Point location, bool carton)
        {
            GroupBox grp = new GroupBox { Text = title, Location = location, Size = new Size(460,325), Font = new Font("Segoe UI",10,FontStyle.Bold) };
            TextBox code = new TextBox { Location = new Point(15,32), Size = new Size(280,27), Font = new Font("Consolas",10) };
            Button find = new Button { Text = carton ? "Find Carton" : "Find Product", Location = new Point(305,31), Size = new Size(135,29), FlatStyle = FlatStyle.Flat };
            ListBox list = new ListBox { Location = new Point(15,70), Size = new Size(210,205), HorizontalScrollbar = true };
            PictureBox pic = new PictureBox { Location = new Point(235,70), Size = new Size(205,205), BorderStyle = BorderStyle.FixedSingle, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.White, Cursor = Cursors.Hand };
            Label tip = new Label { Text = "Click preview for large inspector", Location = new Point(235,278), Size = new Size(205,18), TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI",7) };
            Button add = new Button { Text = carton ? "Add to Carton Lane" : "Add to Product Lane", Location = new Point(235,297), Size = new Size(205,25), FlatStyle = FlatStyle.Flat };

            if (carton) { txtSmartCartonCode = code; lstSmartCartonResults = list; pbSmartCartonPreview = pic; }
            else { txtSmartProductCode = code; lstSmartProductResults = list; pbSmartProductPreview = pic; }

            find.Click += (s,e) => FindSmartImages(carton ? cartonImageIndex : productImageIndex, code.Text, list, pic, carton ? "Carton" : "Product");
            code.KeyDown += (s,e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true; find.PerformClick(); code.SelectAll(); code.Focus();
            };
            list.SelectedIndexChanged += (s,e) => PreviewSmartResult(list,pic);
            list.DoubleClick += (s,e) => OpenInspector(list,code.Text);
            pic.Click += (s,e) => OpenInspector(list,code.Text);
            add.Click += (s,e) => AddSmartResultToLane(list, carton ? pnlCartons : pnlProducts, carton ? "Carton" : "Product");

            grp.Controls.AddRange(new Control[] { code,find,list,pic,tip,add });
            return grp;
        }

        private void SelectSmartFolder(bool carton)
        {
            string folder = SelectFolderModern();
            if (string.IsNullOrWhiteSpace(folder)) return;
            if (carton) { txtSmartCartonFolder.Text = folder; currentSettings.SmartCartonImageFolder = folder; }
            else { txtSmartProductFolder.Text = folder; currentSettings.SmartProductImageFolder = folder; }
            SettingsManager.Save(currentSettings);
            SetupSmartWatchers();
            lblSmartStatus.Text = (carton ? "Carton" : "Product") + " directory changed. Click Incremental Refresh.";
        }

        private async System.Threading.Tasks.Task RefreshSmartIndexesAsync(bool forceAll, bool automatic)
        {
            if (smartRefreshRunning) return;
            string pf = txtSmartProductFolder == null ? "" : txtSmartProductFolder.Text;
            string cf = txtSmartCartonFolder == null ? "" : txtSmartCartonFolder.Text;
            if (!Directory.Exists(pf) && !Directory.Exists(cf))
            {
                if (!automatic) MessageBox.Show("Select at least one valid Product or Carton image directory.", "Smart Image Finder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            smartRefreshRunning = true;
            btnSmartRefresh.Enabled = false;
            btnSmartRefresh.Text = automatic ? "Auto refreshing..." : "Refreshing...";
            try
            {
                SmartImageRefreshResult pr = null, cr = null;
                if (Directory.Exists(pf))
                    pr = await System.Threading.Tasks.Task.Run(() => productImageIndex.RefreshIncremental(pf, p => ReportProgress("Product",p), forceAll));
                if (Directory.Exists(cf))
                    cr = await System.Threading.Tasks.Task.Run(() => cartonImageIndex.RefreshIncremental(cf, p => ReportProgress("Carton",p), forceAll));

                if (Directory.Exists(pf)) currentSettings.SmartProductImageFolder = pf;
                if (Directory.Exists(cf)) currentSettings.SmartCartonImageFolder = cf;
                SettingsManager.Save(currentSettings);

                lblSmartStatus.Text = automatic ? "Folder change detected — index updated automatically." :
                    "Product: " + (pr == null ? "not scanned" : pr.ToString()) + "   |   Carton: " + (cr == null ? "not scanned" : cr.ToString());
                lblSmartHealth.Text = GetSmartHealthSummary();
                SetupSmartWatchers();
            }
            catch (Exception ex)
            {
                lblSmartStatus.Text = "Index refresh failed: " + ex.Message;
                if (!automatic) MessageBox.Show(ex.Message, "Smart Image Finder Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                smartRefreshRunning = false;
                btnSmartRefresh.Text = "↻ Incremental Refresh";
                btnSmartRefresh.Enabled = true;
            }
        }

        private void ReportProgress(string type, SmartImageIndexProgress p)
        {
            if (p == null || (p.Processed != p.Total && p.Processed % 25 != 0)) return;
            try
            {
                BeginInvoke(new Action(() => lblSmartStatus.Text = string.Format(
                    "{0}: {1}/{2} checked | {3} unchanged | {4} decoded | {5} failed",
                    type,p.Processed,p.Total,p.Unchanged,p.Decoded,p.Failed)));
            }
            catch { }
        }

        private string GetSmartIndexSummary()
        {
            return string.Format("Product {0} image(s) / {1} code(s) | Carton {2} image(s) / {3} code(s)",
                productImageIndex.FileCount, productImageIndex.CodeCount, cartonImageIndex.FileCount, cartonImageIndex.CodeCount);
        }

        private string GetSmartHealthSummary()
        {
            return string.Format("Health: Product {0} decoded / {1} failed / {2} duplicate code(s) | Carton {3} decoded / {4} failed / {5} duplicate code(s)",
                productImageIndex.DecodedFileCount, productImageIndex.FailedFileCount, productImageIndex.DuplicateCodeCount,
                cartonImageIndex.DecodedFileCount, cartonImageIndex.FailedFileCount, cartonImageIndex.DuplicateCodeCount);
        }

        private void SetupSmartWatchers()
        {
            DisposeWatcher(ref smartProductWatcher); DisposeWatcher(ref smartCartonWatcher);
            if (chkSmartMonitor == null || !chkSmartMonitor.Checked) return;
            smartProductWatcher = CreateWatcher(txtSmartProductFolder == null ? "" : txtSmartProductFolder.Text);
            smartCartonWatcher = CreateWatcher(txtSmartCartonFolder == null ? "" : txtSmartCartonFolder.Text);
        }

        private FileSystemWatcher CreateWatcher(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return null;
            try
            {
                FileSystemWatcher w = new FileSystemWatcher(folder)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size
                };
                FileSystemEventHandler h = (s,e) => QueueAutoRefresh(e.FullPath);
                RenamedEventHandler r = (s,e) => QueueAutoRefresh(e.FullPath);
                w.Created += h; w.Changed += h; w.Deleted += h; w.Renamed += r; w.EnableRaisingEvents = true;
                return w;
            }
            catch { return null; }
        }

        private void QueueAutoRefresh(string path)
        {
            if (!IsSmartImage(path)) return;
            try
            {
                BeginInvoke(new Action(() =>
                {
                    if (smartMonitorTimer == null) return;
                    smartMonitorTimer.Stop(); smartMonitorTimer.Start();
                    lblSmartStatus.Text = "Image-library change detected. Waiting for file copy to finish...";
                }));
            }
            catch { }
        }

        private bool IsSmartImage(string path)
        {
            string e = Path.GetExtension(path ?? "");
            return e.Equals(".png",StringComparison.OrdinalIgnoreCase) || e.Equals(".jpg",StringComparison.OrdinalIgnoreCase) ||
                   e.Equals(".jpeg",StringComparison.OrdinalIgnoreCase) || e.Equals(".bmp",StringComparison.OrdinalIgnoreCase) ||
                   e.Equals(".tif",StringComparison.OrdinalIgnoreCase) || e.Equals(".tiff",StringComparison.OrdinalIgnoreCase);
        }

        private void DisposeWatcher(ref FileSystemWatcher w)
        {
            if (w == null) return;
            try { w.EnableRaisingEvents = false; w.Dispose(); } catch { }
            w = null;
        }

        private void FindSmartImages(SmartImageFinderEngine index, string code, ListBox list, PictureBox preview, string type)
        {
            string normalized = SmartImageFinderEngine.NormalizeCode(code);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                MessageBox.Show("Enter or scan a " + type + " barcode / code first.", "Smart Image Finder");
                return;
            }

            List<string> matches = index.FindExact(normalized);
            list.Items.Clear();
            for (int i=0;i<matches.Count;i++) list.Items.Add(new SmartImageResultItem(matches[i], i==0 && matches.Count>1));

            if (matches.Count == 0)
            {
                SetSmartPreviewImage(preview,null);
                lblSmartStatus.Text = type + " code " + normalized + " was not found.";
                return;
            }

            list.SelectedIndex = 0;
            lblSmartStatus.Text = matches.Count == 1
                ? type + " exact match found. Click preview to inspect."
                : "⚠ " + matches.Count + " duplicate/version matches for " + normalized + ". Newest modified image is first.";
        }

        private void PreviewSmartResult(ListBox list, PictureBox preview)
        {
            SmartImageResultItem x = list == null ? null : list.SelectedItem as SmartImageResultItem;
            SetSmartPreviewImage(preview, x == null ? null : x.FilePath);
        }

        private void OpenInspector(ListBox list, string code)
        {
            SmartImageResultItem x = list == null ? null : list.SelectedItem as SmartImageResultItem;
            if (x == null || !File.Exists(x.FilePath)) return;
            using (SmartImageInspectorForm f = new SmartImageInspectorForm(x.FilePath, SmartImageFinderEngine.NormalizeCode(code))) f.ShowDialog(this);
        }

        private void SetSmartPreviewImage(PictureBox preview, string file)
        {
            if (preview == null) return;
            Image old = preview.Image; preview.Image = null; if (old != null) old.Dispose();
            if (string.IsNullOrWhiteSpace(file) || !File.Exists(file)) return;
            try { using (Image i = Image.FromFile(file)) preview.Image = new Bitmap(i); } catch { }
        }

        private void ShowFailures(string type, SmartImageFinderEngine index)
        {
            List<string> failed = index.GetFailedFiles();
            if (failed.Count == 0) { MessageBox.Show("No failed " + type + " barcode images are currently recorded.", "Index Health"); return; }
            using (FailedBarcodeInspectorForm f = new FailedBarcodeInspectorForm(type,failed)) f.ShowDialog(this);
        }

        private void AddSmartResultToLane(ListBox list, FlowLayoutPanel lane, string type)
        {
            SmartImageResultItem x = list == null ? null : list.SelectedItem as SmartImageResultItem;
            if (x == null || !File.Exists(x.FilePath)) { MessageBox.Show("Select a " + type + " search result first.", "Smart Image Finder"); return; }
            AddThumbnail(lane,x.FilePath);
            lblSmartStatus.Text = Path.GetFileName(x.FilePath) + " added to the " + type + " lane.";

            if (type == "Product" && txtSmartCartonCode != null) { txtSmartCartonCode.Focus(); txtSmartCartonCode.SelectAll(); }
            else if (type == "Carton" && txtSmartProductCode != null) { txtSmartProductCode.Focus(); txtSmartProductCode.SelectAll(); }
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

        private string SelectFolderModern()
        {
            using (OpenFileDialog ofd = new OpenFileDialog { ValidateNames = false, CheckFileExists = false, CheckPathExists = true, FileName = "Folder Selection" })
                if (ofd.ShowDialog() == DialogResult.OK) return Path.GetDirectoryName(ofd.FileName);
            return "";
        }

        private string GetLastLaneFolder(string laneTitle)
        {
            string folder = "";

            if (laneTitle.StartsWith("Lane 1", StringComparison.OrdinalIgnoreCase))
                folder = currentSettings.LastProductFolder;
            else if (laneTitle.StartsWith("Lane 2", StringComparison.OrdinalIgnoreCase))
                folder = currentSettings.LastCartonFolder;
            else if (laneTitle.StartsWith("Lane 3", StringComparison.OrdinalIgnoreCase))
                folder = currentSettings.LastOtherFolder;

            return !string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder)
                ? folder
                : "";
        }

        private void SaveLastLaneFolder(string laneTitle, string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                return;

            if (laneTitle.StartsWith("Lane 1", StringComparison.OrdinalIgnoreCase))
                currentSettings.LastProductFolder = folder;
            else if (laneTitle.StartsWith("Lane 2", StringComparison.OrdinalIgnoreCase))
                currentSettings.LastCartonFolder = folder;
            else if (laneTitle.StartsWith("Lane 3", StringComparison.OrdinalIgnoreCase))
                currentSettings.LastOtherFolder = folder;
            else
                return;

            SettingsManager.Save(currentSettings);
        }

        private FlowLayoutPanel CreateLane(string title, int xPos, Panel parent)
        {
            Panel laneWrapper = new Panel { Location = new Point(xPos, 20), Size = new Size(310, 320), BackColor = Color.FromArgb(50, 52, 59) };
            Label lbl = new Label { Text = title, Location = new Point(10, 15), Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = Color.FromArgb(252, 213, 53), AutoSize = true };
            FlowLayoutPanel panel = new FlowLayoutPanel { Location = new Point(10, 45), Size = new Size(290, 230), AllowDrop = true, AutoScroll = true, BackColor = Color.FromArgb(38, 40, 46), BorderStyle = BorderStyle.FixedSingle, Cursor = Cursors.Hand };
            Button btnBrowse = new Button { Text = "📂 Browse Files...", Location = new Point(10, 280), Size = new Size(290, 30), Font = new Font("Segoe UI", 9, FontStyle.Bold), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(28, 29, 33), ForeColor = Color.White };
            btnBrowse.FlatAppearance.BorderSize = 0;

            void OpenBrowse(object s, EventArgs e)
            {
                using (OpenFileDialog ofd = new OpenFileDialog
                {
                    Multiselect = true,
                    Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp"
                })
                {
                    string lastFolder = GetLastLaneFolder(title);

                    if (!string.IsNullOrWhiteSpace(lastFolder))
                        ofd.InitialDirectory = lastFolder;

                    if (ofd.ShowDialog() != DialogResult.OK)
                        return;

                    if (ofd.FileNames.Length > 0)
                        SaveLastLaneFolder(title, Path.GetDirectoryName(ofd.FileNames[0]));

                    foreach (string f in ofd.FileNames)
                        AddThumbnail(panel, f);
                }
            }
            panel.Click += OpenBrowse; btnBrowse.Click += OpenBrowse;
            panel.DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
            panel.DragDrop += (s, e) =>
            {
                string[] items = (string[])e.Data.GetData(DataFormats.FileDrop);

                foreach (string item in items)
                {
                    if (Directory.Exists(item))
                    {
                        SaveLastLaneFolder(title, item);

                        foreach (string f in Directory.GetFiles(item))
                        {
                            string ext = Path.GetExtension(f);
                            if (ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                                ext.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                                ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase))
                            {
                                AddThumbnail(panel, f);
                            }
                        }
                    }
                    else if (File.Exists(item))
                    {
                        SaveLastLaneFolder(title, Path.GetDirectoryName(item));
                        AddThumbnail(panel, item);
                    }
                }
            };

            laneWrapper.Controls.Add(lbl);
            laneWrapper.Controls.Add(panel);
            laneWrapper.Controls.Add(btnBrowse);
            parent.Controls.Add(laneWrapper);

            return panel;
        }

        private void AddThumbnail(FlowLayoutPanel panel, string filePath)
        {
            try
            {
                if (panel == null || !File.Exists(filePath))
                    return;

                using (Image img = Image.FromFile(filePath))
                {
                    int picW = Math.Max(50, panel.ClientSize.Width - 30);
                    int picH = Math.Max(
                        1,
                        (int)Math.Round(
                            img.Height * ((double)picW / Math.Max(1, img.Width))));

                    Bitmap thumbnail = new Bitmap(picW, picH);

                    using (Graphics g = Graphics.FromImage(thumbnail))
                    {
                        g.Clear(Color.White);
                        g.InterpolationMode =
                            System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.DrawImage(
                            img,
                            new Rectangle(0, 0, picW, picH),
                            0,
                            0,
                            img.Width,
                            img.Height,
                            GraphicsUnit.Pixel);
                    }

                    PictureBox pb = new PictureBox
                    {
                        Image = thumbnail,
                        Size = new Size(picW, picH),
                        SizeMode = PictureBoxSizeMode.Zoom,
                        Tag = filePath,
                        Cursor = Cursors.Hand,
                        Margin = new Padding(5)
                    };

                    ContextMenuStrip cms = new ContextMenuStrip();
                    ToolStripMenuItem removeItem =
                        new ToolStripMenuItem("❌ Remove Image");

                    removeItem.Click += (s, e) =>
                    {
                        if (pb.IsDisposed)
                            return;

                        panel.SuspendLayout();

                        try
                        {
                            Image oldImage = pb.Image;
                            pb.Image = null;
                            pb.ContextMenuStrip = null;

                            if (panel.Controls.Contains(pb))
                                panel.Controls.Remove(pb);

                            pb.Dispose();
                            oldImage?.Dispose();
                            cms.Dispose();
                        }
                        finally
                        {
                            panel.ResumeLayout(true);
                        }
                    };

                    cms.Items.Add(removeItem);
                    pb.ContextMenuStrip = cms;

                    ToolTip tt = new ToolTip();
                    tt.SetToolTip(pb, "Right-click the image to remove it.");
                    pb.Disposed += (s, e) => tt.Dispose();

                    panel.Controls.Add(pb);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Unable to add image thumbnail.\n\n" +
                    Path.GetFileName(filePath) +
                    "\n\n" +
                    ex.Message,
                    "Thumbnail Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void ClearLane(FlowLayoutPanel panel) { foreach (Control c in panel.Controls.Cast<Control>().ToList()) { if (c is PictureBox pb) { pb.Image?.Dispose(); pb.Dispose(); } } panel.Controls.Clear(); }
        private List<string> GetFilesFromLane(FlowLayoutPanel panel) { List<string> files = new List<string>(); foreach (Control c in panel.Controls) { if (c is PictureBox pb && pb.Tag != null) files.Add(pb.Tag.ToString()); } return files; }

        private async void BtnLoadFindList_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog
            {
                Multiselect = false,
                Filter =
                    "Supported Lists (*.txt;*.xlsx;*.xls)|*.txt;*.xlsx;*.xls|" +
                    "Text Files (*.txt)|*.txt|" +
                    "Excel Files (*.xlsx;*.xls)|*.xlsx;*.xls|" +
                    "All Files (*.*)|*.*"
            })
            {
                if (ofd.ShowDialog() != DialogResult.OK)
                    return;

                try
                {
                    List<string> names =
                        await Task.Run(() => BtwListReader.ReadNames(ofd.FileName));

                    rtbFindNames.Lines =
                        names
                            .Where(n => !string.IsNullOrWhiteSpace(n))
                            .ToArray();

                    rtbFindResults.Clear();
                    lastFindMatches.Clear();

                    MessageBox.Show(
                        $"Loaded {names.Count} BTW name(s).",
                        "List Loaded",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "Unable to read the list file.\n\n" + ex.Message,
                        "List Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }

        private async void BtnFindBtwFiles_Click(object sender, EventArgs e)
        {
            await FindBtwMatchesAsync(true);
        }

        private async void BtnFindAndExtract_Click(object sender, EventArgs e)
        {
            if (!Directory.Exists(txtFindMasterFolder.Text))
            {
                MessageBox.Show(
                    "Please select a valid Master BTW Directory.",
                    "Master Directory Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtFindOutput.Text) ||
                txtFindOutput.Text.StartsWith("Select ", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(
                    "Please select an output folder.",
                    "Output Folder Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            List<string> matches =
                await FindBtwMatchesAsync(false);

            if (matches.Count == 0)
            {
                MessageBox.Show(
                    "No matching BTW files were found.",
                    "Nothing to Extract",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            btnFindExtract.Enabled = false;
            btnFindSearch.Enabled = false;
            btnFindExtract.Text = "⏳ Extracting...";

            try
            {
                Directory.CreateDirectory(txtFindOutput.Text);

                int count =
                    await BarTenderEngine.ExtractImages(
                        matches,
                        txtFindOutput.Text,
                        cmbFindFormat.SelectedItem.ToString(),
                        (int)numFindDpi.Value,
                        chkFindPreserve.Checked,
                        txtFindMasterFolder.Text);

                Logger.LogAction(
                    "FIND_EXTRACT",
                    $"Found {matches.Count} BTW match(es); extracted {count} image(s) to {txtFindOutput.Text}" +
                    (chkFindPreserve.Checked
                        ? " (folder structure preserved)"
                        : ""));

                MessageBox.Show(
                    $"Find & Extract complete!\n\n" +
                    $"BTW matches: {matches.Count}\n" +
                    $"Images extracted: {count}",
                    "Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Logger.LogAction(
                    "ERROR",
                    "Find & Extract: " + ex.Message);

                MessageBox.Show(
                    ex.Message,
                    "Find & Extract Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                btnFindExtract.Text = "▶ Find & Extract";
                btnFindExtract.Enabled = true;
                btnFindSearch.Enabled = true;
            }
        }

        private async Task<List<string>> FindBtwMatchesAsync(
            bool showSummary)
        {
            string masterRoot =
                txtFindMasterFolder.Text == null
                    ? string.Empty
                    : txtFindMasterFolder.Text.Trim();

            if (!Directory.Exists(masterRoot))
            {
                MessageBox.Show(
                    "Please select a valid Master BTW Directory.",
                    "Master Directory Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return new List<string>();
            }

            List<string> requestedNames =
                GetRequestedBtwNames();

            if (requestedNames.Count == 0)
            {
                MessageBox.Show(
                    "Enter BTW names or load a TXT / Excel list first.",
                    "BTW List Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return new List<string>();
            }

            btnFindSearch.Enabled = false;
            string oldSearchText = btnFindSearch.Text;
            btnFindSearch.Text = "⏳ Indexing / Searching...";

            try
            {
                if (!string.Equals(
                    indexedBtwRoot,
                    masterRoot,
                    StringComparison.OrdinalIgnoreCase) ||
                    btwSearchIndex.Count == 0)
                {
                    Dictionary<string, List<string>> newIndex =
                        await Task.Run(
                            () => BuildBtwSearchIndex(masterRoot));

                    btwSearchIndex = newIndex;
                    indexedBtwRoot = masterRoot;
                }

                List<string> matches =
                    new List<string>();

                int foundNames = 0;
                int missingNames = 0;
                int duplicateNames = 0;

                rtbFindResults.Clear();

                foreach (string requested in requestedNames)
                {
                    List<string> paths;

                    if (btwSearchIndex.TryGetValue(
                        requested,
                        out paths) &&
                        paths != null &&
                        paths.Count > 0)
                    {
                        foundNames++;

                        if (paths.Count > 1)
                        {
                            duplicateNames++;
                            rtbFindResults.AppendText(
                                $"DUPLICATE  {requested}.btw  ({paths.Count} matches)\n");

                            foreach (string path in paths)
                            {
                                rtbFindResults.AppendText(
                                    "           " +
                                    GetRelativeDisplayPath(masterRoot, path) +
                                    "\n");
                            }
                        }
                        else
                        {
                            rtbFindResults.AppendText(
                                "FOUND      " +
                                requested +
                                ".btw  ->  " +
                                GetRelativeDisplayPath(
                                    masterRoot,
                                    paths[0]) +
                                "\n");
                        }

                        foreach (string path in paths)
                        {
                            if (!matches.Contains(
                                path,
                                StringComparer.OrdinalIgnoreCase))
                            {
                                matches.Add(path);
                            }
                        }
                    }
                    else
                    {
                        missingNames++;
                        rtbFindResults.AppendText(
                            "NOT FOUND  " +
                            requested +
                            ".btw\n");
                    }
                }

                lastFindMatches = matches;

                rtbFindResults.AppendText(
                    "\n----------------------------------------\n");
                rtbFindResults.AppendText(
                    $"Requested: {requestedNames.Count} | " +
                    $"Found names: {foundNames} | " +
                    $"Missing: {missingNames} | " +
                    $"Duplicates: {duplicateNames} | " +
                    $"BTW files to extract: {matches.Count}\n");
                rtbFindResults.ScrollToCaret();

                if (showSummary)
                {
                    MessageBox.Show(
                        $"Search complete.\n\n" +
                        $"Requested names: {requestedNames.Count}\n" +
                        $"Found names: {foundNames}\n" +
                        $"Missing names: {missingNames}\n" +
                        $"Duplicate names: {duplicateNames}\n" +
                        $"BTW files matched: {matches.Count}",
                        "BTW Search",
                        MessageBoxButtons.OK,
                        missingNames > 0
                            ? MessageBoxIcon.Warning
                            : MessageBoxIcon.Information);
                }

                return matches;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Unable to search the master BTW directory.\n\n" +
                    ex.Message,
                    "BTW Search Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                return new List<string>();
            }
            finally
            {
                btnFindSearch.Text = oldSearchText;
                btnFindSearch.Enabled = true;
            }
        }

        private List<string> GetRequestedBtwNames()
        {
            HashSet<string> unique =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            foreach (string line in rtbFindNames.Lines)
            {
                string normalized =
                    NormalizeBtwLookupName(line);

                if (!string.IsNullOrWhiteSpace(normalized))
                    unique.Add(normalized);
            }

            return unique.ToList();
        }

        private static string NormalizeBtwLookupName(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string name =
                value.Trim()
                    .Trim('"')
                    .Trim();

            name = Path.GetFileName(name);

            if (name.EndsWith(
                ".btw",
                StringComparison.OrdinalIgnoreCase))
            {
                name =
                    Path.GetFileNameWithoutExtension(name);
            }

            return name.Trim();
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

        private static IEnumerable<string>
            EnumerateBtwFilesSafe(
                string root)
        {
            Stack<string> pending =
                new Stack<string>();

            pending.Push(root);

            while (pending.Count > 0)
            {
                string current =
                    pending.Pop();

                string[] files;

                try
                {
                    files =
                        Directory.GetFiles(
                            current,
                            "*.btw",
                            SearchOption.TopDirectoryOnly);
                }
                catch
                {
                    files = new string[0];
                }

                foreach (string file in files)
                    yield return file;

                string[] directories;

                try
                {
                    directories =
                        Directory.GetDirectories(
                            current,
                            "*",
                            SearchOption.TopDirectoryOnly);
                }
                catch
                {
                    directories = new string[0];
                }

                foreach (string directory in directories)
                    pending.Push(directory);
            }
        }

        private static string GetRelativeDisplayPath(
            string root,
            string file)
        {
            try
            {
                string rootWithSeparator =
                    root.TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar) +
                    Path.DirectorySeparatorChar;

                Uri rootUri =
                    new Uri(rootWithSeparator);

                Uri fileUri =
                    new Uri(file);

                return Uri.UnescapeDataString(
                    rootUri
                        .MakeRelativeUri(fileUri)
                        .ToString()
                        .Replace(
                            '/',
                            Path.DirectorySeparatorChar));
            }
            catch
            {
                return file;
            }
        }

        private async void BtnRunBlock1_Click(object sender, EventArgs e)
        {
            List<string> filesToProcess = new List<string>();
            if (selectedBtwFiles != null && selectedBtwFiles.Length > 0) filesToProcess.AddRange(selectedBtwFiles);
            else if (!string.IsNullOrWhiteSpace(selectedBtwFolder) && Directory.Exists(selectedBtwFolder))
            {
                SearchOption opt = chkSubDirs1.Checked ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                filesToProcess.AddRange(Directory.GetFiles(selectedBtwFolder, "*.btw", opt));
            }

            if (filesToProcess.Count == 0 || string.IsNullOrWhiteSpace(txtExport1.Text)) { MessageBox.Show("Please select .btw files and an export folder first."); return; }

            btnRun1.Text = "⏳ Extracting... Please Wait...";
            btnRun1.Enabled = false;

            try
            {
                string sourceRoot =
                    !string.IsNullOrWhiteSpace(selectedBtwFolder)
                        ? selectedBtwFolder
                        : null;

                int count = await BarTenderEngine.ExtractImages(
                    filesToProcess,
                    txtExport1.Text,
                    cmbFormat.SelectedItem.ToString(),
                    (int)numDpi.Value,
                    chkPreserveFolders1.Checked,
                    sourceRoot);

                Logger.LogAction(
                    "EXTRACT",
                    $"Extracted {count} files to {txtExport1.Text}" +
                    (chkPreserveFolders1.Checked ? " (folder structure preserved)" : ""));
                MessageBox.Show($"Step 1 Complete!\nExtracted {count} images.", "Success");
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error"); Logger.LogAction("ERROR", ex.Message); }
            finally
            {
                btnRun1.Text = "▶ Extract Images";
                btnRun1.Enabled = true;
            }
        }

        private async void BtnRunBlock2_Click(object sender, EventArgs e)
        {
            List<string> filesToProcess = new List<string>();
            if (selectedRawFiles != null && selectedRawFiles.Length > 0) filesToProcess.AddRange(selectedRawFiles);
            else if (!string.IsNullOrWhiteSpace(selectedRawFolder) && Directory.Exists(selectedRawFolder))
            {
                SearchOption opt = chkSubDirs2.Checked ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var allFiles = Directory.GetFiles(selectedRawFolder, "*.*", opt);
                filesToProcess.AddRange(allFiles.Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase)));
            }

            if (filesToProcess.Count == 0 || string.IsNullOrWhiteSpace(txtExport2.Text)) { MessageBox.Show("Please select images and an export folder first."); return; }

            Button clickedButton = (Button)sender;
            clickedButton.Text = "⏳ Formatting... Please Wait...";
            clickedButton.Enabled = false;

            rtbFormatConsole.Clear();
            rtbFormatConsole.AppendText($"Starting batch format of {filesToProcess.Count} images...\n");
            rtbFormatConsole.AppendText("--------------------------------------------------\n");

            var progressTracker = new Progress<string>(message =>
            {
                rtbFormatConsole.AppendText(message);
                rtbFormatConsole.ScrollToCaret();
            });

            string sourceRoot =
                !string.IsNullOrWhiteSpace(selectedRawFolder)
                    ? selectedRawFolder
                    : null;

            int count = await FormatEngine.FormatImages(
                filesToProcess,
                txtExport2.Text,
                chkPadding.Checked,
                (int)numThickness.Value,
                selectedBorderColor,
                progressTracker,
                chkPreserveFolders2.Checked,
                sourceRoot,
                cmbFormattingOutputFormat != null &&
                cmbFormattingOutputFormat.SelectedIndex == 1);

            rtbFormatConsole.AppendText("--------------------------------------------------\n");
            rtbFormatConsole.AppendText($"[SYSTEM] Done. Total formatted: {count}\n");

            Logger.LogAction(
                "FORMAT",
                $"Formatted {count} images to {txtExport2.Text}" +
                (chkPreserveFolders2.Checked ? " (folder structure preserved)" : ""));
            clickedButton.Text = "▶ Format Images";
            clickedButton.Enabled = true;
            MessageBox.Show($"Step 2 Complete!\nFormatted {count} images.", "Success");
        }

        private async void BtnGenerateCollage_Click(object sender, EventArgs e)
        {
            List<string> filesToProcess = new List<string>();

            if (selectedCollageFiles != null && selectedCollageFiles.Length > 0)
            {
                filesToProcess.AddRange(
                    selectedCollageFiles.Where(IsSupportedCollageImage));
            }
            else if (!string.IsNullOrWhiteSpace(selectedCollageFolder) &&
                     Directory.Exists(selectedCollageFolder))
            {
                SearchOption option =
                    chkCollageSubDirs.Checked
                        ? SearchOption.AllDirectories
                        : SearchOption.TopDirectoryOnly;

                try
                {
                    filesToProcess.AddRange(
                        Directory.GetFiles(
                            selectedCollageFolder,
                            "*.*",
                            option)
                        .Where(IsSupportedCollageImage));
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "Unable to scan the selected folder.\n\n" + ex.Message,
                        "Collage Input Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }
            }

            filesToProcess =
                filesToProcess
                    .Where(File.Exists)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

            if (filesToProcess.Count == 0)
            {
                MessageBox.Show(
                    "Please select image files or a folder containing supported images.",
                    "No Images",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            string outputFolder =
                txtCollageOutput == null
                    ? string.Empty
                    : txtCollageOutput.Text.Trim();

            if (string.IsNullOrWhiteSpace(outputFolder) ||
                outputFolder.StartsWith("Select", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(
                    "Please select an output folder.",
                    "Output Folder Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            int gridSize = cmbCollageGrid.SelectedIndex + 1;
            string outputType = cmbCollageOutputType.SelectedItem.ToString();
            bool landscape =
                string.Equals(
                    cmbCollageOrientation.SelectedItem.ToString(),
                    "Landscape",
                    StringComparison.OrdinalIgnoreCase);

            Directory.CreateDirectory(outputFolder);

            string extension =
                outputType == "PDF"
                    ? ".pdf"
                    : outputType == "Word"
                        ? ".docx"
                        : ".xlsx";

            string baseFileName;

            if (chkCollageAutoFileName.Checked)
            {
                baseFileName =
                    "ImageCollage_" +
                    DateTime.Now.ToString("yyyyMMdd_HHmmss");
            }
            else
            {
                baseFileName =
                    txtCollageFileName == null
                        ? string.Empty
                        : txtCollageFileName.Text.Trim();

                if (string.IsNullOrWhiteSpace(baseFileName))
                {
                    MessageBox.Show(
                        "Please enter an output file name, or enable Auto-generate filename.",
                        "File Name Required",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                baseFileName =
                    Path.GetFileNameWithoutExtension(baseFileName);

                if (baseFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    MessageBox.Show(
                        "The output file name contains invalid characters.",
                        "Invalid File Name",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }
            }

            string outputPath =
                Path.Combine(
                    outputFolder,
                    baseFileName + extension);

            btnGenerateCollage.Enabled = false;
            string oldButtonText = btnGenerateCollage.Text;
            btnGenerateCollage.Text = "⏳ Generating...";

            rtbFormatConsole.Clear();
            rtbFormatConsole.AppendText(
                "[COLLAGE] Preparing " + filesToProcess.Count + " image(s)...\n");
            rtbFormatConsole.AppendText(
                "[COLLAGE] Grid: " + gridSize + " x " + gridSize +
                " | Output: " + outputType +
                " | Orientation: " + (landscape ? "Landscape" : "Portrait") + "\n");
            rtbFormatConsole.AppendText(
                "--------------------------------------------------\n");

            var progress = new Progress<string>(message =>
            {
                rtbFormatConsole.AppendText(message + Environment.NewLine);
                rtbFormatConsole.ScrollToCaret();
            });

            try
            {
                CollageGenerationResult result =
                    await Task.Run(
                        () => ImageCollageEngine.Generate(
                            filesToProcess,
                            outputPath,
                            outputType,
                            gridSize,
                            landscape,
                            chkCollageShowFilename.Checked,
                            chkCollageBorders.Checked,
                            progress));

                rtbFormatConsole.AppendText(
                    "--------------------------------------------------\n");
                rtbFormatConsole.AppendText(
                    "[COLLAGE] Complete.\n");
                rtbFormatConsole.AppendText(
                    "[COLLAGE] Images: " + result.ImageCount +
                    " | Unique sizes: " + result.UniqueSizeCount + "\n");
                rtbFormatConsole.AppendText(
                    "[COLLAGE] Most common size: " +
                    result.MostCommonSize +
                    " (" + result.MostCommonCount + " image(s))\n");
                rtbFormatConsole.AppendText(
                    "[COLLAGE] Saved: " + result.OutputPath + "\n");

                Logger.LogAction(
                    "COLLAGE",
                    "Generated " + outputType +
                    " collage with " + result.ImageCount +
                    " image(s): " + result.OutputPath);

                MessageBox.Show(
                    "Collage created successfully!\n\n" +
                    "Images: " + result.ImageCount + "\n" +
                    "Unique sizes: " + result.UniqueSizeCount + "\n\n" +
                    result.OutputPath,
                    "Collage Complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                rtbFormatConsole.AppendText(
                    "[COLLAGE ERROR] " + ex.Message + "\n");

                Logger.LogAction(
                    "ERROR",
                    "Collage: " + ex.Message);

                MessageBox.Show(
                    "Unable to generate the collage.\n\n" + ex.Message,
                    "Collage Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                btnGenerateCollage.Text = oldButtonText;
                btnGenerateCollage.Enabled = true;
            }
        }

        private static bool IsSupportedCollageImage(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            string extension =
                Path.GetExtension(path).ToLowerInvariant();

            return extension == ".png" ||
                   extension == ".jpg" ||
                   extension == ".jpeg" ||
                   extension == ".bmp" ||
                   extension == ".tif" ||
                   extension == ".tiff";
        }

        // ==========================================
        // STEP 3: PREVIEW GENERATOR WORKFLOW
        // ==========================================
        private void BtnRunBlock3_Click(object sender, EventArgs e)
        {
            var lanes = new[] { GetFilesFromLane(pnlProducts), GetFilesFromLane(pnlCartons), GetFilesFromLane(pnlOthers) };
            if (lanes[0].Count == 0 && lanes[1].Count == 0 && lanes[2].Count == 0)
            {
                MessageBox.Show("Please add images to at least one lane first.", "No Images", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            this.Cursor = Cursors.WaitCursor;
            try
            {
                int colWidth = 1000, padding = 60, boxHeight = 120;
                int[] colX = { padding, (padding * 2) + colWidth, (padding * 3) + (colWidth * 2) };
                int[] colY = { padding, padding, padding };

                List<CanvasItem> startingItems = new List<CanvasItem>();

                for (int c = 0; c < 3; c++)
                {
                    foreach (string file in lanes[c])
                    {
                        if (!File.Exists(file)) continue;
                        Image heavyImg = Image.FromFile(file);

                        double scale = (c < 2) ? (double)colWidth / heavyImg.Width : Math.Min(1.0, (double)colWidth / heavyImg.Width);
                        int drawW = (c < 2) ? colWidth : (int)Math.Round(heavyImg.Width * scale);
                        int drawH = (int)Math.Round(heavyImg.Height * scale);

                        Bitmap lightImg = new Bitmap(heavyImg, new Size(drawW, drawH));
                        lightImg.SetResolution(heavyImg.HorizontalResolution, heavyImg.VerticalResolution);
                        heavyImg.Dispose();

                        int xPos = (c == 0) ? colX[0] : (c == 1) ? colX[1] : colX[2] + ((colWidth - drawW) / 2);
                        int yPos = colY[c];

                        string mem = customLayoutMemory.FirstOrDefault(m => m.StartsWith(file + "|"));
                        if (mem != null)
                        {
                            var parts = mem.Split('|');
                            if (parts.Length == 5) { xPos = int.Parse(parts[1]); yPos = int.Parse(parts[2]); drawW = int.Parse(parts[3]); drawH = int.Parse(parts[4]); }
                        }

                        string textTemplate = ""; bool drawBox = false;
                        if (c == 0 && chkShowText1.Checked) { textTemplate = txtLane1Text.Text; drawBox = true; }
                        else if (c == 1 && chkShowText2.Checked) { textTemplate = txtLane2Text.Text; drawBox = true; }

                        startingItems.Add(new CanvasItem
                        {
                            FilePath = file,
                            Img = lightImg,
                            X = xPos,
                            Y = yPos,
                            Width = drawW,
                            Height = drawH,
                            OriginalAspect = (double)drawW / drawH,
                            TextTemplate = textTemplate,
                            ShowText = drawBox,
                            ItemFont = (c == 0) ? fontLane1 : fontLane2
                        });

                        colY[c] = Math.Max(colY[c], yPos + drawH + (drawBox ? boxHeight + 20 : 0) + padding);
                    }
                }

                int totalW = (colWidth * 3) + (padding * 4);
                int totalH = Math.Max(1500, colY.Max() + padding);

                Bitmap previewBitmap = new Bitmap(totalW, totalH);
                previewBitmap.SetResolution(300, 300);

                using (Graphics g = Graphics.FromImage(previewBitmap))
                {
                    g.Clear(Color.White);
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

                    using (Pen thickPen = new Pen(Color.Black, 12))
                        g.DrawRectangle(thickPen, padding / 2, padding / 2, totalW - padding, totalH - padding);

                    using (Pen boxPen = new Pen(Color.Black, 6))
                    using (SolidBrush brush = new SolidBrush(Color.Black))
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        foreach (var item in startingItems)
                        {
                            g.DrawImage(item.Img, item.X, item.Y, item.Width, item.Height);
                            if (item.ShowText)
                            {
                                int dynBoxHeight = (int)(item.Width * 0.13);
                                int bY = item.Y + item.Height + 10;
                                g.DrawRectangle(boxPen, item.X, bY, item.Width, dynBoxHeight);

                                int wMM = (int)Math.Round((item.Width / item.Img.HorizontalResolution) * 25.4);
                                int hMM = (int)Math.Round((item.Height / item.Img.VerticalResolution) * 25.4);
                                string liveText = item.TextTemplate.Replace("{W}", wMM.ToString()).Replace("{H}", hMM.ToString());

                                if (!string.IsNullOrEmpty(liveText))
                                {
                                    float maxTextW = item.Width - 40;
                                    float maxTextH = dynBoxHeight - 20;

                                    using (Font testFont = new Font(item.ItemFont.FontFamily, 100f, item.ItemFont.Style))
                                    {
                                        SizeF testSize = g.MeasureString(liveText, testFont);
                                        float finalSize = Math.Max(1f, 100f * Math.Min(maxTextW / testSize.Width, maxTextH / testSize.Height));

                                        using (Font autoFont = new Font(item.ItemFont.FontFamily, finalSize, item.ItemFont.Style))
                                        {
                                            g.DrawString(liveText, autoFont, brush, new RectangleF(item.X, bY, item.Width, dynBoxHeight), sf);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                this.Cursor = Cursors.Default;

                // Capture the exact current three-lane workspace used to create this Job Card.
                WorkspaceData jobCardWorkspace =
                    BuildCurrentWorkspaceData();

                JobCardPreviewForm previewForm = new JobCardPreviewForm(
                    previewBitmap,
                    startingItems,
                    jobCardWorkspace,
                    currentSettings,
                    GetLastJobCardSaveFolder());

                previewForm.FormClosed += (s, ev) =>
                {
                    // If they customized it, save back the custom layout memory
                    // (Note: previewForm closes or hides when customize is clicked, but this catches closure)
                };

                previewForm.ShowDialog();
            }
            catch (Exception ex)
            {
                this.Cursor = Cursors.Default;
                MessageBox.Show("Error generating preview: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string GetLastJobCardSaveFolder()
        {
            if (!string.IsNullOrWhiteSpace(currentSettings.LastJobCardSaveFolder) &&
                Directory.Exists(currentSettings.LastJobCardSaveFolder))
            {
                return currentSettings.LastJobCardSaveFolder;
            }

            return "";
        }

        private string GetNpplStorageFolder()
        {
            // Use the exact physical path requested:
            // C:\Users\<user>\Documents\NPPLPrintMaster
            string userProfile =
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            string folder =
                Path.Combine(userProfile, "Documents", "NPPLPrintMaster");

            Directory.CreateDirectory(folder);
            return folder;
        }

        private WorkspaceData BuildCurrentWorkspaceData()
        {
            return new WorkspaceData
            {
                Lane1Files = GetFilesFromLane(pnlProducts),
                Lane2Files = GetFilesFromLane(pnlCartons),
                Lane3Files = GetFilesFromLane(pnlOthers),
                Text1 = txtLane1Text.Text,
                Text2 = txtLane2Text.Text,
                ShowText1 = chkShowText1.Checked,
                ShowText2 = chkShowText2.Checked,
                Font1 = fontLane1,
                Font2 = fontLane2,
                LayoutMemory = new List<string>(customLayoutMemory)
            };
        }



        private void SaveWorkspace()
        {
            using (SaveFileDialog sfd = new SaveFileDialog { Filter = "NPPL Project|*.nppl", FileName = "MyProject.nppl" })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    WorkspaceData data = BuildCurrentWorkspaceData();
                    WorkspaceEngine.SaveToFile(sfd.FileName, data);
                    Logger.LogAction("WORKSPACE_SAVE", $"Saved to {sfd.FileName}");
                    MessageBox.Show("Workspace and Custom Layout saved successfully!", "Saved");
                }
            }
        }

        private void LoadWorkspace()
        {
            using (OpenFileDialog ofd = new OpenFileDialog { Filter = "NPPL Project|*.nppl" })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    WorkspaceData data = WorkspaceEngine.LoadFromFile(ofd.FileName);
                    ClearLane(pnlProducts); ClearLane(pnlCartons); ClearLane(pnlOthers); customLayoutMemory.Clear();
                    foreach (string file in data.Lane1Files) AddThumbnail(pnlProducts, file);
                    foreach (string file in data.Lane2Files) AddThumbnail(pnlCartons, file);
                    foreach (string file in data.Lane3Files) AddThumbnail(pnlOthers, file);
                    txtLane1Text.Text = data.Text1; txtLane2Text.Text = data.Text2; chkShowText1.Checked = data.ShowText1; chkShowText2.Checked = data.ShowText2; fontLane1 = data.Font1; fontLane2 = data.Font2; customLayoutMemory = data.LayoutMemory;
                    Logger.LogAction("WORKSPACE_LOAD", $"Loaded from {ofd.FileName}");
                }
            }
        }
    }
}