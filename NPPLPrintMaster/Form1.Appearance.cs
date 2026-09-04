using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Threading.Tasks;

namespace NPPLPrintMaster
{
    public partial class Form1 : Form
    {
        private readonly Dictionary<Control, Font> originalAppearanceFonts =
            new Dictionary<Control, Font>();

        private int originalHeaderHeight;
        private Point originalHeaderTitleLocation;
        private Padding originalContentPadding;
        private bool originalAppearanceCaptured;

        private void ApplyTheme(string themeName)
        {
            themeName = AppearanceManager.NormalizeTheme(themeName);

            string typographyChoice =
                cmbTypography != null && cmbTypography.SelectedItem != null
                    ? cmbTypography.SelectedItem.ToString()
                    : currentSettings == null
                        ? AppearanceManager.UseThemeDefault
                        : currentSettings.TypographyStyle;

            string layoutChoice =
                cmbLayout != null && cmbLayout.SelectedItem != null
                    ? cmbLayout.SelectedItem.ToString()
                    : currentSettings == null
                        ? AppearanceManager.UseThemeDefault
                        : currentSettings.LayoutStyle;

            string resolvedTypography =
                AppearanceManager.ResolveTypography(
                    themeName,
                    typographyChoice);

            string resolvedLayout =
                AppearanceManager.ResolveLayout(
                    themeName,
                    layoutChoice);

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

            // BarTender 10.0 SR1-inspired classic desktop polish.
            // IMPORTANT: color/border styling only. No Font, Size, Location,
            // Dock, Anchor, page layout, or header height is changed here.
            bool useBarTender10Classic =
                ThemeManager.IsBarTender10Classic(themeName);

            if (useBarTender10Classic)
            {
                // Classic pale-blue menu chrome.
                menuBar.BackColor = Color.FromArgb(190, 211, 238);
                menuBar.ForeColor = Color.Black;

                foreach (ToolStripItem item in menuBar.Items)
                {
                    item.ForeColor = Color.Black;

                    ToolStripMenuItem menuItem = item as ToolStripMenuItem;
                    if (menuItem != null)
                    {
                        menuItem.BackColor = Color.FromArgb(190, 211, 238);

                        foreach (ToolStripItem subItem in menuItem.DropDownItems)
                        {
                            subItem.BackColor = Color.FromArgb(242, 247, 252);
                            subItem.ForeColor = Color.Black;
                        }
                    }
                }

                // Keep the exact current header height/layout; only tune colors/font.
                pnlHeader.BackColor = Color.FromArgb(211, 227, 244);
                pnlContent.BackColor = Color.FromArgb(151, 176, 207);
                lblHeaderTitle.ForeColor = Color.FromArgb(18, 18, 18);

                pnlLogo.BackColor = Color.FromArgb(183, 207, 234);

                foreach (Button navButton in navButtons)
                {
                    navButton.FlatStyle = FlatStyle.Flat;
                    navButton.FlatAppearance.BorderSize =
                        (navButton == activeNavButton) ? 1 : 0;
                    navButton.FlatAppearance.BorderColor =
                        Color.FromArgb(213, 228, 244);
                }

                // BarTender-style editor hierarchy:
                // dark/desaturated workspace with lighter functional islands.
                Color classicWorkspace = Color.FromArgb(151, 176, 207);

                pageHome.BackColor = classicWorkspace;
                pageExtract.BackColor = classicWorkspace;
                pageFormat.BackColor = classicWorkspace;
                pageCompose.BackColor = classicWorkspace;
                pageSettings.BackColor = classicWorkspace;

                ApplyBarTender10ClassicControlPolish(pageHome.Controls);
                ApplyBarTender10ClassicControlPolish(pageExtract.Controls);
                ApplyBarTender10ClassicControlPolish(pageFormat.Controls);
                ApplyBarTender10ClassicControlPolish(pageCompose.Controls);
                ApplyBarTender10ClassicControlPolish(pageSettings.Controls);
            }
            else
            {
                pageHome.BackColor = bgLight;
                pageExtract.BackColor = bgLight;
                pageFormat.BackColor = bgLight;
                pageCompose.BackColor = bgLight;
                pageSettings.BackColor = bgLight;

                foreach (Button navButton in navButtons)
                    navButton.FlatAppearance.BorderSize = 0;
            }

            ApplyLayoutStyle(resolvedLayout);
            ApplyTypographyStyle(resolvedTypography);

            if (lblAppearanceResolved != null)
            {
                lblAppearanceResolved.Text =
                    AppearanceManager.GetResolvedSummary(
                        themeName,
                        typographyChoice,
                        layoutChoice);
            }
        }

