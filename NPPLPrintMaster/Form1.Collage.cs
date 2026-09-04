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
        private async void BtnGenerateCollage_Click(object sender, EventArgs e)
        {
            List<string> filesToProcess = new List<string>();

            if (selectedCollageFiles != null && selectedCollageFiles.Length > 0)
            {
                filesToProcess.AddRange(
                    selectedCollageFiles.Where(IsSupportedCollageImage));
            }
            else if (!string.IsNullOrWhiteSpace(selectedCollageFolder) &&
                     Directory.Exists(selectedCollageFolder))
            {
                SearchOption option =
                    chkCollageSubDirs.Checked
                        ? SearchOption.AllDirectories
                        : SearchOption.TopDirectoryOnly;

                try
                {
                    filesToProcess.AddRange(
                        Directory.GetFiles(
                            selectedCollageFolder,
                            "*.*",
                            option)
                        .Where(IsSupportedCollageImage));
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "Unable to scan the selected folder.\n\n" + ex.Message,
                        "Collage Input Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }
            }

            filesToProcess =
                filesToProcess
                    .Where(File.Exists)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

            if (filesToProcess.Count == 0)
            {
                MessageBox.Show(
                    "Please select image files or a folder containing supported images.",
                    "No Images",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            string outputFolder =
                txtCollageOutput == null
                    ? string.Empty
                    : txtCollageOutput.Text.Trim();

            if (string.IsNullOrWhiteSpace(outputFolder) ||
                outputFolder.StartsWith("Select", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(
                    "Please select an output folder.",
                    "Output Folder Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            int gridSize = cmbCollageGrid.SelectedIndex + 1;
            string outputType = cmbCollageOutputType.SelectedItem.ToString();
            bool landscape =
                string.Equals(
                    cmbCollageOrientation.SelectedItem.ToString(),
                    "Landscape",
                    StringComparison.OrdinalIgnoreCase);

            Directory.CreateDirectory(outputFolder);

            string extension =
                outputType == "PDF"
                    ? ".pdf"
                    : outputType == "Word"
                        ? ".docx"
                        : ".xlsx";

            string baseFileName;

            if (chkCollageAutoFileName.Checked)
            {
                baseFileName =
                    "ImageCollage_" +
                    DateTime.Now.ToString("yyyyMMdd_HHmmss");
            }
            else
            {
                baseFileName =
                    txtCollageFileName == null
                        ? string.Empty
                        : txtCollageFileName.Text.Trim();

                if (string.IsNullOrWhiteSpace(baseFileName))
                {
                    MessageBox.Show(
                        "Please enter an output file name, or enable Auto-generate filename.",
                        "File Name Required",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                baseFileName =
                    Path.GetFileNameWithoutExtension(baseFileName);

                if (baseFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    MessageBox.Show(
                        "The output file name contains invalid characters.",
                        "Invalid File Name",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }
            }

            string outputPath =
                Path.Combine(
                    outputFolder,
                    baseFileName + extension);

            btnGenerateCollage.Enabled = false;
            string oldButtonText = btnGenerateCollage.Text;
            btnGenerateCollage.Text = "⏳ Generating...";

            rtbFormatConsole.Clear();
            rtbFormatConsole.AppendText(
                "[COLLAGE] Preparing " + filesToProcess.Count + " image(s)...\n");
            rtbFormatConsole.AppendText(
                "[COLLAGE] Grid: " + gridSize + " x " + gridSize +
                " | Output: " + outputType +
                " | Orientation: " + (landscape ? "Landscape" : "Portrait") + "\n");
            rtbFormatConsole.AppendText(
                "--------------------------------------------------\n");

            var progress = new Progress<string>(message =>
            {
                rtbFormatConsole.AppendText(message + Environment.NewLine);
                rtbFormatConsole.ScrollToCaret();
            });

            try
            {
                CollageGenerationResult result =
                    await Task.Run(
                        () => ImageCollageEngine.Generate(
                            filesToProcess,
                            outputPath,
                            outputType,
                            gridSize,
                            landscape,
                            chkCollageShowFilename.Checked,
                            chkCollageBorders.Checked,
                            progress));

                rtbFormatConsole.AppendText(
                    "--------------------------------------------------\n");
                rtbFormatConsole.AppendText(
                    "[COLLAGE] Complete.\n");
                rtbFormatConsole.AppendText(
                    "[COLLAGE] Images: " + result.ImageCount +
                    " | Unique sizes: " + result.UniqueSizeCount + "\n");
                rtbFormatConsole.AppendText(
                    "[COLLAGE] Most common size: " +
                    result.MostCommonSize +
                    " (" + result.MostCommonCount + " image(s))\n");
                rtbFormatConsole.AppendText(
                    "[COLLAGE] Saved: " + result.OutputPath + "\n");

                Logger.LogAction(
                    "COLLAGE",
                    "Generated " + outputType +
                    " collage with " + result.ImageCount +
                    " image(s): " + result.OutputPath);

                MessageBox.Show(
                    "Collage created successfully!\n\n" +
                    "Images: " + result.ImageCount + "\n" +
                    "Unique sizes: " + result.UniqueSizeCount + "\n\n" +
                    result.OutputPath,
                    "Collage Complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                rtbFormatConsole.AppendText(
                    "[COLLAGE ERROR] " + ex.Message + "\n");

                Logger.LogAction(
                    "ERROR",
                    "Collage: " + ex.Message);

                MessageBox.Show(
                    "Unable to generate the collage.\n\n" + ex.Message,
                    "Collage Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                btnGenerateCollage.Text = oldButtonText;
                btnGenerateCollage.Enabled = true;
            }
        }

        private static bool IsSupportedCollageImage(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            string extension =
                Path.GetExtension(path).ToLowerInvariant();

            return extension == ".png" ||
                   extension == ".jpg" ||
                   extension == ".jpeg" ||
                   extension == ".bmp" ||
                   extension == ".tif" ||
                   extension == ".tiff";
        }

    }
}
