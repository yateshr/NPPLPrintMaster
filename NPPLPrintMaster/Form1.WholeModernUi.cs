using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace NPPLPrintMaster
{
    public partial class Form1
    {
        private sealed class ModernWorkspaceState
        {
            internal Panel Page, Root;
            internal Control[] Original;
            internal ModernLayoutSnapshot Current, Modern;
            internal Action Layout;
            internal bool Applied;
        }
        private readonly List<ModernWorkspaceState> wholeModernWorkspaces = new List<ModernWorkspaceState>();
        private readonly HashSet<Control> modernPrimaryActions = new HashSet<Control>();
        private CollageUiButton modernHeaderAction;
        private Button modernFormatRun, modernJobRun, modernHeaderTarget;
        private Action modernHeaderCommand;
        private bool modernExtractionFindMode;
        private readonly HashSet<Button> modernHeaderObserved = new HashSet<Button>();
        private TableLayoutPanel studioLibraryLayout;
        private Tuple<SizeType, float>[] studioLibraryRows;
        private Button studioLibraryFoldersToggle;
        private bool studioLibraryFoldersExpanded;
        private bool studioLibraryLayingOut;
        private BorderStyle studioLibraryGridBorder;
        private bool studioLibraryVisualHeaders;
        private Panel studioLibraryRoot;
        private Size studioLibraryRootMinimum;
        private TableLayoutPanel studioLibrarySelected, studioLibraryActions;
        private Control[] studioLibraryActionButtons;
        private float studioLibraryActionHeight;

        private void ApplyModernLibraryPresentation(bool modern)
        {
            if (studioLibraryLayingOut) return;
            if (studioLibraryLayout == null && modern)
            {
                var root = pageBtwLibrary.Controls.OfType<Panel>().Single();
                studioLibraryRoot = root; studioLibraryRootMinimum = root.MinimumSize;
                studioLibraryLayout = root.Controls.OfType<TableLayoutPanel>().First();
                studioLibrarySelected = (TableLayoutPanel)picBtwLibraryPreview.Parent;
                studioLibraryActions = (TableLayoutPanel)studioLibrarySelected.GetControlFromPosition(0, 1);
                studioLibraryActionButtons = studioLibraryActions.Controls.Cast<Control>().OrderBy(c => studioLibraryActions.GetColumn(c)).ToArray();
                studioLibraryActionHeight = studioLibrarySelected.RowStyles[1].Height;
                studioLibraryGridBorder = dgvBtwLibrary.BorderStyle; studioLibraryVisualHeaders = dgvBtwLibrary.EnableHeadersVisualStyles;
                studioLibraryRows = studioLibraryLayout.RowStyles.Cast<RowStyle>().Select(r => Tuple.Create(r.SizeType, r.Height)).ToArray();
                var header = (Panel)studioLibraryLayout.GetControlFromPosition(0, 0);
                studioLibraryFoldersToggle = new StudioButton { Text = "Source folders", Dock = DockStyle.Right, Width = 150,
                    Font = new Font("Segoe UI", 9, FontStyle.Bold), BackColor = Color.White, ForeColor = Color.FromArgb(27, 39, 51), FlatStyle = FlatStyle.Flat };
                studioLibraryFoldersToggle.FlatAppearance.BorderColor = Color.FromArgb(218, 226, 234);
                header.Controls.Add(studioLibraryFoldersToggle); header.Controls.SetChildIndex(studioLibraryFoldersToggle, header.Controls.Count - 1);
                studioLibraryFoldersToggle.Click += (s, e) => { studioLibraryFoldersExpanded = !studioLibraryFoldersExpanded; ApplyModernLibraryPresentation(true); };
                btnBtwLibraryCancel.EnabledChanged += (s, e) => ApplyModernLibraryPresentation(IsModernInterface);
                studioLibraryLayout.SizeChanged += (s, e) => { if (IsModernInterface) ApplyModernLibraryPresentation(true); };
            }
            if (studioLibraryLayout == null) return;
            studioLibraryLayingOut = true;
            studioLibraryLayout.SuspendLayout();
            try
            {
                studioLibraryRoot.MinimumSize = modern ? new Size(0, 680) : studioLibraryRootMinimum;
                foreach (var panel in btwLibraryThemePanels.OfType<StudioSectionPanel>())
                {
                    panel.SetModern(modern);
                    if (modern)
                    {
                        panel.BackColor = Color.White;
                        SetLibrarySurface(panel);
                    }
                }
                studioLibraryFoldersToggle.Visible = modern;
                var progress = studioLibraryLayout.GetControlFromPosition(0, 2);
                var folders = studioLibraryLayout.GetControlFromPosition(0, 3);
                progress.Visible = !modern || btnBtwLibraryCancel.Enabled;
                folders.Visible = !modern || studioLibraryFoldersExpanded;
                for (int i = 0; i < studioLibraryRows.Length; i++)
                {
                    var row = studioLibraryLayout.RowStyles[i]; row.SizeType = studioLibraryRows[i].Item1; row.Height = studioLibraryRows[i].Item2;
                    if (modern && i == 1) row.Height = studioLibraryLayout.Width < 1120 ? 180 : 144;
                    if (modern && i == 2) row.Height = btnBtwLibraryCancel.Enabled ? 162 : 0;
                    if (modern && i == 3) row.Height = studioLibraryFoldersExpanded ? 142 : 0;
                }
                if (modern)
                {
                    dgvBtwLibrary.EnableHeadersVisualStyles = false; dgvBtwLibrary.BorderStyle = BorderStyle.None;
                    dgvBtwLibrary.BackgroundColor = Color.White; dgvBtwLibrary.GridColor = Color.FromArgb(231, 235, 240);
                    dgvBtwLibrary.DefaultCellStyle.SelectionBackColor = Color.FromArgb(255, 233, 213);
                    dgvBtwLibrary.DefaultCellStyle.SelectionForeColor = Color.FromArgb(27, 39, 51);
                }
                else { dgvBtwLibrary.BorderStyle = studioLibraryGridBorder; dgvBtwLibrary.EnableHeadersVisualStyles = studioLibraryVisualHeaders; }
                bool narrowActions = modern && studioLibraryActions.ClientSize.Width < 320;
                int actionColumns = narrowActions ? 2 : 5;
                studioLibraryActions.SuspendLayout();
                try
                {
                    studioLibraryActions.ColumnCount = actionColumns;
                    studioLibraryActions.RowCount = narrowActions ? 3 : 1;
                    studioLibraryActions.ColumnStyles.Clear(); studioLibraryActions.RowStyles.Clear();
                    for (int i = 0; i < actionColumns; i++) studioLibraryActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / actionColumns));
                    for (int i = 0; i < studioLibraryActions.RowCount; i++) studioLibraryActions.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / studioLibraryActions.RowCount));
                    for (int i = 0; i < studioLibraryActionButtons.Length; i++)
                        studioLibraryActions.SetCellPosition(studioLibraryActionButtons[i], new TableLayoutPanelCellPosition(i % actionColumns, i / actionColumns));
                    studioLibrarySelected.RowStyles[1].Height = narrowActions ? 96 : studioLibraryActionHeight;
                }
                finally { studioLibraryActions.ResumeLayout(true); }
            }
            finally { studioLibraryLayout.ResumeLayout(true); studioLibraryLayingOut = false; }
        }
        private static void SetLibrarySurface(Control root)
        {
            foreach (Control child in root.Controls)
            {
                if (child is Panel || child is Label) { child.BackColor = Color.White; SetLibrarySurface(child); }
            }
        }

        private void UpdateModernHeaderAction()
        {
            if (modernHeaderAction == null)
            {
                modernHeaderAction = new CollageUiButton { Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), Visible = false };
                modernHeaderAction.Click += (s, e) => { if (modernHeaderCommand != null) modernHeaderCommand(); };
                pnlHeader.Controls.Add(modernHeaderAction);
            }
            modernHeaderAction.Visible = IsModernInterface;
            if (!IsModernInterface) return;
            int index = navButtons.IndexOf(activeNavButton); modernHeaderTarget = null;
            modernHeaderAction.Visible = index != 0 && index != 7;
            if (index == 1) modernHeaderTarget = modernExtractionFindMode ? btnFindExtract : btnRun1;
            if (index == 2) modernHeaderTarget = modernFormatRun;
            if (index == 3) modernHeaderTarget = modernJobRun;
            if (index == 4) modernHeaderTarget = btnBtwLibraryUseImage;
            if (index == 5) modernHeaderTarget = btnGenerateCollage;
            var target = modernHeaderTarget;
            modernHeaderCommand = index == 0 ? (Action)(() => OpenCollagePage()) : index == 7 ?
                (Action)(() => SavePreferences()) : target == null ? null : (Action)(() => { if (target.Enabled) target.PerformClick(); });
            modernHeaderAction.Text = index == 0 ? "Create collage" : index == 7 ? "Save preferences" :
                index == 1 ? (modernExtractionFindMode ? "Find && extract" : "Extract images") : index == 2 ? "Format images" :
                index == 3 ? "Generate job card" : index == 4 ? "Use selected image" : index == 5 ? btnGenerateCollage.Text.TrimEnd('.', '…') : "Open workspace";
            modernHeaderAction.Enabled = modernHeaderCommand != null && (target == null || target.Enabled);
            modernHeaderAction.BackColor = ModernStyles.Accent; modernHeaderAction.ForeColor = Color.White;
            modernHeaderAction.MutedColor = Color.FromArgb(100, 116, 133);
            modernHeaderAction.FlatAppearance.BorderColor = ModernStyles.Accent;
            modernHeaderAction.FlatAppearance.MouseOverBackColor = Color.FromArgb(154, 52, 18);
            if (target != null && modernHeaderObserved.Add(target))
            {
                target.EnabledChanged += (s, e) => { if (target == modernHeaderTarget) modernHeaderAction.Enabled = target.Enabled; };
                target.TextChanged += (s, e) => { if (target == modernHeaderTarget && IsModernInterface) UpdateModernHeaderAction(); };
            }
            modernHeaderAction.SetBounds(Math.Max(300, pnlHeader.Width - (pnlHeader.Width >= 900 ? 400 : 250)), 22, pnlHeader.Width >= 900 ? 180 : 160, 38);
            modernWorkspaceStatus.Visible = false;
            if (pnlHeader.Width < 900) interfacePreviewBadge.Visible = false;
            modernHeaderAction.BringToFront();
        }

        private ModernWorkspaceState NewModernWorkspace(Panel page, string heading, string description)
        {
            var state = new ModernWorkspaceState { Page = page, Original = page.Controls.Cast<Control>().ToArray(),
                Current = new ModernLayoutSnapshot(page), Root = new Panel { Dock = DockStyle.Top, BackColor = InterfaceAppearance.ModernTheme().BgLight } };
            var title = ModernStyles.Label(heading, 21, true); var helper = ModernStyles.Label(description, 10, false, true);
            state.Root.Controls.Add(title); state.Root.Controls.Add(helper);
            state.Root.SizeChanged += (s, e) =>
            {
                title.SetBounds(2, 0, Math.Max(200, state.Root.Width - 4), 38);
                helper.SetBounds(3, 43, Math.Max(200, state.Root.Width - 6), 39);
                if (state.Applied && state.Layout != null) state.Layout();
            };
            wholeModernWorkspaces.Add(state); return state;
        }
        private void FinishModernWorkspace(ModernWorkspaceState state, Action layout)
        {
            state.Layout = layout; state.Modern = new ModernLayoutSnapshot(state.Root);
            foreach (Control c in state.Original) if (c.Parent == state.Page) c.Visible = false;
            state.Page.Controls.Add(state.Root); state.Root.BringToFront(); state.Applied = true;
            state.Root.Width = state.Page.ClientSize.Width; layout();
        }
        private void RestoreWholeModernUi()
        {
            foreach (var state in wholeModernWorkspaces.Where(s => s.Applied))
            {
                state.Applied = false; state.Root.Visible = false; state.Page.AutoScrollPosition = Point.Empty;
                state.Current.Restore(); foreach (Control c in state.Original) c.Visible = true;
                state.Page.AutoScrollMinSize = Size.Empty;
            }
        }
        private void ApplyWholeModernUi(bool modern)
        {
            ModernUiSession.SetEnabled(modern);
            ApplyModernLibraryPresentation(modern);
            if (!modern)
            {
                foreach (Panel page in wholeModernWorkspaces.Select(s => s.Page))
                {
                    page.AutoScrollPosition = Point.Empty;
                    page.AutoScroll = false;
                    foreach (Control root in page.Controls)
                        if (root.Dock == DockStyle.Fill) root.Bounds = page.ClientRectangle;
                    page.PerformLayout(); page.AutoScroll = true;
                }
                return;
            }
            if (wholeModernWorkspaces.Count == 0)
            { BuildModernExtraction(); BuildModernFormatting(); BuildModernJobCards(); }
            foreach (var state in wholeModernWorkspaces)
            {
                if (!state.Applied)
                {
                    state.Modern.Restore(); foreach (Control c in state.Original) if (c.Parent == state.Page) c.Visible = false;
                    state.Root.Visible = true; state.Root.BringToFront(); state.Applied = true;
                }
                ModernStyles.Apply(state.Root, modernPrimaryActions, btnColor);
                state.Layout();
            }
            UpdateModernHeaderAction();
            // The library already has a responsive table layout and its own
            // theme renderer. Keep its sizing rules, using the shared palette.
            if (rtbFormatConsole != null)
            {
                rtbFormatConsole.BackColor = Color.FromArgb(24, 33, 38); rtbFormatConsole.ForeColor = Color.FromArgb(218, 230, 237);
                rtbFormatConsole.Font = new Font("Consolas", 9.5f);
            }
        }
        private Button ButtonWith(Control root, string text)
        { return root.Controls.OfType<Button>().First(b => b.Text.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0); }
        private void SetPrimary(Button button) { modernPrimaryActions.Add(button); }
        private void StackCards(ModernWorkspaceState state, int start, params ModernFormCard[] cards)
        {
            int y = start; int width = Math.Max(480, state.Root.ClientSize.Width);
            foreach (var card in cards) { card.SetBounds(0, y, width - 2, card.Height); y += card.Height + 18; }
            state.Root.Height = y + 8;
        }

        private void BuildModernExtraction()
        {
            var state = NewModernWorkspace(pageExtract, "Extract label artwork", "Choose templates or find them by name. Review your output settings before starting.");
            var original = state.Original.OfType<GroupBox>().First();
            var finder = state.Original.OfType<GroupBox>().Last();
            var files = ButtonWith(original, "Files"); var folder = ButtonWith(original, "Folder"); var output = ButtonWith(original, "Browse");
            var selectTab = new CollageUiButton { Text = "Select templates" }; var findTab = new CollageUiButton { Text = "Find by name" };
            state.Root.Controls.Add(selectTab); state.Root.Controls.Add(findTab);
            var card = new ModernFormCard("Templates and output", "Render your selected BarTender files into consistently named images.");
            card.Field("Source templates", txtSource1, files, folder);
            card.Options(chkSubDirs1, chkPreserveFolders1);
            card.Field("Destination folder", txtExport1, output);
            card.Pair("Image format", cmbFormat, "Resolution / DPI", numDpi);
            card.Options(chkExtractAndFormat); card.Actions(btnRun1); SetPrimary(btnRun1);
            var search = new ModernFormCard("Find templates by filename", "Search your master directory using a typed or imported list.");
            var masterBrowse = finder.Controls.OfType<Button>().First(b => b.Top < 60);
            var outputBrowse = finder.Controls.OfType<Button>().First(b => b.Top > 300 && b.Top < 340);
            var include = finder.Controls.OfType<CheckBox>().First(c => c != chkFindPreserve);
            var load = ButtonWith(finder, "Load TXT"); var clear = ButtonWith(finder, "Clear List");
            search.Field("Master BTW directory", txtFindMasterFolder, masterBrowse); search.Options(include);
            var inputLabel = ModernStyles.Label("Template names / one per line", 9.5f, true);
            var resultLabel = ModernStyles.Label("Search results", 9.5f, true);
            search.Row(210, (y, w) =>
            {
                int half = (w - 20) / 2;
                inputLabel.SetBounds(24, y, half, 24); resultLabel.SetBounds(44 + half, y, half, 24);
                rtbFindNames.SetBounds(24, y + 30, half, 162); rtbFindResults.SetBounds(44 + half, y + 30, half, 162);
            }, inputLabel, resultLabel, rtbFindNames, rtbFindResults);
            search.Actions(load, clear, btnFindSearch);
            search.Field("Destination folder", txtFindOutput, outputBrowse);
            search.Pair("Image format", cmbFindFormat, "Resolution / DPI", numFindDpi);
            search.Options(chkFindPreserve); search.Actions(btnFindExtract); SetPrimary(btnFindExtract);
            var hint = ModernStyles.Label("Exact filenames are matched without case sensitivity; .btw is optional. Duplicate names return every match.", 9, false, true);
            search.Row(48, (y, w) => hint.SetBounds(24, y, w, 43), hint);
            state.Root.Controls.Add(card); state.Root.Controls.Add(search);
            bool finding = false;
            Action layout = () =>
            {
                selectTab.SetBounds(0, 88, 170, 40); findTab.SetBounds(182, 88, 170, 40);
                card.Visible = !finding; search.Visible = finding;
                StackCards(state, 148, finding ? search : card);
                selectTab.BackColor = finding ? Color.White : ModernStyles.Accent; selectTab.ForeColor = finding ? Color.FromArgb(27, 39, 51) : Color.White;
                findTab.BackColor = finding ? ModernStyles.Accent : Color.White; findTab.ForeColor = finding ? Color.White : Color.FromArgb(27, 39, 51);
            };
            selectTab.Click += (s, e) => { finding = modernExtractionFindMode = false; layout(); UpdateModernHeaderAction(); };
            findTab.Click += (s, e) => { finding = modernExtractionFindMode = true; layout(); UpdateModernHeaderAction(); };
            FinishModernWorkspace(state, layout);
        }

        private void BuildModernFormatting()
        {
            var state = NewModernWorkspace(pageFormat, "Prepare images for production", "Choose your images, set the border and padding, and save to a separate output folder.");
            var source = state.Original.OfType<GroupBox>().First();
            var files = ButtonWith(source, "Files"); var folder = ButtonWith(source, "Folder"); var output = ButtonWith(source, "Browse");
            var run = ButtonWith(source, "Format Images");
            modernFormatRun = run;
            var card = new ModernFormCard("Source and appearance", "Keep the originals available while creating your formatted output.");
            card.Field("Source images", txtSource2, files, folder); card.Options(chkSubDirs2, chkPreserveFolders2);
            card.Pair("Border width / pixels", numThickness, "Border colour", btnColor); card.Options(chkPadding);
            card.Field("Destination folder", txtExport2, output); card.Field("Output format", cmbFormattingOutputFormat);
            card.Actions(run); SetPrimary(run);
            var activity = new ModernFormCard("Activity", "Progress and processing messages appear here while the task runs.");
            activity.Row(230, (y, w) => rtbFormatConsole.SetBounds(24, y, w, 212), rtbFormatConsole);
            state.Root.Controls.Add(card); state.Root.Controls.Add(activity);
            FinishModernWorkspace(state, () => StackCards(state, 88, card, activity));
        }

        private void BuildModernJobCards()
        {
            var state = NewModernWorkspace(pageCompose, "Build a production job card", "Collect product, carton and supporting artwork, then review the result in the editor.");
            var lanes = state.Original.OfType<Panel>().Where(p => p.Width == 310).OrderBy(p => p.Left).ToArray();
            var captions = new[] { txtLane1Text, txtLane2Text }; var checks = new[] { chkShowText1, chkShowText2 };
            var fontButtons = state.Original.OfType<Button>().Where(b => b.Text.Contains("Font")).OrderBy(b => b.Left).ToArray();
            var laneCards = new List<ModernFormCard>();
            for (int i = 0; i < lanes.Length; i++)
            {
                int index = i; var lane = lanes[i]; var flow = lane.Controls.OfType<FlowLayoutPanel>().Single(); var browse = lane.Controls.OfType<Button>().Single();
                flow.Paint += (s, e) =>
                {
                    if (IsModernInterface && flow.Controls.Count == 0)
                        TextRenderer.DrawText(e.Graphics, "No artwork yet\nChoose images to fill this lane", ModernStyles.Font(9.5f),
                            new Rectangle(12, 36, Math.Max(80, flow.Width - 24), Math.Max(70, flow.Height - 72)), Color.FromArgb(100, 116, 133),
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
                };
                var card = new ModernFormCard(new[] { "Product artwork", "Carton artwork", "Supporting images" }[i],
                    new[] { "Add the product's approved label images.", "Keep carton references together.", "Optional artwork for this job." }[i]);
                card.Row(194, (y, w) => { flow.SetBounds(24, y, w, 180); flow.AutoScroll = true; }, flow);
                card.Actions(browse);
                if (i < 2) { card.Options(checks[i]); card.Field("Caption", captions[i], fontButtons[i]); }
                else card.Row(112, (y, w) => { });
                laneCards.Add(card); state.Root.Controls.Add(card);
            }
            var generate = state.Original.OfType<Button>().First(b => b.Text.Contains("Generate Job Card"));
            modernJobRun = generate;
            var clear = state.Original.OfType<Button>().First(b => b.Text.Contains("Clear All"));
            var actions = new ModernFormCard("Review and create", "Open the editor to fine-tune the layout before saving the finished job.");
            actions.Options(chkOpenDirectFreeform); actions.Actions(generate, clear); SetPrimary(generate); state.Root.Controls.Add(actions);
            var smart = state.Original.OfType<GroupBox>().Single();
            var smartCards = BuildModernSmartFinder(smart); foreach (var c in smartCards) state.Root.Controls.Add(c);
            var finderToggle = new CollageUiButton { Text = "Find images by barcode  +" }; state.Root.Controls.Add(finderToggle);
            bool finderVisible = false;
            finderToggle.Click += (s, e) => { finderVisible = !finderVisible; finderToggle.Text = finderVisible ? "Hide barcode finder  −" : "Find images by barcode  +"; state.Layout(); };
            FinishModernWorkspace(state, () =>
            {
                int width = Math.Max(480, state.Root.Width - 2); int columns = width >= 900 ? 3 : 1;
                int cardWidth = (width - (columns - 1) * 18) / columns; int y = 88;
                int rowHeight = laneCards.Max(c => c.Height);
                for (int i = 0; i < laneCards.Count; i++)
                    laneCards[i].SetBounds((i % columns) * (cardWidth + 18), y + (i / columns) * (rowHeight + 18), cardWidth, laneCards[i].Height);
                y += ((laneCards.Count + columns - 1) / columns) * (rowHeight + 18);
                actions.SetBounds(0, y, width, actions.Height); y += actions.Height + 18;
                finderToggle.SetBounds(0, y, Math.Min(290, width), 40); y += 58;
                foreach (var card in smartCards) { card.Visible = finderVisible; if (finderVisible) { card.SetBounds(0, y, width, card.Height); y += card.Height + 18; } }
                state.Root.Height = y + 8;
            });
        }

        private List<ModernFormCard> BuildModernSmartFinder(GroupBox original)
        {
            var settings = new ModernFormCard("Image finder / source folders", "Scan or type a barcode to find artwork already held in your image folders.");
            var folders = original.Controls.OfType<Button>().Where(b => b.Text == "Folder").OrderBy(b => b.Top).ToArray();
            var failures = original.Controls.OfType<Button>().Where(b => b.Text.Contains("Failures")).ToArray();
            settings.Field("Product image folder", txtSmartProductFolder, folders[0]); settings.Field("Carton image folder", txtSmartCartonFolder, folders[1]);
            settings.Options(chkSmartMonitor); settings.Actions(btnSmartRefresh);
            settings.Row(92, (y, w) => { lblSmartStatus.SetBounds(24, y, w, 36); lblSmartHealth.SetBounds(24, y + 40, w, 46); }, lblSmartStatus, lblSmartHealth);
            settings.Actions(failures);
            var result = new List<ModernFormCard> { settings };
            var groups = original.Controls.OfType<GroupBox>().OrderBy(g => g.Left).ToArray();
            for (int i = 0; i < groups.Length; i++)
            {
                var group = groups[i]; var code = group.Controls.OfType<TextBox>().Single(); var list = group.Controls.OfType<ListBox>().Single();
                var preview = group.Controls.OfType<PictureBox>().Single(); var buttons = group.Controls.OfType<Button>().OrderBy(b => b.Top).ToArray();
                var card = new ModernFormCard(i == 0 ? "Find product artwork" : "Find carton artwork", "Review the matching image before adding it to the appropriate lane.");
                card.Field(i == 0 ? "Product barcode / EAN" : "Carton SAP code", code, buttons[0]);
                card.Row(232, (y, w) => { int half = (w - 20) / 2; list.SetBounds(24, y, half, 216); preview.SetBounds(44 + half, y, half, 216); }, list, preview);
                card.Actions(buttons[1]); result.Add(card);
            }
            return result;
        }
    }
}
