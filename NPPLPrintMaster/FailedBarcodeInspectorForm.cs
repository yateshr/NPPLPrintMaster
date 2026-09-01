using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace NPPLPrintMaster
{
    public sealed class FailedBarcodeInspectorForm : Form
    {
        private readonly ListBox list = new ListBox();
        private readonly PictureBox preview = new PictureBox();
        private readonly Label pathLabel = new Label();

        public FailedBarcodeInspectorForm(string typeName, IList<string> files)
        {
            Text = typeName + " - Failed Barcode Inspection";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1000, 680);

            Label top = new Label
            {
                Dock = DockStyle.Top, Height = 42, Padding = new Padding(12,10,0,0),
                Text = string.Format("{0} image(s) could not be decoded.", files == null ? 0 : files.Count),
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };

            SplitContainer split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 350 };
            list.Dock = DockStyle.Fill; list.HorizontalScrollbar = true;
            if (files != null) foreach (string f in files) list.Items.Add(new Item(f));

            preview.Dock = DockStyle.Fill; preview.SizeMode = PictureBoxSizeMode.Zoom;
            preview.BackColor = Color.FromArgb(35,39,43); preview.BorderStyle = BorderStyle.FixedSingle;
            pathLabel.Dock = DockStyle.Bottom; pathLabel.Height = 45; pathLabel.AutoEllipsis = true;
            Panel buttons = new Panel { Dock = DockStyle.Bottom, Height = 40 };
            Button large = new Button { Text = "Large Preview", Location = new Point(6,5), Size = new Size(120,30) };
            Button folder = new Button { Text = "Open File Location", Location = new Point(132,5), Size = new Size(145,30) };
            buttons.Controls.Add(large); buttons.Controls.Add(folder);

            split.Panel1.Controls.Add(list);
            split.Panel2.Controls.Add(preview); split.Panel2.Controls.Add(buttons); split.Panel2.Controls.Add(pathLabel);
            Controls.Add(split); Controls.Add(top);

            list.SelectedIndexChanged += (s,e) => ShowSelected();
            list.DoubleClick += (s,e) => OpenLarge();
            large.Click += (s,e) => OpenLarge();
            folder.Click += (s,e) =>
            {
                Item x = list.SelectedItem as Item;
                if (x == null || !File.Exists(x.Path)) return;
                try { Process.Start("explorer.exe", "/select,\"" + x.Path + "\""); } catch { }
            };
            FormClosed += (s,e) => SetImage(null);
            if (list.Items.Count > 0) list.SelectedIndex = 0;
        }

        private void ShowSelected()
        {
            Item x = list.SelectedItem as Item;
            pathLabel.Text = x == null ? "" : x.Path;
            SetImage(x == null ? null : x.Path);
        }

        private void SetImage(string path)
        {
            Image old = preview.Image; preview.Image = null; if (old != null) old.Dispose();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
            try { using (Image i = Image.FromFile(path)) preview.Image = new Bitmap(i); } catch { }
        }

        private void OpenLarge()
        {
            Item x = list.SelectedItem as Item;
            if (x == null || !File.Exists(x.Path)) return;
            using (SmartImageInspectorForm f = new SmartImageInspectorForm(x.Path)) f.ShowDialog(this);
        }

        private sealed class Item
        {
            public Item(string path) { Path = path; }
            public string Path { get; private set; }
            public override string ToString() { return System.IO.Path.GetFileName(Path); }
        }
    }
}
