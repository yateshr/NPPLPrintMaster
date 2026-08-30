using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Markdig;

namespace NPPLPrintMaster
{
    public partial class Form1 : Form
    {
        // Step 1 Controls
        private TextBox txtSource1, txtExport1;
        private NumericUpDown numDpi;
        private ComboBox cmbFormat;
        private CheckBox chkSubDirs1;
        private string[] selectedBtwFiles = null;
        private string selectedBtwFolder = "";

        // Step 2 Controls
        private TextBox txtSource2, txtExport2;
        private NumericUpDown numThickness;
        private CheckBox chkPadding, chkSubDirs2;
        private Button btnColor;
        private string[] selectedRawFiles = null;
        private string selectedRawFolder = "";
        private Color selectedBorderColor = Color.FromArgb(255, 60, 70, 90);

        // Step 3 Controls
        private FlowLayoutPanel pnlProducts, pnlCartons, pnlOthers;
        private TextBox txtLane1Text, txtLane2Text;
        private CheckBox chkShowText1, chkShowText2;
        private Font fontLane1 = new Font("Arial", 14, FontStyle.Bold);
        private Font fontLane2 = new Font("Arial", 14, FontStyle.Bold);

        // Layout Memory Engine
        private List<string> customLayoutMemory = new List<string>();

        // ==========================================
        // NEW UI ARCHITECTURE & THEME VARIABLES
        // ==========================================
        private Panel pnlContent, pnlSidebar, pnlHeader, pnlLogo;
        private Label lblHeaderTitle, lblAppTitle, lblAppSub;
        private Panel pageHome, pageExtract, pageFormat, pageCompose, pageSettings;
        private Button activeNavButton;
        private List<Button> navButtons = new List<Button>();
        private Splitter sidebarSplitter;
        private MenuStrip menuBar;

        // Settings Controls
        private ComboBox cmbTheme;
        private TextBox txtDefExport1, txtDefExport2;
        private NumericUpDown numDefDpi;

        // FIX: Made this global so the Theme Engine can find it
        private Button btnRun1;

        // Current Theme Colors
        private Color sidebarColor, sidebarHover, activeColor, bgLight, contentBg, textColor, controlBg;
        private string currentTheme = "BarTender Classic";

        public Form1()
        {
            BuildInterface();
            ApplyTheme("BarTender Classic"); // Load default theme on boot
        }

        private void Form1_Load(object sender, EventArgs e) { }

        // ==========================================
        // PHASE 1: RESPONSIVE SIDEBAR & RESTORED MENU
        // ==========================================
        private void BuildInterface()
        {
            this.Text = "NPPLPrintMaster - Enterprise Edition";
            this.Size = new Size(1200, 800);
            this.MinimumSize = new Size(900, 600);
            this.StartPosition = FormStartPosition.CenterScreen;

            // ----------------------------------------------------
            // 1. PINNED MENU BAR
            // ----------------------------------------------------
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
                item.Click += (s, e) => ShowDocumentViewer(doc.Key, doc.Value);
                helpMenu.DropDownItems.Add(item);
            }

            menuBar.Items.AddRange(new ToolStripItem[] { fileMenu, editMenu, toolsMenu, helpMenu });
            this.MainMenuStrip = menuBar;

            // ----------------------------------------------------
            // 2. THE DRAGGABLE LEFT SIDEBAR
            // ----------------------------------------------------
            pnlSidebar = new Panel { Dock = DockStyle.Left, Width = 240, MinimumSize = new Size(60, 0) };

            sidebarSplitter = new Splitter { Dock = DockStyle.Left, Width = 3, Cursor = Cursors.VSplit };

            pnlLogo = new Panel { Dock = DockStyle.Top, Height = 80 };
            lblAppTitle = new Label { Text = "NPPL", Font = new Font("Segoe UI Black", 20, FontStyle.Bold), Location = new Point(15, 15), AutoSize = true };
            lblAppSub = new Label { Text = "PRINT MASTER", Font = new Font("Segoe UI", 8, FontStyle.Regular), Location = new Point(18, 48), AutoSize = true };
            pnlLogo.Controls.AddRange(new Control[] { lblAppTitle, lblAppSub });
            pnlSidebar.Controls.Add(pnlLogo);

            // ----------------------------------------------------
            // 3. THE MAIN CONTENT FRAME & HEADER
            // ----------------------------------------------------
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

            // ----------------------------------------------------
            // 4. GENERATING THE DYNAMIC PAGES
            // ----------------------------------------------------
            pageHome = new Panel { Size = new Size(1000, 800), Dock = DockStyle.Fill, Visible = false };
            pageExtract = new Panel { Size = new Size(1000, 800), Dock = DockStyle.Fill, Visible = false };
            pageFormat = new Panel { Size = new Size(1000, 800), Dock = DockStyle.Fill, Visible = false };
            pageCompose = new Panel { Size = new Size(1000, 800), Dock = DockStyle.Fill, Visible = false, AutoScroll = true };
            pageSettings = new Panel { Size = new Size(1000, 800), Dock = DockStyle.Fill, Visible = false };

            pnlContent.Controls.AddRange(new Control[] { pageHome, pageExtract, pageFormat, pageCompose, pageSettings });

            // --- BUILD HOME PAGE ---
            Label lblWelcome = new Label { Text = "Workflow Automation Suite", Font = new Font("Segoe UI", 14), Location = new Point(0, 0), AutoSize = true };
            Label lblInstr = new Label { Text = "Use the sidebar on the left to navigate between extraction, formatting, and job card building.\n\nYou can drag the line next to the sidebar to shrink or expand it.", Font = new Font("Segoe UI", 11), Location = new Point(0, 40), AutoSize = true };

            Button btnSaveMem = new Button { Text = "💾 Save Current Workspace", Location = new Point(0, 120), Size = new Size(250, 45), Font = new Font("Segoe UI", 10, FontStyle.Bold), FlatStyle = FlatStyle.Flat };
            btnSaveMem.Click += (s, e) => SaveWorkspace();

            Button btnLoadMem = new Button { Text = "📂 Load Previous Workspace", Location = new Point(270, 120), Size = new Size(250, 45), Font = new Font("Segoe UI", 10, FontStyle.Bold), FlatStyle = FlatStyle.Flat };
            btnLoadMem.Click += (s, e) => LoadWorkspace();

            Button btnDocs = new Button { Text = "📖 View Documentation / Help", Location = new Point(0, 180), Size = new Size(520, 45), Font = new Font("Segoe UI", 10, FontStyle.Bold), FlatStyle = FlatStyle.Flat };
            btnDocs.Click += (s, e) => ShowDocumentViewer("FAQ & How to Use", "faq.md");

            pageHome.Controls.AddRange(new Control[] { lblWelcome, lblInstr, btnSaveMem, btnLoadMem, btnDocs });

            // --- BUILD EXTRACTION PAGE (STEP 1) ---
            GroupBox grpStep1 = new GroupBox { Text = "BarTender Auto-Extractor", Font = new Font("Segoe UI", 11, FontStyle.Bold), Location = new Point(0, 0), Size = new Size(850, 160) };

            txtSource1 = new TextBox { Location = new Point(20, 40), Size = new Size(245, 27), ReadOnly = true, Text = "Select .btw Files or Folder..." };
            Button btnSource1Files = new Button { Text = "Files", Location = new Point(270, 39), Size = new Size(60, 29), FlatStyle = FlatStyle.Flat };
            btnSource1Files.Click += (s, e) => { using (OpenFileDialog ofd = new OpenFileDialog { Multiselect = true, Filter = "BarTender Files (*.btw)|*.btw" }) { if (ofd.ShowDialog() == DialogResult.OK) { selectedBtwFiles = ofd.FileNames; selectedBtwFolder = ""; txtSource1.Text = $"{selectedBtwFiles.Length} .btw file(s) selected"; } } };
            Button btnSource1Folder = new Button { Text = "Folder", Location = new Point(335, 39), Size = new Size(70, 29), FlatStyle = FlatStyle.Flat };
            btnSource1Folder.Click += (s, e) => { string folder = SelectFolderModern(); if (!string.IsNullOrEmpty(folder)) { selectedBtwFolder = folder; selectedBtwFiles = null; txtSource1.Text = folder; } };
            chkSubDirs1 = new CheckBox { Text = "Include Sub-directories", Location = new Point(20, 75), AutoSize = true, Font = new Font("Segoe UI", 10) };

            txtExport1 = new TextBox { Location = new Point(430, 40), Size = new Size(250, 27), ReadOnly = true, Text = "Select Output Folder..." };
            Button btnExport1 = new Button { Text = "Browse", Location = new Point(690, 39), Size = new Size(70, 29), FlatStyle = FlatStyle.Flat };
            btnExport1.Click += (s, e) => { txtExport1.Text = SelectFolderModern(); };

