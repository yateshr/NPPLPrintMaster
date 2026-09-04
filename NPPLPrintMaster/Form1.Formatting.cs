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
        private async void BtnRunBlock2_Click(object sender, EventArgs e)
        {
            List<string> filesToProcess = new List<string>();
            if (selectedRawFiles != null && selectedRawFiles.Length > 0) filesToProcess.AddRange(selectedRawFiles);
            else if (!string.IsNullOrWhiteSpace(selectedRawFolder) && Directory.Exists(selectedRawFolder))
            {
                SearchOption opt = chkSubDirs2.Checked ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var allFiles = Directory.GetFiles(selectedRawFolder, "*.*", opt);
                filesToProcess.AddRange(allFiles.Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase)));
            }

            if (filesToProcess.Count == 0 || string.IsNullOrWhiteSpace(txtExport2.Text)) { MessageBox.Show("Please select images and an export folder first."); return; }

            Button clickedButton = (Button)sender;
            clickedButton.Text = "⏳ Formatting... Please Wait...";
            clickedButton.Enabled = false;

            rtbFormatConsole.Clear();
            rtbFormatConsole.AppendText($"Starting batch format of {filesToProcess.Count} images...\n");
            rtbFormatConsole.AppendText("--------------------------------------------------\n");

            var progressTracker = new Progress<string>(message =>
            {
                rtbFormatConsole.AppendText(message);
                rtbFormatConsole.ScrollToCaret();
            });

            string sourceRoot =
                !string.IsNullOrWhiteSpace(selectedRawFolder)
                    ? selectedRawFolder
                    : null;

            int count = await FormatEngine.FormatImages(
                filesToProcess,
                txtExport2.Text,
                chkPadding.Checked,
                (int)numThickness.Value,
                selectedBorderColor,
                progressTracker,
                chkPreserveFolders2.Checked,
                sourceRoot,
                cmbFormattingOutputFormat != null &&
                cmbFormattingOutputFormat.SelectedIndex == 1);

            rtbFormatConsole.AppendText("--------------------------------------------------\n");
            rtbFormatConsole.AppendText($"[SYSTEM] Done. Total formatted: {count}\n");

            Logger.LogAction(
                "FORMAT",
                $"Formatted {count} images to {txtExport2.Text}" +
                (chkPreserveFolders2.Checked ? " (folder structure preserved)" : ""));
            clickedButton.Text = "▶ Format Images";
            clickedButton.Enabled = true;
            MessageBox.Show($"Step 2 Complete!\nFormatted {count} images.", "Success");
        }

    }
}
