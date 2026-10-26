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
        private void OfferCollageRecovery(bool startup)
        {
            try
            {
                string path = CollageRecoveryStore.FindLatest();
                if (path == null)
                {
                    if (!startup) MessageBox.Show(this, "No saved collage recovery draft was found.", "Collage Recovery");
                    return;
                }
                if (MessageBox.Show(this,
                    "An autosaved collage from " + File.GetLastWriteTime(path).ToString("dd MMM yyyy HH:mm") +
                    " is available.\n\nRestore its images, page arrangement and text?\n" +
                    "Choose No to keep the draft for later in File > Recover autosaved collage.",
                    "Collage Recovery", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;
                string previousOutput = txtCollageOutput.Text;
                bool imported = ImportCollageLayout(path);
                // The recovery folder is internal storage, not an export destination.
                txtCollageOutput.Text = previousOutput;
                if (imported)
                {
                    SwitchPage(pageFormat, navButtons[2], "Step 2: Image Formatting");
                    BtnArrangeCollage_Click(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "The recovery draft could not be opened.\n\n" + ex.Message,
                    "Collage Recovery", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

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

        // Goes back to the automatic arrangement. Called whenever the image
        // source or the grid changes, because a manual arrangement only
        // makes sense for the images and grid it was made with.
        private void ResetManualCollageArrangement()
        {
            manualCollageSlots = null;
            manualCollageCaptions = null;
            manualCollageCustomNames = null;
            manualCollageNotes = null;
            UpdateCollageArrangementButtons();
        }

        // Everything about the sheet, as chosen on this page.
        private CollagePageSetup GetCollagePageSetup()
        {
            return new CollagePageSetup
            {
                Columns = cmbCollageColumns.SelectedIndex + 1,
                Rows = cmbCollageRows.SelectedIndex + 1,
                Landscape =
                    cmbCollageOrientation.SelectedItem != null &&
                    string.Equals(
                        cmbCollageOrientation.SelectedItem.ToString(),
                        "Landscape",
                        StringComparison.OrdinalIgnoreCase),
                DrawBorders = chkCollageBorders.Checked,
                CaptionFontPoints =
                    CollagePageSetup.CaptionFontChoices[
                        Math.Max(0, cmbCollageTextSize.SelectedIndex)],
                HeaderText = txtCollageHeader.Text.Trim(),
                ShowDate = chkCollageDate.Checked,
                ShowPageNumbers = chkCollagePageNumbers.Checked
            };
        }

        // Shows a page setup that came back from the arrangement workspace
        // or from an imported layout.
        private void ApplyCollagePageSetup(CollagePageSetup page)
        {
            if (page == null)
                return;

            page = page.Clone();
            page.Normalize();

            cmbCollageColumns.SelectedIndex = page.Columns - 1;
            cmbCollageRows.SelectedIndex = page.Rows - 1;
            cmbCollageOrientation.SelectedIndex = page.Landscape ? 1 : 0;
            chkCollageBorders.Checked = page.DrawBorders;
            cmbCollageTextSize.SelectedIndex =
                CollagePageSetup.NearestCaptionFontChoice(page.CaptionFontPoints);
            txtCollageHeader.Text = page.HeaderText;
            chkCollageDate.Checked = page.ShowDate;
            chkCollagePageNumbers.Checked = page.ShowPageNumbers;
        }

        // Which text is printed under each image. The check box switches
        // all of it on or off. With a manual arrangement the workspace
        // decides which lines are shown; otherwise it is the file name.
        private string GetCollageTextMode(bool manualArrangement)
        {
            if (!chkCollageShowFilename.Checked)
                return "None";

            if (!manualArrangement)
                return "Filename";

            return string.IsNullOrWhiteSpace(manualCollageDisplayMode) ||
                   string.Equals(
                       manualCollageDisplayMode,
                       "None",
                       StringComparison.OrdinalIgnoreCase)
                ? "Both"
                : manualCollageDisplayMode;
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

            List<string> defaultOrder;

            Cursor previousCursor = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;

            try
            {
                defaultOrder =
                    ImageCollageEngine.GetDefaultOrderedImagePaths(
                        filesToProcess);
            }
            finally
            {
                Cursor.Current = previousCursor;
            }

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

            using (CollageArrangementForm form =
                new CollageArrangementForm(
                    defaultOrder,
                    GetCollagePageSetup(),
                    currentSettings,
                    initialSlots,
                    manualCollageCaptions,
                    manualCollageCustomNames,
                    GetCollageTextMode(true),
                    manualCollageNotes))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    // The grid and the text size can be changed in the
                    // workspace; show what was chosen there.
                    ApplyCollagePageSetup(form.ResultPage);
                    // Images were added or removed in the workspace: from
                    // now on the collage is made from exactly that list.
                    if (form.ResultImages != null &&
                        !new HashSet<string>(
                            defaultOrder,
                            StringComparer.OrdinalIgnoreCase)
                            .SetEquals(form.ResultImages))
                    {
                        selectedCollageFiles = form.ResultImages.ToArray();
                        selectedCollageFolder = "";
                        txtCollageSource.Text =
                            selectedCollageFiles.Length + " image(s) selected";
                    }

                    manualCollageNotes =
                        form.ResultNotes == null
                            ? null
                            : new Dictionary<string, string>(
                                form.ResultNotes,
                                StringComparer.OrdinalIgnoreCase);

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

                    // "No text" in the workspace and the check box here are
                    // the same switch.
                    chkCollageShowFilename.Checked =
                        !string.Equals(
                            manualCollageDisplayMode,
                            "None",
                            StringComparison.OrdinalIgnoreCase);

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

        // ------------------------------------------------------------
        // Importing a saved arrangement
        //
        // Every collage PDF carries its own arrangement, so a PDF made
        // earlier (or a layout saved from the workspace) can be opened
        // again to revise it: change a remark, move or add an image, and
        // create the PDF again.
        // ------------------------------------------------------------
        private void BtnImportCollageLayout_Click(object sender, EventArgs e)
        {
            string path;

            using (OpenFileDialog ofd = new OpenFileDialog
            {
                Title = "Import a collage to continue editing it",
                Filter =
                    "Collage PDF or saved layout|*.pdf;*" +
                    CollageLayoutFile.Extension +
                    "|All files|*.*"
            })
            {
                if (DialogMemoryManager.ShowOpenDialog(
                    ofd,
                    "Collage.ImportLayout") != DialogResult.OK)
                    return;

                path = ofd.FileName;
            }

            // Straight into the workspace to edit it.
            if (ImportCollageLayout(path))
                BtnArrangeCollage_Click(sender, e);
        }

        // Loads the arrangement stored in a collage PDF or layout file and
        // makes it the current one. Returns false when nothing was imported.
        private bool ImportCollageLayout(string path)
        {
            CollageLayout layout;

            try
            {
                layout = CollageLayoutFile.Load(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "This file could not be read.\n\n" + ex.Message,
                    "Import Layout",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return false;
            }

            if (layout == null)
            {
                MessageBox.Show(
                    "No collage layout was found in this file.\n\n" +
                    "A layout can be imported from a PDF created by the Image " +
                    "Collage Builder in this version or later, or from a layout " +
                    "file saved in the Arrange Images workspace.\n\n" +
                    "PDFs made with an earlier version do not contain one.",
                    "Import Layout",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return false;
            }

            if (!LocateMissingCollageImages(layout))
                return false;

            if (layout.Images.Count == 0)
            {
                MessageBox.Show(
                    "None of the images used by this layout could be found.",
                    "Import Layout",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return false;
            }

            // The source, grid and page settings follow the layout.
            selectedCollageFiles = layout.Images.ToArray();
            selectedCollageFolder = "";
            txtCollageSource.Text =
                layout.Images.Count + " image(s) from " + Path.GetFileName(path);

            cmbCollageOutputType.SelectedIndex = 0;
            ApplyCollagePageSetup(layout.Page);

            manualCollageSlots = new List<string>(layout.Slots);

            manualCollageCaptions =
                new Dictionary<string, string>(
                    layout.Remarks,
                    StringComparer.OrdinalIgnoreCase);

            manualCollageCustomNames =
                new Dictionary<string, string>(
                    layout.Remarks,
                    StringComparer.OrdinalIgnoreCase);

            manualCollageNotes =
                new Dictionary<string, string>(
                    layout.Notes,
                    StringComparer.OrdinalIgnoreCase);

            manualCollageDisplayMode =
                string.IsNullOrWhiteSpace(layout.DisplayMode)
                    ? "Both"
                    : layout.DisplayMode;

            chkCollageShowFilename.Checked =
                !string.Equals(
                    manualCollageDisplayMode,
                    "None",
                    StringComparison.OrdinalIgnoreCase);

            // Revised PDFs go next to the original unless a folder is
            // already chosen.
            string currentOutput = txtCollageOutput.Text.Trim();

            if (string.IsNullOrWhiteSpace(currentOutput) ||
                currentOutput.StartsWith("Select", StringComparison.OrdinalIgnoreCase))
            {
                txtCollageOutput.Text = Path.GetDirectoryName(path);
            }

            UpdateCollageArrangementButtons();

            rtbFormatConsole.AppendText(
                "[COLLAGE] Imported layout from " +
                Path.GetFileName(path) +
                ": " +
                layout.Images.Count +
                " image(s), " +
                layout.Page.Columns + " x " + layout.Page.Rows +
                " grid." +
                Environment.NewLine);

            return true;
        }

        // Images are referenced by the layout, not stored in it. When some
        // are no longer where they were, the user can point to the folder
        // that holds them now. Returns false when the import is cancelled.
        private bool LocateMissingCollageImages(CollageLayout layout)
        {
            while (layout.MissingImages.Count > 0)
            {
                string examples =
                    string.Join(
                        Environment.NewLine,
                        layout.MissingImages
                            .Take(3)
                            .Select(p => "   " + p));

                DialogResult answer =
                    MessageBox.Show(
                        layout.MissingImages.Count +
                        " of the " +
                        layout.Images.Count +
                        " image(s) in this layout are not where they were saved, for example:\n\n" +
                        examples +
                        "\n\n" +
                        "Yes  -  choose the folder that contains them now\n" +
                        "No  -  continue without these images\n" +
                        "Cancel  -  stop the import",
                        "Images Not Found",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Warning);

                if (answer == DialogResult.Cancel)
                    return false;

                if (answer == DialogResult.No)
                    break;

                string folder = SelectFolderModern("Collage.RelinkFolder");

                if (string.IsNullOrWhiteSpace(folder))
                    continue;

                if (CollageLayoutFile.Relink(layout, folder) == 0)
                {
                    MessageBox.Show(
                        "None of the missing images were found in that folder.",
                        "Images Not Found",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }

            CollageLayoutFile.DropMissing(layout);
            return true;
        }

        // The arrangement is still usable as long as every image it places
        // is still part of the source. Source images that it does not place
        // are simply unplaced; the caller warns about those separately.
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

            return arranged.IsSubsetOf(source);
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

            string outputType = cmbCollageOutputType.SelectedItem.ToString();
            CollagePageSetup pageSetup = GetCollagePageSetup();

            // A manual arrangement applies to PDF output only.
            bool manualArrangement =
                manualCollageSlots != null &&
                outputType.Equals("PDF", StringComparison.OrdinalIgnoreCase);

            string textMode = GetCollageTextMode(manualArrangement);

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
                "[COLLAGE] Grid: " + pageSetup.Columns + " x " + pageSetup.Rows +
                " | Output: " + outputType +
                " | Orientation: " + (pageSetup.Landscape ? "Landscape" : "Portrait") + "\n");
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
                            pageSetup,
                            progress,
                            manualArrangement ? manualCollageSlots : null,
                            manualArrangement ? manualCollageCaptions : null,
                            manualArrangement ? manualCollageCustomNames : null,
                            textMode,
                            manualArrangement ? manualCollageNotes : null));

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
            return ImageCollageEngine.IsSupportedImageFile(path);
        }

    }
}
