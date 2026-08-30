using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace NPPLPrintMaster
{
    public static class BarTenderEngine
    {
        public static async Task<int> ExtractImages(
            List<string> filesToProcess,
            string outputFolder,
            string format,
            int dpi,
            bool preserveSourceFolders = false,
            string sourceRoot = null)
        {
            return await Task.Run(() =>
            {
                int count = 0;
                dynamic btApp = null;

                try
                {
                    Directory.CreateDirectory(outputFolder);

                    btApp = Activator.CreateInstance(
                        Type.GetTypeFromProgID("BarTender.Application"));

                    btApp.Visible = true;

                    foreach (string file in filesToProcess)
                    {
                        dynamic btFormat = null;

                        try
                        {
                            string targetDirectory =
                                GetTargetDirectory(
                                    file,
                                    outputFolder,
                                    preserveSourceFolders,
                                    sourceRoot);

                            Directory.CreateDirectory(targetDirectory);

                            string outImg =
                                Path.Combine(
                                    targetDirectory,
                                    Path.GetFileNameWithoutExtension(file) +
                                    "." +
                                    format);

                            btFormat =
                                btApp.Formats.Open(
                                    file,
                                    false,
                                    "");

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
                }

                return count;
            });
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
