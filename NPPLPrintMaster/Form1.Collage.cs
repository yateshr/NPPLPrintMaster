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
        private List<string> GetCurrentCollageFiles()
        {
            List<string> filesToProcess = new List<string>();

            if (selectedCollageFiles != null &&
                selectedCollageFiles.Length > 0)
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
                        "Unable to scan the selected folder.\n\n" +
                        ex.Message,
                        "Collage Input Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return new List<string>();
                }
            }

            return filesToProcess
                .Where(File.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void UpdateCollageArrangementButtons()
        {
            if (btnArrangeCollage == null)
                return;

            bool isPdf =
                cmbCollageOutputType != null &&
                cmbCollageOutputType.SelectedItem != null &&
                string.Equals(
                    cmbCollageOutputType.SelectedItem.ToString(),
                    "PDF",
                    StringComparison.OrdinalIgnoreCase);

            bool hasSource =
                (selectedCollageFiles != null &&
                 selectedCollageFiles.Length > 0) ||
                !string.IsNullOrWhiteSpace(selectedCollageFolder);

            btnArrangeCollage.Enabled =
                isPdf && hasSource;

            btnClearCollageArrangement.Enabled =
                isPdf && manualCollageSlots != null;

            btnArrangeCollage.Text =
                isPdf && manualCollageSlots != null
                    ? "✓ Edit Arrangement"
                    : "🖼  Arrange Images...";

            btnClearCollageArrangement.Text =
                manualCollageSlots == null
                    ? "↺  Automatic"
                    : "↺  Reset Automatic";
        }

        private void BtnArrangeCollage_Click(object sender, EventArgs e)
        {
            if (cmbCollageOutputType.SelectedItem == null ||
                !string.Equals(
                    cmbCollageOutputType.SelectedItem.ToString(),
                    "PDF",
                    StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(
                    "Manual image arrangement is available for PDF output.",
                    "PDF Arrangement",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            List<string> filesToProcess =
                GetCurrentCollageFiles();

            if (filesToProcess.Count == 0)
            {
                MessageBox.Show(
                    "Please select image files or a folder containing supported images.",
                    "No Images",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            int gridSize =
                cmbCollageGrid.SelectedIndex + 1;

            List<string> defaultOrder =
                ImageCollageEngine.GetDefaultOrderedImagePaths(
                    filesToProcess);

            if (defaultOrder.Count == 0)
            {
                MessageBox.Show(
                    "No readable images were found.",
                    "No Images",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            List<string> initialSlots =
                manualCollageSlots == null
                    ? defaultOrder
                    : manualCollageSlots;

            bool landscape =
                cmbCollageOrientation.SelectedItem != null &&
                string.Equals(
                    cmbCollageOrientation.SelectedItem.ToString(),
                    "Landscape",
                    StringComparison.OrdinalIgnoreCase);

            using (CollageArrangementForm form =
                new CollageArrangementForm(
                    defaultOrder,
                    gridSize,
                    landscape,
                    currentSettings,
                    initialSlots,
                    manualCollageCaptions,
                    manualCollageCustomNames))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    manualCollageSlots =
                        form.ResultSlots == null
                            ? null
                            : new List<string>(form.ResultSlots);

                    manualCollageCaptions =
                        form.ResultCaptions == null
                            ? null
                            : new Dictionary<string, string>(
                                form.ResultCaptions,
                                StringComparer.OrdinalIgnoreCase);

                    manualCollageCustomNames =
                        form.ResultCustomNames == null
                            ? null
                            : new Dictionary<string, string>(
                                form.ResultCustomNames,
                                StringComparer.OrdinalIgnoreCase);

                    manualCollageDisplayMode =
                        string.IsNullOrWhiteSpace(form.ResultDisplayMode)
                            ? "Both"
                            : form.ResultDisplayMode;

                    UpdateCollageArrangementButtons();

                    int assigned =
                        manualCollageSlots == null
                            ? 0
                            : manualCollageSlots.Count(
                                p => !string.IsNullOrWhiteSpace(p));

                    rtbFormatConsole.AppendText(
                        "[COLLAGE] Manual arrangement saved: " +
                        assigned +
                        " image(s) assigned to layout cells." +
                        Environment.NewLine);
                }
            }
        }

        private static bool ManualArrangementMatchesSource(
            IList<string> sourceFiles,
            IList<string> slots)
        {
            if (slots == null)
                return true;

            HashSet<string> source =
                new HashSet<string>(
                    sourceFiles,
                    StringComparer.OrdinalIgnoreCase);

            HashSet<string> arranged =
                new HashSet<string>(
                    slots.Where(
                        p => !string.IsNullOrWhiteSpace(p)),
                    StringComparer.OrdinalIgnoreCase);

            return source.SetEquals(arranged);
        }

        private async void BtnGenerateCollage_Click(object sender, EventArgs e)
        {
            List<string> filesToProcess = GetCurrentCollageFiles();

            if (filesToProcess.Count == 0)
            {
                MessageBox.Show(
                    "Please select image files or a folder containing supported images.",
                    "No Images",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (manualCollageSlots != null &&
                cmbCollageOutputType.SelectedItem != null &&
                string.Equals(
                    cmbCollageOutputType.SelectedItem.ToString(),
                    "PDF",
                    StringComparison.OrdinalIgnoreCase) &&
                !ManualArrangementMatchesSource(
                    filesToProcess,
                    manualCollageSlots))
            {
                MessageBox.Show(
                    "The image source has changed since the manual arrangement was created.\n\n" +
                    "Please open 'Edit Arrangement' and save the arrangement again before creating the PDF.",
                    "Manual Arrangement Needs Update",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (manualCollageSlots != null &&
                cmbCollageOutputType.SelectedItem != null &&
                string.Equals(
                    cmbCollageOutputType.SelectedItem.ToString(),
                    "PDF",
                    StringComparison.OrdinalIgnoreCase))
            {
                HashSet<string> placedPaths =
                    new HashSet<string>(
                        manualCollageSlots.Where(
                            p => !string.IsNullOrWhiteSpace(p)),
                        StringComparer.OrdinalIgnoreCase);

                int unplacedCount =
                    filesToProcess.Count(
                        p => !placedPaths.Contains(p));

                if (unplacedCount > 0)
                {
                    DialogResult continueResult =
                        MessageBox.Show(
                            unplacedCount +
                            " image(s) are still unplaced.\n\n" +
                            "They will not appear in the PDF.\n\n" +
                            "Do you want to create the PDF anyway?",
                            "Unplaced Images",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning);

                    if (continueResult != DialogResult.Yes)
                        return;
                }
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
                            progress,
                            outputType.Equals(
                                "PDF",
                                StringComparison.OrdinalIgnoreCase)
                                ? manualCollageSlots
                                : null,
                            outputType.Equals(
                                "PDF",
                                StringComparison.OrdinalIgnoreCase)
                                ? manualCollageCaptions
                                : null,
                            outputType.Equals(
                                "PDF",
                                StringComparison.OrdinalIgnoreCase)
                                ? manualCollageCustomNames
                                : null,
                            outputType.Equals(
                                "PDF",
                                StringComparison.OrdinalIgnoreCase)
                                ? (string.IsNullOrWhiteSpace(manualCollageDisplayMode)
                                    ? "Both"
                                    : manualCollageDisplayMode)
                                : "Filename"));

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
