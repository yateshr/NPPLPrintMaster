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
        private void BuildSmartImageFinder()
        {
            GroupBox grp = new GroupBox
            {
                Text = "Smart Image Finder V1.2 (Optional)",
                Location = new Point(20, 520),
                Size = new Size(970, 545),
                Font = new Font("Segoe UI", 11, FontStyle.Bold)
            };

            Label hint = new Label
            {
                Text = "Scanner-ready: scan or type a code — matching preview appears automatically. Click preview for large inspector.",
                Location = new Point(20, 30),
                Size = new Size(920, 22),
                Font = new Font("Segoe UI", 9)
            };

            Label lp = new Label
            {
                Text = "Product Barcode Directory:",
                Location = new Point(20, 67),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };

            Label lc = new Label
            {
                Text = "Carton Barcode Directory:",
                Location = new Point(20, 102),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };

            // Compact read-only path fields. Keep the complete physical path in
            // Text so indexing/watchers continue using exactly the same value.
            txtSmartProductFolder = new TextBox
            {
                Location = new Point(215, 62),
                Size = new Size(485, 27),
                ReadOnly = true,
                TabStop = false,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 8.25f, FontStyle.Regular),
                Text = currentSettings.SmartProductImageFolder ?? ""
            };

            txtSmartCartonFolder = new TextBox
            {
                Location = new Point(215, 97),
                Size = new Size(485, 27),
                ReadOnly = true,
                TabStop = false,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 8.25f, FontStyle.Regular),
                Text = currentSettings.SmartCartonImageFolder ?? ""
            };

            // Hovering either compact path field reveals the complete directory.
            smartFolderToolTip = new ToolTip
            {
                InitialDelay = 350,
                ReshowDelay = 100,
                AutoPopDelay = 12000,
                ShowAlways = true
            };
            smartFolderToolTip.SetToolTip(txtSmartProductFolder, txtSmartProductFolder.Text);
            smartFolderToolTip.SetToolTip(txtSmartCartonFolder, txtSmartCartonFolder.Text);

            txtSmartProductFolder.TextChanged +=
                (s, e) => smartFolderToolTip.SetToolTip(
                    txtSmartProductFolder,
                    txtSmartProductFolder.Text);

            txtSmartCartonFolder.TextChanged +=
                (s, e) => smartFolderToolTip.SetToolTip(
                    txtSmartCartonFolder,
                    txtSmartCartonFolder.Text);
            Button bp = new Button { Text = "Folder", Location = new Point(710, 61), Size = new Size(75, 29), FlatStyle = FlatStyle.Flat };
            Button bc = new Button { Text = "Folder", Location = new Point(710, 96), Size = new Size(75, 29), FlatStyle = FlatStyle.Flat };
            btnSmartRefresh = new Button { Text = "↻ Incremental Refresh", Location = new Point(795, 61), Size = new Size(155, 29), FlatStyle = FlatStyle.Flat };
            chkSmartMonitor = new CheckBox { Text = "Monitor folders automatically", Location = new Point(795, 99), Size = new Size(170, 24), Checked = true, Font = new Font("Segoe UI", 8) };
            lblSmartStatus = new Label
            {
                Text = "● READY   " + GetSmartIndexSummary(),
                Location = new Point(20, 132),
                Size = new Size(930, 20),
                Font = new Font("Segoe UI", 8.25f, FontStyle.Regular)
            };

            lblSmartHealth = new Label
            {
                Text = GetSmartHealthSummary(),
                Location = new Point(20, 154),
                Size = new Size(720, 22),
                Font = new Font("Segoe UI", 8.25f, FontStyle.Bold)
            };

            Button bfp = new Button
            {
                Text = "Product Failures",
                Location = new Point(755, 150),
                Size = new Size(95, 28),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8)
            };

            Button bfc = new Button
            {
                Text = "Carton Failures",
                Location = new Point(855, 150),
                Size = new Size(95, 28),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8)
            };

            GroupBox gp = CreateSmartSearchGroup("Product Barcode / EAN", new Point(20, 190), false);
            GroupBox gc = CreateSmartSearchGroup("Carton SAP Code", new Point(490, 190), true);

            bp.Click += (s, e) => SelectSmartFolder(false);
            bc.Click += (s, e) => SelectSmartFolder(true);
            btnSmartRefresh.Click += async (s, e) => await RefreshSmartIndexesAsync(false, false);
            chkSmartMonitor.CheckedChanged += (s, e) => SetupSmartWatchers();
            bfp.Click += (s, e) => ShowFailures("Product", productImageIndex);
            bfc.Click += (s, e) => ShowFailures("Carton", cartonImageIndex);

            grp.Controls.AddRange(new Control[] { hint, lp, lc, txtSmartProductFolder, txtSmartCartonFolder, bp, bc, btnSmartRefresh, chkSmartMonitor, lblSmartStatus, lblSmartHealth, bfp, bfc, gp, gc });
            pageCompose.Controls.Add(grp);
            pageCompose.AutoScrollMinSize = new Size(0, 1095);

            smartMonitorTimer = new System.Windows.Forms.Timer { Interval = 2500 };
            smartMonitorTimer.Tick += async (s, e) =>
            {
                smartMonitorTimer.Stop();
                if (!smartRefreshRunning && chkSmartMonitor.Checked)
                    await RefreshSmartIndexesAsync(false, true);
            };

            // Safety pass: catches a rare FileSystemWatcher event that Windows
            // may drop during a burst copy. Unchanged files reuse cached data.
            smartSafetyTimer = new System.Windows.Forms.Timer { Interval = 45000 };
            smartSafetyTimer.Tick += async (s, e) =>
            {
                if (!smartRefreshRunning && chkSmartMonitor != null && chkSmartMonitor.Checked)
                    await RefreshSmartIndexesAsync(false, true);
            };
            smartSafetyTimer.Start();
            SetupSmartWatchers();
        }

        private GroupBox CreateSmartSearchGroup(string title, Point location, bool carton)
        {
            GroupBox grp = new GroupBox
            {
                Text = title,
                Location = location,
                Size = new Size(460, 325),
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };

            // Scanner input is the main interaction point, so give it a little
            // more visual weight while keeping the existing auto-search logic.
            TextBox code = new TextBox
            {
                Location = new Point(15, 31),
                Size = new Size(295, 30),
                Font = new Font("Consolas", 11, FontStyle.Bold)
            };

            // Manual Find remains available as a compact fallback because
            // normal scanner/typing searches already happen automatically.
            Button find = new Button
            {
                Text = carton ? "Find Carton" : "Find Product",
                Location = new Point(320, 31),
                Size = new Size(120, 30),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };

            ListBox list = new ListBox
            {
                Location = new Point(15, 72),
                Size = new Size(210, 203),
                HorizontalScrollbar = true,
                Font = new Font("Segoe UI", 8.5f)
            };

            PictureBox pic = new PictureBox
            {
                Location = new Point(235, 72),
                Size = new Size(205, 203),
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.White,
                Cursor = Cursors.Hand
            };

            // A quiet empty-state message makes the preview area feel
            // intentional instead of looking like an unused white box.
            pic.Paint += (s, e) =>
            {
                PictureBox preview = s as PictureBox;
                if (preview == null || preview.Image != null) return;

                string emptyText = carton
                    ? "No Carton Image\r\nScan or enter SAP"
                    : "No Product Image\r\nScan or enter EAN";

                TextRenderer.DrawText(
                    e.Graphics,
                    emptyText,
                    new Font("Segoe UI", 9, FontStyle.Regular),
                    preview.ClientRectangle,
                    Color.DimGray,
                    TextFormatFlags.HorizontalCenter |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.WordBreak);
            };

            Label tip = new Label
            {
                Text = "Click preview for large inspector",
                Location = new Point(235, 278),
                Size = new Size(205, 17),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 7.5f)
            };

            Button add = new Button
            {
                Text = carton ? "Add to Carton Lane" : "Add to Product Lane",
                Location = new Point(235, 296),
                Size = new Size(205, 27),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };

            if (smartFolderToolTip != null)
            {
                smartFolderToolTip.SetToolTip(
                    code,
                    carton
                        ? "Scanner-ready: scan or type Carton SAP code. Search runs automatically."
                        : "Scanner-ready: scan or type Product EAN. Search runs automatically.");

                smartFolderToolTip.SetToolTip(
                    find,
                    "Manual fallback search. Normal typing/scanning searches automatically.");
            }

            if (carton) { txtSmartCartonCode = code; lstSmartCartonResults = list; pbSmartCartonPreview = pic; }
            else { txtSmartProductCode = code; lstSmartProductResults = list; pbSmartProductPreview = pic; }

            SmartImageFinderEngine searchIndex = carton ? cartonImageIndex : productImageIndex;
            string searchType = carton ? "Carton" : "Product";

            find.Click += (s, e) => FindSmartImages(searchIndex, code.Text, list, pic, searchType);

            System.Windows.Forms.Timer searchTimer = new System.Windows.Forms.Timer { Interval = 300 };
            searchTimer.Tick += (s, e) =>
            {
                searchTimer.Stop();
                if (!string.IsNullOrWhiteSpace(code.Text))
                    FindSmartImages(searchIndex, code.Text, list, pic, searchType);
            };

            if (carton) smartCartonSearchTimer = searchTimer;
            else smartProductSearchTimer = searchTimer;

            code.TextChanged += (s, e) =>
            {
                searchTimer.Stop();

                if (string.IsNullOrWhiteSpace(code.Text))
                {
                    ClearSmartSearch(list, pic, searchType);
                    return;
                }

                // Short debounce: typing does not search on every key,
                // while a USB scanner still feels immediate.
                searchTimer.Start();
            };

            code.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;
                searchTimer.Stop();
                if (!string.IsNullOrWhiteSpace(code.Text))
                    FindSmartImages(searchIndex, code.Text, list, pic, searchType);
                code.SelectAll();
                code.Focus();
            };
            list.SelectedIndexChanged += (s, e) => PreviewSmartResult(list, pic);
            list.DoubleClick += (s, e) => OpenInspector(list, code.Text);
            pic.Click += (s, e) => OpenInspector(list, code.Text);
            add.Click += (s, e) => AddSmartResultToLane(list, carton ? pnlCartons : pnlProducts, carton ? "Carton" : "Product");

            grp.Controls.AddRange(new Control[] { code, find, list, pic, tip, add });
            return grp;
        }

        private void SelectSmartFolder(bool carton)
        {
            string folder =
                SelectFolderModern(
                    carton
                        ? "SmartFinder.CartonFolder"
                        : "SmartFinder.ProductFolder");

            if (string.IsNullOrWhiteSpace(folder)) return;
            if (carton) { txtSmartCartonFolder.Text = folder; currentSettings.SmartCartonImageFolder = folder; }
            else { txtSmartProductFolder.Text = folder; currentSettings.SmartProductImageFolder = folder; }
            SettingsManager.Save(currentSettings);
            SetupSmartWatchers();
            lblSmartStatus.Text = (carton ? "Carton" : "Product") + " directory changed. Click Incremental Refresh.";
        }

        private async System.Threading.Tasks.Task RefreshSmartIndexesAsync(bool forceAll, bool automatic)
        {
            if (smartRefreshRunning) return;
            string pf = txtSmartProductFolder == null ? "" : txtSmartProductFolder.Text;
            string cf = txtSmartCartonFolder == null ? "" : txtSmartCartonFolder.Text;
            if (!Directory.Exists(pf) && !Directory.Exists(cf))
            {
                if (!automatic) MessageBox.Show("Select at least one valid Product or Carton image directory.", "Smart Image Finder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            smartRefreshRunning = true;
            btnSmartRefresh.Enabled = false;
            btnSmartRefresh.Text = automatic ? "Auto refreshing..." : "Refreshing...";
            try
            {
                SmartImageRefreshResult pr = null, cr = null;
                if (Directory.Exists(pf))
                    pr = await System.Threading.Tasks.Task.Run(() => productImageIndex.RefreshIncremental(pf, p => ReportProgress("Product", p), forceAll));
                if (Directory.Exists(cf))
                    cr = await System.Threading.Tasks.Task.Run(() => cartonImageIndex.RefreshIncremental(cf, p => ReportProgress("Carton", p), forceAll));

                if (Directory.Exists(pf)) currentSettings.SmartProductImageFolder = pf;
                if (Directory.Exists(cf)) currentSettings.SmartCartonImageFolder = cf;
                SettingsManager.Save(currentSettings);

                lblSmartStatus.Text = automatic ? "Folder change detected — index updated automatically." :
                    "Product: " + (pr == null ? "not scanned" : pr.ToString()) + "   |   Carton: " + (cr == null ? "not scanned" : cr.ToString());
                lblSmartHealth.Text = GetSmartHealthSummary();
                SetupSmartWatchers();

                // If a code is already in either search box, immediately re-run
                // it after an index update so a newly copied image can appear
                // without the operator touching the search field again.
                if (txtSmartProductCode != null &&
                    !string.IsNullOrWhiteSpace(txtSmartProductCode.Text))
                {
                    FindSmartImages(
                        productImageIndex,
                        txtSmartProductCode.Text,
                        lstSmartProductResults,
                        pbSmartProductPreview,
                        "Product");
                }

                if (txtSmartCartonCode != null &&
                    !string.IsNullOrWhiteSpace(txtSmartCartonCode.Text))
                {
                    FindSmartImages(
                        cartonImageIndex,
                        txtSmartCartonCode.Text,
                        lstSmartCartonResults,
                        pbSmartCartonPreview,
                        "Carton");
                }
            }
            catch (Exception ex)
            {
                lblSmartStatus.Text = "Index refresh failed: " + ex.Message;
                if (!automatic) MessageBox.Show(ex.Message, "Smart Image Finder Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                smartRefreshRunning = false;
                btnSmartRefresh.Text = "↻ Incremental Refresh";
                btnSmartRefresh.Enabled = true;
            }
        }

        private void ReportProgress(string type, SmartImageIndexProgress p)
        {
            if (p == null || (p.Processed != p.Total && p.Processed % 25 != 0)) return;
            try
            {
                BeginInvoke(new Action(() => lblSmartStatus.Text = string.Format(
                    "{0}: {1}/{2} checked | {3} unchanged | {4} decoded | {5} failed",
                    type, p.Processed, p.Total, p.Unchanged, p.Decoded, p.Failed)));
            }
            catch { }
        }

        private string GetSmartIndexSummary()
        {
            return string.Format("Product {0} image(s) / {1} code(s) | Carton {2} image(s) / {3} code(s)",
                productImageIndex.FileCount, productImageIndex.CodeCount, cartonImageIndex.FileCount, cartonImageIndex.CodeCount);
        }

        private string GetSmartHealthSummary()
        {
            return string.Format("Health: Product {0} decoded / {1} failed / {2} duplicate code(s) | Carton {3} decoded / {4} failed / {5} duplicate code(s)",
                productImageIndex.DecodedFileCount, productImageIndex.FailedFileCount, productImageIndex.DuplicateCodeCount,
                cartonImageIndex.DecodedFileCount, cartonImageIndex.FailedFileCount, cartonImageIndex.DuplicateCodeCount);
        }

        private void SetupSmartWatchers()
        {
            DisposeWatcher(ref smartProductWatcher); DisposeWatcher(ref smartCartonWatcher);
            if (chkSmartMonitor == null || !chkSmartMonitor.Checked) return;
            smartProductWatcher = CreateWatcher(txtSmartProductFolder == null ? "" : txtSmartProductFolder.Text);
            smartCartonWatcher = CreateWatcher(txtSmartCartonFolder == null ? "" : txtSmartCartonFolder.Text);
        }

        private FileSystemWatcher CreateWatcher(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return null;
            try
            {
                FileSystemWatcher w = new FileSystemWatcher(folder)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
                    InternalBufferSize = 64 * 1024
                };
                FileSystemEventHandler h = (s, e) => QueueAutoRefresh(e.FullPath);
                RenamedEventHandler r = (s, e) => QueueAutoRefresh(e.FullPath);
                ErrorEventHandler er = (s, e) => QueueAutoRefresh(folder);
                w.Created += h; w.Changed += h; w.Deleted += h; w.Renamed += r; w.Error += er; w.EnableRaisingEvents = true;
                return w;
            }
            catch { return null; }
        }

        private void QueueAutoRefresh(string path)
        {
            // Directory rename/delete events have no image extension, but can
            // affect many indexed files, so they also trigger the debounce.
            if (!string.IsNullOrWhiteSpace(path) &&
                Path.HasExtension(path) &&
                !IsSmartImage(path))
                return;
            try
            {
                BeginInvoke(new Action(() =>
                {
                    if (smartMonitorTimer == null) return;
                    smartMonitorTimer.Stop(); smartMonitorTimer.Start();
                    lblSmartStatus.Text = "Image-library change detected. Waiting for file copy to finish...";
                }));
            }
            catch { }
        }

        private bool IsSmartImage(string path)
        {
            string e = Path.GetExtension(path ?? "");
            return e.Equals(".png", StringComparison.OrdinalIgnoreCase) || e.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                   e.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) || e.Equals(".bmp", StringComparison.OrdinalIgnoreCase) ||
                   e.Equals(".tif", StringComparison.OrdinalIgnoreCase) || e.Equals(".tiff", StringComparison.OrdinalIgnoreCase);
        }

        private void DisposeWatcher(ref FileSystemWatcher w)
        {
            if (w == null) return;
            try { w.EnableRaisingEvents = false; w.Dispose(); } catch { }
            w = null;
        }

        private void ClearSmartSearch(ListBox list, PictureBox preview, string type)
        {
            if (list != null)
            {
                list.Items.Clear();
                list.ClearSelected();
            }

            SetSmartPreviewImage(preview, null);

            if (lblSmartStatus != null)
                lblSmartStatus.Text = type + " search cleared. Scanner ready.";
        }

        private void ClearAllLanesAndSmartFinder()
        {
            ClearLane(pnlProducts);
            ClearLane(pnlCartons);
            ClearLane(pnlOthers);

            if (smartProductSearchTimer != null) smartProductSearchTimer.Stop();
            if (smartCartonSearchTimer != null) smartCartonSearchTimer.Stop();

            if (txtSmartProductCode != null) txtSmartProductCode.Clear();
            if (txtSmartCartonCode != null) txtSmartCartonCode.Clear();

            if (lstSmartProductResults != null) lstSmartProductResults.Items.Clear();
            if (lstSmartCartonResults != null) lstSmartCartonResults.Items.Clear();

            SetSmartPreviewImage(pbSmartProductPreview, null);
            SetSmartPreviewImage(pbSmartCartonPreview, null);

            if (lblSmartStatus != null)
                lblSmartStatus.Text = "All lanes and Smart Finder codes cleared. Scanner ready.";
        }

        private void FindSmartImages(SmartImageFinderEngine index, string code, ListBox list, PictureBox preview, string type)
        {
            string normalized = SmartImageFinderEngine.NormalizeCode(code);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                ClearSmartSearch(list, preview, type);
                return;
            }

            List<string> matches = index.FindExact(normalized);
            list.Items.Clear();
            for (int i = 0; i < matches.Count; i++) list.Items.Add(new SmartImageResultItem(matches[i], i == 0 && matches.Count > 1));

            if (matches.Count == 0)
            {
                SetSmartPreviewImage(preview, null);
                lblSmartStatus.Text = type + " code " + normalized + " was not found.";
                return;
            }

            list.SelectedIndex = 0;
            lblSmartStatus.Text = matches.Count == 1
                ? type + " exact match found. Click preview to inspect."
                : "⚠ " + matches.Count + " duplicate/version matches for " + normalized + ". Newest modified image is first.";
        }

        private void PreviewSmartResult(ListBox list, PictureBox preview)
        {
            SmartImageResultItem x = list == null ? null : list.SelectedItem as SmartImageResultItem;
            SetSmartPreviewImage(preview, x == null ? null : x.FilePath);
        }

        private void OpenInspector(ListBox list, string code)
        {
            SmartImageResultItem x = list == null ? null : list.SelectedItem as SmartImageResultItem;
            if (x == null || !File.Exists(x.FilePath)) return;
            using (SmartImageInspectorForm f = new SmartImageInspectorForm(x.FilePath, SmartImageFinderEngine.NormalizeCode(code))) f.ShowDialog(this);
        }

        private void SetSmartPreviewImage(PictureBox preview, string file)
        {
            if (preview == null) return;
            Image old = preview.Image; preview.Image = null; if (old != null) old.Dispose();
            if (string.IsNullOrWhiteSpace(file) || !File.Exists(file)) return;
            try { using (Image i = Image.FromFile(file)) preview.Image = new Bitmap(i); } catch { }
        }

        private void ShowFailures(string type, SmartImageFinderEngine index)
        {
            List<string> failed = index.GetFailedFiles();
            if (failed.Count == 0) { MessageBox.Show("No failed " + type + " barcode images are currently recorded.", "Index Health"); return; }
            using (FailedBarcodeInspectorForm f = new FailedBarcodeInspectorForm(type, failed)) f.ShowDialog(this);
        }

        private void AddSmartResultToLane(ListBox list, FlowLayoutPanel lane, string type)
        {
            SmartImageResultItem x = list == null ? null : list.SelectedItem as SmartImageResultItem;
            if (x == null || !File.Exists(x.FilePath)) { MessageBox.Show("Select a " + type + " search result first.", "Smart Image Finder"); return; }
            AddThumbnail(lane, x.FilePath);
            lblSmartStatus.Text = Path.GetFileName(x.FilePath) + " added to the " + type + " lane.";

            if (type == "Product" && txtSmartCartonCode != null) { txtSmartCartonCode.Focus(); txtSmartCartonCode.SelectAll(); }
            else if (type == "Carton" && txtSmartProductCode != null) { txtSmartProductCode.Focus(); txtSmartProductCode.SelectAll(); }
        }

    }
}
