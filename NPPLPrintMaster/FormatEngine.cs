using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;

namespace NPPLPrintMaster
{
    public static class FormatEngine
    {
        public static async Task<int> FormatImages(
            List<string> filesToProcess,
            string outputFolder,
            bool addPadding,
            int thickness,
            Color borderColor,
            IProgress<string> consoleLog,
            bool preserveSourceFolders = false,
            string sourceRoot = null,
            bool forcePngOutput = false)
        {
            return await Task.Run(() =>
            {
                int count = 0;
                int pad = addPadding ? 40 : 0;

                Directory.CreateDirectory(outputFolder);

                foreach (string file in filesToProcess)
                {
                    try
                    {
                        string fileName =
                            Path.GetFileName(file);

                        string targetDirectory =
                            GetTargetDirectory(
                                file,
                                outputFolder,
                                preserveSourceFolders,
                                sourceRoot);

                        Directory.CreateDirectory(
                            targetDirectory);

                        string sourceExtension =
                            Path.GetExtension(file);

                        string outputExtension =
                            forcePngOutput
                                ? ".png"
                                : sourceExtension;

                        string outputPath =
                            Path.Combine(
                                targetDirectory,
                                Path.GetFileNameWithoutExtension(file) +
                                outputExtension);

                        using (Image img = Image.FromFile(file))
                        using (Bitmap bmp = new Bitmap(
                            img.Width + (pad * 2),
                            img.Height + (pad * 2),
                            PixelFormat.Format32bppArgb))
                        {
                            float horizontalDpi =
                                img.HorizontalResolution > 0
                                    ? img.HorizontalResolution
                                    : 96f;

                            float verticalDpi =
                                img.VerticalResolution > 0
                                    ? img.VerticalResolution
                                    : 96f;

                            bmp.SetResolution(
                                horizontalDpi,
                                verticalDpi);

                            using (Graphics g =
                                Graphics.FromImage(bmp))
                            {
                                g.SmoothingMode =
                                    SmoothingMode.HighQuality;

                                g.CompositingQuality =
                                    CompositingQuality.HighQuality;

                                g.InterpolationMode =
                                    InterpolationMode.HighQualityBicubic;

                                g.PixelOffsetMode =
                                    PixelOffsetMode.HighQuality;

                                // PNG supports transparency. JPEG/BMP do not
                                // reliably preserve alpha, so use white outside
                                // the rounded artwork for those formats.
                                bool supportsAlpha =
                                    string.Equals(
                                        outputExtension,
                                        ".png",
                                        StringComparison.OrdinalIgnoreCase);

                                g.Clear(
                                    supportsAlpha
                                        ? Color.Transparent
                                        : Color.White);

                                using (GraphicsPath path =
                                    new GraphicsPath())
                                {
                                    int d =
                                        Math.Min(
                                            90,
                                            Math.Min(
                                                img.Width,
                                                img.Height));

                                    d =
                                        Math.Max(
                                            2,
                                            d);

                                    path.AddArc(
                                        pad,
                                        pad,
                                        d,
                                        d,
                                        180,
                                        90);

                                    path.AddArc(
                                        pad + img.Width - d,
                                        pad,
                                        d,
                                        d,
                                        270,
                                        90);

                                    path.AddArc(
                                        pad + img.Width - d,
                                        pad + img.Height - d,
                                        d,
                                        d,
                                        0,
                                        90);

                                    path.AddArc(
                                        pad,
                                        pad + img.Height - d,
                                        d,
                                        d,
                                        90,
                                        90);

                                    path.CloseFigure();

                                    g.SetClip(path);

                                    // Copy the source pixels 1:1. This avoids unnecessary
                                    // interpolation/resampling of text, barcodes and
                                    // fine artwork during the formatting stage.
                                    g.DrawImageUnscaled(
                                        img,
                                        pad,
                                        pad);

                                    g.ResetClip();

                                    if (thickness > 0)
                                    {
                                        using (Pen pen =
                                            new Pen(
                                                borderColor,
                                                thickness))
                                        {
                                            pen.Alignment =
                                                PenAlignment.Inset;

                                            g.DrawPath(
                                                pen,
                                                path);
                                        }
                                    }
                                }
                            }

                            SaveUsingSourceFormat(
                                bmp,
                                outputPath,
                                outputExtension);
                        }

                        string relativeDisplayPath =
                            GetDisplayPath(
                                outputFolder,
                                outputPath);

                        consoleLog?.Report(
                            $"> Processed: {fileName} -> {relativeDisplayPath} ... [SUCCESS]\n");

                        count++;
                    }
                    catch (Exception ex)
                    {
                        consoleLog?.Report(
                            $"> ERROR on {Path.GetFileName(file)}: {ex.Message}\n");

                        continue;
                    }
                }

                return count;
            });
        }

        private static void SaveUsingSourceFormat(
            Bitmap bitmap,
            string outputPath,
            string sourceExtension)
        {
            string ext =
                (sourceExtension ?? string.Empty)
                    .ToLowerInvariant();

            switch (ext)
            {
                case ".jpg":
                case ".jpeg":
                    ImageCodecInfo jpegCodec =
                        GetEncoder(
                            ImageFormat.Jpeg);

                    if (jpegCodec != null)
                    {
                        using (EncoderParameters parameters =
                            new EncoderParameters(1))
                        {
                            // Maximum JPEG quality to avoid adding visible
                            // compression artifacts to text and line artwork.
                            parameters.Param[0] =
                                new EncoderParameter(
                                    Encoder.Quality,
                                    100L);

                            bitmap.Save(
                                outputPath,
                                jpegCodec,
                                parameters);
                        }
                    }
                    else
                    {
                        bitmap.Save(
                            outputPath,
                            ImageFormat.Jpeg);
                    }

                    break;

                case ".bmp":
                    bitmap.Save(
                        outputPath,
                        ImageFormat.Bmp);
                    break;

                case ".png":
                default:
                    bitmap.Save(
                        outputPath,
                        ImageFormat.Png);
                    break;
            }
        }

        private static ImageCodecInfo GetEncoder(
            ImageFormat format)
        {
            foreach (ImageCodecInfo codec in
                ImageCodecInfo.GetImageEncoders())
            {
                if (codec.FormatID == format.Guid)
                    return codec;
            }

            return null;
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

            string parentFolderName =
                new DirectoryInfo(sourceDirectory).Name;

            return string.IsNullOrWhiteSpace(parentFolderName)
                ? outputFolder
                : Path.Combine(
                    outputFolder,
                    parentFolderName);
        }

        private static string GetDisplayPath(
            string outputFolder,
            string outputFile)
        {
            try
            {
                string root =
                    Path.GetFullPath(outputFolder)
                        .TrimEnd(
                            Path.DirectorySeparatorChar,
                            Path.AltDirectorySeparatorChar) +
                    Path.DirectorySeparatorChar;

                string file =
                    Path.GetFullPath(outputFile);

                if (file.StartsWith(
                    root,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return file.Substring(
                        root.Length);
                }
            }
            catch
            {
            }

            return Path.GetFileName(outputFile);
        }
    }
}
