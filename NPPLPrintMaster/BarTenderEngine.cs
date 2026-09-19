using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace NPPLPrintMaster
{
    public static class BarTenderEngine
    {
        public static Task<int> ExtractImages(
            List<string> filesToProcess,
            string outputFolder,
            string format,
            int dpi,
            bool preserveSourceFolders = false,
            string sourceRoot = null)
        {
            // BarTender 10 is a COM/desktop application. Run the automation
            // session on a dedicated STA thread instead of Task.Run's normal
            // thread-pool (MTA) thread.
            TaskCompletionSource<int> completion =
                new TaskCompletionSource<int>();

            Thread worker =
                new Thread(
                    () =>
                    {
                        try
                        {
                            int count =
                                ExtractImagesSta(
                                    filesToProcess,
                                    outputFolder,
                                    format,
                                    dpi,
                                    preserveSourceFolders,
                                    sourceRoot);

                            completion.SetResult(count);
                        }
                        catch (Exception ex)
                        {
                            completion.SetException(ex);
                        }
                    });

            worker.IsBackground = true;
            worker.Name = "NPPLPrintMaster.BarTender";
            worker.SetApartmentState(ApartmentState.STA);
            worker.Start();

            return completion.Task;
        }

        private static int ExtractImagesSta(
            List<string> filesToProcess,
            string outputFolder,
            string format,
            int dpi,
            bool preserveSourceFolders,
            string sourceRoot)
        {
            int count = 0;

            object btAppObject = null;
            object btFormatsObject = null;

            dynamic btApp = null;
            dynamic btFormats = null;

            try
            {
                Directory.CreateDirectory(outputFolder);

                Type barTenderType =
                    Type.GetTypeFromProgID(
                        "BarTender.Application");

                if (barTenderType == null)
                {
                    throw new InvalidOperationException(
                        "BarTender.Application COM automation is not registered.");
                }

                btAppObject =
                    Activator.CreateInstance(
                        barTenderType);

                btApp = btAppObject;

                // Extraction does not need the BarTender UI. Keeping it hidden
                // also avoids leaving an unnecessary interactive window alive.
                btApp.Visible = false;

                // Hold the Formats collection explicitly so its COM reference
                // can be released deterministically at the end of the batch.
                btFormatsObject = btApp.Formats;
                btFormats = btFormatsObject;

                foreach (string file in filesToProcess)
                {
                    object btFormatObject = null;
                    dynamic btFormat = null;

                    try
                    {
                        string targetDirectory =
                            GetTargetDirectory(
                                file,
                                outputFolder,
                                preserveSourceFolders,
                                sourceRoot);

                        Directory.CreateDirectory(
                            targetDirectory);

                        string outImg =
                            Path.Combine(
                                targetDirectory,
                                Path.GetFileNameWithoutExtension(file) +
                                "." +
                                format);

                        btFormatObject =
                            btFormats.Open(
                                file,
                                false,
                                "");

                        btFormat = btFormatObject;

                        if (btFormat == null)
                            continue;

                        btFormat.ExportToFile(
                            outImg,
                            format,
                            4,
                            dpi,
                            2);

                        count++;
                    }
                    catch
                    {
                        // Keep batch processing if one BTW file fails.
                        continue;
                    }
                    finally
                    {
                        if (btFormat != null)
                        {
                            try
                            {
                                btFormat.Close(2);
                            }
                            catch
                            {
                            }
                        }

                        ReleaseComObject(
                            btFormatObject);

                        btFormat = null;
                        btFormatObject = null;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception(
                    "BarTender Error: " +
                    ex.Message,
                    ex);
            }
            finally
            {
                // Release the Formats collection before shutting down the
                // application. Every individual Format has already been closed
                // and released inside the loop above.
                ReleaseComObject(
                    btFormatsObject);

                btFormats = null;
                btFormatsObject = null;

                if (btApp != null)
                {
                    try
                    {
                        btApp.Quit(2);
                    }
                    catch
                    {
                    }
                }

                ReleaseComObject(
                    btAppObject);

                btApp = null;
                btAppObject = null;

                // Dynamic COM calls can create short-lived RCWs internally.
                // Force finalization once at the end of the complete batch so
                // BarTender is not left "busy" after NPPL has finished.
                try
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
                catch
                {
                }
            }

            return count;
        }

        private static void ReleaseComObject(
            object comObject)
        {
            if (comObject == null)
                return;

            try
            {
                if (Marshal.IsComObject(comObject))
                {
                    Marshal.FinalReleaseComObject(
                        comObject);
                }
            }
            catch
            {
                // COM cleanup must never mask the extraction result.
            }
        }

        private static string GetTargetDirectory(
            string sourceFile,
            string outputFolder,
            bool preserveSourceFolders,
            string sourceRoot)
        {
            if (!preserveSourceFolders)
                return outputFolder;

            string sourceDirectory =
                Path.GetDirectoryName(
                    Path.GetFullPath(sourceFile));

            if (string.IsNullOrWhiteSpace(sourceDirectory))
                return outputFolder;

            // Folder-selection mode:
            // recreate the hierarchy below the selected source folder.
            if (!string.IsNullOrWhiteSpace(sourceRoot))
            {
                string root =
                    Path.GetFullPath(sourceRoot)
                        .TrimEnd(
                            Path.DirectorySeparatorChar,
                            Path.AltDirectorySeparatorChar);

                string directory =
                    sourceDirectory.TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar);

                if (string.Equals(
                    directory,
                    root,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return outputFolder;
                }

                string rootWithSeparator =
                    root +
                    Path.DirectorySeparatorChar;

                if (directory.StartsWith(
                    rootWithSeparator,
                    StringComparison.OrdinalIgnoreCase))
                {
                    string relativeDirectory =
                        directory.Substring(
                            rootWithSeparator.Length);

                    return Path.Combine(
                        outputFolder,
                        relativeDirectory);
                }
            }

            // Manually-selected-files mode:
            // there is no explicit source root, so preserve the immediate
            // parent folder name rather than reproducing an entire drive path.
            string parentFolderName =
                new DirectoryInfo(sourceDirectory).Name;

            return string.IsNullOrWhiteSpace(parentFolderName)
                ? outputFolder
                : Path.Combine(
                    outputFolder,
                    parentFolderName);
        }
    }
}