        private void CaptureOriginalAppearance()
        {
            if (originalAppearanceCaptured)
                return;

            originalAppearanceFonts.Clear();
            CaptureFontsRecursive(this);

            originalHeaderHeight = pnlHeader == null ? 80 : pnlHeader.Height;
            originalHeaderTitleLocation =
                lblHeaderTitle == null
                    ? new Point(30, 25)
                    : lblHeaderTitle.Location;
            originalContentPadding =
                pnlContent == null
                    ? new Padding(30)
                    : pnlContent.Padding;

            originalAppearanceCaptured = true;
        }

        private void CaptureFontsRecursive(Control root)
        {
            if (root == null)
                return;

            if (!originalAppearanceFonts.ContainsKey(root))
            {
                originalAppearanceFonts[root] =
                    new Font(
                        root.Font.FontFamily,
                        root.Font.Size,
                        root.Font.Style,
                        root.Font.Unit);
            }

            foreach (Control child in root.Controls)
                CaptureFontsRecursive(child);
        }

        private void RestoreOriginalFonts()
        {
            if (!originalAppearanceCaptured)
                return;

            foreach (KeyValuePair<Control, Font> item in originalAppearanceFonts)
            {
                if (item.Key == null || item.Key.IsDisposed)
                    continue;

                item.Key.Font = item.Value;
            }
        }

