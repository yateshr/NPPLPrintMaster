using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace NPPLPrintMaster
{
    public partial class Form1
    {
        private ComboBox cmbInterfaceStyle;
        private Label interfaceDescription, interfacePreviewBadge;
        private GroupBox currentAppearanceGroup;
        private Button restoreCurrentInterface;
        private Panel modernHome;
        private bool changingInterface, modernPresentationApplied;
        private int savedSidebarWidth, savedSplitterWidth, savedLogoHeight;
        private readonly Dictionary<Button, Rectangle> savedNavigationBounds = new Dictionary<Button, Rectangle>();
        private readonly Dictionary<Button, string> savedNavigationText = new Dictionary<Button, string>();

        private bool IsModernInterface
        {
            get { return InterfaceAppearance.IsModern(cmbInterfaceStyle == null ? currentSettings.InterfaceStyle :
                Convert.ToString(cmbInterfaceStyle.SelectedItem)); }
        }

        private void BuildInterfaceSelector()
        {
            // Add the selector without replacing any existing settings controls.
            foreach (Control control in pageSettings.Controls) control.Top += 164;
            pageSettings.AutoScroll = true;
            currentAppearanceGroup = pageSettings.Controls.OfType<GroupBox>().First(g => g.Text == "Appearance");
            var group = new GroupBox { Text = "Interface experience", Location = Point.Empty,
                Size = new Size(600, 148), Font = new Font("Segoe UI", 11, FontStyle.Bold) };
            var label = new Label { Text = "Interface", AutoSize = true, Location = new Point(20, 36), Font = new Font("Segoe UI", 10) };
            cmbInterfaceStyle = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(150, 32), Size = new Size(250, 30), Font = new Font("Segoe UI", 10) };
            cmbInterfaceStyle.Items.AddRange(new object[] { InterfaceAppearance.Current, InterfaceAppearance.Modern });
            currentSettings.InterfaceStyle = InterfaceAppearance.Normalize(currentSettings.InterfaceStyle);
            cmbInterfaceStyle.SelectedItem = currentSettings.InterfaceStyle;
            restoreCurrentInterface = new Button { Text = "Use Current UI", Location = new Point(420, 32),
                Size = new Size(155, 30), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9) };
            restoreCurrentInterface.Click += (s, e) => cmbInterfaceStyle.SelectedItem = InterfaceAppearance.Current;
            interfaceDescription = new Label { Location = new Point(20, 76), Size = new Size(555, 60),
                Font = new Font("Segoe UI", 9), AutoEllipsis = true };
            group.Controls.AddRange(new Control[] { label, cmbInterfaceStyle, restoreCurrentInterface, interfaceDescription });
            pageSettings.Controls.Add(group);
            interfacePreviewBadge = new Label { Text = "MODERN UI  /  PREVIEW", Dock = DockStyle.Right, Width = 230,
                Padding = new Padding(0, 0, 24, 0), TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Segoe UI", 9, FontStyle.Bold), Visible = false };
            pnlHeader.Controls.Add(interfacePreviewBadge);
            cmbInterfaceStyle.SelectedIndexChanged += (s, e) => ChangeInterfaceStyle();
        }

        private void ChangeInterfaceStyle()
        {
            if (changingInterface) return;
            changingInterface = true;
            try
            {
                currentSettings.InterfaceStyle = InterfaceAppearance.Normalize(Convert.ToString(cmbInterfaceStyle.SelectedItem));
                ApplyTheme(Convert.ToString(cmbTheme.SelectedItem));
            }
            catch (Exception ex)
            {
                // The existing controls are still present, so fall back to
                // Current UI if the experimental presentation cannot be applied.
                currentSettings.InterfaceStyle = InterfaceAppearance.Current;
                cmbInterfaceStyle.SelectedItem = InterfaceAppearance.Current;
                ApplyTheme(Convert.ToString(cmbTheme.SelectedItem));
                MessageBox.Show(this, "The preview could not be applied. Current UI has been restored.\n\n" + ex.Message,
                    "Interface preview", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally { changingInterface = false; }
        }

        private void CaptureCurrentInterfaceGeometry()
        {
            savedSidebarWidth = pnlSidebar.Width; savedSplitterWidth = sidebarSplitter.Width;
            savedLogoHeight = pnlLogo.Height;
            savedNavigationBounds.Clear(); savedNavigationText.Clear();
            foreach (Button button in navButtons)
            { savedNavigationBounds[button] = button.Bounds; savedNavigationText[button] = button.Text; }
        }

        private void ApplyInterfacePresentation()
        {
            if (cmbInterfaceStyle == null) return;
            bool modern = IsModernInterface;
            SuspendLayout();
            try
            {
                if (modern)
                {
                    if (modernHome == null) BuildModernHome();
                    if (!modernPresentationApplied) CaptureCurrentInterfaceGeometry();
                    modernPresentationApplied = true;
                    pnlSidebar.Width = 224; sidebarSplitter.Width = 1;
                    sidebarSplitter.BackColor = activeTheme.BorderColor;
                    pnlHeader.Height = 72; pnlContent.Padding = new Padding(24);
                    lblHeaderTitle.Location = new Point(24, 23);
                    lblHeaderTitle.Font = new Font("Segoe UI", 17, FontStyle.Bold);
                    pnlLogo.Height = 84; pnlLogo.BackColor = activeTheme.SidebarColor;
                    string[] names = { "Overview", "Extract Labels", "Format Images", "Job Cards",
                        "Image Library", "Image Collage", "Freeform Studio", "Settings" };
                    for (int i = 0; i < navButtons.Count; i++)
                    {
                        Button button = navButtons[i];
                        button.SetBounds(10, 100 + i * 48, 204, 42);
                        button.Text = "  " + names[i]; button.Font = new Font("Segoe UI", 9.5f);
                        button.Padding = new Padding(12, 0, 0, 0);
                    }
                    ApplyModernHomeColors();
                }
                else if (modernPresentationApplied)
                {
                    pnlSidebar.Width = savedSidebarWidth; sidebarSplitter.Width = savedSplitterWidth;
                    // ApplyLayoutStyle/ApplyTypographyStyle have already
                    // restored the selected Current UI appearance profile.
                    pnlLogo.Height = savedLogoHeight;
                    foreach (Button button in navButtons)
                    {
                        button.Bounds = savedNavigationBounds[button]; button.Text = savedNavigationText[button];
                        button.Padding = new Padding(15, 0, 0, 0);
                    }
                    modernPresentationApplied = false;
                }
                interfacePreviewBadge.Visible = modern;
                interfacePreviewBadge.ForeColor = activeTheme.ActiveColor;
                foreach (Control control in pageHome.Controls)
                    if (control != modernHome) control.Visible = !modern;
                if (modernHome != null)
                {
                    modernHome.Visible = modern;
                    if (modern) modernHome.BringToFront();
                }
                cmbTheme.Enabled = !modern; cmbTypography.Enabled = !modern; cmbLayout.Enabled = !modern;
                foreach (Button button in currentAppearanceGroup.Controls.OfType<Button>()) button.Enabled = !modern;
                restoreCurrentInterface.Enabled = modern;
                interfaceDescription.Text = modern
                    ? "Preview includes the new shell, dashboard and collage workspace. Other tools keep their current layouts. Save Preferences to remember this choice."
                    : "Current UI is the default. Try Modern UI without clearing your work, and switch back here at any time. Save Preferences to remember your choice.";
                ApplyModernPolish(modern);
                UpdateInterfaceHeading();
            }
            finally { ResumeLayout(true); }
        }

        private void UpdateInterfaceHeading()
        {
            if (activeNavButton == null) return;
            int index = navButtons.IndexOf(activeNavButton);
            string[] currentTitles = { "Dashboard Home", "Extract Labels", "Format Images", "Quick Job Card",
                "BTW Image Library", "Image Collage", "Freeform Designer", "Application Settings" };
            string[] modernTitles = { "Overview", "Extract Labels", "Format Images", "Job Cards",
                "Image Library", "Image Collage", "Freeform Studio", "Settings" };
            if (index >= 0 && index < currentTitles.Length)
                lblHeaderTitle.Text = IsModernInterface ? modernTitles[index] : currentTitles[index];
            foreach (Button button in navButtons)
            {
                var navigation = button as ModernNavigationButton;
                if (navigation != null) { navigation.Active = button == activeNavButton; navigation.Invalidate(); }
            }
        }

        private void BuildModernHome()
        {
            modernHome = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Visible = false };
            var body = new Panel { Dock = DockStyle.Top, Height = 560 };
            body.Controls.Add(new Label { Text = "Your print production workspace", Location = new Point(0, 0),
                Size = new Size(760, 38), Font = new Font("Segoe UI", 21, FontStyle.Bold) });
            body.Controls.Add(new Label { Text = "Choose a workflow to start, or continue an editable workspace.",
                Location = new Point(2, 44), Size = new Size(750, 24), Font = new Font("Segoe UI", 10) });
            string[] titles = { "Create an image collage", "Extract label images", "Browse your image library", "Continue a saved workspace" };
            string[] descriptions = { "Arrange labels on a print-ready sheet and export a PDF.",
                "Extract artwork from your BarTender source files.", "Find existing artwork and use it in your next job.",
                "Open an editable job-card workspace and keep working." };
            Action[] actions = { OpenCollagePage, () => navButtons[1].PerformClick(),
                () => navButtons[4].PerformClick(), LoadWorkspace };
            var cards = new List<CollageCard>();
            for (int i = 0; i < titles.Length; i++)
            {
                Action action = actions[i];
                var card = new CollageCard { Step = i + 1, Height = 208, ShowStepBadge = false };
                card.Controls.Add(new Label { Text = titles[i], Location = new Point(64, 23), Size = new Size(320, 26),
                    Font = new Font("Segoe UI", 12, FontStyle.Bold), AutoEllipsis = true });
                card.Controls.Add(new Label { Text = descriptions[i], Location = new Point(24, 78), Size = new Size(360, 44),
                    Font = new Font("Segoe UI", 10) });
                string[] buttonTitles = { "Create collage", "Extract labels", "Open library", "Open workspace" };
                var button = new CollageUiButton { Text = buttonTitles[i], Tag = i == 0 ? "PrimaryAction" : "SecondaryAction",
                    Location = new Point(24, 146), Size = new Size(180, 38), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
                button.Click += (s, e) => action(); card.Controls.Add(button);
                card.Controls.Add(new ModernIconTile { IconIndex = new[] { 5, 1, 4, 8 }[i], Location = new Point(24, 22), Size = new Size(28, 28) });
                cards.Add(card); body.Controls.Add(card);
            }
            Action arrange = () =>
            {
                int columns = body.ClientSize.Width >= 780 ? 2 : 1;
                body.Controls[0].Width = Math.Max(300, body.ClientSize.Width - 12);
                body.Controls[1].Width = Math.Max(300, body.ClientSize.Width - 12);
                int width = Math.Max(420, (body.ClientSize.Width - (columns - 1) * 20) / columns);
                for (int i = 0; i < cards.Count; i++)
                {
                    cards[i].SetBounds((i % columns) * (width + 20), 94 + (i / columns) * 228, width, 208);
                    cards[i].Controls[0].Width = width - 88; cards[i].Controls[1].Width = width - 48;
                }
                body.Height = 94 + ((cards.Count + columns - 1) / columns) * 228;
            };
            body.SizeChanged += (s, e) => arrange();
            modernHome.Controls.Add(body); pageHome.Controls.Add(modernHome); arrange();
        }

        private void ApplyModernHomeColors()
        {
            modernHome.BackColor = activeTheme.BgLight;
            foreach (Control control in WalkCollageControls(modernHome))
            {
                var card = control as CollageCard;
                if (card != null) { card.BackColor = activeTheme.ContentBg; card.BorderColor = activeTheme.BorderColor; card.AccentColor = activeTheme.ActiveColor; }
                if (control is Label) control.ForeColor = activeTheme.TextColor;
                var button = control as CollageUiButton;
                if (button != null)
                {
                    bool primary = object.Equals(button.Tag, "PrimaryAction");
                    button.BackColor = primary ? activeTheme.ActiveColor : activeTheme.ContentBg;
                    button.ForeColor = primary ? ThemeManager.GetContrastTextColor(activeTheme.ActiveColor) : activeTheme.TextColor;
                    button.FlatAppearance.BorderColor = primary ? activeTheme.ActiveColor : activeTheme.BorderColor;
                    button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(0, 115, 76) : activeTheme.ButtonHover;
                }
            }
        }
    }
}
