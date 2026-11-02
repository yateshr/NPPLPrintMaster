using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace NPPLPrintMaster
{
    internal static class ModernUiSession
    {
        private sealed class Style
        {
            internal Control Control;
            internal Font Font;
            internal Color Back, Fore;
            internal BorderStyle? Border;
            internal FlatStyle? Flat;
            internal int BorderSize;
            internal Color ButtonBorder, Hover;
            internal bool AutoSize;
            internal Rectangle Bounds;
            internal DataGridViewCellStyle GridDefault, GridAlternate, GridHeader;
            internal Color GridColor;
            internal BorderStyle GridBorder;
            internal bool GridVisualHeaders;
            internal string HeaderText;
        }
        private sealed class StripStyle
        {
            internal ToolStrip Strip;
            internal ToolStripRenderer Renderer;
            internal readonly Dictionary<ToolStripItem, Tuple<Font, Color, Color>> Items = new Dictionary<ToolStripItem, Tuple<Font, Color, Color>>();
        }
        private sealed class WindowStyle
        {
            internal readonly List<Style> Controls = new List<Style>();
            internal readonly List<StripStyle> Strips = new List<StripStyle>();
            internal WindowStyle(Control root) { Capture(root); }
            private void Capture(Control root)
            {
                Controls.Add(new Style { Control = root, Font = root.Font, Back = root.BackColor, Fore = root.ForeColor,
                    Border = root is TextBoxBase ? (BorderStyle?)((TextBoxBase)root).BorderStyle : null,
                    Flat = root is Button ? (FlatStyle?)((Button)root).FlatStyle : null,
                    BorderSize = root is Button ? ((Button)root).FlatAppearance.BorderSize : 0,
                    ButtonBorder = root is Button ? ((Button)root).FlatAppearance.BorderColor : Color.Empty,
                    Hover = root is Button ? ((Button)root).FlatAppearance.MouseOverBackColor : Color.Empty });
                var state = Controls[Controls.Count - 1]; state.AutoSize = root.AutoSize; state.Bounds = root.Bounds;
                if (root is Label && root.Text.Contains("BUILDER TOOLS")) state.HeaderText = root.Text;
                var grid = root as DataGridView;
                if (grid != null)
                {
                    state.GridDefault = grid.DefaultCellStyle.Clone(); state.GridAlternate = grid.AlternatingRowsDefaultCellStyle.Clone();
                    state.GridHeader = grid.ColumnHeadersDefaultCellStyle.Clone(); state.GridColor = grid.GridColor;
                    state.GridBorder = grid.BorderStyle; state.GridVisualHeaders = grid.EnableHeadersVisualStyles;
                }
                var strip = root as ToolStrip;
                if (strip != null)
                {
                    var saved = new StripStyle { Strip = strip, Renderer = strip.Renderer };
                    foreach (ToolStripItem item in strip.Items) saved.Items[item] = Tuple.Create(item.Font, item.BackColor, item.ForeColor);
                    Strips.Add(saved);
                }
                if (root.GetType().Name.Contains("Canvas") || root is PictureBox || root is TextBoxBase || root is NumericUpDown || root is DataGridView) return;
                foreach (Control c in root.Controls) Capture(c);
            }
            internal void Restore()
            {
                foreach (var s in Controls.Where(s => !s.Control.IsDisposed))
                {
                    s.Control.Font = s.Font; s.Control.BackColor = s.Back; s.Control.ForeColor = s.Fore;
                    s.Control.AutoSize = s.AutoSize;
                    if (s.HeaderText != null) s.Control.Text = s.HeaderText;
                    if (s.Control.Dock == DockStyle.None) s.Control.Bounds = s.Bounds;
                    if (s.Border.HasValue) ((TextBoxBase)s.Control).BorderStyle = s.Border.Value;
                    if (s.Flat.HasValue)
                    { var b = (Button)s.Control; b.FlatStyle = s.Flat.Value; b.FlatAppearance.BorderSize = s.BorderSize; b.FlatAppearance.BorderColor = s.ButtonBorder; b.FlatAppearance.MouseOverBackColor = s.Hover; }
                    var grid = s.Control as DataGridView;
                    if (grid != null) { grid.DefaultCellStyle = s.GridDefault; grid.AlternatingRowsDefaultCellStyle = s.GridAlternate;
                        grid.ColumnHeadersDefaultCellStyle = s.GridHeader; grid.GridColor = s.GridColor; grid.BorderStyle = s.GridBorder; grid.EnableHeadersVisualStyles = s.GridVisualHeaders; }
                }
                foreach (var s in Strips.Where(s => !s.Strip.IsDisposed))
                {
                    s.Strip.Renderer = s.Renderer;
                    foreach (var item in s.Items.Where(i => !i.Key.IsDisposed))
                    { item.Key.Font = item.Value.Item1; item.Key.BackColor = item.Value.Item2; item.Key.ForeColor = item.Value.Item3; }
                }
            }
        }
        private static readonly Dictionary<BrandedForm, WindowStyle> windows = new Dictionary<BrandedForm, WindowStyle>();
        internal static bool Enabled { get; private set; }
        internal static void SetEnabled(bool enabled)
        {
            if (Enabled == enabled) return;
            Enabled = enabled;
            if (enabled)
                foreach (var window in Application.OpenForms.OfType<BrandedForm>().Where(f => !(f is Form1)).ToArray()) StyleWindow(window);
            else foreach (var window in windows.Where(p => !p.Key.IsDisposed).ToArray()) window.Value.Restore();
        }
        internal static void StyleWindow(BrandedForm form)
        {
            if (form is Form1 || !Enabled) return;
            if (!windows.ContainsKey(form))
            {
                windows[form] = new WindowStyle(form);
                form.FormClosed += (s, e) => windows.Remove(form);
                form.Disposed += (s, e) => windows.Remove(form);
            }
            var primary = new HashSet<Control>();
            CollectActions(form, primary);
            form.BackColor = InterfaceAppearance.ModernTheme().BgLight; form.ForeColor = Color.FromArgb(27, 39, 51);
            ModernStyles.Apply(form, primary);
            if (form is FreeformBuilderForm) RenameBuilderHeading(form);
            StyleStrips(form);
        }
        private static void RenameBuilderHeading(Control root)
        {
            foreach (Control c in root.Controls)
            {
                if (c is Label && c.Text.Contains("BUILDER TOOLS")) c.Text = "DESIGN TOOLS";
                if (!c.GetType().Name.Contains("Canvas") && !(c is TextBoxBase) && !(c is NumericUpDown)) RenameBuilderHeading(c);
            }
        }
        private static void CollectActions(Control root, ISet<Control> primary)
        {
            foreach (Control c in root.Controls)
            {
                if (c is Button && (c.Text.StartsWith("Save", StringComparison.OrdinalIgnoreCase) ||
                    c.Text.StartsWith("Export", StringComparison.OrdinalIgnoreCase) || c.Text.StartsWith("Apply", StringComparison.OrdinalIgnoreCase) ||
                    c.BackColor.ToArgb() == ModernStyles.Accent.ToArgb())) primary.Add(c);
                if (!c.GetType().Name.Contains("Canvas") && !(c is TextBoxBase) && !(c is NumericUpDown)) CollectActions(c, primary);
            }
        }
        private static void StyleStrips(Control root)
        {
            foreach (Control c in root.Controls)
            {
                if (c is ToolStripPanel) c.BackColor = Color.White;
                var strip = c as ToolStrip;
                if (strip != null)
                {
                    strip.BackColor = Color.White; strip.ForeColor = Color.FromArgb(27, 39, 51);
                    strip.Renderer = new ToolStripProfessionalRenderer(new StudioStripColors());
                    foreach (ToolStripItem item in strip.Items)
                    {
                        bool action = item is ToolStripButton && (item.Text.IndexOf("Save", StringComparison.OrdinalIgnoreCase) >= 0 || item.Text.IndexOf("Export", StringComparison.OrdinalIgnoreCase) >= 0);
                        item.Font = ModernStyles.Font(9, action ? FontStyle.Bold : FontStyle.Regular);
                        item.ForeColor = action ? ModernStyles.Accent : Color.FromArgb(27, 39, 51); item.BackColor = Color.White;
                    }
                }
                if (!c.GetType().Name.Contains("Canvas") && !(c is NumericUpDown) && !(c is TextBoxBase)) StyleStrips(c);
            }
        }
        private sealed class StudioStripColors : ProfessionalColorTable
        {
            public override Color ToolStripGradientBegin { get { return Color.White; } }
            public override Color ToolStripGradientMiddle { get { return Color.White; } }
            public override Color ToolStripGradientEnd { get { return Color.White; } }
            public override Color ButtonSelectedGradientBegin { get { return Color.FromArgb(255, 237, 222); } }
            public override Color ButtonSelectedGradientEnd { get { return Color.FromArgb(255, 237, 222); } }
            public override Color ButtonSelectedBorder { get { return ModernStyles.Accent; } }
            public override Color ButtonCheckedGradientBegin { get { return Color.FromArgb(255, 225, 199); } }
            public override Color ButtonCheckedGradientEnd { get { return Color.FromArgb(255, 225, 199); } }
        }
    }
}
