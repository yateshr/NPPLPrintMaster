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
        private string SelectFolderModern(string memoryKey)
        {
            return DialogMemoryManager.SelectFolder(memoryKey);
        }

        private string GetLastLaneFolder(string laneTitle)
        {
            string folder = "";

            if (laneTitle.StartsWith("Lane 1", StringComparison.OrdinalIgnoreCase))
                folder = currentSettings.LastProductFolder;
            else if (laneTitle.StartsWith("Lane 2", StringComparison.OrdinalIgnoreCase))
                folder = currentSettings.LastCartonFolder;
            else if (laneTitle.StartsWith("Lane 3", StringComparison.OrdinalIgnoreCase))
                folder = currentSettings.LastOtherFolder;

            return !string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder)
                ? folder
                : "";
        }

        private void SaveLastLaneFolder(string laneTitle, string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                return;

            if (laneTitle.StartsWith("Lane 1", StringComparison.OrdinalIgnoreCase))
                currentSettings.LastProductFolder = folder;
            else if (laneTitle.StartsWith("Lane 2", StringComparison.OrdinalIgnoreCase))
                currentSettings.LastCartonFolder = folder;
            else if (laneTitle.StartsWith("Lane 3", StringComparison.OrdinalIgnoreCase))
                currentSettings.LastOtherFolder = folder;
            else
                return;

            SettingsManager.Save(currentSettings);
        }

        private FlowLayoutPanel CreateLane(string title, int xPos, Panel parent)
        {
            Panel laneWrapper = new Panel { Location = new Point(xPos, 20), Size = new Size(310, 320), BackColor = Color.FromArgb(50, 52, 59) };
            Label lbl = new Label { Text = title, Location = new Point(10, 15), Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = Color.FromArgb(252, 213, 53), AutoSize = true };
            FlowLayoutPanel panel = new FlowLayoutPanel { Location = new Point(10, 45), Size = new Size(290, 230), AllowDrop = true, AutoScroll = true, BackColor = Color.FromArgb(38, 40, 46), BorderStyle = BorderStyle.FixedSingle, Cursor = Cursors.Hand };
            Button btnBrowse = new Button { Text = "📂 Browse Files...", Location = new Point(10, 280), Size = new Size(290, 30), Font = new Font("Segoe UI", 9, FontStyle.Bold), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(28, 29, 33), ForeColor = Color.White };
            btnBrowse.FlatAppearance.BorderSize = 0;

            void OpenBrowse(object s, EventArgs e)
            {
                using (OpenFileDialog ofd = new OpenFileDialog
                {
                    Multiselect = true,
                    Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp"
                })
                {
                    string lastFolder = GetLastLaneFolder(title);

                    string laneMemoryKey =
                        title.StartsWith("Lane 1", StringComparison.OrdinalIgnoreCase)
                            ? "QuickJobCard.Lane1Products"
                            : title.StartsWith("Lane 2", StringComparison.OrdinalIgnoreCase)
                                ? "QuickJobCard.Lane2Cartons"
                                : "QuickJobCard.Lane3Others";

                    if (!string.IsNullOrWhiteSpace(lastFolder))
                        ofd.InitialDirectory = lastFolder;

                    if (DialogMemoryManager.ShowOpenDialog(
                        ofd,
                        laneMemoryKey) != DialogResult.OK)
                    {
                        return;
                    }

                    if (ofd.FileNames.Length > 0)
                        SaveLastLaneFolder(title, Path.GetDirectoryName(ofd.FileNames[0]));

                    foreach (string f in ofd.FileNames)
                        AddThumbnail(panel, f);
                }
            }
            panel.Click += OpenBrowse; btnBrowse.Click += OpenBrowse;
            panel.DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
            panel.DragDrop += (s, e) =>
            {
                string[] items = (string[])e.Data.GetData(DataFormats.FileDrop);

                foreach (string item in items)
                {
                    if (Directory.Exists(item))
                    {
                        SaveLastLaneFolder(title, item);

                        foreach (string f in Directory.GetFiles(item))
                        {
                            string ext = Path.GetExtension(f);
                            if (ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                                ext.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                                ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase))
                            {
                                AddThumbnail(panel, f);
                            }
                        }
                    }
                    else if (File.Exists(item))
                    {
                        SaveLastLaneFolder(title, Path.GetDirectoryName(item));
                        AddThumbnail(panel, item);
                    }
                }
            };

            laneWrapper.Controls.Add(lbl);
            laneWrapper.Controls.Add(panel);
            laneWrapper.Controls.Add(btnBrowse);
            parent.Controls.Add(laneWrapper);

            return panel;
        }

        private void AddThumbnail(FlowLayoutPanel panel, string filePath)
        {
            try
            {
                if (panel == null || !File.Exists(filePath))
                    return;

                using (Image img = Image.FromFile(filePath))
                {
                    int picW = Math.Max(50, panel.ClientSize.Width - 30);
                    int picH = Math.Max(
                        1,
                        (int)Math.Round(
                            img.Height * ((double)picW / Math.Max(1, img.Width))));

                    Bitmap thumbnail = new Bitmap(picW, picH);

                    using (Graphics g = Graphics.FromImage(thumbnail))
                    {
                        g.Clear(Color.White);
                        g.InterpolationMode =
                            System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.DrawImage(
                            img,
                            new Rectangle(0, 0, picW, picH),
                            0,
                            0,
                            img.Width,
                            img.Height,
                            GraphicsUnit.Pixel);
                    }

                    PictureBox pb = new PictureBox
                    {
                        Image = thumbnail,
                        Size = new Size(picW, picH),
                        SizeMode = PictureBoxSizeMode.Zoom,
                        Tag = filePath,
                        Cursor = Cursors.Hand,
                        Margin = new Padding(5)
                    };

                    ContextMenuStrip cms = new ContextMenuStrip();
                    ToolStripMenuItem removeItem =
                        new ToolStripMenuItem("❌ Remove Image");

                    removeItem.Click += (s, e) =>
                    {
                        if (pb.IsDisposed)
                            return;

                        panel.SuspendLayout();

                        try
                        {
                            Image oldImage = pb.Image;
                            pb.Image = null;
                            pb.ContextMenuStrip = null;

                            if (panel.Controls.Contains(pb))
                                panel.Controls.Remove(pb);

                            pb.Dispose();
                            oldImage?.Dispose();
                            cms.Dispose();
                        }
                        finally
                        {
                            panel.ResumeLayout(true);
                        }
                    };

                    cms.Items.Add(removeItem);
                    pb.ContextMenuStrip = cms;

                    ToolTip tt = new ToolTip();
                    tt.SetToolTip(pb, "Right-click the image to remove it.");
                    pb.Disposed += (s, e) => tt.Dispose();

                    panel.Controls.Add(pb);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Unable to add image thumbnail.\n\n" +
                    Path.GetFileName(filePath) +
                    "\n\n" +
                    ex.Message,
                    "Thumbnail Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void ClearLane(FlowLayoutPanel panel) { foreach (Control c in panel.Controls.Cast<Control>().ToList()) { if (c is PictureBox pb) { pb.Image?.Dispose(); pb.Dispose(); } } panel.Controls.Clear(); }

        private List<string> GetFilesFromLane(FlowLayoutPanel panel) { List<string> files = new List<string>(); foreach (Control c in panel.Controls) { if (c is PictureBox pb && pb.Tag != null) files.Add(pb.Tag.ToString()); } return files; }

        private void BtnRunBlock3_Click(object sender, EventArgs e)
        {
            var lanes = new[] { GetFilesFromLane(pnlProducts), GetFilesFromLane(pnlCartons), GetFilesFromLane(pnlOthers) };
            if (lanes[0].Count == 0 && lanes[1].Count == 0 && lanes[2].Count == 0)
            {
                MessageBox.Show("Please add images to at least one lane first.", "No Images", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            this.Cursor = Cursors.WaitCursor;
            try
            {
                int colWidth = 1000, padding = 60, boxHeight = 120;
                int[] colX = { padding, (padding * 2) + colWidth, (padding * 3) + (colWidth * 2) };
                int[] colY = { padding, padding, padding };

                List<CanvasItem> startingItems = new List<CanvasItem>();

                for (int c = 0; c < 3; c++)
                {
                    foreach (string file in lanes[c])
                    {
                        if (!File.Exists(file)) continue;
                        Image heavyImg = Image.FromFile(file);

                        double scale = (c < 2) ? (double)colWidth / heavyImg.Width : Math.Min(1.0, (double)colWidth / heavyImg.Width);
                        int drawW = (c < 2) ? colWidth : (int)Math.Round(heavyImg.Width * scale);
                        int drawH = (int)Math.Round(heavyImg.Height * scale);

                        Bitmap lightImg = new Bitmap(heavyImg, new Size(drawW, drawH));
                        lightImg.SetResolution(heavyImg.HorizontalResolution, heavyImg.VerticalResolution);
                        heavyImg.Dispose();

                        int xPos = (c == 0) ? colX[0] : (c == 1) ? colX[1] : colX[2] + ((colWidth - drawW) / 2);
                        int yPos = colY[c];

                        string mem = customLayoutMemory.FirstOrDefault(m => m.StartsWith(file + "|"));
                        if (mem != null)
                        {
                            var parts = mem.Split('|');
                            if (parts.Length == 5) { xPos = int.Parse(parts[1]); yPos = int.Parse(parts[2]); drawW = int.Parse(parts[3]); drawH = int.Parse(parts[4]); }
                        }

                        string textTemplate = ""; bool drawBox = false;
                        if (c == 0 && chkShowText1.Checked) { textTemplate = txtLane1Text.Text; drawBox = true; }
                        else if (c == 1 && chkShowText2.Checked) { textTemplate = txtLane2Text.Text; drawBox = true; }

                        startingItems.Add(new CanvasItem
                        {
                            FilePath = file,
                            Img = lightImg,
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

                int totalW = (colWidth * 3) + (padding * 4);
                int totalH = Math.Max(1500, colY.Max() + padding);

                // Capture the exact current three-lane workspace once.
                WorkspaceData jobCardWorkspace =
                    BuildCurrentWorkspaceData();

                bool openDirectlyInFreeform =
                    chkOpenDirectFreeform != null
                        ? chkOpenDirectFreeform.Checked
                        : currentSettings.OpenJobCardDirectlyInFreeform;

                if (openDirectlyInFreeform)
                {
                    this.Cursor = Cursors.Default;

                    FreeformBuilderForm builder =
                        new FreeformBuilderForm(
                            startingItems,
                            jobCardWorkspace,
                            currentSettings,
                            GetLastJobCardSaveFolder());

                    builder.WindowState = FormWindowState.Maximized;
                    builder.ShowDialog();
                    return;
                }

                Bitmap previewBitmap = new Bitmap(totalW, totalH);
                previewBitmap.SetResolution(300, 300);

                using (Graphics g = Graphics.FromImage(previewBitmap))
                {
                    g.Clear(Color.White);
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

                    using (Pen thickPen = new Pen(Color.Black, 12))
                        g.DrawRectangle(thickPen, padding / 2, padding / 2, totalW - padding, totalH - padding);

                    using (Pen boxPen = new Pen(Color.Black, 6))
                    using (SolidBrush brush = new SolidBrush(Color.Black))
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        foreach (var item in startingItems)
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

                                    using (Font testFont = new Font(item.ItemFont.FontFamily, 100f, item.ItemFont.Style))
                                    {
                                        SizeF testSize = g.MeasureString(liveText, testFont);
                                        float finalSize = Math.Max(1f, 100f * Math.Min(maxTextW / testSize.Width, maxTextH / testSize.Height));

                                        using (Font autoFont = new Font(item.ItemFont.FontFamily, finalSize, item.ItemFont.Style))
                                        {
                                            g.DrawString(liveText, autoFont, brush, new RectangleF(item.X, bY, item.Width, dynBoxHeight), sf);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                this.Cursor = Cursors.Default;

                JobCardPreviewForm previewForm = new JobCardPreviewForm(
                    previewBitmap,
                    startingItems,
                    jobCardWorkspace,
                    currentSettings,
                    GetLastJobCardSaveFolder());

                previewForm.FormClosed += (s, ev) =>
                {
                    // If they customized it, save back the custom layout memory
                    // (Note: previewForm closes or hides when customize is clicked, but this catches closure)
                };

                previewForm.ShowDialog();
            }
            catch (Exception ex)
            {
                this.Cursor = Cursors.Default;
                MessageBox.Show("Error generating preview: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string GetLastJobCardSaveFolder()
        {
            if (!string.IsNullOrWhiteSpace(currentSettings.LastJobCardSaveFolder) &&
                Directory.Exists(currentSettings.LastJobCardSaveFolder))
            {
                return currentSettings.LastJobCardSaveFolder;
            }

            return "";
        }

        private string GetNpplStorageFolder()
        {
            // Use the exact physical path requested:
            // C:\Users\<user>\Documents\NPPLPrintMaster
            string userProfile =
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            string folder =
                Path.Combine(userProfile, "Documents", "NPPLPrintMaster");

            Directory.CreateDirectory(folder);
            return folder;
        }

    }
}