            Label lblFormat = new Label { Text = "Format:", Location = new Point(20, 115), AutoSize = true, Font = new Font("Segoe UI", 10) };
            cmbFormat = new ComboBox { Location = new Point(85, 112), Size = new Size(80, 28), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbFormat.Items.AddRange(new object[] { "jpg", "png", "bmp" }); cmbFormat.SelectedIndex = 0;
            Label lblDpi = new Label { Text = "DPI:", Location = new Point(190, 115), AutoSize = true, Font = new Font("Segoe UI", 10) };
            numDpi = new NumericUpDown { Location = new Point(235, 112), Size = new Size(70, 27), Minimum = 96, Maximum = 2400, Value = 800 };

            btnRun1 = new Button { Text = "▶ Extract Images", Location = new Point(430, 90), Size = new Size(330, 50), Font = new Font("Segoe UI", 11, FontStyle.Bold), FlatStyle = FlatStyle.Flat };
            btnRun1.FlatAppearance.BorderSize = 0;
            btnRun1.Click += BtnRunBlock1_Click;

            grpStep1.Controls.AddRange(new Control[] { txtSource1, btnSource1Files, btnSource1Folder, chkSubDirs1, txtExport1, btnExport1, lblFormat, cmbFormat, lblDpi, numDpi, btnRun1 });
            pageExtract.Controls.Add(grpStep1);

            // --- BUILD FORMATTING PAGE (STEP 2) ---
            GroupBox grpStep2 = new GroupBox { Text = "Image Formatting Engine", Font = new Font("Segoe UI", 11, FontStyle.Bold), Location = new Point(0, 0), Size = new Size(850, 160) };

            txtSource2 = new TextBox { Location = new Point(20, 40), Size = new Size(245, 27), ReadOnly = true, Text = "Select Images or Folder..." };
            Button btnSource2Files = new Button { Text = "Files", Location = new Point(270, 39), Size = new Size(60, 29), FlatStyle = FlatStyle.Flat };
            btnSource2Files.Click += (s, e) => { using (OpenFileDialog ofd = new OpenFileDialog { Multiselect = true, Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp" }) { if (ofd.ShowDialog() == DialogResult.OK) { selectedRawFiles = ofd.FileNames; selectedRawFolder = ""; txtSource2.Text = $"{selectedRawFiles.Length} image(s) selected"; } } };
            Button btnSource2Folder = new Button { Text = "Folder", Location = new Point(335, 39), Size = new Size(70, 29), FlatStyle = FlatStyle.Flat };
            btnSource2Folder.Click += (s, e) => { string folder = SelectFolderModern(); if (!string.IsNullOrEmpty(folder)) { selectedRawFolder = folder; selectedRawFiles = null; txtSource2.Text = folder; } };
            chkSubDirs2 = new CheckBox { Text = "Include Sub-directories", Location = new Point(20, 75), AutoSize = true, Font = new Font("Segoe UI", 10) };

            txtExport2 = new TextBox { Location = new Point(430, 40), Size = new Size(250, 27), ReadOnly = true, Text = "Select Formatted Output..." };
            Button btnExport2 = new Button { Text = "Browse", Location = new Point(690, 39), Size = new Size(70, 29), FlatStyle = FlatStyle.Flat };
            btnExport2.Click += (s, e) => { txtExport2.Text = SelectFolderModern(); };

            Label lblThick = new Label { Text = "Border Width:", Location = new Point(20, 115), AutoSize = true, Font = new Font("Segoe UI", 10) };
            numThickness = new NumericUpDown { Location = new Point(125, 112), Size = new Size(50, 27), Minimum = 0, Maximum = 50, Value = 4 };
            btnColor = new Button { Text = "Pick Color", Location = new Point(190, 110), Size = new Size(90, 29), BackColor = selectedBorderColor, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnColor.Click += (s, e) => { using (ColorDialog cd = new ColorDialog()) { if (cd.ShowDialog() == DialogResult.OK) { btnColor.BackColor = cd.Color; selectedBorderColor = cd.Color; } } };
            chkPadding = new CheckBox { Text = "Add Padding", Location = new Point(300, 115), AutoSize = true, Font = new Font("Segoe UI", 10) };

            Button btnRun2 = new Button { Text = "▶ Format Images", Location = new Point(430, 90), Size = new Size(330, 50), BackColor = Color.MediumPurple, ForeColor = Color.White, Font = new Font("Segoe UI", 11, FontStyle.Bold), FlatStyle = FlatStyle.Flat };
            btnRun2.FlatAppearance.BorderSize = 0;
            btnRun2.Click += BtnRunBlock2_Click;

            grpStep2.Controls.AddRange(new Control[] { txtSource2, btnSource2Files, btnSource2Folder, chkSubDirs2, txtExport2, btnExport2, lblThick, numThickness, btnColor, chkPadding, btnRun2 });
            pageFormat.Controls.Add(grpStep2);

            // --- BUILD COMPOSITION PAGE (STEP 3) ---
            pnlProducts = CreateLane("Lane 1: Products", 0, pageCompose);
            chkShowText1 = new CheckBox { Text = "Show Text Box Below", Checked = true, Location = new Point(0, 330), AutoSize = true, Font = new Font("Segoe UI", 10) };
            txtLane1Text = new TextBox { Location = new Point(0, 360), Size = new Size(210, 27), Text = "PRODUCT BARCODE {W} X {H} MM" };
            Button btnFont1 = new Button { Text = "Aa Font...", Location = new Point(220, 359), Size = new Size(80, 29), FlatStyle = FlatStyle.Flat };
            btnFont1.Click += (s, e) => { using (FontDialog fd = new FontDialog { Font = fontLane1 }) { if (fd.ShowDialog() == DialogResult.OK) { fontLane1 = fd.Font; } } };

            pnlCartons = CreateLane("Lane 2: Cartons", 330, pageCompose);
            chkShowText2 = new CheckBox { Text = "Show Text Box Below", Checked = true, Location = new Point(330, 330), AutoSize = true, Font = new Font("Segoe UI", 10) };
            txtLane2Text = new TextBox { Location = new Point(330, 360), Size = new Size(210, 27), Text = "CARTON BARCODE {W} X {H} MM" };
            Button btnFont2 = new Button { Text = "Aa Font...", Location = new Point(550, 359), Size = new Size(80, 29), FlatStyle = FlatStyle.Flat };
            btnFont2.Click += (s, e) => { using (FontDialog fd = new FontDialog { Font = fontLane2 }) { if (fd.ShowDialog() == DialogResult.OK) { fontLane2 = fd.Font; } } };

            pnlOthers = CreateLane("Lane 3: Others", 660, pageCompose);

            Button btnRun3 = new Button { Text = "▶ Generate Basic Job Card", Font = new Font("Segoe UI", 12, FontStyle.Bold), Location = new Point(0, 420), Size = new Size(630, 50), BackColor = Color.SeaGreen, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnRun3.FlatAppearance.BorderSize = 0;
            btnRun3.Click += BtnRunBlock3_Click;

            Button btnClear = new Button { Text = "🗑️ Clear All Images", Font = new Font("Segoe UI", 10, FontStyle.Bold), Location = new Point(660, 420), Size = new Size(300, 50), BackColor = Color.White, ForeColor = Color.IndianRed, FlatStyle = FlatStyle.Flat };
            btnClear.FlatAppearance.BorderColor = Color.IndianRed;
            btnClear.Click += (s, e) => { ClearLane(pnlProducts); ClearLane(pnlCartons); ClearLane(pnlOthers); };

            pageCompose.Controls.AddRange(new Control[] { chkShowText1, txtLane1Text, btnFont1, chkShowText2, txtLane2Text, btnFont2, btnRun3, btnClear });

            // --- BUILD SETTINGS PAGE (NEW) ---
            GroupBox grpTheme = new GroupBox { Text = "Appearance", Font = new Font("Segoe UI", 11, FontStyle.Bold), Location = new Point(0, 0), Size = new Size(600, 100) };
            Label lblTheme = new Label { Text = "Global Theme:", Location = new Point(20, 40), AutoSize = true, Font = new Font("Segoe UI", 10) };
            cmbTheme = new ComboBox { Location = new Point(140, 37), Size = new Size(200, 28), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbTheme.Items.AddRange(new object[] { "BarTender Classic", "Midnight Dark", "Industrial", "Modern Windows" });
            cmbTheme.SelectedIndex = 0;
            cmbTheme.SelectedIndexChanged += (s, e) => ApplyTheme(cmbTheme.SelectedItem.ToString());
            grpTheme.Controls.AddRange(new Control[] { lblTheme, cmbTheme });

            GroupBox grpDefs = new GroupBox { Text = "Application Defaults", Font = new Font("Segoe UI", 11, FontStyle.Bold), Location = new Point(0, 120), Size = new Size(600, 200) };
            Label lblDefDpi = new Label { Text = "Default Extraction DPI:", Location = new Point(20, 40), AutoSize = true, Font = new Font("Segoe UI", 10) };
            numDefDpi = new NumericUpDown { Location = new Point(200, 37), Size = new Size(80, 27), Minimum = 96, Maximum = 2400, Value = 800 };

            Label lblDefEx1 = new Label { Text = "Default Extraction Output:", Location = new Point(20, 85), AutoSize = true, Font = new Font("Segoe UI", 10) };
            txtDefExport1 = new TextBox { Location = new Point(200, 82), Size = new Size(280, 27) };
            Button btnDefEx1 = new Button { Text = "Browse", Location = new Point(490, 81), Size = new Size(80, 29), FlatStyle = FlatStyle.Flat };
            btnDefEx1.Click += (s, e) => { txtDefExport1.Text = SelectFolderModern(); };

            Label lblDefEx2 = new Label { Text = "Default Formatting Output:", Location = new Point(20, 130), AutoSize = true, Font = new Font("Segoe UI", 10) };
            txtDefExport2 = new TextBox { Location = new Point(200, 127), Size = new Size(280, 27) };
            Button btnDefEx2 = new Button { Text = "Browse", Location = new Point(490, 126), Size = new Size(80, 29), FlatStyle = FlatStyle.Flat };
            btnDefEx2.Click += (s, e) => { txtDefExport2.Text = SelectFolderModern(); };

            grpDefs.Controls.AddRange(new Control[] { lblDefDpi, numDefDpi, lblDefEx1, txtDefExport1, btnDefEx1, lblDefEx2, txtDefExport2, btnDefEx2 });
            pageSettings.Controls.AddRange(new Control[] { grpTheme, grpDefs });

            // ----------------------------------------------------
            // 5. SIDEBAR NAVIGATION GENERATOR
            // ----------------------------------------------------
            int navY = 90;

            navButtons.Add(CreateNavButton("🏠 Dashboard Home", navY)); navY += 50;
            navButtons.Add(CreateNavButton("📂 1. Extraction", navY)); navY += 50;
            navButtons.Add(CreateNavButton("🎨 2. Formatting", navY)); navY += 50;
            navButtons.Add(CreateNavButton("🖨️ 3. Quick Job Card", navY)); navY += 70;

            Button btnNavFreeform = CreateNavButton("🚀 Pro Freeform Builder", navY);
            navButtons.Add(btnNavFreeform); navY += 70;

            Button btnNavSettings = CreateNavButton("⚙️ Settings", navY);
            navButtons.Add(btnNavSettings);

            pnlSidebar.Controls.AddRange(navButtons.ToArray());

            // ----------------------------------------------------
            // 6. NAVIGATION LOGIC
            // ----------------------------------------------------
            navButtons[0].Click += (s, e) => SwitchPage(pageHome, navButtons[0], "Dashboard Home");
            navButtons[1].Click += (s, e) => SwitchPage(pageExtract, navButtons[1], "Step 1: BarTender Extraction");
            navButtons[2].Click += (s, e) => SwitchPage(pageFormat, navButtons[2], "Step 2: Image Formatting");
            navButtons[3].Click += (s, e) => SwitchPage(pageCompose, navButtons[3], "Step 3: Quick Job Card");
            btnNavFreeform.Click += (s, e) => { FreeformBuilderForm builder = new FreeformBuilderForm(); builder.Show(); };
            btnNavSettings.Click += (s, e) => SwitchPage(pageSettings, btnNavSettings, "Application Settings");

            // Start on Home
            SwitchPage(pageHome, navButtons[0], "Dashboard Home");
        }

        // ==========================================
        // UI & THEME HELPER METHODS
        // ==========================================
        private Button CreateNavButton(string text, int yPos)
        {
            Button btn = new Button
            {
                Text = "  " + text,
                Location = new Point(0, yPos),
                Size = new Size(pnlSidebar.Width, 50),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
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

            if (activeNavButton != null)
            {
                activeNavButton.BackColor = sidebarColor;
                activeNavButton.ForeColor = (sidebarColor.R > 200) ? Color.Black : Color.LightGray;
            }

            activeNavButton = clickedButton;
            activeNavButton.BackColor = activeColor;
            activeNavButton.ForeColor = Color.White;

            targetPage.Visible = true;
            targetPage.BringToFront();
            lblHeaderTitle.Text = headerTitle;
        }

        // ==========================================
        // THEME ENGINE CORE
        // ==========================================
        private void ApplyTheme(string themeName)
        {
            currentTheme = themeName;

            if (themeName == "Midnight Dark")
            {
                sidebarColor = Color.FromArgb(25, 25, 28);
                sidebarHover = Color.FromArgb(45, 45, 50);
                bgLight = Color.FromArgb(30, 30, 30);
                contentBg = Color.FromArgb(45, 45, 48);
                textColor = Color.White;
                controlBg = Color.FromArgb(55, 55, 58);
                activeColor = Color.FromArgb(0, 122, 204);
            }
            else if (themeName == "Industrial")
            {
                sidebarColor = Color.Black;
                sidebarHover = Color.FromArgb(35, 35, 35);
                bgLight = Color.FromArgb(40, 40, 40);
                contentBg = Color.FromArgb(50, 50, 50);
                textColor = Color.White;
                controlBg = Color.FromArgb(70, 70, 70);
                activeColor = Color.DarkOrange;
            }
            else if (themeName == "Modern Windows")
            {
                sidebarColor = Color.FromArgb(240, 240, 240);
                sidebarHover = Color.FromArgb(220, 220, 220);
                bgLight = Color.FromArgb(250, 250, 250);
                contentBg = Color.White;
                textColor = Color.Black;
                controlBg = Color.White;
                activeColor = Color.FromArgb(0, 99, 177);
            }
            else // BarTender Classic (Default)
            {
                sidebarColor = Color.FromArgb(30, 30, 35);
                sidebarHover = Color.FromArgb(50, 50, 55);
                bgLight = Color.FromArgb(245, 246, 248);
                contentBg = Color.White;
                textColor = Color.Black;
                controlBg = Color.White;
                activeColor = Color.FromArgb(0, 122, 204);
            }

            // Apply to Main Shell
            this.BackColor = bgLight;
            pnlContent.BackColor = bgLight;
            pnlHeader.BackColor = contentBg;
            lblHeaderTitle.ForeColor = textColor;
            pnlSidebar.BackColor = sidebarColor;
            sidebarSplitter.BackColor = activeColor;

            pnlLogo.BackColor = (sidebarColor.R > 200) ? Color.FromArgb(220, 220, 220) : Color.FromArgb(20, 20, 24);
            lblAppTitle.ForeColor = (sidebarColor.R > 200) ? Color.Black : Color.White;
            lblAppSub.ForeColor = activeColor;

            menuBar.BackColor = contentBg;
            menuBar.ForeColor = textColor;
            foreach (ToolStripItem item in menuBar.Items)
            {
                item.ForeColor = textColor;
                if (item is ToolStripMenuItem dropDownItem)
                {
                    foreach (ToolStripItem subItem in dropDownItem.DropDownItems)
                    {
                        subItem.BackColor = contentBg;
                        subItem.ForeColor = textColor;
                    }
                }
            }

            // Update Sidebar Buttons
            foreach (Button btn in navButtons)
            {
                btn.BackColor = (btn == activeNavButton) ? activeColor : sidebarColor;
                btn.ForeColor = (btn == activeNavButton) ? Color.White : ((sidebarColor.R > 200) ? Color.Black : Color.LightGray);
                if (btn.Text.Contains("Pro Freeform")) btn.ForeColor = (sidebarColor.R > 200 && btn != activeNavButton) ? Color.DarkGoldenrod : Color.Gold;
            }

            // Execute recursive sweep over all pages
            ApplyColorsToControls(pageHome.Controls);
            ApplyColorsToControls(pageExtract.Controls);
            ApplyColorsToControls(pageFormat.Controls);
            ApplyColorsToControls(pageCompose.Controls);
            ApplyColorsToControls(pageSettings.Controls);

            // Re-apply special logic to run buttons to make them pop!
            if (btnRun1 != null) { btnRun1.BackColor = activeColor; btnRun1.ForeColor = Color.White; }
        }

        private void ApplyColorsToControls(Control.ControlCollection controls)
        {
            foreach (Control c in controls)
            {
                if (c is Label lbl) { lbl.ForeColor = textColor; lbl.BackColor = Color.Transparent; }
                else if (c is CheckBox chk) { chk.ForeColor = textColor; chk.BackColor = Color.Transparent; }
                else if (c is GroupBox grp) { grp.ForeColor = textColor; grp.BackColor = Color.Transparent; ApplyColorsToControls(grp.Controls); }
                else if (c is TextBox txt && !txt.ReadOnly) { txt.BackColor = controlBg; txt.ForeColor = textColor; txt.BorderStyle = BorderStyle.FixedSingle; }
                else if (c is TextBox txtR && txtR.ReadOnly) { txtR.BackColor = (currentTheme.Contains("Dark") || currentTheme == "Industrial") ? Color.FromArgb(controlBg.R - 10, controlBg.G - 10, controlBg.B - 10) : Color.WhiteSmoke; txtR.ForeColor = textColor; txtR.BorderStyle = BorderStyle.FixedSingle; }
                else if (c is ComboBox cmb) { cmb.BackColor = controlBg; cmb.ForeColor = textColor; }
                else if (c is NumericUpDown num) { num.BackColor = controlBg; num.ForeColor = textColor; }
                else if (c is FlowLayoutPanel flp) { flp.BackColor = controlBg; ApplyColorsToControls(flp.Controls); }
                else if (c is Panel pnl) { ApplyColorsToControls(pnl.Controls); }

                // Style standard buttons (skipping run buttons)
                if (c is Button btn && btn.Text != "▶ Extract Images" && btn.Text != "▶ Format Images" && btn.Text != "▶ Generate Basic Job Card" && btn.Text != "🗑️ Clear All Images" && btn.Text != "Pick Color")
                {
                    btn.BackColor = controlBg;
                    btn.ForeColor = textColor;
                    btn.FlatAppearance.BorderColor = (currentTheme.Contains("Dark") || currentTheme == "Industrial") ? Color.Gray : Color.LightGray;
                }
            }
        }
        // ==========================================
        // FEATURE: SAVE / LOAD WORKSPACE (MANUAL & SILENT)
        // ==========================================
        private void SaveWorkspace()
        {
            using (SaveFileDialog sfd = new SaveFileDialog { Filter = "NPPL Project|*.nppl", FileName = "MyProject.nppl" })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    SaveWorkspaceToFile(sfd.FileName);
                    MessageBox.Show("Workspace and Custom Layout saved successfully!", "Saved");
                }
            }
        }

        // Silent version for the Auto-Saver
        private void SaveWorkspaceToFile(string filePath)
        {
            using (StreamWriter sw = new StreamWriter(filePath))
            {
                sw.WriteLine("[LANE1]"); foreach (string f in GetFilesFromLane(pnlProducts)) sw.WriteLine(f);
                sw.WriteLine("[LANE2]"); foreach (string f in GetFilesFromLane(pnlCartons)) sw.WriteLine(f);
                sw.WriteLine("[LANE3]"); foreach (string f in GetFilesFromLane(pnlOthers)) sw.WriteLine(f);

                sw.WriteLine("[TEXT]");
                sw.WriteLine(txtLane1Text.Text); sw.WriteLine(txtLane2Text.Text);
                sw.WriteLine(chkShowText1.Checked.ToString()); sw.WriteLine(chkShowText2.Checked.ToString());
                sw.WriteLine($"{fontLane1.FontFamily.Name}|{fontLane1.Size}|{(int)fontLane1.Style}");
                sw.WriteLine($"{fontLane2.FontFamily.Name}|{fontLane2.Size}|{(int)fontLane2.Style}");

                sw.WriteLine("[LAYOUT]");
                foreach (string layoutData in customLayoutMemory) sw.WriteLine(layoutData);
            }
        }

        private void LoadWorkspace()
        {
            using (OpenFileDialog ofd = new OpenFileDialog { Filter = "NPPL Project|*.nppl" })
            {
                if (ofd.ShowDialog() == DialogResult.OK) LoadWorkspaceFromFile(ofd.FileName);
            }
        }

        // Silent version for the Auto-Loader
        private void LoadWorkspaceFromFile(string filePath)
        {
            ClearLane(pnlProducts); ClearLane(pnlCartons); ClearLane(pnlOthers);
            customLayoutMemory.Clear();
            string[] lines = File.ReadAllLines(filePath);
            int mode = 0;
            List<string> textData = new List<string>();

            foreach (string line in lines)
            {
                if (line == "[LANE1]") { mode = 1; continue; }
                if (line == "[LANE2]") { mode = 2; continue; }
                if (line == "[LANE3]") { mode = 3; continue; }
                if (line == "[TEXT]") { mode = 4; continue; }
                if (line == "[LAYOUT]") { mode = 5; continue; }

                if (mode == 1 && File.Exists(line)) AddThumbnail(pnlProducts, line);
                if (mode == 2 && File.Exists(line)) AddThumbnail(pnlCartons, line);
                if (mode == 3 && File.Exists(line)) AddThumbnail(pnlOthers, line);
                if (mode == 4) textData.Add(line);
                if (mode == 5) customLayoutMemory.Add(line);
            }

            if (textData.Count >= 6)
            {
                txtLane1Text.Text = textData[0]; txtLane2Text.Text = textData[1];
                chkShowText1.Checked = bool.Parse(textData[2]); chkShowText2.Checked = bool.Parse(textData[3]);
                var f1 = textData[4].Split('|'); fontLane1 = new Font(f1[0], float.Parse(f1[1]), (FontStyle)int.Parse(f1[2]));
                var f2 = textData[5].Split('|'); fontLane2 = new Font(f2[0], float.Parse(f2[1]), (FontStyle)int.Parse(f2[2]));
            }
        }

        // ==========================================
        // CUSTOM DOCUMENT VIEWER FOR .MD FILES
        // ==========================================
        private void ShowDocumentViewer(string title, string fileName)
        {
            string filePath = Path.Combine(Application.StartupPath, "Docs", fileName);
            string htmlContent = $"<html><body><h2>Oops! I couldn't find the file '{fileName}'.</h2><p>Please make sure this file exists inside the 'Docs' folder.</p></body></html>";

            if (File.Exists(filePath))
            {
                string mdText = File.ReadAllText(filePath);

                var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
                string convertedHtml = Markdown.ToHtml(mdText, pipeline);

                string css = @"
                <style>
                    body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; padding: 25px; color: #333; line-height: 1.6; background-color: #FFFFFF; }
                    h1, h2, h3 { color: #005A9E; border-bottom: 1px solid #EAECEF; padding-bottom: 5px; margin-top: 20px; }
                    p, li { font-size: 14px; }
                    strong { font-weight: 600; color: #000; }
                    code { background-color: #F6F8FA; padding: 3px 6px; border-radius: 4px; font-family: Consolas, monospace; font-size: 13px; }
                    pre { background-color: #F6F8FA; padding: 15px; border-radius: 6px; overflow: auto; }
                    table { border-collapse: collapse; width: 100%; margin-bottom: 20px; }
                    th, td { border: 1px solid #DDD; padding: 10px; text-align: left; }
                    th { background-color: #F2F2F2; font-weight: bold; }
                    blockquote { border-left: 4px solid #DFE2E5; margin: 0; padding: 0 15px; color: #6A737D; }
                </style>";

                htmlContent = $"<!DOCTYPE html><html><head><meta http-equiv='X-UA-Compatible' content='IE=edge'>{css}</head><body>{convertedHtml}</body></html>";
            }

            Form viewer = new Form { Text = title, Size = new Size(850, 700), StartPosition = FormStartPosition.CenterParent, ShowIcon = false };
            WebBrowser browser = new WebBrowser { Dock = DockStyle.Fill, DocumentText = htmlContent };
            viewer.Controls.Add(browser);
            viewer.ShowDialog();
        }

        private string SelectFolderModern()
        {
            using (OpenFileDialog ofd = new OpenFileDialog { ValidateNames = false, CheckFileExists = false, CheckPathExists = true, FileName = "Folder Selection" })
            {
                if (ofd.ShowDialog() == DialogResult.OK) return Path.GetDirectoryName(ofd.FileName);
            }
            return "";
        }

        // ==========================================
        // THUMBNAIL DRAG & DROP LOGIC
        // ==========================================
        private FlowLayoutPanel CreateLane(string title, int xPos, Panel parent)
        {
            Label lbl = new Label { Text = title, Location = new Point(xPos, 30), AutoSize = true };

            FlowLayoutPanel panel = new FlowLayoutPanel
            {
                Location = new Point(xPos, 55),
                Size = new Size(300, 240),
                AllowDrop = true,
                AutoScroll = true,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Cursor = Cursors.Hand
            };

            Button btnBrowse = new Button { Text = "Browse Files...", Location = new Point(xPos, 300), Size = new Size(300, 30), Font = new Font("Segoe UI", 9) };

            void OpenBrowse(object s, EventArgs e)
            {
                using (OpenFileDialog ofd = new OpenFileDialog { Multiselect = true, Filter = "Images|*.jpg;*.png;*.bmp;*.jpeg" })
                {
                    if (ofd.ShowDialog() == DialogResult.OK) { foreach (string f in ofd.FileNames) AddThumbnail(panel, f); }
                }
            }

            panel.Click += OpenBrowse;
            btnBrowse.Click += OpenBrowse;

            panel.DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
            panel.DragDrop += (s, e) =>
            {
                string[] items = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (string item in items)
                {
                    if (Directory.Exists(item))
                    {
                        foreach (string f in Directory.GetFiles(item)) { if (f.EndsWith(".jpg") || f.EndsWith(".png") || f.EndsWith(".bmp")) AddThumbnail(panel, f); }
                    }
                    else { AddThumbnail(panel, item); }
                }
            };

            parent.Controls.Add(lbl);
            parent.Controls.Add(panel);
            parent.Controls.Add(btnBrowse);
            return panel;
        }

        private void AddThumbnail(FlowLayoutPanel panel, string filePath)
        {
            try
            {
                using (Image img = Image.FromFile(filePath))
                {
                    int picW = panel.Width - 30;
                    int picH = (int)(img.Height * ((float)picW / img.Width));

                    PictureBox pb = new PictureBox
                    {
                        Image = new Bitmap(img, new Size(picW, picH)),
                        Size = new Size(picW, picH),
                        SizeMode = PictureBoxSizeMode.Zoom,
                        Tag = filePath,
                        Cursor = Cursors.Hand,
                        Margin = new Padding(5)
                    };

                    ContextMenuStrip cms = new ContextMenuStrip();
                    cms.Items.Add("❌ Remove Image", null, (s, e) => { pb.Image?.Dispose(); panel.Controls.Remove(pb); pb.Dispose(); });
                    pb.ContextMenuStrip = cms;

                    pb.MouseDown += (s, e) => { pb.ContextMenuStrip.Show(pb, e.Location); };
                    ToolTip tt = new ToolTip();
                    tt.SetToolTip(pb, "Click to remove.");
                    panel.Controls.Add(pb);
                }
            }
            catch { }
        }

        private void ClearLane(FlowLayoutPanel panel)
        {
            foreach (Control c in panel.Controls.Cast<Control>().ToList()) { if (c is PictureBox pb) { pb.Image?.Dispose(); pb.Dispose(); } }
            panel.Controls.Clear();
        }

        private List<string> GetFilesFromLane(FlowLayoutPanel panel)
        {
            List<string> files = new List<string>();
            foreach (Control c in panel.Controls) { if (c is PictureBox pb && pb.Tag != null) files.Add(pb.Tag.ToString()); }
            return files;
        }

        // ==========================================
        // STEP 1: BARTENDER LOGIC
        // ==========================================
        private void BtnRunBlock1_Click(object sender, EventArgs e)
        {
            List<string> filesToProcess = new List<string>();
            if (selectedBtwFiles != null && selectedBtwFiles.Length > 0) filesToProcess.AddRange(selectedBtwFiles);
            else if (!string.IsNullOrWhiteSpace(selectedBtwFolder) && Directory.Exists(selectedBtwFolder))
            {
                SearchOption opt = chkSubDirs1.Checked ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                filesToProcess.AddRange(Directory.GetFiles(selectedBtwFolder, "*.btw", opt));
            }

            if (filesToProcess.Count == 0 || string.IsNullOrWhiteSpace(txtExport1.Text)) { MessageBox.Show("Please select .btw files and an export folder first."); return; }

            this.Cursor = Cursors.WaitCursor;
            int count = 0;
            string format = cmbFormat.SelectedItem.ToString();

            try
            {
                dynamic btApp = Activator.CreateInstance(Type.GetTypeFromProgID("BarTender.Application"));
                btApp.Visible = true;

                foreach (string file in filesToProcess)
                {
                    try
                    {
                        dynamic btFormat = btApp.Formats.Open(file, false, "");
                        if (btFormat != null)
                        {
                            string outImg = Path.Combine(txtExport1.Text, Path.GetFileNameWithoutExtension(file) + "." + format);
                            btFormat.ExportToFile(outImg, format, 4, (int)numDpi.Value, 2);
                            btFormat.Close(2);
                            count++;
                        }
                    }
                    catch { continue; }
                }
                btApp.Quit(2);
                MessageBox.Show($"Step 1 Complete!\nExtracted {count} images.", "Success");
            }
            catch (Exception ex) { MessageBox.Show("BarTender Error: " + ex.Message); }
            finally { this.Cursor = Cursors.Default; }
        }

        // ==========================================
        // STEP 2: FORMATTER LOGIC
        // ==========================================
        private void BtnRunBlock2_Click(object sender, EventArgs e)
        {
            List<string> filesToProcess = new List<string>();
            if (selectedRawFiles != null && selectedRawFiles.Length > 0) filesToProcess.AddRange(selectedRawFiles);
            else if (!string.IsNullOrWhiteSpace(selectedRawFolder) && Directory.Exists(selectedRawFolder))
            {
                SearchOption opt = chkSubDirs2.Checked ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var allFiles = Directory.GetFiles(selectedRawFolder, "*.*", opt);
                filesToProcess.AddRange(allFiles.Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase)));
            }

            if (filesToProcess.Count == 0 || string.IsNullOrWhiteSpace(txtExport2.Text)) { MessageBox.Show("Please select images and an export folder first."); return; }

            this.Cursor = Cursors.WaitCursor;
            int count = 0;

            foreach (string file in filesToProcess)
            {
                try
                {
                    int pad = chkPadding.Checked ? 40 : 0;
                    int thick = (int)numThickness.Value;

                    using (Image img = Image.FromFile(file))
                    using (Bitmap bmp = new Bitmap(img.Width + (pad * 2), img.Height + (pad * 2)))
                    {
                        bmp.SetResolution(img.HorizontalResolution, img.VerticalResolution);
                        using (Graphics g = Graphics.FromImage(bmp))
                        {
                            g.SmoothingMode = SmoothingMode.AntiAlias;
                            g.Clear(Color.Transparent);

                            using (GraphicsPath path = new GraphicsPath())
                            {
                                int d = 90;
                                path.AddArc(pad, pad, d, d, 180, 90);
                                path.AddArc(pad + img.Width - d, pad, d, d, 270, 90);
                                path.AddArc(pad + img.Width - d, pad + img.Height - d, d, d, 0, 90);
                                path.AddArc(pad, pad + img.Height - d, d, d, 90, 90);
                                path.CloseFigure();

                                g.SetClip(path);
                                g.DrawImage(img, pad, pad, img.Width, img.Height);
                                g.ResetClip();

                                if (thick > 0) { using (Pen pen = new Pen(selectedBorderColor, thick)) { pen.Alignment = PenAlignment.Inset; g.DrawPath(pen, path); } }
                            }
                        }
                        bmp.Save(Path.Combine(txtExport2.Text, Path.GetFileNameWithoutExtension(file) + ".png"), ImageFormat.Png);
                    }
                    count++;
                }
                catch { continue; }
            }
            this.Cursor = Cursors.Default;
            MessageBox.Show($"Step 2 Complete!\nFormatted {count} images.", "Success");
        }

        // ==========================================
        // STEP 3: FINAL JOB CARD GENERATOR (WITH AUTO-CROP, UNDO SYSTEM, KEYBOARD CONTROLS,ZOOM & MAGNETIC SNAP)
        // ==========================================
        private void BtnRunBlock3_Click(object sender, EventArgs e)
        {
            var lanes = new[] { GetFilesFromLane(pnlProducts), GetFilesFromLane(pnlCartons), GetFilesFromLane(pnlOthers) };
            if (lanes[0].Count == 0 && lanes[1].Count == 0 && lanes[2].Count == 0) { MessageBox.Show("Add images first."); return; }

            this.Cursor = Cursors.WaitCursor;

            int colWidth = 1000, padding = 60, boxHeight = 120;
            int workspaceWidth = 5000, workspaceHeight = 5000;
            int[] colX = { padding, (padding * 2) + colWidth, (padding * 3) + (colWidth * 2) };
            int[] colY = { padding, padding, padding };

            List<CanvasItem> items = new List<CanvasItem>();

            for (int c = 0; c < 3; c++)
            {
                foreach (string file in lanes[c])
                {
                    Image img = Image.FromFile(file);
                    double scale = (c < 2) ? (double)colWidth / img.Width : Math.Min(1.0, (double)colWidth / img.Width);

                    int drawW = (c < 2) ? colWidth : (int)Math.Round(img.Width * scale);
                    int drawH = (int)Math.Round(img.Height * scale);
                    int xPos = (c == 0) ? colX[0] : (c == 1) ? colX[1] : colX[2] + ((colWidth - drawW) / 2);
                    int yPos = colY[c];

                    string mem = customLayoutMemory.FirstOrDefault(m => m.StartsWith(file + "|"));
                    if (mem != null)
                    {
                        var parts = mem.Split('|');
                        if (parts.Length == 5)
                        {
                            xPos = int.Parse(parts[1]); yPos = int.Parse(parts[2]);
                            drawW = int.Parse(parts[3]); drawH = int.Parse(parts[4]);
                        }
                    }

                    string textTemplate = ""; bool drawBox = false;
                    if (c == 0 && chkShowText1.Checked) { textTemplate = txtLane1Text.Text; drawBox = true; }
                    else if (c == 1 && chkShowText2.Checked) { textTemplate = txtLane2Text.Text; drawBox = true; }

                    items.Add(new CanvasItem
                    {
                        FilePath = file,
                        Img = img,
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
            this.Cursor = Cursors.Default;

            Form previewForm = new Form { Text = "Final Job Card Preview", Size = new Size(1200, 800), StartPosition = FormStartPosition.CenterParent };
            previewForm.KeyPreview = true;

            Panel scrollPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.DarkGray };
            PictureBox pbCanvas = new PictureBox { Location = new Point(0, 0), BackColor = Color.White };
            scrollPanel.Controls.Add(pbCanvas);

            // --- MAIN HEADER BAR (BarTender Light Blue Theme) ---
            Color barTenderBlue = Color.FromArgb(191, 219, 255);
            Panel topPanel = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = barTenderBlue };
            Button btnToggleEdit = new Button { Text = "✏️ Customize Layout", Location = new Point(20, 10), Size = new Size(180, 40), Font = new Font("Segoe UI", 10, FontStyle.Bold), BackColor = Color.White };
            float zoom = 0.28f;
            Button btnZoomOut = new Button { Text = "➖ Zoom", Location = new Point(210, 10), Size = new Size(90, 40), Font = new Font("Segoe UI", 10, FontStyle.Bold), BackColor = Color.White };
            Button btnZoomIn = new Button { Text = "➕ Zoom", Location = new Point(310, 10), Size = new Size(90, 40), Font = new Font("Segoe UI", 10, FontStyle.Bold), BackColor = Color.White };
            Button btnSaveFinal = new Button { Text = "💾 Save Final Auto-Cropped BMP", Location = new Point(420, 10), Size = new Size(260, 40), Font = new Font("Segoe UI", 10, FontStyle.Bold), BackColor = Color.SeaGreen, ForeColor = Color.White };
            topPanel.Controls.AddRange(new Control[] { btnToggleEdit, btnZoomOut, btnZoomIn, btnSaveFinal });

            // --- NEW: BARTENDER-STYLE TOOLSTRIP ---
            ToolStrip toolStrip = new ToolStrip { Dock = DockStyle.Top, BackColor = barTenderBlue, GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(10, 5, 0, 5), Visible = false };

            ToolStripComboBox cmbFont = new ToolStripComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
            cmbFont.Items.AddRange(new object[] { "Arial", "Calibri", "Tahoma", "Times New Roman", "Segoe UI", "Verdana", "Courier New" });

            ToolStripComboBox cmbSize = new ToolStripComboBox { Width = 50 };
            cmbSize.Items.AddRange(new object[] { "8", "10", "12", "14", "16", "18", "20", "24", "28", "32", "36", "48", "72" });

            ToolStripButton btnBold = new ToolStripButton { Text = "B", Font = new Font("Times New Roman", 10, FontStyle.Bold), DisplayStyle = ToolStripItemDisplayStyle.Text };
            ToolStripButton btnItalic = new ToolStripButton { Text = "I", Font = new Font("Times New Roman", 10, FontStyle.Italic), DisplayStyle = ToolStripItemDisplayStyle.Text };
            ToolStripSeparator sep1 = new ToolStripSeparator();
            ToolStripButton btnEditText = new ToolStripButton { Text = "📝 Edit Text", DisplayStyle = ToolStripItemDisplayStyle.Text };
            ToolStripButton btnToggleText = new ToolStripButton { Text = "👁️ Show/Hide Box", DisplayStyle = ToolStripItemDisplayStyle.Text };
            ToolStripSeparator sep2 = new ToolStripSeparator();
            ToolStripButton btnRotate = new ToolStripButton { Text = "🔄 Rotate 90°", DisplayStyle = ToolStripItemDisplayStyle.Text };

            toolStrip.Items.AddRange(new ToolStripItem[] { cmbFont, cmbSize, btnBold, btnItalic, sep1, btnEditText, btnToggleText, sep2, btnRotate });

            previewForm.Controls.Add(scrollPanel);
            previewForm.Controls.Add(toolStrip);
            previewForm.Controls.Add(topPanel);

            bool isEditMode = false;
            CanvasItem selectedItem = null;
            CanvasItem clipboardItem = null;
            bool isDragging = false, isResizing = false;
            Point dragOffset = Point.Empty;
            int? snapLineX = null, snapLineY = null;
            int snapDistance = 30;

            // ==========================================
            // NEW ZERO-MEMORY UNDO ENGINE (FIXES OOM CRASH)
            // ==========================================
            List<List<CanvasItem>> undoStack = new List<List<CanvasItem>>();
            Action saveUndoState = () =>
            {
                // By passing Img = i.Img by reference (no 'new Bitmap'), this takes exactly 0 bytes of RAM!
                var snapshot = items.Select(i => new CanvasItem { FilePath = i.FilePath, Img = i.Img, X = i.X, Y = i.Y, Width = i.Width, Height = i.Height, OriginalAspect = i.OriginalAspect, TextTemplate = i.TextTemplate, ShowText = i.ShowText, ItemFont = i.ItemFont }).ToList();
                undoStack.Add(snapshot);
                if (undoStack.Count > 30) undoStack.RemoveAt(0);
            };
            saveUndoState();

            // --- UI & TOOLBAR UPDATERS ---
            Action updateCanvasSize = () => { pbCanvas.Size = new Size((int)(workspaceWidth * zoom), (int)(workspaceHeight * zoom)); pbCanvas.Invalidate(); };
            updateCanvasSize();

            Action updateToolbar = () =>
            {
                bool hasSelection = (selectedItem != null);
                toolStrip.Enabled = hasSelection;
                if (hasSelection)
                {
                    string fontName = selectedItem.ItemFont.FontFamily.Name;
                    if (!cmbFont.Items.Contains(fontName)) cmbFont.Items.Add(fontName);
                    cmbFont.SelectedItem = fontName;
                    cmbSize.Text = selectedItem.ItemFont.Size.ToString();
                    btnBold.Checked = selectedItem.ItemFont.Bold;
                    btnItalic.Checked = selectedItem.ItemFont.Italic;
                }
            };

            // ZOOM CONTROLS
            btnZoomIn.Click += (s, ev) => { zoom = Math.Min(1.0f, zoom + 0.1f); updateCanvasSize(); };
            btnZoomOut.Click += (s, ev) => { zoom = Math.Max(0.1f, zoom - 0.1f); updateCanvasSize(); };
            previewForm.MouseWheel += (s, ev) =>
            {
                if (Control.ModifierKeys == Keys.Control)
                {
                    if (ev.Delta > 0) zoom = Math.Min(1.0f, zoom + 0.1f); else zoom = Math.Max(0.1f, zoom - 0.1f);
                    updateCanvasSize(); ((HandledMouseEventArgs)ev).Handled = true;
                }
            };

            btnToggleEdit.Click += (s, ev) =>
            {
                isEditMode = !isEditMode;
                btnToggleEdit.BackColor = isEditMode ? Color.Gold : Color.White;
                btnToggleEdit.Text = isEditMode ? "✅ Lock Layout" : "✏️ Customize Layout";
                toolStrip.Visible = isEditMode;
                selectedItem = null; updateToolbar(); pbCanvas.Invalidate();
            };

            // --- FONT APPLY LOGIC ---
            Action applyFont = () =>
            {
                if (selectedItem == null) return;

                float fSize = 14f;
                // SAFETY CATCH: If it fails to read the size, or if size is 0, default to 14
                if (!float.TryParse(cmbSize.Text, out fSize) || fSize <= 0)
                {
                    fSize = 14f;
                }

                FontStyle style = FontStyle.Regular;
                if (btnBold.Checked) style |= FontStyle.Bold;
                if (btnItalic.Checked) style |= FontStyle.Italic;

                string fontName = cmbFont.SelectedItem?.ToString();
                if (string.IsNullOrEmpty(fontName)) fontName = "Arial";

                selectedItem.ItemFont = new Font(fontName, fSize, style);
                selectedItem.ItemFont = new Font(fontName, fSize, style);
                pbCanvas.Invalidate();
            };

            cmbFont.SelectedIndexChanged += (s, ev) => { applyFont(); saveUndoState(); };
            cmbSize.SelectedIndexChanged += (s, ev) => { applyFont(); saveUndoState(); };
            cmbSize.Leave += (s, ev) => { applyFont(); saveUndoState(); }; // Triggers if they type a manual number
            btnBold.Click += (s, ev) => { btnBold.Checked = !btnBold.Checked; applyFont(); saveUndoState(); };
            btnItalic.Click += (s, ev) => { btnItalic.Checked = !btnItalic.Checked; applyFont(); saveUndoState(); };

            Func<string, string> PromptText = (currentText) =>
            {
                Form prompt = new Form { Width = 450, Height = 180, FormBorderStyle = FormBorderStyle.FixedDialog, Text = "Edit Text", StartPosition = FormStartPosition.CenterParent };
                Label lbl = new Label { Left = 20, Top = 20, Text = "Enter new text (Use {W} and {H} for auto-sizes):", AutoSize = true };
                TextBox tb = new TextBox { Left = 20, Top = 45, Width = 390, Text = currentText };
                Button btnOk = new Button { Text = "Save", Left = 310, Top = 90, Width = 100, DialogResult = DialogResult.OK };
                prompt.Controls.AddRange(new Control[] { lbl, tb, btnOk }); prompt.AcceptButton = btnOk;
                return prompt.ShowDialog() == DialogResult.OK ? tb.Text : currentText;
            };

            btnRotate.Click += (s, ev) =>
            {
                if (selectedItem != null)
                {
                    selectedItem.Img.RotateFlip(RotateFlipType.Rotate90FlipNone);
                    int temp = selectedItem.Width; selectedItem.Width = selectedItem.Height; selectedItem.Height = temp;
                    selectedItem.OriginalAspect = (double)selectedItem.Width / selectedItem.Height;
                    saveUndoState(); pbCanvas.Invalidate();
                }
            };
            btnEditText.Click += (s, ev) =>
            {
                if (selectedItem != null) { selectedItem.TextTemplate = PromptText(selectedItem.TextTemplate); saveUndoState(); pbCanvas.Invalidate(); }
            };
            btnToggleText.Click += (s, ev) =>
            {
                if (selectedItem != null) { selectedItem.ShowText = !selectedItem.ShowText; saveUndoState(); pbCanvas.Invalidate(); }
            };

            // --- ZERO-MEMORY UNDO KEYBOARD BINDS ---
            previewForm.KeyDown += (s, ev) =>
            {
                if (ev.Control && ev.KeyCode == Keys.Z)
                {
                    if (undoStack.Count > 1)
                    {
                        undoStack.RemoveAt(undoStack.Count - 1);
                        // Restores coordinates perfectly without making heavy copies of images
                        items = undoStack.Last().Select(i => new CanvasItem { FilePath = i.FilePath, Img = i.Img, X = i.X, Y = i.Y, Width = i.Width, Height = i.Height, OriginalAspect = i.OriginalAspect, TextTemplate = i.TextTemplate, ShowText = i.ShowText, ItemFont = i.ItemFont }).ToList();
                        selectedItem = null; updateToolbar(); pbCanvas.Invalidate();
                    }
                    return;
                }

                if (!isEditMode) return;

                if (ev.Control && ev.KeyCode == Keys.C && selectedItem != null)
                {
                    clipboardItem = selectedItem;
                }
                else if (ev.Control && ev.KeyCode == Keys.V && clipboardItem != null)
                {
                    var clone = new CanvasItem
                    {
                        FilePath = clipboardItem.FilePath,
                        Img = new Bitmap(clipboardItem.Img), // Clone physical image only once on paste
                        X = clipboardItem.X + 50,
                        Y = clipboardItem.Y + 50,
                        Width = clipboardItem.Width,
                        Height = clipboardItem.Height,
                        OriginalAspect = clipboardItem.OriginalAspect,
                        TextTemplate = clipboardItem.TextTemplate,
                        ShowText = clipboardItem.ShowText,
                        ItemFont = clipboardItem.ItemFont
                    };
                    items.Add(clone); selectedItem = clone; updateToolbar(); saveUndoState(); pbCanvas.Invalidate();
                }

                if (selectedItem == null) return;

                int step = ev.Shift ? 10 : 1;
                bool moved = false;

                if (ev.KeyCode == Keys.Delete) { items.Remove(selectedItem); selectedItem = null; updateToolbar(); saveUndoState(); pbCanvas.Invalidate(); }
                else if (ev.KeyCode == Keys.Up) { selectedItem.Y -= step; moved = true; ev.Handled = true; ev.SuppressKeyPress = true; }
                else if (ev.KeyCode == Keys.Down) { selectedItem.Y += step; moved = true; ev.Handled = true; ev.SuppressKeyPress = true; }
                else if (ev.KeyCode == Keys.Left) { selectedItem.X -= step; moved = true; ev.Handled = true; ev.SuppressKeyPress = true; }
                else if (ev.KeyCode == Keys.Right) { selectedItem.X += step; moved = true; ev.Handled = true; ev.SuppressKeyPress = true; }

                if (moved) pbCanvas.Invalidate();
            };

            previewForm.KeyUp += (s, ev) =>
            {
                if (isEditMode && selectedItem != null && (ev.KeyCode == Keys.Up || ev.KeyCode == Keys.Down || ev.KeyCode == Keys.Left || ev.KeyCode == Keys.Right)) saveUndoState();
            };

            pbCanvas.Paint += (s, ev) =>
            {
                Graphics g = ev.Graphics; g.ScaleTransform(zoom, zoom);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.SmoothingMode = SmoothingMode.HighQuality;

                int minX = items.Count > 0 ? items.Min(i => i.X) : 0, minY = items.Count > 0 ? items.Min(i => i.Y) : 0;
                int maxX = items.Count > 0 ? items.Max(i => i.X + i.Width) : colWidth, maxY = items.Count > 0 ? items.Max(i => i.Y + i.Height + (i.ShowText ? boxHeight + 10 : 0)) : 1000;
                int cropX = Math.Max(0, minX - padding), cropY = Math.Max(0, minY - padding), cropW = (maxX - minX) + (padding * 2), cropH = (maxY - minY) + (padding * 2);

                using (Pen thickPen = new Pen(Color.Black, 12)) g.DrawRectangle(thickPen, cropX + 10, cropY + 10, cropW - 20, cropH - 20);
                using (Pen boxPen = new Pen(Color.Black, 6)) using (SolidBrush brush = new SolidBrush(Color.Black)) using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    foreach (var item in items)
                    {
                        g.DrawImage(item.Img, item.X, item.Y, item.Width, item.Height);
                        if (item.ShowText)
                        {
                            int boxY = item.Y + item.Height + 10; g.DrawRectangle(boxPen, item.X, boxY, item.Width, boxHeight);
                            int wMM = (int)Math.Round((item.Width / item.Img.HorizontalResolution) * 25.4), hMM = (int)Math.Round((item.Height / item.Img.VerticalResolution) * 25.4);
                            string liveText = item.TextTemplate.Replace("{W}", wMM.ToString()).Replace("{H}", hMM.ToString());
                            float currentFontSize = item.ItemFont.Size * 10; Font font = new Font(item.ItemFont.FontFamily, currentFontSize, item.ItemFont.Style);
                            SizeF textSize = g.MeasureString(liveText, font);
                            while ((textSize.Width > item.Width - 40 || textSize.Height > boxHeight - 20) && currentFontSize > 8)
                            {
                                currentFontSize -= 2; font.Dispose(); font = new Font(item.ItemFont.FontFamily, currentFontSize, item.ItemFont.Style); textSize = g.MeasureString(liveText, font);
                            }
                            g.DrawString(liveText, font, brush, new RectangleF(item.X, boxY, item.Width, boxHeight), sf); font.Dispose();
                        }
                        if (isEditMode && selectedItem == item)
                        {
                            using (Pen selectPen = new Pen(Color.DodgerBlue, 4) { DashStyle = DashStyle.Dash }) g.DrawRectangle(selectPen, item.X, item.Y, item.Width, item.Height + (item.ShowText ? boxHeight + 10 : 0));
                            g.FillRectangle(Brushes.DodgerBlue, item.ResizeHandle);
                        }
                    }
                }
                if (isDragging)
                {
                    using (Pen snapPen = new Pen(Color.DeepSkyBlue, 5) { DashStyle = DashStyle.Dash })
                    {
                        if (snapLineX.HasValue) g.DrawLine(snapPen, snapLineX.Value, 0, snapLineX.Value, workspaceHeight);
                        if (snapLineY.HasValue) g.DrawLine(snapPen, 0, snapLineY.Value, workspaceWidth, snapLineY.Value);
                    }
                }
            };

            pbCanvas.MouseDown += (s, ev) =>
            {
                if (!isEditMode) return;
                int mx = (int)(ev.X / zoom), my = (int)(ev.Y / zoom);
                for (int i = items.Count - 1; i >= 0; i--)
                {
                    var item = items[i];
                    if (selectedItem == item && item.ResizeHandle.Contains(new Point(mx, my))) { isResizing = true; return; }
                    if (new Rectangle(item.X, item.Y, item.Width, item.Height + (item.ShowText ? boxHeight + 10 : 0)).Contains(new Point(mx, my)))
                    {
                        selectedItem = item; updateToolbar(); isDragging = true; dragOffset = new Point(mx - item.X, my - item.Y); pbCanvas.Invalidate(); return;
                    }
                }
                selectedItem = null; updateToolbar(); pbCanvas.Invalidate();
            };

            pbCanvas.MouseDoubleClick += (s, ev) =>
            {
                if (!isEditMode) return;
                int mx = (int)(ev.X / zoom), my = (int)(ev.Y / zoom);
                for (int i = items.Count - 1; i >= 0; i--)
                {
                    var item = items[i];
                    if (item.ShowText && new Rectangle(item.X, item.Y + item.Height + 10, item.Width, boxHeight).Contains(new Point(mx, my)))
                    {
                        item.TextTemplate = PromptText(item.TextTemplate);
                        saveUndoState(); pbCanvas.Invalidate(); return;
                    }
                }
            };

            pbCanvas.MouseMove += (s, ev) =>
            {
                if (!isEditMode || selectedItem == null) return;
                int mx = (int)(ev.X / zoom), my = (int)(ev.Y / zoom);
                if (isDragging)
                {
                    int targetX = mx - dragOffset.X, targetY = my - dragOffset.Y;
                    snapLineX = null; snapLineY = null;
                    foreach (var other in items)
                    {
                        if (other == selectedItem) continue;
                        if (Math.Abs(targetX - other.X) < snapDistance) { targetX = other.X; snapLineX = other.X; }
                        else if (Math.Abs((targetX + selectedItem.Width) - (other.X + other.Width)) < snapDistance) { targetX = (other.X + other.Width) - selectedItem.Width; snapLineX = other.X + other.Width; }
                        else if (Math.Abs((targetX + (selectedItem.Width / 2)) - (other.X + (other.Width / 2))) < snapDistance) { targetX = (other.X + (other.Width / 2)) - (selectedItem.Width / 2); snapLineX = other.X + (other.Width / 2); }
                        if (Math.Abs(targetY - other.Y) < snapDistance) { targetY = other.Y; snapLineY = other.Y; }
                        else if (Math.Abs((targetY + selectedItem.Height) - (other.Y + other.Height)) < snapDistance) { targetY = (other.Y + other.Height) - selectedItem.Height; snapLineY = other.Y + other.Height; }
                    }
                    selectedItem.X = targetX; selectedItem.Y = targetY; pbCanvas.Invalidate();
                }
                else if (isResizing)
                {
                    int newWidth = Math.Max(200, mx - selectedItem.X);
                    selectedItem.Width = newWidth; selectedItem.Height = (int)(newWidth / selectedItem.OriginalAspect); pbCanvas.Invalidate();
                }
            };

            pbCanvas.MouseUp += (s, ev) =>
            {
                if (isDragging || isResizing) saveUndoState();
                isDragging = false; isResizing = false; snapLineX = null; snapLineY = null; pbCanvas.Invalidate();
            };

            btnSaveFinal.Click += (s, ev) =>
            {
                isEditMode = false; selectedItem = null; snapLineX = null; snapLineY = null; updateToolbar();
                int minX = items.Count > 0 ? items.Min(i => i.X) : 0, minY = items.Count > 0 ? items.Min(i => i.Y) : 0;
                int maxX = items.Count > 0 ? items.Max(i => i.X + i.Width) : colWidth, maxY = items.Count > 0 ? items.Max(i => i.Y + i.Height + (i.ShowText ? boxHeight + 10 : 0)) : 1000;
                int cropX = Math.Max(0, minX - padding), cropY = Math.Max(0, minY - padding), finalW = Math.Max(100, (maxX - minX) + (padding * 2)), finalH = Math.Max(100, (maxY - minY) + (padding * 2));

                Bitmap finalBmp = new Bitmap(finalW, finalH); finalBmp.SetResolution(300, 300);
                using (Graphics g = Graphics.FromImage(finalBmp))
                {
                    g.Clear(Color.White); g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.SmoothingMode = SmoothingMode.HighQuality;
                    g.TranslateTransform(-cropX, -cropY);
                    using (Pen thickPen = new Pen(Color.Black, 12)) g.DrawRectangle(thickPen, cropX + 10, cropY + 10, finalW - 20, finalH - 20);
                    using (Pen boxPen = new Pen(Color.Black, 6)) using (SolidBrush brush = new SolidBrush(Color.Black)) using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        foreach (var item in items)
                        {
                            g.DrawImage(item.Img, item.X, item.Y, item.Width, item.Height);
                            if (item.ShowText)
                            {
                                int boxY = item.Y + item.Height + 10; g.DrawRectangle(boxPen, item.X, boxY, item.Width, boxHeight);
                                int wMM = (int)Math.Round((item.Width / item.Img.HorizontalResolution) * 25.4), hMM = (int)Math.Round((item.Height / item.Img.VerticalResolution) * 25.4);
                                string liveText = item.TextTemplate.Replace("{W}", wMM.ToString()).Replace("{H}", hMM.ToString());
                                float currentFontSize = item.ItemFont.Size * 10; Font font = new Font(item.ItemFont.FontFamily, currentFontSize, item.ItemFont.Style);
                                SizeF textSize = g.MeasureString(liveText, font);
                                while ((textSize.Width > item.Width - 40 || textSize.Height > boxHeight - 20) && currentFontSize > 8)
                                {
                                    currentFontSize -= 2; font.Dispose(); font = new Font(item.ItemFont.FontFamily, currentFontSize, item.ItemFont.Style); textSize = g.MeasureString(liveText, font);
                                }
                                g.DrawString(liveText, font, brush, new RectangleF(item.X, boxY, item.Width, boxHeight), sf); font.Dispose();
                            }
                        }
                    }
                }

                using (SaveFileDialog sfd = new SaveFileDialog { Filter = "Bitmap Image|*.bmp", FileName = "JobCard_Final.bmp" })
                {
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        string baseName = Path.GetFileNameWithoutExtension(sfd.FileName);
                        string docsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                        string autoSaveFolder = Path.Combine(docsFolder, "NPPLPrintMaster");
                        Directory.CreateDirectory(autoSaveFolder);
                        string autoSavePath = Path.Combine(autoSaveFolder, baseName + ".nppl");

                        if (File.Exists(autoSavePath))
                        {
                            var result = MessageBox.Show($"A template named '{baseName}' already exists in the system memory.\n\nDo you want to LOAD the previous layout template instead?\n(Clicking 'Yes' will discard your current screen and load the old layout).", "Previous Template Found", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                            if (result == DialogResult.Yes)
                            {
                                previewForm.Close(); LoadWorkspaceFromFile(autoSavePath);
                                MessageBox.Show("Template loaded! Click 'Preview & Generate Final Job Card' again to view it.", "Template Restored");
                                finalBmp.Dispose(); return;
                            }
                        }

                        finalBmp.Save(sfd.FileName, ImageFormat.Bmp);
                        customLayoutMemory.Clear();
                        foreach (var item in items) customLayoutMemory.Add($"{item.FilePath}|{item.X}|{item.Y}|{item.Width}|{item.Height}");
                        SaveWorkspaceToFile(autoSavePath);

                        MessageBox.Show("Saved successfully!\n(A background template was also auto-saved for future use).", "Saved");
                        previewForm.Close();
                    }
                }
                finalBmp.Dispose();
            };

            previewForm.FormClosed += (s, ev) =>
            {
                customLayoutMemory.Clear();
                foreach (var item in items)
                {
                    customLayoutMemory.Add($"{item.FilePath}|{item.X}|{item.Y}|{item.Width}|{item.Height}");
                    item.Img.Dispose();
                }
                undoStack.Clear();
                clipboardItem?.Img?.Dispose();
            };
            previewForm.ShowDialog();
        }

        // --- HELPER CLASS FOR FREEFORM DRAGGING ---
        public class CanvasItem
        {
            public string FilePath;
            public Image Img;
            public int X, Y, Width, Height;
            public double OriginalAspect;
            public string TextTemplate;
            public bool ShowText;
            public Font ItemFont;
            public int Rotation { get; set; } = 0;

            public Rectangle ResizeHandle
            {
                get
                {
                    // DYNAMIC RESIZE FIX: The box scales proportionally to the image width!
                    int dynamicBoxHeight = (int)(Width * 0.13);
                    return new Rectangle(X + Width - 15, Y + Height + (ShowText ? dynamicBoxHeight + 10 : 0) - 15, 30, 30);
                }
            }
        }
    } // <-- This safely closes Form1

    // ==========================================
    // STANDALONE FREEFORM BUILDER (ULTRA-SMOOTH ENGINE)
    // ==========================================
    public class FreeformBuilderForm : Form
    {
        private PictureBox pbCanvas;
        private Panel scrollPanel;
        private ToolStrip toolStrip;
        private ToolStripComboBox cmbFont, cmbTemplate;
        private ToolStripButton btnBold, btnItalic, btnRotate, btnToggleText, btnEditText;

        private List<Form1.CanvasItem> items = new List<Form1.CanvasItem>();
        private List<List<Form1.CanvasItem>> undoStack = new List<List<Form1.CanvasItem>>();

        private int workspaceWidth = 5000;
        private int workspaceHeight = 5000;
        private float zoom = 0.28f;

        private bool isDragging = false, isResizing = false;
        private Form1.CanvasItem selectedItem = null, clipboardItem = null;
        private Point dragOffset = Point.Empty;
        private int? snapLineX = null, snapLineY = null;
        private int snapDistance = 30;

        private Font lastUsedFont = new Font("Calibri", 14, FontStyle.Bold);

        public FreeformBuilderForm()
        {
            this.Text = "Freeform Job Card Builder (Standalone)";
            this.Size = new Size(1300, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.KeyPreview = true;
            this.AllowDrop = true;

            // PERFORMANCE: Force the main form to double-buffer
            this.DoubleBuffered = true;

            // 1. TOOLBAR
            Color barTenderBlue = Color.FromArgb(191, 219, 255);
            toolStrip = new ToolStrip { Dock = DockStyle.Top, BackColor = barTenderBlue, Padding = new Padding(5) };

            ToolStripButton btnSave = new ToolStripButton { Text = "💾 Save Final BMP", Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = Color.DarkGreen };
            ToolStripSeparator sep0 = new ToolStripSeparator();
            ToolStripButton btnAddImage = new ToolStripButton { Text = "➕ Add Images", Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            ToolStripSeparator sep00 = new ToolStripSeparator();

            cmbTemplate = new ToolStripComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
            cmbTemplate.Items.AddRange(new object[] { "📋 Quick Text...", "BARCODE SAMPLE 50 X 38 MM", "BARCODE SAMPLE 150 X 200 MM", "PRODUCT BARCODE {W} X {H} MM", "CARTON BARCODE {W} X {H} MM" });
            cmbTemplate.SelectedIndex = 0;

            cmbFont = new ToolStripComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
            cmbFont.Items.AddRange(new object[] { "Arial", "Calibri", "Tahoma", "Times New Roman", "Segoe UI", "Verdana", "Courier New" });

            btnBold = new ToolStripButton { Text = "B", Font = new Font("Times New Roman", 10, FontStyle.Bold) };
            btnItalic = new ToolStripButton { Text = "I", Font = new Font("Times New Roman", 10, FontStyle.Italic) };
            ToolStripSeparator sep1 = new ToolStripSeparator();

            btnEditText = new ToolStripButton { Text = "📝 Edit Text" };
            btnToggleText = new ToolStripButton { Text = "👁️ Show/Hide" };
            btnRotate = new ToolStripButton { Text = "🔄 Rotate 90°" };

            toolStrip.Items.AddRange(new ToolStripItem[] { btnSave, sep0, btnAddImage, sep00, cmbTemplate, cmbFont, btnBold, btnItalic, sep1, btnEditText, btnToggleText, btnRotate });
            this.Controls.Add(toolStrip);

            // 2. WORKSPACE 
            scrollPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.DarkGray };
            scrollPanel.AllowDrop = true;

            // PERFORMANCE: Hack to force DoubleBuffering on a standard C# Panel to stop screen tearing
            typeof(Panel).InvokeMember("DoubleBuffered", System.Reflection.BindingFlags.SetProperty | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, scrollPanel, new object[] { true });

            this.Controls.Add(scrollPanel);

            // 3. CANVAS 
            pbCanvas = new PictureBox { BackColor = Color.White, Location = new Point(0, 0), Size = new Size((int)(workspaceWidth * zoom), (int)(workspaceHeight * zoom)), Cursor = Cursors.Default };
            pbCanvas.AllowDrop = true;
            scrollPanel.Controls.Add(pbCanvas);

            pbCanvas.MouseEnter += (s, e) => { pbCanvas.Focus(); };
            SaveUndoState();

            // --- UNIVERSAL IMAGE ADDER ---
            Action<string[], int, int> AddImagesToCanvas = (files, startX, startY) => {
                foreach (string file in files)
                {
                    if (file.ToLower().EndsWith(".jpg") || file.ToLower().EndsWith(".png") || file.ToLower().EndsWith(".bmp") || file.ToLower().EndsWith(".jpeg"))
                    {
                        Image img = Image.FromFile(file);
                        int colWidth = 1000;
                        double scale = (double)colWidth / img.Width;
                        int drawW = colWidth;
                        int drawH = (int)Math.Round(img.Height * scale);
                        string baseName = Path.GetFileNameWithoutExtension(file).ToUpper();

                        items.Add(new Form1.CanvasItem
                        {
                            FilePath = file,
                            Img = img,
                            X = startX,
                            Y = startY,
                            Width = drawW,
                            Height = drawH,
                            OriginalAspect = (double)drawW / drawH,
                            TextTemplate = $"{baseName} {{W}} X {{H}} MM",
                            ShowText = true,
                            ItemFont = lastUsedFont
                        });
                        startY += drawH + 150;
                    }
                }
                SaveUndoState(); pbCanvas.Invalidate();
            };

            btnAddImage.Click += (s, e) => {
                using (OpenFileDialog ofd = new OpenFileDialog { Multiselect = true, Filter = "Images|*.jpg;*.png;*.bmp;*.jpeg" })
                {
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        int startY = 100;
                        if (items.Count > 0) startY = items.Max(i => i.Y + i.Height + (i.ShowText ? (int)(i.Width * 0.13) + 20 : 20));
                        AddImagesToCanvas(ofd.FileNames, 100, startY);
                    }
                }
            };

            DragEventHandler dragEnter = (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
            DragEventHandler dragDrop = (s, e) => {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                Point clientPoint = pbCanvas.PointToClient(new Point(e.X, e.Y));
                int dropX = (int)(clientPoint.X / zoom);
                int dropY = (int)(clientPoint.Y / zoom);
                if (dropX < 0) dropX = 100; if (dropY < 0) dropY = 100;
                AddImagesToCanvas(files, dropX, dropY);
            };

            pbCanvas.DragEnter += dragEnter; pbCanvas.DragDrop += dragDrop;
            scrollPanel.DragEnter += dragEnter; scrollPanel.DragDrop += dragDrop;
            this.DragEnter += dragEnter; this.DragDrop += dragDrop;

            // --- ULTRA-SMOOTH PAINT ENGINE ---
            pbCanvas.Paint += (s, ev) => {
                Graphics g = ev.Graphics;
                g.ScaleTransform(zoom, zoom);

                // PERFORMANCE: THE PHOTOSHOP TRICK
                // If dragging or resizing, drop graphics to super-fast low quality.
                // If stationary, render in gorgeous High Quality Bicubic.
                if (isDragging || isResizing)
                {
                    g.InterpolationMode = InterpolationMode.Low;
                    g.SmoothingMode = SmoothingMode.HighSpeed;
                    g.CompositingQuality = CompositingQuality.HighSpeed;
                }
                else
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.CompositingQuality = CompositingQuality.HighQuality;
                }

                if (items.Count > 0)
                {
                    int padding = 60;
                    int minX = items.Min(i => i.X), minY = items.Min(i => i.Y);
                    int maxX = items.Max(i => i.X + i.Width), maxY = items.Max(i => i.Y + i.Height + (i.ShowText ? (int)(i.Width * 0.13) + 10 : 0));
                    int cropX = Math.Max(0, minX - padding), cropY = Math.Max(0, minY - padding);
                    int cropW = (maxX - minX) + (padding * 2), cropH = (maxY - minY) + (padding * 2);

                    using (Pen thickPen = new Pen(Color.Black, 12))
                    {
                        g.DrawRectangle(thickPen, cropX + 10, cropY + 10, cropW - 20, cropH - 20);
                    }
                }

                using (Pen boxPen = new Pen(Color.Black, 6))
                using (SolidBrush brush = new SolidBrush(Color.Black))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    foreach (var item in items)
                    {
                        g.DrawImage(item.Img, item.X, item.Y, item.Width, item.Height);

                        int dynBoxHeight = (int)(item.Width * 0.13);

                        if (item.ShowText)
                        {
                            int bY = item.Y + item.Height + 10;
                            g.DrawRectangle(boxPen, item.X, bY, item.Width, dynBoxHeight);
                            int wMM = (int)Math.Round((item.Width / item.Img.HorizontalResolution) * 25.4);
                            int hMM = (int)Math.Round((item.Height / item.Img.VerticalResolution) * 25.4);
                            string liveText = item.TextTemplate.Replace("{W}", wMM.ToString()).Replace("{H}", hMM.ToString());

                            if (!string.IsNullOrEmpty(liveText))
                            {
                                float maxTextW = item.Width - 40;
                                float maxTextH = dynBoxHeight - 20;

                                Font testFont = new Font(item.ItemFont.FontFamily, 100f, item.ItemFont.Style);
                                SizeF testSize = g.MeasureString(liveText, testFont);
                                testFont.Dispose();

                                float ratioW = maxTextW / testSize.Width;
                                float ratioH = maxTextH / testSize.Height;
                                float finalSize = 100f * Math.Min(ratioW, ratioH);
                                if (finalSize < 1f) finalSize = 1f;

                                Font autoFont = new Font(item.ItemFont.FontFamily, finalSize, item.ItemFont.Style);
                                g.DrawString(liveText, autoFont, brush, new RectangleF(item.X, bY, item.Width, dynBoxHeight), sf);
                                autoFont.Dispose();
                            }
                        }

                        if (selectedItem == item)
                        {
                            using (Pen selPen = new Pen(Color.DodgerBlue, 4) { DashStyle = DashStyle.Dash })
                                g.DrawRectangle(selPen, item.X, item.Y, item.Width, item.Height + (item.ShowText ? dynBoxHeight + 10 : 0));
                            g.FillRectangle(Brushes.DodgerBlue, item.ResizeHandle);
                        }
                    }
                }

                if (isDragging && snapLineX.HasValue) g.DrawLine(new Pen(Color.DeepSkyBlue, 3), snapLineX.Value, 0, snapLineX.Value, workspaceHeight);
                if (isDragging && snapLineY.HasValue) g.DrawLine(new Pen(Color.DeepSkyBlue, 3), 0, snapLineY.Value, workspaceWidth, snapLineY.Value);
            };

            // --- FONT & TOOLBAR LOGIC ---
            bool isUpdatingUI = false;

            Action UpdateToolbar = () => {
                isUpdatingUI = true;
                toolStrip.Enabled = true;
                btnRotate.Enabled = selectedItem != null;
                btnToggleText.Enabled = selectedItem != null;
                btnEditText.Enabled = selectedItem != null;

                if (selectedItem != null)
                {
                    string fName = selectedItem.ItemFont.FontFamily.Name;
                    if (!cmbFont.Items.Contains(fName)) cmbFont.Items.Add(fName);

                    cmbFont.SelectedItem = fName;
                    btnBold.Checked = selectedItem.ItemFont.Bold;
                    btnItalic.Checked = selectedItem.ItemFont.Italic;

                    if (cmbTemplate.Items.Contains(selectedItem.TextTemplate)) cmbTemplate.SelectedItem = selectedItem.TextTemplate;
                    else cmbTemplate.SelectedIndex = 0;
                }
                isUpdatingUI = false;
            };

            Action ApplyFont = () => {
                if (isUpdatingUI || selectedItem == null) return;

                FontStyle style = FontStyle.Regular;
                if (btnBold.Checked) style |= FontStyle.Bold;
                if (btnItalic.Checked) style |= FontStyle.Italic;

                string fontName = cmbFont.SelectedItem?.ToString();
                if (string.IsNullOrEmpty(fontName)) fontName = "Calibri";

                selectedItem.ItemFont = new Font(fontName, 14f, style);
                lastUsedFont = selectedItem.ItemFont;
                pbCanvas.Invalidate();
            };

            cmbTemplate.SelectedIndexChanged += (s, e) => {
                if (isUpdatingUI || selectedItem == null || cmbTemplate.SelectedIndex == 0) return;
                selectedItem.TextTemplate = cmbTemplate.SelectedItem.ToString();
                SaveUndoState();
                pbCanvas.Invalidate();
            };

            Func<string, string> PromptText = (currentText) => {
                Form prompt = new Form { Width = 450, Height = 180, FormBorderStyle = FormBorderStyle.FixedDialog, Text = "Edit Text", StartPosition = FormStartPosition.CenterParent };
                Label lbl = new Label { Left = 20, Top = 20, Text = "Enter new text (Use {W} and {H} for auto-sizes):", AutoSize = true };
                TextBox tb = new TextBox { Left = 20, Top = 45, Width = 390, Text = currentText };
                Button btnOk = new Button { Text = "Save", Left = 310, Top = 90, Width = 100, DialogResult = DialogResult.OK };
                prompt.Controls.AddRange(new Control[] { lbl, tb, btnOk }); prompt.AcceptButton = btnOk;
                return prompt.ShowDialog() == DialogResult.OK ? tb.Text : currentText;
            };

            cmbFont.SelectedIndexChanged += (s, e) => { ApplyFont(); SaveUndoState(); };
            btnBold.Click += (s, e) => { btnBold.Checked = !btnBold.Checked; ApplyFont(); SaveUndoState(); };
            btnItalic.Click += (s, e) => { btnItalic.Checked = !btnItalic.Checked; ApplyFont(); SaveUndoState(); };

            btnRotate.Click += (s, e) => { if (selectedItem != null) { selectedItem.Img.RotateFlip(RotateFlipType.Rotate90FlipNone); int t = selectedItem.Width; selectedItem.Width = selectedItem.Height; selectedItem.Height = t; selectedItem.OriginalAspect = (double)selectedItem.Width / selectedItem.Height; SaveUndoState(); pbCanvas.Invalidate(); } };
            btnToggleText.Click += (s, e) => { if (selectedItem != null) { selectedItem.ShowText = !selectedItem.ShowText; SaveUndoState(); pbCanvas.Invalidate(); } };
            btnEditText.Click += (s, e) => { if (selectedItem != null) { selectedItem.TextTemplate = PromptText(selectedItem.TextTemplate); SaveUndoState(); pbCanvas.Invalidate(); } };

            // --- UNIVERSAL ZOOM LOGIC ---
            MouseEventHandler ZoomHandler = (s, ev) => {
                if (Control.ModifierKeys == Keys.Control)
                {
                    zoom = ev.Delta > 0 ? Math.Min(1.0f, zoom + 0.1f) : Math.Max(0.1f, zoom - 0.1f);
                    pbCanvas.Size = new Size((int)(workspaceWidth * zoom), (int)(workspaceHeight * zoom));
                    pbCanvas.Invalidate();
                    if (ev is HandledMouseEventArgs he) he.Handled = true;
                }
            };

            this.MouseWheel += ZoomHandler;
            pbCanvas.MouseWheel += ZoomHandler;
            scrollPanel.MouseWheel += ZoomHandler;

            // --- MOUSE CLICK & DRAG LOGIC ---
            pbCanvas.MouseDown += (s, ev) => {
                pbCanvas.Focus();
                int mx = (int)(ev.X / zoom), my = (int)(ev.Y / zoom);
                for (int i = items.Count - 1; i >= 0; i--)
                {
                    var item = items[i];
                    if (selectedItem == item && item.ResizeHandle.Contains(new Point(mx, my))) { isResizing = true; return; }

                    int dynBoxHeight = (int)(item.Width * 0.13);
                    if (new Rectangle(item.X, item.Y, item.Width, item.Height + (item.ShowText ? dynBoxHeight + 10 : 0)).Contains(new Point(mx, my)))
                    {
                        selectedItem = item; UpdateToolbar(); isDragging = true; dragOffset = new Point(mx - item.X, my - item.Y); pbCanvas.Invalidate(); return;
                    }
                }
                selectedItem = null; UpdateToolbar(); pbCanvas.Invalidate();
            };

            pbCanvas.MouseDoubleClick += (s, ev) => {
                int mx = (int)(ev.X / zoom), my = (int)(ev.Y / zoom);
                for (int i = items.Count - 1; i >= 0; i--)
                {
                    var item = items[i];
                    int dynBoxHeight = (int)(item.Width * 0.13);
                    if (item.ShowText && new Rectangle(item.X, item.Y + item.Height + 10, item.Width, dynBoxHeight).Contains(new Point(mx, my)))
                    {
                        item.TextTemplate = PromptText(item.TextTemplate);
                        SaveUndoState(); pbCanvas.Invalidate(); return;
                    }
                }
            };

            pbCanvas.MouseMove += (s, ev) => {
                if (selectedItem == null) return;
                int mx = (int)(ev.X / zoom), my = (int)(ev.Y / zoom);
                if (isDragging)
                {
                    int tX = mx - dragOffset.X, tY = my - dragOffset.Y;
                    snapLineX = null; snapLineY = null;
                    foreach (var other in items)
                    {
                        if (other == selectedItem) continue;
                        if (Math.Abs(tX - other.X) < snapDistance) { tX = other.X; snapLineX = other.X; }
                        else if (Math.Abs((tX + selectedItem.Width) - (other.X + other.Width)) < snapDistance) { tX = (other.X + other.Width) - selectedItem.Width; snapLineX = other.X + other.Width; }
                        if (Math.Abs(tY - other.Y) < snapDistance) { tY = other.Y; snapLineY = other.Y; }
                    }
                    selectedItem.X = tX; selectedItem.Y = tY; pbCanvas.Invalidate();
                }
                else if (isResizing)
                {
                    int nw = Math.Max(200, mx - selectedItem.X);
                    selectedItem.Width = nw; selectedItem.Height = (int)(nw / selectedItem.OriginalAspect); pbCanvas.Invalidate();
                }
            };

            pbCanvas.MouseUp += (s, ev) => {
                if (isDragging || isResizing) SaveUndoState();
                isDragging = false;
                isResizing = false;
                snapLineX = null;
                snapLineY = null;
                // PERFORMANCE: Triggers one final high-quality draw when mouse is released
                pbCanvas.Invalidate();
            };

            this.KeyDown += (s, ev) => {
                if (ev.Control && ev.KeyCode == Keys.Z && undoStack.Count > 1)
                {
                    undoStack.RemoveAt(undoStack.Count - 1);
                    items = undoStack.Last().Select(i => new Form1.CanvasItem { FilePath = i.FilePath, Img = i.Img, X = i.X, Y = i.Y, Width = i.Width, Height = i.Height, OriginalAspect = i.OriginalAspect, TextTemplate = i.TextTemplate, ShowText = i.ShowText, ItemFont = i.ItemFont }).ToList();
                    selectedItem = null; UpdateToolbar(); pbCanvas.Invalidate();
                }

                if (ev.Control && ev.KeyCode == Keys.C && selectedItem != null) { clipboardItem = selectedItem; }
                else if (ev.Control && ev.KeyCode == Keys.V && clipboardItem != null)
                {
                    var clone = new Form1.CanvasItem
                    {
                        FilePath = clipboardItem.FilePath,
                        Img = new Bitmap(clipboardItem.Img),
                        X = clipboardItem.X + 50,
                        Y = clipboardItem.Y + 50,
                        Width = clipboardItem.Width,
                        Height = clipboardItem.Height,
                        OriginalAspect = clipboardItem.OriginalAspect,
                        TextTemplate = clipboardItem.TextTemplate,
                        ShowText = clipboardItem.ShowText,
                        ItemFont = clipboardItem.ItemFont
                    };
                    items.Add(clone); selectedItem = clone; UpdateToolbar(); SaveUndoState(); pbCanvas.Invalidate();
                }

                if (selectedItem == null) return;
                int step = ev.Shift ? 10 : 1;
                bool moved = false;

                if (ev.KeyCode == Keys.Delete) { items.Remove(selectedItem); selectedItem = null; UpdateToolbar(); SaveUndoState(); pbCanvas.Invalidate(); }
                else if (ev.KeyCode == Keys.Up) { selectedItem.Y -= step; moved = true; ev.Handled = true; ev.SuppressKeyPress = true; }
                else if (ev.KeyCode == Keys.Down) { selectedItem.Y += step; moved = true; ev.Handled = true; ev.SuppressKeyPress = true; }
                else if (ev.KeyCode == Keys.Left) { selectedItem.X -= step; moved = true; ev.Handled = true; ev.SuppressKeyPress = true; }
                else if (ev.KeyCode == Keys.Right) { selectedItem.X += step; moved = true; ev.Handled = true; ev.SuppressKeyPress = true; }
                if (moved) pbCanvas.Invalidate();
            };
            this.KeyUp += (s, ev) => { if (selectedItem != null && (ev.KeyCode == Keys.Up || ev.KeyCode == Keys.Down || ev.KeyCode == Keys.Left || ev.KeyCode == Keys.Right)) SaveUndoState(); };

            // --- EXPORT (AUTO-CROP WITH AUTO-FIT MATH) ---
            btnSave.Click += (s, ev) => {
                if (items.Count == 0) return;
                selectedItem = null; pbCanvas.Invalidate();

                int padding = 60;
                int minX = items.Count > 0 ? items.Min(i => i.X) : 0, minY = items.Count > 0 ? items.Min(i => i.Y) : 0;
                int maxX = items.Count > 0 ? items.Max(i => i.X + i.Width) : 1000, maxY = items.Count > 0 ? items.Max(i => i.Y + i.Height + (i.ShowText ? (int)(i.Width * 0.13) + 10 : 0)) : 1000;
                int cropX = Math.Max(0, minX - padding), cropY = Math.Max(0, minY - padding);
                int finalW = Math.Max(100, (maxX - minX) + (padding * 2)), finalH = Math.Max(100, (maxY - minY) + (padding * 2));

                Bitmap finalBmp = new Bitmap(finalW, finalH); finalBmp.SetResolution(300, 300);
                using (Graphics g = Graphics.FromImage(finalBmp))
                {
                    g.Clear(Color.White);
                    g.TranslateTransform(-cropX, -cropY);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    using (Pen thickPen = new Pen(Color.Black, 12)) g.DrawRectangle(thickPen, cropX + 10, cropY + 10, finalW - 20, finalH - 20);

                    using (Pen boxPen = new Pen(Color.Black, 6))
                    using (SolidBrush brush = new SolidBrush(Color.Black))
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        foreach (var item in items)
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

                                    Font testFont = new Font(item.ItemFont.FontFamily, 100f, item.ItemFont.Style);
                                    SizeF testSize = g.MeasureString(liveText, testFont);
                                    testFont.Dispose();

                                    float ratioW = maxTextW / testSize.Width;
                                    float ratioH = maxTextH / testSize.Height;
                                    float finalSize = 100f * Math.Min(ratioW, ratioH);
                                    if (finalSize < 1f) finalSize = 1f;

                                    Font autoFont = new Font(item.ItemFont.FontFamily, finalSize, item.ItemFont.Style);
                                    g.DrawString(liveText, autoFont, brush, new RectangleF(item.X, bY, item.Width, dynBoxHeight), sf);
                                    autoFont.Dispose();
                                }
                            }
                        }
                    }
                }
                using (SaveFileDialog sfd = new SaveFileDialog { Filter = "BMP Image|*.bmp", FileName = "JobCard_Final.bmp" })
                {
                    if (sfd.ShowDialog() == DialogResult.OK) { finalBmp.Save(sfd.FileName, ImageFormat.Bmp); MessageBox.Show("Saved successfully!"); }
                }
                finalBmp.Dispose();
            };

            UpdateToolbar();
        }

        private void SaveUndoState()
        {
            if (items.Count == 0 && undoStack.Count == 0) return;
            undoStack.Add(items.Select(i => new Form1.CanvasItem { FilePath = i.FilePath, Img = i.Img, X = i.X, Y = i.Y, Width = i.Width, Height = i.Height, OriginalAspect = i.OriginalAspect, TextTemplate = i.TextTemplate, ShowText = i.ShowText, ItemFont = i.ItemFont }).ToList());

            if (undoStack.Count > 15)
            {
                undoStack.RemoveAt(0);
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            var allImages = items.Select(i => i.Img).Union(undoStack.SelectMany(s => s.Select(i => i.Img))).Where(img => img != null).Distinct().ToList();
            foreach (var img in allImages) { try { img.Dispose(); } catch { } }
            undoStack.Clear(); base.OnFormClosed(e);
        }
    }
}