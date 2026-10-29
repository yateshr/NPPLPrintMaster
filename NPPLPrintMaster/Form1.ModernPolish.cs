using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace NPPLPrintMaster
{
    public partial class Form1
    {
        private Panel modernSettings, modernBrand, modernSidebarFooter;
        private Label modernBreadcrumb, modernWorkspaceStatus, originalCredit;
        private Button modernAccountButton, modernRestoreCurrent;
        private ComboBox modernStyleSelector;
        private NumericUpDown modernDefaultDpi;
        private ModernNumericField modernDpiField;
        private TextBox modernDefaultExport1, modernDefaultExport2;
        private readonly List<CollageTextField> modernPreferenceFields = new List<CollageTextField>();

        private void SavePreferences(bool showConfirmation = true)
        {
            currentSettings.Theme = Convert.ToString(cmbTheme.SelectedItem);
            currentSettings.TypographyStyle = Convert.ToString(cmbTypography.SelectedItem);
            currentSettings.LayoutStyle = Convert.ToString(cmbLayout.SelectedItem);
            currentSettings.InterfaceStyle = InterfaceAppearance.Normalize(Convert.ToString(cmbInterfaceStyle.SelectedItem));
            currentSettings.DefaultDpi = (int)numDefDpi.Value;
            currentSettings.DefaultExport1 = txtDefExport1.Text;
            currentSettings.DefaultExport2 = txtDefExport2.Text;
            SettingsManager.Save(currentSettings);
            if (showConfirmation) MessageBox.Show(this, "Preferences saved.", "Settings", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BuildModernChrome()
        {
            modernBrand = new Panel { Dock = DockStyle.Top, Height = 90, BackColor = InterfaceAppearance.ModernTheme().SidebarColor };
            modernBrand.Controls.Add(new Label { Text = "NPPL", Location = new Point(18, 23), Size = new Size(38, 38),
                BackColor = Color.FromArgb(0, 135, 90), ForeColor = Color.White,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter });
            modernBrand.Controls.Add(new Label { Text = "PrintMaster", Location = new Point(66, 22), Size = new Size(150, 28),
                ForeColor = Color.White, Font = new Font("Segoe UI", 13, FontStyle.Bold) });
            modernBrand.Controls.Add(new Label { Text = "PRODUCTION WORKSPACE", Location = new Point(67, 50), Size = new Size(150, 18),
                ForeColor = Color.FromArgb(145, 164, 177), Font = new Font("Segoe UI", 7.5f) });
            originalCredit = pnlSidebar.Controls.OfType<Label>().FirstOrDefault(l => l.Text.StartsWith("Built by"));
            modernSidebarFooter = new Panel { Dock = DockStyle.Bottom, Height = 64, BackColor = modernBrand.BackColor };
            modernSidebarFooter.Controls.Add(new Label { Text = "●  Local workspace", Location = new Point(20, 10), Size = new Size(190, 20),
                Font = new Font("Segoe UI", 9), ForeColor = Color.FromArgb(132, 213, 174) });
            modernSidebarFooter.Controls.Add(new Label { Text = "NPPL PrintMaster  /  Preview", Location = new Point(20, 33), Size = new Size(190, 18),
                Font = new Font("Segoe UI", 8), ForeColor = Color.FromArgb(145, 164, 177) });
            pnlSidebar.Controls.Add(modernBrand); pnlSidebar.Controls.Add(modernSidebarFooter);
            modernBrand.BringToFront(); modernSidebarFooter.BringToFront();
            modernBreadcrumb = new Label { Text = "NPPL  /  WORKSPACE", Location = new Point(25, 12), Size = new Size(300, 17),
                Font = new Font("Segoe UI", 8), ForeColor = Color.FromArgb(100, 116, 133) };
            modernWorkspaceStatus = new Label { Text = "●  Local workspace", Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(87, 106, 119), TextAlign = ContentAlignment.MiddleLeft };
            modernAccountButton = ModernPreferenceButton("YR", () => navButtons[navButtons.Count - 1].PerformClick());
            pnlHeader.Controls.AddRange(new Control[] { modernBreadcrumb, modernWorkspaceStatus, modernAccountButton });
            pnlHeader.SizeChanged += (s, e) => LayoutModernChrome();
        }

        private void LayoutModernChrome()
        {
            if (modernBreadcrumb == null || !IsModernInterface) return;
            lblHeaderTitle.Location = new Point(24, 34);
            modernWorkspaceStatus.SetBounds(Math.Max(400, pnlHeader.Width - 355), 27, 145, 30);
            interfacePreviewBadge.SetBounds(Math.Max(550, pnlHeader.Width - 205), 29, 122, 26);
            interfacePreviewBadge.Visible = pnlHeader.Width >= 740;
            modernAccountButton.SetBounds(pnlHeader.Width - 68, 22, 44, 36);
            modernWorkspaceStatus.Visible = pnlHeader.Width >= 850;
        }

        private void ApplyModernPolish(bool modern)
        {
            if (modern && modernBrand == null) BuildModernChrome();
            if (modern && modernSettings == null) BuildModernSettings();
            for (int i = 0; i < navButtons.Count; i++)
            {
                var navigation = navButtons[i] as ModernNavigationButton;
                if (navigation != null) { navigation.ModernStyle = modern; navigation.IconIndex = i; navigation.Invalidate(); }
            }
            if (modernBrand != null)
            {
                modernBrand.Visible = modern; modernSidebarFooter.Visible = modern;
                modernBreadcrumb.Visible = modern; modernWorkspaceStatus.Visible = modern; modernAccountButton.Visible = modern;
                pnlLogo.Visible = !modern;
                if (originalCredit != null) originalCredit.Visible = !modern;
            }
            if (modernSettings != null)
            {
                foreach (Control control in pageSettings.Controls) if (control != modernSettings) control.Visible = !modern;
                modernSettings.Visible = modern;
                if (modern) modernSettings.BringToFront();
            }
            if (modern)
            {
                pnlHeader.Height = 84;
                interfacePreviewBadge.Dock = DockStyle.None;
                interfacePreviewBadge.Padding = Padding.Empty;
                interfacePreviewBadge.Text = "Modern preview";
                interfacePreviewBadge.TextAlign = ContentAlignment.MiddleCenter;
                interfacePreviewBadge.Font = new Font("Segoe UI", 9);
                interfacePreviewBadge.ForeColor = Color.FromArgb(100, 116, 133);
                modernBreadcrumb.Font = new Font("Segoe UI", 8);
                modernWorkspaceStatus.Font = new Font("Segoe UI", 9);
                modernAccountButton.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
                LayoutModernChrome();
                EnsureModernFont(modernSettings);
                EnsureModernFont(modernHome);
                EnsureModernFont(modernBrand);
                EnsureModernFont(modernSidebarFooter);
                ApplyModernPreferenceColors();
            }
        }

        private static void EnsureModernFont(Control root)
        {
            if (root.Font.FontFamily.Name != "Segoe UI") root.Font = new Font("Segoe UI", root.Font.Size, root.Font.Style, root.Font.Unit);
            foreach (Control control in root.Controls) EnsureModernFont(control);
        }

        private CollageUiButton ModernPreferenceButton(string text, Action action, bool primary = false)
        {
            var button = new CollageUiButton { Text = text, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Tag = primary ? "PrimaryAction" : "SecondaryAction", Size = new Size(150, 38) };
            button.Click += (s, e) => action(); return button;
        }

        private Label ModernPreferenceLabel(string text, bool muted = false, float size = 9.5f)
        {
            return new Label { Text = text, Font = new Font("Segoe UI", size),
                ForeColor = muted ? Color.FromArgb(100, 116, 133) : Color.FromArgb(27, 39, 51),
                Size = new Size(400, 24), AutoEllipsis = true, Tag = muted ? "Muted" : null };
        }

        private CollageCard ModernPreferenceCard(string heading, string subtitle)
        {
            var card = new CollageCard { ShowStepBadge = false };
            var title = ModernPreferenceLabel(heading, false, 12);
            title.Font = new Font("Segoe UI", 12, FontStyle.Bold); title.Location = new Point(24, 23);
            var description = ModernPreferenceLabel(subtitle, true); description.Location = new Point(24, 55);
            card.Controls.Add(title); card.Controls.Add(description); return card;
        }

        private CollageTextField MirrorPreferenceText(TextBox original, out TextBox mirror)
        {
            var text = new TextBox { Text = original.Text, Font = new Font("Segoe UI", 9.5f) };
            text.TextChanged += (s, e) => { if (original.Text != text.Text) original.Text = text.Text; };
            original.TextChanged += (s, e) => { if (text.Text != original.Text) text.Text = original.Text; };
            mirror = text;
            var field = new CollageTextField(text) { Font = text.Font };
            modernPreferenceFields.Add(field); return field;
        }

        private void BuildModernSettings()
        {
            modernSettings = new Panel { Dock = DockStyle.Top, Height = 744, MinimumSize = new Size(520, 0) };
            var title = ModernPreferenceLabel("Personalize your workspace", false, 20);
            title.Font = new Font("Segoe UI", 20, FontStyle.Bold); title.SetBounds(0, 0, 650, 38);
            var intro = ModernPreferenceLabel("Your interface and export defaults, kept together in one place.", true);
            intro.SetBounds(1, 40, 700, 25);
            var experience = ModernPreferenceCard("Interface experience", "Choose the interface you prefer. Your jobs stay in place when switching.");
            var defaults = ModernPreferenceCard("Export defaults", "Set the quality and folders used for new extraction and formatting jobs.");
            var styleLabel = ModernPreferenceLabel("Interface");
            modernStyleSelector = new CollageThemedComboBox(InterfaceAppearance.ModernTheme()) { ItemHeight = 30, Font = new Font("Segoe UI", 9.5f) };
                ((CollageThemedComboBox)modernStyleSelector).RoundedAppearance = true;
            modernStyleSelector.Items.AddRange(new object[] { InterfaceAppearance.Current, InterfaceAppearance.Modern });
            modernStyleSelector.SelectedItem = cmbInterfaceStyle.SelectedItem;
            modernStyleSelector.SelectedIndexChanged += (s, e) => cmbInterfaceStyle.SelectedItem = modernStyleSelector.SelectedItem;
            cmbInterfaceStyle.SelectedIndexChanged += (s, e) => {
                if (modernStyleSelector.SelectedIndex != cmbInterfaceStyle.SelectedIndex) modernStyleSelector.SelectedIndex = cmbInterfaceStyle.SelectedIndex;
            };
            modernRestoreCurrent = ModernPreferenceButton("Use Current UI", () => cmbInterfaceStyle.SelectedItem = InterfaceAppearance.Current);
            var rememberedTheme = ModernPreferenceLabel("Current UI appearance is preserved. This preview uses a clean light workspace.", true);
            experience.Controls.AddRange(new Control[] { styleLabel, modernStyleSelector, modernRestoreCurrent, rememberedTheme });
            var dpiLabel = ModernPreferenceLabel("Extraction resolution");
            modernDefaultDpi = new NumericUpDown { Minimum = numDefDpi.Minimum, Maximum = numDefDpi.Maximum,
                Value = numDefDpi.Value, Font = new Font("Segoe UI", 11), BorderStyle = BorderStyle.FixedSingle };
            modernDefaultDpi.ValueChanged += (s, e) => numDefDpi.Value = modernDefaultDpi.Value;
            numDefDpi.ValueChanged += (s, e) => { if (modernDefaultDpi.Value != numDefDpi.Value) modernDefaultDpi.Value = numDefDpi.Value; };
            modernDpiField = new ModernNumericField(modernDefaultDpi);
            var dpiHint = ModernPreferenceLabel("DPI  /  Higher values preserve more label detail.", true);
            var extractionLabel = ModernPreferenceLabel("Default extraction folder");
            var formattingLabel = ModernPreferenceLabel("Default formatting folder");
            var extraction = MirrorPreferenceText(txtDefExport1, out modernDefaultExport1);
            var formatting = MirrorPreferenceText(txtDefExport2, out modernDefaultExport2);
            var browseExtraction = ModernPreferenceButton("Browse", () => {
                string path = SelectFolderModern("Settings.DefaultExtractionOutput"); if (!string.IsNullOrEmpty(path)) txtDefExport1.Text = path;
            });
            var browseFormatting = ModernPreferenceButton("Browse", () => {
                string path = SelectFolderModern("Settings.DefaultFormattingOutput"); if (!string.IsNullOrEmpty(path)) txtDefExport2.Text = path;
            });
            defaults.Controls.AddRange(new Control[] { dpiLabel, modernDpiField, dpiHint, extractionLabel, extraction,
                browseExtraction, formattingLabel, formatting, browseFormatting });
            var save = ModernPreferenceButton("Save preferences", () => SavePreferences(), true);
            var footer = ModernPreferenceLabel("Save to remember these preferences next time.", true);
            modernSettings.Controls.AddRange(new Control[] { title, intro, experience, defaults, save, footer });
            Action layout = () => {
                int w = modernSettings.ClientSize.Width;
                title.Width = Math.Max(400, w); intro.Width = Math.Max(400, w);
                experience.SetBounds(0, 80, w, 222); defaults.SetBounds(0, 318, w, 334);
                foreach (var card in new[] { experience, defaults }) { card.Controls[0].Width = w - 48; card.Controls[1].Width = w - 48; }
                styleLabel.SetBounds(24, 94, 250, 22);
                modernStyleSelector.SetBounds(24, 122, Math.Min(380, w - 234), 38);
                modernRestoreCurrent.SetBounds(w - 174, 122, 150, 38);
                rememberedTheme.SetBounds(24, 175, w - 48, 30);
                dpiLabel.SetBounds(24, 91, 240, 22); modernDpiField.SetBounds(24, 120, 180, 38); dpiHint.SetBounds(224, 122, w - 248, 30);
                extractionLabel.SetBounds(24, 174, w - 48, 22); extraction.SetBounds(24, 201, w - 156, 38); browseExtraction.SetBounds(w - 120, 201, 96, 38);
                formattingLabel.SetBounds(24, 254, w - 48, 22); formatting.SetBounds(24, 282, w - 156, 38); browseFormatting.SetBounds(w - 120, 282, 96, 38);
                footer.SetBounds(0, 677, w - 210, 36); save.SetBounds(w - 184, 672, 184, 42);
            };
            modernSettings.SizeChanged += (s, e) => layout(); pageSettings.Controls.Add(modernSettings); layout();
        }

        private void ApplyModernPreferenceColors()
        {
            var theme = InterfaceAppearance.ModernTheme(); modernSettings.BackColor = theme.BgLight;
            foreach (Control control in WalkCollageControls(modernSettings))
            {
                var card = control as CollageCard;
                if (card != null) { card.BackColor = theme.ContentBg; card.BorderColor = theme.BorderColor; }
                if (control is Label) control.ForeColor = object.Equals(control.Tag, "Muted") ? theme.MutedTextColor : theme.TextColor;
                var combo = control as CollageThemedComboBox; if (combo != null) combo.SetTheme(theme);
                var button = control as CollageUiButton; if (button != null) ApplyModernButtonColors(button, theme);
            }
            foreach (var field in modernPreferenceFields)
            {
                field.BackColor = theme.ReadOnlyBg; field.BorderColor = theme.BorderColor; field.ForeColor = theme.ActiveColor; field.MutedColor = theme.MutedTextColor;
                foreach (Control text in field.Controls)
                {
                    text.BackColor = theme.ReadOnlyBg; text.ForeColor = theme.TextColor;
                    var box = text as TextBox;
                    if (box != null) { box.BorderStyle = BorderStyle.None; box.AutoSize = false; }
                }
            }
            modernDpiField.BackColor = Color.White;
            modernDefaultDpi.BackColor = Color.White;
            modernDefaultDpi.ForeColor = theme.TextColor;
            modernDefaultDpi.BorderStyle = BorderStyle.None;
            ApplyModernButtonColors((CollageUiButton)modernAccountButton, theme);
        }

        private static void ApplyModernButtonColors(CollageUiButton button, ThemeColors theme)
        {
            bool primary = object.Equals(button.Tag, "PrimaryAction");
            button.BackColor = primary ? theme.ActiveColor : theme.ContentBg;
            button.ForeColor = primary ? Color.White : theme.TextColor; button.MutedColor = theme.MutedTextColor;
            button.FlatAppearance.BorderColor = primary ? theme.ActiveColor : theme.BorderColor;
            button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(0, 115, 76) : theme.ButtonHover;
        }
    }
}
