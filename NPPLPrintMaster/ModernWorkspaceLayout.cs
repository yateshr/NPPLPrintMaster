using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace NPPLPrintMaster
{
    // Presentation changes operate on the original controls. No job data or
    // processing event handlers are copied into a second implementation.
    internal sealed class ModernLayoutSnapshot
    {
        private sealed class Entry
        {
            internal Control Control, Parent;
            internal int Index, TabIndex;
            internal Rectangle Bounds;
            internal DockStyle Dock;
            internal AnchorStyles Anchor;
            internal bool AutoSize;
            internal Padding Padding, Margin;
            internal BorderStyle? Border;
        }
        private readonly List<Entry> entries = new List<Entry>();
        internal ModernLayoutSnapshot(Control root) { Capture(root); }
        private void Capture(Control root)
        {
            foreach (Control c in root.Controls)
            {
                entries.Add(new Entry { Control = c, Parent = root, Index = root.Controls.GetChildIndex(c),
                    Bounds = c.Bounds, Dock = c.Dock, Anchor = c.Anchor, AutoSize = c.AutoSize,
                    TabIndex = c.TabIndex,
                    Padding = c.Padding, Margin = c.Margin,
                    Border = c is UpDownBase ? (BorderStyle?)((UpDownBase)c).BorderStyle : c is TextBoxBase ? (BorderStyle?)((TextBoxBase)c).BorderStyle :
                        c is Panel ? (BorderStyle?)((Panel)c).BorderStyle : null });
                if (!(c is FlowLayoutPanel) && (c is Panel || c is GroupBox || c is SplitContainer || c is TabControl || c is TabPage)) Capture(c);
            }
        }
        internal void Restore()
        {
            foreach (var e in entries)
            {
                if (e.Control.IsDisposed) continue;
                e.Control.Parent = e.Parent;
                e.Control.Dock = e.Dock; e.Control.Anchor = e.Anchor; e.Control.AutoSize = e.AutoSize;
                e.Control.Padding = e.Padding; e.Control.Margin = e.Margin;
                e.Control.TabIndex = e.TabIndex;
                if (e.Border.HasValue && e.Control is TextBoxBase) ((TextBoxBase)e.Control).BorderStyle = e.Border.Value;
                if (e.Border.HasValue && e.Control is Panel) ((Panel)e.Control).BorderStyle = e.Border.Value;
                if (e.Border.HasValue && e.Control is UpDownBase) ((UpDownBase)e.Control).BorderStyle = e.Border.Value;
                if (e.Dock == DockStyle.None) e.Control.Bounds = e.Bounds;
            }
            foreach (var e in entries)
                if (!e.Control.IsDisposed) e.Parent.Controls.SetChildIndex(e.Control, Math.Min(e.Index, e.Parent.Controls.Count - 1));
        }
    }

    internal sealed class ModernFormCard : CollageCardBase
    {
        private readonly List<Tuple<int, Action<int, int>>> rows = new List<Tuple<int, Action<int, int>>>();
        private readonly Label title, description;
        internal ModernFormCard(string heading, string helper)
        {
            title = ModernStyles.Label(heading, 12, true);
            description = ModernStyles.Label(helper, 9, false, true);
            Controls.Add(title); Controls.Add(description);
            SizeChanged += (s, e) => Arrange();
        }
        internal void Row(int height, Action<int, int> layout, params Control[] controls)
        {
            foreach (Control c in controls.Where(c => c != null))
            { c.Dock = DockStyle.None; c.Anchor = AnchorStyles.Top | AnchorStyles.Left; c.TabIndex = Controls.Count; Controls.Add(c); }
            rows.Add(Tuple.Create(height, layout)); Arrange();
        }
        internal void Field(string caption, params Control[] controls)
        {
            var label = ModernStyles.Label(caption, 9.5f, true);
            var display = controls.Select(c => c is TextBox && !((TextBox)c).Multiline ?
                (Control)new CollageTextField((TextBox)c) : c is NumericUpDown ?
                new ModernNumericField((NumericUpDown)c) : c).ToArray();
            Row(76, (y, w) =>
            {
                label.SetBounds(24, y, w, 22);
                int fixedWidth = controls.Skip(1).Sum(c => c is Button ? Math.Max(96, Math.Min(150, c.Text.Length * 7 + 24)) + 10 : 150);
                int x = 24;
                for (int i = 0; i < controls.Length; i++)
                {
                    var c = display[i]; c.AutoSize = false;
                    int width = i == 0 ? Math.Max(100, w - fixedWidth) : c is Button ? Math.Max(96, Math.Min(150, c.Text.Length * 7 + 24)) : 140;
                    c.SetBounds(x, y + 27, width, c is ComboBox ? 30 : 36); x += width + 10;
                }
            }, new Control[] { label }.Concat(display).ToArray());
        }
        internal void Options(params Control[] controls)
        {
            Row(controls.Length > 1 ? 68 : 36, (y, w) =>
            {
                for (int i = 0; i < controls.Length; i++)
                { controls[i].AutoSize = false; controls[i].SetBounds(24, y + i * 30, w, 28); }
            }, controls);
        }
        internal void Pair(string leftTitle, Control left, string rightTitle, Control right)
        {
            var a = ModernStyles.Label(leftTitle, 9.5f, true); var b = ModernStyles.Label(rightTitle, 9.5f, true);
            if (left is NumericUpDown) left = new ModernNumericField((NumericUpDown)left);
            if (right is NumericUpDown) right = new ModernNumericField((NumericUpDown)right);
            Row(76, (y, w) =>
            {
                int half = (w - 20) / 2;
                a.SetBounds(24, y, half, 22); b.SetBounds(44 + half, y, half, 22);
                left.AutoSize = right.AutoSize = false;
                left.SetBounds(24, y + 27, half, left is ComboBox ? 30 : 36);
                right.SetBounds(44 + half, y + 27, half, right is ComboBox ? 30 : 36);
            }, a, b, left, right);
        }
        internal void Actions(params Button[] buttons)
        {
            Row(52, (y, w) =>
            {
                int width = Math.Min(240, (w - (buttons.Length - 1) * 12) / buttons.Length);
                int start = buttons.Length == 1 ? 24 + w - width : 24;
                for (int i = 0; i < buttons.Length; i++) buttons[i].SetBounds(start + i * (width + 12), y + 4, width, 40);
            }, buttons.Cast<Control>().ToArray());
        }
        private void Arrange()
        {
            int w = Math.Max(220, Width - 48);
            title.SetBounds(24, 22, w, 28); description.SetBounds(24, 54, w, 37);
            int y = 102;
            foreach (var row in rows) { row.Item2(y, w); y += row.Item1; }
            Height = y + 16;
        }
    }

    internal class CollageCardBase : Panel
    {
        internal CollageCardBase() { DoubleBuffered = true; BackColor = Color.White; }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null ? Color.FromArgb(245, 247, 250) : Parent.BackColor);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var path = CollageShapes.Rounded(new RectangleF(.5f, .5f, Width - 1, Height - 1), 12))
            using (var fill = new SolidBrush(BackColor))
            using (var line = new Pen(Color.FromArgb(221, 227, 234)))
            { e.Graphics.FillPath(fill, path); e.Graphics.DrawPath(line, path); }
        }
    }

    internal sealed class StudioSectionPanel : Panel
    {
        private bool modern;
        private Padding originalMargin;
        private bool savedMargin;
        internal StudioSectionPanel() { DoubleBuffered = true; }
        internal void SetModern(bool value)
        {
            modern = value; BorderStyle = value ? BorderStyle.None : BorderStyle.FixedSingle;
            if (!savedMargin) { originalMargin = Margin; savedMargin = true; }
            Margin = value ? new Padding(0, 0, 8, 14) : originalMargin;
            Padding = value ? new Padding(12, 6, 12, 6) : new Padding(1); Invalidate();
        }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (!modern) { base.OnPaintBackground(e); return; }
            e.Graphics.Clear(Parent == null ? Color.FromArgb(245, 247, 250) : Parent.BackColor);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var path = CollageShapes.Rounded(new RectangleF(.5f, .5f, Width - 1, Height - 1), 10))
            using (var fill = new SolidBrush(Color.White))
            using (var line = new Pen(Color.FromArgb(218, 226, 234)))
            { e.Graphics.FillPath(fill, path); e.Graphics.DrawPath(line, path); }
        }
    }

    internal static class ModernStyles
    {
        internal static readonly Color Accent = Color.FromArgb(194, 65, 12);
        private static readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();
        internal static Font Font(float size, FontStyle style = FontStyle.Regular)
        {
            string key = size.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + (int)style;
            Font font; if (!fonts.TryGetValue(key, out font)) fonts[key] = font = new Font("Segoe UI", size, style);
            return font;
        }
        internal static Label Label(string text, float size = 9.5f, bool bold = false, bool muted = false)
        { return new Label { Text = text, Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
            ForeColor = muted ? Color.FromArgb(75, 90, 105) : Color.FromArgb(27, 39, 51), Tag = muted ? "ModernMuted" : null, AutoEllipsis = true }; }
        internal static void Apply(Control root, ISet<Control> primary = null, Control colorPicker = null)
        {
            var theme = InterfaceAppearance.ModernTheme();
            foreach (Control c in root.Controls)
            {
                if (c.GetType().Name.Contains("Canvas") || c is PictureBox) continue;
                if (!(c is PictureBox)) c.Font = Font(Math.Max(9, c.Font.Size), c.Font.Style);
                if (c is Button)
                {
                    var b = (Button)c; bool active = primary != null && primary.Contains(c);
                    b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderSize = 1;
                    b.FlatAppearance.BorderColor = active ? Accent : theme.BorderColor;
                    b.FlatAppearance.MouseOverBackColor = active ? Color.FromArgb(154, 52, 18) : Color.FromArgb(255, 241, 231);
                    if (c != colorPicker) { b.BackColor = active ? Accent : Color.White; b.ForeColor = active ? Color.White : theme.TextColor; }
                    b.Font = Font(9.5f, FontStyle.Bold); b.Cursor = Cursors.Hand;
                }
                else if (c is TextBoxBase)
                {
                    var input = (TextBoxBase)c; input.BorderStyle = BorderStyle.FixedSingle;
                    input.Font = Font(9.5f);
                    input.BackColor = input.ReadOnly ? theme.ReadOnlyBg : Color.White; input.ForeColor = theme.TextColor;
                    if (c is TextBox && !((TextBox)c).Multiline) ((TextBox)c).AutoSize = false;
                }
                else if (c is ComboBox || c is NumericUpDown || c is ListBox)
                { c.BackColor = Color.White; c.ForeColor = theme.TextColor; c.Font = Font(9.5f); }
                else if (c is Label || c is CheckBox || c is RadioButton)
                { c.ForeColor = object.Equals(c.Tag, "ModernMuted") ? theme.MutedTextColor : theme.TextColor; c.BackColor = Color.Transparent;
                    if (!(c is Label)) c.Font = Font(9.5f); }
                else if (c is DataGridView)
                {
                    var grid = (DataGridView)c; grid.EnableHeadersVisualStyles = false; grid.BackgroundColor = Color.White;
                    grid.BorderStyle = BorderStyle.None; grid.GridColor = theme.BorderColor;
                    grid.ColumnHeadersDefaultCellStyle.BackColor = theme.ReadOnlyBg;
                    grid.ColumnHeadersDefaultCellStyle.ForeColor = theme.TextColor;
                    grid.ColumnHeadersDefaultCellStyle.Font = Font(9, FontStyle.Bold);
                    grid.DefaultCellStyle.BackColor = Color.White; grid.DefaultCellStyle.ForeColor = theme.TextColor;
                    grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(255, 233, 213);
                    grid.DefaultCellStyle.SelectionForeColor = theme.TextColor;
                    grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 252);
                }
                else if (c is Panel || c is GroupBox || c is SplitContainer || c is TabPage)
                { c.BackColor = c is ModernFormCard || c is GroupBox ? Color.White : theme.BgLight; c.ForeColor = theme.TextColor; }
                if (!(c is NumericUpDown) && !(c is TextBoxBase) && !(c is DataGridView)) Apply(c, primary, colorPicker);
                var frame = c as CollageTextField;
                if (frame != null)
                {
                    frame.BackColor = frame.Controls.OfType<TextBox>().Any(t => t.ReadOnly) ? theme.ReadOnlyBg : Color.White;
                    frame.BorderColor = theme.BorderColor; frame.ForeColor = Accent; frame.MutedColor = theme.MutedTextColor;
                    foreach (TextBox text in frame.Controls.OfType<TextBox>()) { text.BorderStyle = BorderStyle.None; text.BackColor = frame.BackColor; }
                }
                if (c is ModernNumericField) { c.BackColor = Color.White; foreach (NumericUpDown n in c.Controls.OfType<NumericUpDown>()) n.BorderStyle = BorderStyle.None; }
            }
        }
    }
}
