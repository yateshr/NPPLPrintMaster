using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace NPPLPrintMaster
{
    public sealed class SmartImageInspectorForm : Form
    {
        private readonly string filePath;
        private readonly Panel viewport;
        private readonly PictureBox picture;
        private readonly Label lblZoom;
        private Image source;
        private double zoom = 1.0;
        private bool dragging;
        private Point dragStart, scrollStart;

        public SmartImageInspectorForm(string filePath, string code = "")
        {
            this.filePath = filePath;
            Text = "Image Inspector";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1100, 800);
            MinimumSize = new Size(760, 560);
            KeyPreview = true;

            ToolStrip bar = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden };
            ToolStripButton fit = new ToolStripButton("Fit");
            ToolStripButton one = new ToolStripButton("100%");
            ToolStripButton minus = new ToolStripButton("−");
            ToolStripButton plus = new ToolStripButton("+");
            bar.Items.Add(fit); bar.Items.Add(one); bar.Items.Add(minus); bar.Items.Add(plus);
            bar.Items.Add(new ToolStripSeparator());
            bar.Items.Add(new ToolStripLabel(Path.GetFileName(filePath)));
            if (!string.IsNullOrWhiteSpace(code))
            {
                bar.Items.Add(new ToolStripSeparator());
                bar.Items.Add(new ToolStripLabel("Code: " + code));
            }

            lblZoom = new Label
            {
                Dock = DockStyle.Bottom, Height = 30, Padding = new Padding(10, 0, 0, 0),
                TextAlign = ContentAlignment.MiddleLeft
            };
            viewport = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.FromArgb(35, 39, 43) };
            picture = new PictureBox { Location = Point.Empty, SizeMode = PictureBoxSizeMode.StretchImage, Cursor = Cursors.Hand };
            viewport.Controls.Add(picture);
            Controls.Add(viewport); Controls.Add(lblZoom); Controls.Add(bar);

            Load += (s, e) => { LoadImage(); Fit(); };
            FormClosed += (s, e) => { picture.Image = null; if (source != null) source.Dispose(); };
            fit.Click += (s, e) => Fit();
            one.Click += (s, e) => SetZoom(1.0);
            minus.Click += (s, e) => SetZoom(zoom / 1.2);
            plus.Click += (s, e) => SetZoom(zoom * 1.2);
            viewport.MouseWheel += Wheel; picture.MouseWheel += Wheel;
            picture.MouseEnter += (s, e) => picture.Focus();
            picture.MouseDown += (s, e) =>
            {
                if (e.Button != MouseButtons.Left) return;
                dragging = true; dragStart = e.Location;
                scrollStart = new Point(-viewport.AutoScrollPosition.X, -viewport.AutoScrollPosition.Y);
            };
            picture.MouseMove += (s, e) =>
            {
                if (!dragging) return;
                viewport.AutoScrollPosition = new Point(
                    Math.Max(0, scrollStart.X - (e.X - dragStart.X)),
                    Math.Max(0, scrollStart.Y - (e.Y - dragStart.Y)));
            };
            picture.MouseUp += (s, e) => dragging = false;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
        }

        private void LoadImage()
        {
            if (!File.Exists(filePath)) { Close(); return; }
            using (Image i = Image.FromFile(filePath)) source = new Bitmap(i);
            picture.Image = source;
        }

        private void Fit()
        {
            if (source == null) return;
            double zx = (double)Math.Max(100, viewport.ClientSize.Width - 30) / source.Width;
            double zy = (double)Math.Max(100, viewport.ClientSize.Height - 30) / source.Height;
            SetZoom(Math.Min(zx, zy));
        }

        private void SetZoom(double z)
        {
            if (source == null) return;
            zoom = Math.Max(0.05, Math.Min(8.0, z));
            picture.Size = new Size(
                Math.Max(1, (int)Math.Round(source.Width * zoom)),
                Math.Max(1, (int)Math.Round(source.Height * zoom)));
            lblZoom.Text = string.Format("{0:0}%   |   Mouse wheel = zoom   |   Drag = pan", zoom * 100);
        }

        private void Wheel(object sender, MouseEventArgs e) { SetZoom(zoom * (e.Delta > 0 ? 1.15 : 1.0 / 1.15)); }
    }
}
