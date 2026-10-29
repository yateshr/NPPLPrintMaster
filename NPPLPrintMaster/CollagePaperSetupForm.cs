using System;
using System.Drawing;
using System.Windows.Forms;

namespace NPPLPrintMaster
{
    internal sealed class CollagePaperSetupForm : Form
    {
        private readonly ComboBox _paper = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly ComboBox _orientation = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly NumericUpDown _shortSide = CreateDimension();
        private readonly NumericUpDown _longSide = CreateDimension();
        private readonly Label _dimensions = new Label { AutoSize = true };
        private readonly Label _customHint = new Label { AutoSize = true };
        public CollagePageSetup ResultPage { get; private set; }

        public CollagePaperSetupForm(CollagePageSetup page)
        {
            ResultPage = page.Clone();
            ResultPage.Normalize();
            Text = "PDF paper size and orientation";
            ClientSize = new Size(460, 310);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Font = SystemFonts.MessageBoxFont;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16),
                ColumnCount = 2, RowCount = 7 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 7; i++) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, i == 4 ? 48 : 36));
            _paper.Items.AddRange(new object[] { "A4", "A3", "A2", "Custom" });
            _orientation.Items.AddRange(new object[] { "Portrait", "Landscape" });
            _paper.SelectedItem = ResultPage.PaperSize;
            _orientation.SelectedIndex = ResultPage.Landscape ? 1 : 0;
            _shortSide.Value = (decimal)Math.Min(ResultPage.CustomWidthMm, ResultPage.CustomHeightMm);
            _longSide.Value = (decimal)Math.Max(ResultPage.CustomWidthMm, ResultPage.CustomHeightMm);
            AddRow(layout, "Paper size", _paper, 0);
            AddRow(layout, "Orientation", _orientation, 1);
            AddRow(layout, "Short side (mm)", _shortSide, 2);
            AddRow(layout, "Long side (mm)", _longSide, 3);
            _customHint.Text = "Custom sides: 50 to 2,000 mm.\nLandscape places the longer side horizontally.";
            layout.Controls.Add(_customHint, 0, 4); layout.SetColumnSpan(_customHint, 2);
            layout.Controls.Add(_dimensions, 0, 5); layout.SetColumnSpan(_dimensions, 2);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 90 };
            var apply = new Button { Text = "Apply", Width = 90 };
            apply.Click += (s, e) => { UpdatePage(); DialogResult = DialogResult.OK; Close(); };
            buttons.Controls.Add(cancel); buttons.Controls.Add(apply);
            layout.Controls.Add(buttons, 0, 6); layout.SetColumnSpan(buttons, 2);
            Controls.Add(layout);
            AcceptButton = apply; CancelButton = cancel;
            _paper.SelectedIndexChanged += (s, e) => UpdatePage();
            _orientation.SelectedIndexChanged += (s, e) => UpdatePage();
            _shortSide.ValueChanged += (s, e) => UpdatePage();
            _longSide.ValueChanged += (s, e) => UpdatePage();
            UpdatePage();
        }

        private static NumericUpDown CreateDimension()
        {
            return new NumericUpDown { Minimum = (decimal)CollagePageSetup.MinCustomSideMm,
                Maximum = (decimal)CollagePageSetup.MaxCustomSideMm, DecimalPlaces = 2, Increment = 1 };
        }

        private static void AddRow(TableLayoutPanel layout, string text, Control control, int row)
        {
            layout.Controls.Add(new Label { Text = text, AutoSize = true, Padding = new Padding(0, 5, 0, 0) }, 0, row);
            control.Dock = DockStyle.Fill;
            layout.Controls.Add(control, 1, row);
        }

        private void UpdatePage()
        {
            ResultPage.PaperSize = _paper.SelectedItem.ToString();
            ResultPage.Landscape = _orientation.SelectedIndex == 1;
            ResultPage.CustomWidthMm = (float)_shortSide.Value;
            ResultPage.CustomHeightMm = (float)_longSide.Value;
            ResultPage.Normalize();
            bool custom = ResultPage.PaperSize == "Custom";
            _shortSide.Enabled = custom; _longSide.Enabled = custom; _customHint.Enabled = custom;
            _dimensions.Text = "Sheet: " + ResultPage.PaperDescription;
        }
    }
}
