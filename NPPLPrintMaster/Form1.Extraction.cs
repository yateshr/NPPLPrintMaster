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
        private async void BtnLoadFindList_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog
            {
                Multiselect = false,
                Filter =
                    "Supported Lists (*.txt;*.xlsx;*.xls)|*.txt;*.xlsx;*.xls|" +
                    "Text Files (*.txt)|*.txt|" +
                    "Excel Files (*.xlsx;*.xls)|*.xlsx;*.xls|" +
                    "All Files (*.*)|*.*"
            })
            {
                if (DialogMemoryManager.ShowOpenDialog(
                    ofd,
                    "FindExtract.LoadListFile") != DialogResult.OK)
                    return;

                try
                {
                    List<string> names =
                        await Task.Run(() => BtwListReader.ReadNames(ofd.FileName));

                    rtbFindNames.Lines =
                        names
                            .Where(n => !string.IsNullOrWhiteSpace(n))
                            .ToArray();

                    rtbFindResults.Clear();
                    lastFindMatches.Clear();

                    MessageBox.Show(
                        $"Loaded {names.Count} BTW name(s).",
                        "List Loaded",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "Unable to read the list file.\n\n" + ex.Message,
                        "List Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }

        private async void BtnFindBtwFiles_Click(object sender, EventArgs e)
        {
            await FindBtwMatchesAsync(true);
        }

        private async void BtnFindAndExtract_Click(object sender, EventArgs e)
        {
            if (!Directory.Exists(txtFindMasterFolder.Text))
            {
                MessageBox.Show(
                    "Please select a valid Master BTW Directory.",
                    "Master Directory Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtFindOutput.Text) ||
                txtFindOutput.Text.StartsWith("Select ", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(
                    "Please select an output folder.",
                    "Output Folder Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            List<string> matches =
                await FindBtwMatchesAsync(false);

            if (matches.Count == 0)
            {
                MessageBox.Show(
                    "No matching BTW files were found.",
                    "Nothing to Extract",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            btnFindExtract.Enabled = false;
            btnFindSearch.Enabled = false;
            btnFindExtract.Text = "⏳ Extracting...";

            try
            {
                Directory.CreateDirectory(txtFindOutput.Text);

                int count =
                    await BarTenderEngine.ExtractImages(
                        matches,
                        txtFindOutput.Text,
                        cmbFindFormat.SelectedItem.ToString(),
                        (int)numFindDpi.Value,
                        chkFindPreserve.Checked,
                        txtFindMasterFolder.Text);

                Logger.LogAction(
                    "FIND_EXTRACT",
                    $"Found {matches.Count} BTW match(es); extracted {count} image(s) to {txtFindOutput.Text}" +
                    (chkFindPreserve.Checked
                        ? " (folder structure preserved)"
                        : ""));

                MessageBox.Show(
                    $"Find & Extract complete!\n\n" +
                    $"BTW matches: {matches.Count}\n" +
                    $"Images extracted: {count}",
                    "Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Logger.LogAction(
                    "ERROR",
                    "Find & Extract: " + ex.Message);

                MessageBox.Show(
                    ex.Message,
                    "Find & Extract Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                btnFindExtract.Text = "▶ Find & Extract";
                btnFindExtract.Enabled = true;
                btnFindSearch.Enabled = true;
            }
        }

        private async Task<List<string>> FindBtwMatchesAsync(
            bool showSummary)
        {
            string masterRoot =
                txtFindMasterFolder.Text == null
                    ? string.Empty
                    : txtFindMasterFolder.Text.Trim();

            if (!Directory.Exists(masterRoot))
            {
                MessageBox.Show(
                    "Please select a valid Master BTW Directory.",
                    "Master Directory Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return new List<string>();
            }

            List<string> requestedNames =
                GetRequestedBtwNames();

            if (requestedNames.Count == 0)
            {
                MessageBox.Show(
                    "Enter BTW names or load a TXT / Excel list first.",
                    "BTW List Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return new List<string>();
            }

            btnFindSearch.Enabled = false;
            string oldSearchText = btnFindSearch.Text;
            btnFindSearch.Text = "⏳ Indexing / Searching...";

            try
            {
                if (!string.Equals(
                    indexedBtwRoot,
                    masterRoot,
                    StringComparison.OrdinalIgnoreCase) ||
                    btwSearchIndex.Count == 0)
                {
                    Dictionary<string, List<string>> newIndex =
                        await Task.Run(
                            () => BuildBtwSearchIndex(masterRoot));

                    btwSearchIndex = newIndex;
                    indexedBtwRoot = masterRoot;
                }

                List<string> matches =
                    new List<string>();

                int foundNames = 0;
                int missingNames = 0;
                int duplicateNames = 0;

                rtbFindResults.Clear();

                foreach (string requested in requestedNames)
                {
                    List<string> paths;

                    if (btwSearchIndex.TryGetValue(
                        requested,
                        out paths) &&
                        paths != null &&
                        paths.Count > 0)
                    {
                        foundNames++;

                        if (paths.Count > 1)
                        {
                            duplicateNames++;
                            rtbFindResults.AppendText(
                                $"DUPLICATE  {requested}.btw  ({paths.Count} matches)\n");

                            foreach (string path in paths)
                            {
                                rtbFindResults.AppendText(
                                    "           " +
                                    GetRelativeDisplayPath(masterRoot, path) +
                                    "\n");
                            }
                        }
                        else
                        {
                            rtbFindResults.AppendText(
                                "FOUND      " +
                                requested +
                                ".btw  ->  " +
                                GetRelativeDisplayPath(
                                    masterRoot,
                                    paths[0]) +
                                "\n");
                        }

                        foreach (string path in paths)
                        {
                            if (!matches.Contains(
                                path,
                                StringComparer.OrdinalIgnoreCase))
                            {
                                matches.Add(path);
                            }
                        }
                    }
                    else
                    {
                        missingNames++;
                        rtbFindResults.AppendText(
                            "NOT FOUND  " +
                            requested +
                            ".btw\n");
                    }
                }

                lastFindMatches = matches;

                rtbFindResults.AppendText(
                    "\n----------------------------------------\n");
                rtbFindResults.AppendText(
                    $"Requested: {requestedNames.Count} | " +
                    $"Found names: {foundNames} | " +
                    $"Missing: {missingNames} | " +
                    $"Duplicates: {duplicateNames} | " +
                    $"BTW files to extract: {matches.Count}\n");
                rtbFindResults.ScrollToCaret();

                if (showSummary)
                {
                    MessageBox.Show(
                        $"Search complete.\n\n" +
                        $"Requested names: {requestedNames.Count}\n" +
                        $"Found names: {foundNames}\n" +
                        $"Missing names: {missingNames}\n" +
                        $"Duplicate names: {duplicateNames}\n" +
                        $"BTW files matched: {matches.Count}",
                        "BTW Search",
                        MessageBoxButtons.OK,
                        missingNames > 0
                            ? MessageBoxIcon.Warning
                            : MessageBoxIcon.Information);
                }

                return matches;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Unable to search the master BTW directory.\n\n" +
                    ex.Message,
                    "BTW Search Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                return new List<string>();
            }
            finally
            {
                btnFindSearch.Text = oldSearchText;
                btnFindSearch.Enabled = true;
            }
        }

        private List<string> GetRequestedBtwNames()
        {
            HashSet<string> unique =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            foreach (string line in rtbFindNames.Lines)
            {
                string normalized =
                    NormalizeBtwLookupName(line);

                if (!string.IsNullOrWhiteSpace(normalized))
                    unique.Add(normalized);
            }

            return unique.ToList();
        }

        private static string NormalizeBtwLookupName(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string name =
                value.Trim()
                    .Trim('"')
                    .Trim();

            name = Path.GetFileName(name);

            if (name.EndsWith(
                ".btw",
                StringComparison.OrdinalIgnoreCase))
            {
                name =
                    Path.GetFileNameWithoutExtension(name);
            }

            return name.Trim();
        }

        private static IEnumerable<string>
            EnumerateBtwFilesSafe(
                string root)
        {
            Stack<string> pending =
                new Stack<string>();

            pending.Push(root);

            while (pending.Count > 0)
            {
                string current =
                    pending.Pop();

                string[] files;

                try
                {
                    files =
                        Directory.GetFiles(
                            current,
                            "*.btw",
                            SearchOption.TopDirectoryOnly);
                }
                catch
                {
                    files = new string[0];
                }

                foreach (string file in files)
                    yield return file;

                string[] directories;

                try
                {
                    directories =
                        Directory.GetDirectories(
                            current,
                            "*",
                            SearchOption.TopDirectoryOnly);
                }
                catch
                {
                    directories = new string[0];
                }

                foreach (string directory in directories)
                    pending.Push(directory);
            }
        }

        private static string GetRelativeDisplayPath(
            string root,
            string file)
        {
            try
            {
                string rootWithSeparator =
                    root.TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar) +
                    Path.DirectorySeparatorChar;

                Uri rootUri =
                    new Uri(rootWithSeparator);

                Uri fileUri =
                    new Uri(file);

                return Uri.UnescapeDataString(
                    rootUri
                        .MakeRelativeUri(fileUri)
                        .ToString()
                        .Replace(
                            '/',
                            Path.DirectorySeparatorChar));
            }
            catch
            {
                return file;
            }
        }

        private async void BtnRunBlock1_Click(object sender, EventArgs e)
        {
            List<string> filesToProcess = new List<string>();
            if (selectedBtwFiles != null && selectedBtwFiles.Length > 0)
                filesToProcess.AddRange(selectedBtwFiles);
            else if (!string.IsNullOrWhiteSpace(selectedBtwFolder) && Directory.Exists(selectedBtwFolder))
            {
                SearchOption opt = chkSubDirs1.Checked
                    ? SearchOption.AllDirectories
                    : SearchOption.TopDirectoryOnly;

                filesToProcess.AddRange(
                    Directory.GetFiles(selectedBtwFolder, "*.btw", opt));
            }

            if (filesToProcess.Count == 0 ||
                string.IsNullOrWhiteSpace(txtExport1.Text) ||
                txtExport1.Text.StartsWith("Select ", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Please select .btw files and an export folder first.");
                return;
            }

            bool extractAndFormat =
                chkExtractAndFormat != null &&
                chkExtractAndFormat.Checked;

            btnRun1.Text = extractAndFormat
                ? "⏳ Extracting + Formatting..."
                : "⏳ Extracting... Please Wait...";
            btnRun1.Enabled = false;

            string tempExtractFolder = null;

            try
            {
                string sourceRoot =
                    !string.IsNullOrWhiteSpace(selectedBtwFolder)
                        ? selectedBtwFolder
                        : null;

                string extractTarget = txtExport1.Text;

                // One-task mode uses an isolated temporary extraction folder.
                // This avoids loading and overwriting the same image file during
                // formatting, and leaves only the final formatted output behind.
                if (extractAndFormat)
                {
                    tempExtractFolder = Path.Combine(
                        Path.GetTempPath(),
                        "NPPLPrintMaster",
                        "ExtractFormat_" + Guid.NewGuid().ToString("N"));

                    Directory.CreateDirectory(tempExtractFolder);
                    extractTarget = tempExtractFolder;
                }

                int extractedCount = await BarTenderEngine.ExtractImages(
                    filesToProcess,
                    extractTarget,
                    cmbFormat.SelectedItem.ToString(),
                    (int)numDpi.Value,
                    chkPreserveFolders1.Checked,
                    sourceRoot);

                if (!extractAndFormat)
                {
                    Logger.LogAction(
                        "EXTRACT",
                        $"Extracted {extractedCount} files to {txtExport1.Text}" +
                        (chkPreserveFolders1.Checked
                            ? " (folder structure preserved)"
                            : ""));

                    MessageBox.Show(
                        $"Step 1 Complete!\nExtracted {extractedCount} images.",
                        "Success");

                    return;
                }

                SearchOption tempSearchOption =
                    chkPreserveFolders1.Checked
                        ? SearchOption.AllDirectories
                        : SearchOption.TopDirectoryOnly;

                List<string> extractedImages =
                    Directory.GetFiles(
                        tempExtractFolder,
                        "*.*",
                        tempSearchOption)
                    .Where(f =>
                        f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (extractedImages.Count == 0)
                    throw new InvalidOperationException(
                        "BarTender extraction completed, but no extracted images were available for formatting.");

                var progressTracker = new Progress<string>(message =>
                {
                    if (rtbFormatConsole == null) return;
                    rtbFormatConsole.AppendText(message);
                    rtbFormatConsole.ScrollToCaret();
                });

                if (rtbFormatConsole != null)
                {
                    rtbFormatConsole.Clear();
                    rtbFormatConsole.AppendText(
                        $"[ONE TASK] Extracted {extractedCount} image(s). Formatting now...\n");
                    rtbFormatConsole.AppendText(
                        "--------------------------------------------------\n");
                }

                int formattedCount = await FormatEngine.FormatImages(
                    extractedImages,
                    txtExport1.Text,
                    chkPadding != null && chkPadding.Checked,
                    numThickness == null ? 0 : (int)numThickness.Value,
                    selectedBorderColor,
                    progressTracker,
                    chkPreserveFolders1.Checked,
                    tempExtractFolder,
                    cmbFormattingOutputFormat != null &&
                    cmbFormattingOutputFormat.SelectedIndex == 1);

                if (rtbFormatConsole != null)
                {
                    rtbFormatConsole.AppendText(
                        "--------------------------------------------------\n");
                    rtbFormatConsole.AppendText(
                        $"[SYSTEM] One-task complete. Final formatted: {formattedCount}\n");
                }

                Logger.LogAction(
                    "EXTRACT_FORMAT",
                    $"BTW files: {filesToProcess.Count}; extracted: {extractedCount}; formatted: {formattedCount}; output: {txtExport1.Text}" +
                    (chkPreserveFolders1.Checked
                        ? " (folder structure preserved)"
                        : ""));

                MessageBox.Show(
                    $"Extract + Format complete!\n\n" +
                    $"Images extracted: {extractedCount}\n" +
                    $"Final formatted images: {formattedCount}\n\n" +
                    $"Output:\n{txtExport1.Text}",
                    "One Task Complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error");
                Logger.LogAction("ERROR", ex.Message);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(tempExtractFolder) &&
                    Directory.Exists(tempExtractFolder))
                {
                    try
                    {
                        Directory.Delete(tempExtractFolder, true);
                    }
                    catch
                    {
                        // Temporary cleanup must never interrupt production.
                    }
                }

                btnRun1.Text = "▶ Extract Images";
                btnRun1.Enabled = true;
            }
        }

    }
}