        private void ApplyTypographyStyle(string resolvedTypography)
        {
            RestoreOriginalFonts();

            if (!string.Equals(
                resolvedTypography,
                AppearanceManager.BarTender10Typography,
                StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // BarTender 10 text profile:
            // change the UI font family while preserving each control's
            // existing size/style hierarchy. Main chrome gets compact sizes.
            ApplyFontFamilyRecursive(this, "Tahoma");

            if (menuBar != null)
                menuBar.Font =
                    new Font("Tahoma", 8.25F, FontStyle.Regular);

            if (lblHeaderTitle != null)
                lblHeaderTitle.Font =
                    new Font("Tahoma", 14F, FontStyle.Bold);

            foreach (Button navButton in navButtons)
            {
                navButton.Font =
                    new Font("Tahoma", 9F, FontStyle.Bold);
            }
        }

        private void ApplyFontFamilyRecursive(Control root, string fontFamily)
        {
            if (root == null)
                return;

            Font current = root.Font;

            // Preserve specialized monospaced/preview content where changing
            // the font would alter the meaning of the content itself.
            bool preserveSpecialFont =
                root is RichTextBox &&
                root.BackColor.R < 25 &&
                root.BackColor.G < 25 &&
                root.BackColor.B < 25;

            if (!preserveSpecialFont)
            {
                try
                {
                    root.Font =
                        new Font(
                            fontFamily,
                            current.Size,
                            current.Style,
                            current.Unit);
                }
                catch
                {
                    // If the requested font is unavailable, retain the
                    // control's current font rather than breaking the UI.
                }
            }

            foreach (Control child in root.Controls)
                ApplyFontFamilyRecursive(child, fontFamily);
        }

        private void ApplyLayoutStyle(string resolvedLayout)
        {
            if (!originalAppearanceCaptured)
                return;

            this.SuspendLayout();

            try
            {
                // Always restore the canonical Forest Graphite / NPPL geometry
                // first so switching profiles never stacks layout changes.
                if (pnlHeader != null)
                    pnlHeader.Height = originalHeaderHeight;

                if (lblHeaderTitle != null)
                    lblHeaderTitle.Location = originalHeaderTitleLocation;

                if (pnlContent != null)
                    pnlContent.Padding = originalContentPadding;

                if (string.Equals(
                    resolvedLayout,
                    AppearanceManager.BarTender10Layout,
                    StringComparison.OrdinalIgnoreCase))
                {
                    // Only selected when the user explicitly chooses this
                    // layout or uses BarTender 10's Theme Default.
                    // It does NOT alter any page-internal control positions.
                    if (pnlHeader != null)
                        pnlHeader.Height = 58;

                    if (lblHeaderTitle != null)
                        lblHeaderTitle.Location =
                            new Point(
                                originalHeaderTitleLocation.X,
                                17);

                    if (pnlContent != null)
                        pnlContent.Padding = new Padding(22);
                }
            }
            finally
            {
                this.ResumeLayout(true);
            }
        }

        private void ApplyBarTender10ClassicControlPolish(
            Control.ControlCollection controls)
        {
            if (controls == null)
                return;

            Color panelSurface = Color.FromArgb(214, 228, 243);
            Color controlSurface = Color.FromArgb(248, 250, 252);
            Color border = Color.FromArgb(118, 146, 180);
            Color hover = Color.FromArgb(219, 230, 242);
            Color text = Color.FromArgb(18, 18, 18);

            foreach (Control c in controls)
            {
                GroupBox grp = c as GroupBox;
                if (grp != null)
                {
                    grp.BackColor = panelSurface;
                    grp.ForeColor = text;
                }
                else
                {
                    Label lbl = c as Label;
                    if (lbl != null)
                    {
                        lbl.ForeColor = text;
                    }

                    CheckBox chk = c as CheckBox;
                    if (chk != null)
                    {
                        chk.ForeColor = text;
                    }
                }

                Button btn = c as Button;
                if (btn != null)
                {
                    string caption = btn.Text ?? string.Empty;

                    bool primary =
                        caption.Contains("Extract Images") ||
                        caption.Contains("Find & Extract") ||
                        caption.Contains("Format Images") ||
                        caption.Contains("Generate Collage") ||
                        caption.Contains("Generate Job Card") ||
                        caption.Contains("Save Preferences");

                    bool destructive = caption.Contains("Clear All");
                    btn.FlatStyle = FlatStyle.Flat;

                    if (primary)
                    {
                        btn.BackColor = activeColor;
                        btn.ForeColor =
                            ThemeManager.GetContrastTextColor(activeColor);
                        btn.FlatAppearance.BorderColor =
                            Color.FromArgb(55, 108, 164);
                        btn.FlatAppearance.BorderSize = 1;
                    }
                    else if (destructive)
                    {
                        btn.BackColor = controlSurface;
                        btn.ForeColor = Color.FromArgb(170, 42, 42);
                        btn.FlatAppearance.BorderColor = Color.FromArgb(190, 76, 76);
                        btn.FlatAppearance.BorderSize = 1;
                    }
                    else
                    {
                        btn.BackColor = controlSurface;
                        btn.ForeColor = text;
                        btn.FlatAppearance.BorderColor = border;
                        btn.FlatAppearance.BorderSize = 1;
                        btn.FlatAppearance.MouseOverBackColor = hover;
                    }
                }

                TextBox txt = c as TextBox;
                if (txt != null)
                {
                    txt.BackColor = txt.ReadOnly
                        ? Color.FromArgb(232, 239, 246)
                        : controlSurface;
                    txt.ForeColor = text;
                    txt.BorderStyle = BorderStyle.FixedSingle;
                }

                RichTextBox rtb = c as RichTextBox;
                if (rtb != null)
                {
                    bool console =
                        rtb.BackColor.R < 25 &&
                        rtb.BackColor.G < 25 &&
                        rtb.BackColor.B < 25;

                    if (!console)
                    {
                        rtb.BackColor = rtb.ReadOnly
                            ? Color.FromArgb(232, 239, 246)
                            : controlSurface;
                        rtb.ForeColor = text;
                        rtb.BorderStyle = BorderStyle.FixedSingle;
                    }
                }

                ComboBox cmb = c as ComboBox;
                if (cmb != null)
                {
                    cmb.BackColor = controlSurface;
                    cmb.ForeColor = text;
                }

                NumericUpDown num = c as NumericUpDown;
                if (num != null)
                {
                    num.BackColor = controlSurface;
                    num.ForeColor = text;
                }

                FlowLayoutPanel flow = c as FlowLayoutPanel;
                if (flow != null)
                    flow.BackColor = Color.FromArgb(226, 236, 247);

                if (c.HasChildren)
                    ApplyBarTender10ClassicControlPolish(c.Controls);
            }
        }

    }
}
