using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using NPOI.XWPF.UserModel;

namespace NPPLPrintMaster
{
    public sealed class CollageGenerationResult
    {
        public int ImageCount { get; set; }
        public int UniqueSizeCount { get; set; }
        public string MostCommonSize { get; set; }
        public int MostCommonCount { get; set; }
        public string OutputPath { get; set; }
    }

    internal sealed class CollageImageInfo
    {
        public string Path { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public string SizeKey
        {
            get { return Width + " x " + Height; }
        }
    }

    public static class ImageCollageEngine
    {
        private const int PdfDpi = 300;

        public static CollageGenerationResult Generate(
            IList<string> imageFiles,
            string outputPath,
            string outputType,
            int gridSize,
            bool landscape,
            bool showFilename,
            bool drawBorders,
            IProgress<string> progress,
            IList<string> manualSlotPaths = null,
            IDictionary<string, string> manualCaptions = null,
            IDictionary<string, string> manualCustomNames = null,
            string manualDisplayMode = "Filename")
        {
            if (imageFiles == null || imageFiles.Count == 0)
                throw new ArgumentException("No images were supplied.");

            if (gridSize < 1 || gridSize > 4)
                throw new ArgumentOutOfRangeException("gridSize");

            List<CollageImageInfo> images =
                ReadImageInformation(imageFiles, progress);

            if (images.Count == 0)
                throw new InvalidOperationException("No readable images were found.");

            Dictionary<string, int> sizeCounts =
                images
                    .GroupBy(i => i.SizeKey, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Count(),
                        StringComparer.OrdinalIgnoreCase);

            List<CollageImageInfo> sorted =
                images
                    .OrderByDescending(i => sizeCounts[i.SizeKey])
                    .ThenByDescending(i => i.Width)
                    .ThenByDescending(i => i.Height)
                    .ThenBy(
                        i => System.IO.Path.GetFileName(i.Path),
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();

            KeyValuePair<string, int> mostCommon =
                sizeCounts
                    .OrderByDescending(kv => kv.Value)
                    .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                    .First();

            Report(
                progress,
                "[COLLAGE] Size groups: " + sizeCounts.Count +
                " | Most common: " + mostCommon.Key +
                " (" + mostCommon.Value + ")");

            List<CollageImageInfo> renderImages = sorted;

            if (manualSlotPaths != null)
            {
                if (!outputType.Equals("PDF", StringComparison.OrdinalIgnoreCase))
                    throw new NotSupportedException(
                        "Manual collage arrangement is currently supported for PDF output only.");

                renderImages =
                    BuildManualRenderImages(
                        sorted,
                        manualSlotPaths,
                        progress);

                Report(
                    progress,
                    "[COLLAGE] Manual arrangement: " +
                    renderImages.Count(i => i != null && !string.IsNullOrWhiteSpace(i.Path)) +
                    " assigned image(s) across " +
                    (int)Math.Ceiling(renderImages.Count / (double)(gridSize * gridSize)) +
                    " page(s).");
            }

            if (outputType.Equals("PDF", StringComparison.OrdinalIgnoreCase))
            {
                GeneratePdf(
                    renderImages,
                    outputPath,
                    gridSize,
                    landscape,
                    showFilename,
                    drawBorders,
                    progress,
                    manualCaptions,
                    manualCustomNames,
                    manualDisplayMode);
            }
            else if (outputType.Equals("Word", StringComparison.OrdinalIgnoreCase))
            {
                GenerateWordDocx(
                    sorted,
                    outputPath,
                    gridSize,
                    landscape,
                    showFilename,
                    drawBorders,
                    progress);
            }
            else if (outputType.Equals("Excel", StringComparison.OrdinalIgnoreCase))
            {
                GenerateExcel(
                    sorted,
                    outputPath,
                    gridSize,
                    landscape,
                    showFilename,
                    drawBorders,
                    progress);
            }
            else
            {
                throw new NotSupportedException(
                    "Unsupported output type: " + outputType);
            }

            return new CollageGenerationResult
            {
                ImageCount =
                    manualSlotPaths == null
                        ? sorted.Count
                        : renderImages.Count(i => i != null && !string.IsNullOrWhiteSpace(i.Path)),
                UniqueSizeCount = sizeCounts.Count,
                MostCommonSize = mostCommon.Key,
                MostCommonCount = mostCommon.Value,
                OutputPath = outputPath
            };
        }

        // Returns the same deterministic order used by the normal collage
        // generator. The manual arrangement editor uses this so the first
        // manual layout exactly matches the existing automatic layout.
        public static List<string> GetDefaultOrderedImagePaths(
            IList<string> imageFiles,
            IProgress<string> progress = null)
        {
            List<CollageImageInfo> images =
                ReadImageInformation(imageFiles, progress);

            if (images.Count == 0)
                return new List<string>();

            Dictionary<string, int> sizeCounts =
                images
                    .GroupBy(i => i.SizeKey, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Count(),
                        StringComparer.OrdinalIgnoreCase);

            return images
                .OrderByDescending(i => sizeCounts[i.SizeKey])
                .ThenByDescending(i => i.Width)
                .ThenByDescending(i => i.Height)
                .ThenBy(
                    i => System.IO.Path.GetFileName(i.Path),
                    StringComparer.OrdinalIgnoreCase)
                .Select(i => i.Path)
                .ToList();
        }

        private static List<CollageImageInfo> BuildManualRenderImages(
            IList<CollageImageInfo> sorted,
            IList<string> manualSlotPaths,
            IProgress<string> progress)
        {
            Dictionary<string, CollageImageInfo> byPath =
                sorted.ToDictionary(
                    i => i.Path,
                    i => i,
                    StringComparer.OrdinalIgnoreCase);

            HashSet<string> used =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            List<CollageImageInfo> result =
                new List<CollageImageInfo>();

            foreach (string path in manualSlotPaths)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    result.Add(null);
                    continue;
                }

                CollageImageInfo info;

                if (!byPath.TryGetValue(path, out info))
                {
                    Report(
                        progress,
                        "[COLLAGE] Manual layout skipped missing image: " +
                        System.IO.Path.GetFileName(path));
                    result.Add(null);
                    continue;
                }

                if (!used.Add(path))
                {
                    Report(
                        progress,
                        "[COLLAGE] Manual layout ignored duplicate image: " +
                        System.IO.Path.GetFileName(path));
                    result.Add(null);
                    continue;
                }

                result.Add(info);
            }

            return result;
        }

        private static List<CollageImageInfo> ReadImageInformation(
            IList<string> imageFiles,
            IProgress<string> progress)
        {
            List<CollageImageInfo> result =
                new List<CollageImageInfo>();

            for (int i = 0; i < imageFiles.Count; i++)
            {
                string file = imageFiles[i];

                try
                {
                    using (Image img = Image.FromFile(file))
                    {
                        result.Add(
                            new CollageImageInfo
                            {
                                Path = file,
                                Width = img.Width,
                                Height = img.Height
                            });
                    }
                }
                catch (Exception ex)
                {
                    Report(
                        progress,
                        "[COLLAGE] Skipped unreadable image: " +
                        System.IO.Path.GetFileName(file) +
                        " (" + ex.Message + ")");
                }

                if (i == 0 ||
                    i == imageFiles.Count - 1 ||
                    (i + 1) % 25 == 0)
                {
                    Report(
                        progress,
                        "[COLLAGE] Scanned " +
                        (i + 1) +
                        " / " +
                        imageFiles.Count +
                        " image(s)");
                }
            }

            return result;
        }

        // ============================================================
        // PDF
        // Creates each A4 page as a high-quality 300 DPI bitmap, then
        // embeds that bitmap into a standards-compliant PDF page.
        // No extra PDF NuGet package is required.
        // ============================================================
        private static void GeneratePdf(
            IList<CollageImageInfo> images,
            string outputPath,
            int gridSize,
            bool landscape,
            bool showFilename,
            bool drawBorders,
            IProgress<string> progress,
            IDictionary<string, string> captions,
            IDictionary<string, string> customNames,
            string displayMode)
        {
            const double a4WidthInches = 8.2677165354;
            const double a4HeightInches = 11.6929133858;

            int pagePixelWidth =
                (int)Math.Round(
                    (landscape ? a4HeightInches : a4WidthInches) *
                    PdfDpi);

            int pagePixelHeight =
                (int)Math.Round(
                    (landscape ? a4WidthInches : a4HeightInches) *
                    PdfDpi);

            double pageWidthPoints =
                (landscape ? a4HeightInches : a4WidthInches) * 72.0;

            double pageHeightPoints =
                (landscape ? a4WidthInches : a4HeightInches) * 72.0;

            bool showCaption =
                !string.Equals(displayMode, "None", StringComparison.OrdinalIgnoreCase) &&
                (showFilename ||
                 string.Equals(displayMode, "Remark", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(displayMode, "Both", StringComparison.OrdinalIgnoreCase) ||
                 (captions != null &&
                  captions.Any(
                      p => !string.IsNullOrWhiteSpace(p.Value))));

            int perPage = gridSize * gridSize;
            List<byte[]> jpegPages = new List<byte[]>();

            int totalPages =
                (int)Math.Ceiling(
                    images.Count / (double)perPage);

            for (int pageIndex = 0;
                 pageIndex < totalPages;
                 pageIndex++)
            {
                using (Bitmap pageBitmap =
                    new Bitmap(
                        pagePixelWidth,
                        pagePixelHeight,
                        PixelFormat.Format24bppRgb))
                {
                    pageBitmap.SetResolution(PdfDpi, PdfDpi);

                    using (Graphics g = Graphics.FromImage(pageBitmap))
                    {
                        g.Clear(Color.White);
                        g.CompositingQuality =
                            CompositingQuality.HighQuality;
                        g.InterpolationMode =
                            InterpolationMode.HighQualityBicubic;
                        g.SmoothingMode =
                            SmoothingMode.HighQuality;
                        g.PixelOffsetMode =
                            PixelOffsetMode.HighQuality;

                        DrawCollagePage(
                            g,
                            pagePixelWidth,
                            pagePixelHeight,
                            images,
                            pageIndex * perPage,
                            gridSize,
                            showCaption,
                            drawBorders,
                            captions,
                            customNames,
                            displayMode);
                    }

                    jpegPages.Add(
                        SaveJpegToBytes(
                            pageBitmap,
                            100L));
                }

                Report(
                    progress,
                    "[PDF] Page " +
                    (pageIndex + 1) +
                    " / " +
                    totalPages +
                    " rendered");
            }

            WriteJpegPagesToPdf(
                jpegPages,
                pagePixelWidth,
                pagePixelHeight,
                pageWidthPoints,
                pageHeightPoints,
                outputPath);

            Report(
                progress,
                "[PDF] Saved " + totalPages + " page(s)");
        }

        private static void DrawCollagePage(
            Graphics g,
            int pageWidth,
            int pageHeight,
            IList<CollageImageInfo> images,
            int startIndex,
            int gridSize,
            bool showFilename,
            bool drawBorders,
            IDictionary<string, string> captions,
            IDictionary<string, string> customNames,
            string displayMode)
        {
            int outerMargin =
                (int)Math.Round(20.0 / 72.0 * PdfDpi);

            int innerMargin =
                (int)Math.Round(10.0 / 72.0 * PdfDpi);

            bool showOriginalFilename =
                string.Equals(displayMode, "Filename", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(displayMode, "Both", StringComparison.OrdinalIgnoreCase);
            bool showRemark =
                string.Equals(displayMode, "Remark", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(displayMode, "Both", StringComparison.OrdinalIgnoreCase);

            bool hasAnyRemark =
                captions != null &&
                captions.Any(
                    p => !string.IsNullOrWhiteSpace(p.Value));

            int captionLineHeight =
                (int)Math.Round(16.0 / 72.0 * PdfDpi);

            int filenameHeight =
                showOriginalFilename
                    ? captionLineHeight
                    : 0;
            int remarkHeight =
                showRemark
                    ? captionLineHeight
                    : 0;

            int captionAreaHeight =
                filenameHeight + remarkHeight;

            int usableWidth =
                pageWidth - (outerMargin * 2);

            int usableHeight =
                pageHeight - (outerMargin * 2);

            float cellWidth =
                usableWidth / (float)gridSize;

            float cellHeight =
                usableHeight / (float)gridSize;

            using (Pen borderPen = new Pen(Color.Gray, 2f))
            using (Font filenameFont =
                new Font("Arial", 8f, FontStyle.Regular, GraphicsUnit.Point))
            using (SolidBrush textBrush =
                new SolidBrush(Color.Black))
            using (StringFormat centerText =
                new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter
                })
            {
                int perPage = gridSize * gridSize;

                for (int slot = 0; slot < perPage; slot++)
                {
                    int imageIndex = startIndex + slot;

                    if (imageIndex >= images.Count)
                        break;

                    int row = slot / gridSize;
                    int col = slot % gridSize;

                    RectangleF cell =
                        new RectangleF(
                            outerMargin + (col * cellWidth),
                            outerMargin + (row * cellHeight),
                            cellWidth,
                            cellHeight);

                    if (drawBorders)
                        g.DrawRectangle(
                            borderPen,
                            cell.X,
                            cell.Y,
                            cell.Width,
                            cell.Height);

                    RectangleF imageArea =
                        new RectangleF(
                            cell.X + innerMargin,
                            cell.Y + innerMargin,
                            Math.Max(
                                1,
                                cell.Width - (innerMargin * 2)),
                            Math.Max(
                                1,
                                cell.Height -
                                (innerMargin * 2) -
                                captionAreaHeight));

                    CollageImageInfo info =
                        images[imageIndex];

                    if (info == null ||
                        string.IsNullOrWhiteSpace(info.Path))
                    {
                        // Manual layouts may intentionally leave cells empty.
                        continue;
                    }

                    try
                    {
                        RectangleF drawRect;

                        using (Image image =
                            Image.FromFile(info.Path))
                        {
                            drawRect =
                                CalculateFitRectangle(
                                    image.Width,
                                    image.Height,
                                    imageArea);

                            Rectangle drawRectPixels =
                                Rectangle.Round(drawRect);

                            g.DrawImage(
                                image,
                                drawRectPixels,
                                new Rectangle(
                                    0,
                                    0,
                                    image.Width,
                                    image.Height),
                                GraphicsUnit.Pixel);
                        }

                        string remark = null;

                        if (customNames != null)
                            customNames.TryGetValue(info.Path, out remark);

                        if (string.IsNullOrWhiteSpace(remark) &&
                            captions != null)
                        {
                            captions.TryGetValue(info.Path, out remark);
                        }

                        if (string.IsNullOrWhiteSpace(remark))
                        {
                            remark =
                                System.IO.Path.GetFileNameWithoutExtension(
                                    info.Path);
                        }

                        bool drawFilename = showOriginalFilename;
                        bool drawRemark = showRemark;

                        // The filename line is always the original source
                        // filename. The editable custom remark is independent
                        // and is shown on the second line when selected.
                        string displayFilename =
                            System.IO.Path.GetFileNameWithoutExtension(
                                info.Path);

                        if (drawFilename || drawRemark)
                        {
                            float textY =
                                cell.Bottom -
                                captionAreaHeight +
                                Math.Max(
                                    2f,
                                    (float)Math.Round(
                                        2.0 / 72.0 * PdfDpi));

                            if (drawFilename)
                            {
                                g.DrawString(
                                    displayFilename,
                                    filenameFont,
                                    textBrush,
                                    new RectangleF(
                                        cell.X,
                                        textY,
                                        cell.Width,
                                        filenameHeight),
                                    centerText);
                                textY += filenameHeight;
                            }

                            if (drawRemark)
                            {
                                g.DrawString(
                                    remark,
                                    filenameFont,
                                    textBrush,
                                    new RectangleF(
                                        cell.X,
                                        textY,
                                        cell.Width,
                                        remarkHeight),
                                    centerText);
                            }
                        }
                    }
                    catch
                    {
                        // Image was already validated during scanning.
                        // If it becomes unavailable during generation,
                        // leave its cell blank and continue.
                    }
                }
            }
        }

        private static RectangleF CalculateFitRectangle(
            int sourceWidth,
            int sourceHeight,
            RectangleF target)
        {
            double scale =
                Math.Min(
                    target.Width / Math.Max(1.0, sourceWidth),
                    target.Height / Math.Max(1.0, sourceHeight));

            float width =
                (float)(sourceWidth * scale);

            float height =
                (float)(sourceHeight * scale);

            return new RectangleF(
                target.X + ((target.Width - width) / 2f),
                target.Y + ((target.Height - height) / 2f),
                width,
                height);
        }

        private static byte[] SaveJpegToBytes(
            Bitmap bitmap,
            long quality)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                ImageCodecInfo codec =
                    ImageCodecInfo
                        .GetImageEncoders()
                        .FirstOrDefault(
                            c => c.FormatID ==
                                 ImageFormat.Jpeg.Guid);

                if (codec == null)
                {
                    bitmap.Save(
                        stream,
                        ImageFormat.Jpeg);
                }
                else
                {
                    using (EncoderParameters parameters =
                        new EncoderParameters(1))
                    {
                        parameters.Param[0] =
                            new EncoderParameter(
                                System.Drawing.Imaging.Encoder.Quality,
                                quality);

                        bitmap.Save(
                            stream,
                            codec,
                            parameters);
                    }
                }

                return stream.ToArray();
            }
        }

        private static void WriteJpegPagesToPdf(
            IList<byte[]> jpegPages,
            int imagePixelWidth,
            int imagePixelHeight,
            double pageWidthPoints,
            double pageHeightPoints,
            string outputPath)
        {
            int pageCount = jpegPages.Count;

            int catalogObject = 1;
            int pagesObject = 2;

            List<int> pageObjects = new List<int>();

            for (int i = 0; i < pageCount; i++)
                pageObjects.Add(3 + (i * 3));

            List<byte[]> objects = new List<byte[]>();

            objects.Add(
                Ascii(
                    "<< /Type /Catalog /Pages " +
                    pagesObject +
                    " 0 R >>"));

            StringBuilder kids =
                new StringBuilder();

            foreach (int pageObject in pageObjects)
                kids.Append(pageObject).Append(" 0 R ");

            objects.Add(
                Ascii(
                    "<< /Type /Pages /Kids [ " +
                    kids +
                    "] /Count " +
                    pageCount +
                    " >>"));

            for (int i = 0; i < pageCount; i++)
            {
                int pageObject = 3 + (i * 3);
                int imageObject = pageObject + 1;
                int contentObject = pageObject + 2;

                string imageName = "Im" + (i + 1);

                string pageBody =
                    "<< /Type /Page /Parent " +
                    pagesObject +
                    " 0 R /MediaBox [0 0 " +
                    FormatPdfNumber(pageWidthPoints) +
                    " " +
                    FormatPdfNumber(pageHeightPoints) +
                    "] /Resources << /XObject << /" +
                    imageName +
                    " " +
                    imageObject +
                    " 0 R >> >> /Contents " +
                    contentObject +
                    " 0 R >>";

                objects.Add(
                    Ascii(pageBody));

                byte[] jpeg =
                    jpegPages[i];

                byte[] imageHeader =
                    Ascii(
                        "<< /Type /XObject /Subtype /Image /Width " +
                        imagePixelWidth +
                        " /Height " +
                        imagePixelHeight +
                        " /ColorSpace /DeviceRGB /BitsPerComponent 8 " +
                        "/Filter /DCTDecode /Length " +
                        jpeg.Length +
                        " >>\nstream\n");

                byte[] imageFooter =
                    Ascii(
                        "\nendstream");

                objects.Add(
                    Combine(
                        imageHeader,
                        jpeg,
                        imageFooter));

                string content =
                    "q\n" +
                    FormatPdfNumber(pageWidthPoints) +
                    " 0 0 " +
                    FormatPdfNumber(pageHeightPoints) +
                    " 0 0 cm\n/" +
                    imageName +
                    " Do\nQ\n";

                byte[] contentBytes =
                    Ascii(content);

                objects.Add(
                    Combine(
                        Ascii(
                            "<< /Length " +
                            contentBytes.Length +
                            " >>\nstream\n"),
                        contentBytes,
                        Ascii("endstream")));
            }

            using (FileStream fs =
                new FileStream(
                    outputPath,
                    FileMode.Create,
                    FileAccess.Write))
            {
                WriteAscii(
                    fs,
                    "%PDF-1.4\n%NPPLPrintMaster\n");

                List<long> offsets =
                    new List<long>();

                for (int i = 0; i < objects.Count; i++)
                {
                    int objectNumber = i + 1;

                    offsets.Add(fs.Position);

                    WriteAscii(
                        fs,
                        objectNumber +
                        " 0 obj\n");

                    fs.Write(
                        objects[i],
                        0,
                        objects[i].Length);

                    WriteAscii(
                        fs,
                        "\nendobj\n");
                }

                long xrefPosition =
                    fs.Position;

                WriteAscii(
                    fs,
                    "xref\n0 " +
                    (objects.Count + 1) +
                    "\n");

                WriteAscii(
                    fs,
                    "0000000000 65535 f \n");

                foreach (long offset in offsets)
                {
                    WriteAscii(
                        fs,
                        offset.ToString("0000000000") +
                        " 00000 n \n");
                }

                WriteAscii(
                    fs,
                    "trailer\n<< /Size " +
                    (objects.Count + 1) +
                    " /Root " +
                    catalogObject +
                    " 0 R >>\nstartxref\n" +
                    xrefPosition +
                    "\n%%EOF");
            }
        }

        // ============================================================
        // WORD
        // Generates a self-contained Rich Text Format file that opens
        // directly in Microsoft Word. Images are embedded in the file.
        // ============================================================
        // ============================================================
        // WORD
        // Generates a real .DOCX file using NPOI XWPF.
        // The image is placed first in each grid cell, with the
        // optional filename underneath it.
        // ============================================================
        // ============================================================
        // WORD
        // Generates a real .DOCX file.
        //
        // Word's table layout engine can reflow rows independently of
        // image dimensions, which can cause collage images to overlap.
        // To guarantee stable positioning, each collage page is first
        // rendered with the same renderer used by PDF, then inserted
        // into Word as one page image.
        // ============================================================
        private static void GenerateWordDocx(
            IList<CollageImageInfo> images,
            string outputPath,
            int gridSize,
            bool landscape,
            bool showFilename,
            bool drawBorders,
            IProgress<string> progress)
        {
            const double a4WidthInches = 8.2677165354;
            const double a4HeightInches = 11.6929133858;

            int pagePixelWidth =
                (int)Math.Round(
                    (landscape ? a4HeightInches : a4WidthInches) *
                    PdfDpi);

            int pagePixelHeight =
                (int)Math.Round(
                    (landscape ? a4WidthInches : a4HeightInches) *
                    PdfDpi);

            int perPage =
                gridSize * gridSize;

            int totalPages =
                (int)Math.Ceiling(
                    images.Count /
                    (double)perPage);

            XWPFDocument document =
                new XWPFDocument();

            // Word otherwise inherits the application's/default printer
            // paper size, which can be Letter. Force the document to A4.
            NPOI.OpenXmlFormats.Wordprocessing.CT_SectPr sectionProperties =
                document.Document.body.sectPr;

            if (sectionProperties == null)
            {
                sectionProperties =
                    document.Document.body.AddNewSectPr();
            }

            NPOI.OpenXmlFormats.Wordprocessing.CT_PageSz pageSize =
                sectionProperties.pgSz;

            if (pageSize == null)
            {
                pageSize =
                    sectionProperties.AddNewPgSz();
            }

            pageSize.w =
                landscape ? 16838UL : 11906UL;

            pageSize.h =
                landscape ? 11906UL : 16838UL;

            try
            {
                for (int pageIndex = 0;
                     pageIndex < totalPages;
                     pageIndex++)
                {
                    using (Bitmap pageBitmap =
                        new Bitmap(
                            pagePixelWidth,
                            pagePixelHeight,
                            PixelFormat.Format24bppRgb))
                    {
                        pageBitmap.SetResolution(
                            PdfDpi,
                            PdfDpi);

                        using (Graphics g =
                            Graphics.FromImage(pageBitmap))
                        {
                            g.Clear(Color.White);
                            g.CompositingQuality =
                                CompositingQuality.HighQuality;
                            g.InterpolationMode =
                                InterpolationMode.HighQualityBicubic;
                            g.SmoothingMode =
                                SmoothingMode.HighQuality;
                            g.PixelOffsetMode =
                                PixelOffsetMode.HighQuality;

                            DrawCollagePage(
                                g,
                                pagePixelWidth,
                                pagePixelHeight,
                                images,
                                pageIndex * perPage,
                                gridSize,
                                showFilename,
                                drawBorders,
                                null,
                                null,
                                "Filename");
                        }

                        using (MemoryStream imageStream =
                            new MemoryStream())
                        {
                            pageBitmap.Save(
                                imageStream,
                                ImageFormat.Png);

                            imageStream.Position = 0;

                            XWPFParagraph paragraph =
                                document.CreateParagraph();

                            paragraph.Alignment =
                                ParagraphAlignment.CENTER;

                            // Keep the rendered page safely inside
                            // Word's A4 printable area. This prevents
                            // Word from pushing the first collage onto
                            // a second page and leaving page 1 blank.
                            double maxWordWidthInches =
                                landscape ? 8.55 : 6.05;

                            double maxWordHeightInches =
                                landscape ? 6.05 : 8.55;

                            double pageAspect =
                                pagePixelHeight /
                                Math.Max(
                                    1.0,
                                    pagePixelWidth);

                            double drawWidthInches =
                                maxWordWidthInches;

                            double drawHeightInches =
                                drawWidthInches *
                                pageAspect;

                            if (drawHeightInches >
                                maxWordHeightInches)
                            {
                                drawHeightInches =
                                    maxWordHeightInches;

                                drawWidthInches =
                                    drawHeightInches /
                                    Math.Max(
                                        0.01,
                                        pageAspect);
                            }

                            int widthEmu =
                                (int)Math.Round(
                                    drawWidthInches *
                                    914400.0);

                            int heightEmu =
                                (int)Math.Round(
                                    drawHeightInches *
                                    914400.0);

                            XWPFRun run =
                                paragraph.CreateRun();

                            run.AddPicture(
                                imageStream,
                                (int)NPOI.XWPF.UserModel.PictureType.PNG,
                                "CollagePage_" +
                                (pageIndex + 1) +
                                ".png",
                                widthEmu,
                                heightEmu);

                            if (pageIndex <
                                totalPages - 1)
                            {
                                run.AddBreak(
                                    BreakType.PAGE);
                            }
                        }
                    }

                    Report(
                        progress,
                        "[WORD] Page " +
                        (pageIndex + 1) +
                        " / " +
                        totalPages +
                        " rendered");
                }

                using (FileStream stream =
                    new FileStream(
                        outputPath,
                        FileMode.Create,
                        FileAccess.Write))
                {
                    document.Write(stream);
                }
            }
            finally
            {
                document.Close();
            }

            Report(
                progress,
                "[WORD] Saved " +
                totalPages +
                " page(s)");
        }

        private static byte[] ConvertImageToPngBytes(
            string imagePath)
        {
            using (Image image =
                Image.FromFile(imagePath))
            using (MemoryStream stream =
                new MemoryStream())
            {
                image.Save(
                    stream,
                    ImageFormat.Png);

                return stream.ToArray();
            }
        }

        private static void GenerateExcel(
            IList<CollageImageInfo> images,
            string outputPath,
            int gridSize,
            bool landscape,
            bool showFilename,
            bool drawBorders,
            IProgress<string> progress)
        {
            XSSFWorkbook workbook =
                new XSSFWorkbook();

            try
            {
                ISheet sheet =
                    workbook.CreateSheet(
                        "Image Collage");

                sheet.PrintSetup.Landscape =
                    landscape;

                // A4 paper size (Excel/BIFF paper-size code 9).
                sheet.PrintSetup.PaperSize = 9;

                sheet.FitToPage =
                    true;

                sheet.PrintSetup.FitWidth =
                    1;

                sheet.PrintSetup.FitHeight =
                    0;

                const int columnWidthCharacters = 38;
                const float rowHeightPoints = 190f;
                const int approxCellWidthPixels = 266;
                const int approxCellHeightPixels = 253;
                const int cellPaddingPixels = 10;
                const int filenameReservePixels = 28;
                const int emuPerPixel = 9525;

                for (int col = 0;
                     col < gridSize;
                     col++)
                {
                    sheet.SetColumnWidth(
                        col,
                        columnWidthCharacters * 256);
                }

                ICellStyle cellStyle =
                    workbook.CreateCellStyle();

                cellStyle.Alignment =
                    HorizontalAlignment.Center;

                cellStyle.VerticalAlignment =
                    VerticalAlignment.Bottom;

                cellStyle.WrapText = true;

                if (drawBorders)
                {
                    cellStyle.BorderTop =
                        BorderStyle.Thin;
                    cellStyle.BorderBottom =
                        BorderStyle.Thin;
                    cellStyle.BorderLeft =
                        BorderStyle.Thin;
                    cellStyle.BorderRight =
                        BorderStyle.Thin;
                }

                IFont fileNameFont =
                    workbook.CreateFont();

                fileNameFont.FontHeightInPoints = 9;
                cellStyle.SetFont(fileNameFont);

                var drawing =
                    sheet.CreateDrawingPatriarch();

                ICreationHelper helper =
                    workbook.GetCreationHelper();

                int totalRows =
                    (int)Math.Ceiling(
                        images.Count /
                        (double)gridSize);

                int imageIndex = 0;

                for (int rowIndex = 0;
                     rowIndex < totalRows;
                     rowIndex++)
                {
                    IRow row =
                        sheet.CreateRow(rowIndex);

                    row.HeightInPoints =
                        rowHeightPoints;

                    for (int col = 0;
                         col < gridSize;
                         col++)
                    {
                        NPOI.SS.UserModel.ICell cell =
                            row.CreateCell(col);

                        cell.CellStyle =
                            cellStyle;

                        if (imageIndex >= images.Count)
                            continue;

                        CollageImageInfo info =
                            images[imageIndex];

                        if (showFilename)
                        {
                            cell.SetCellValue(
                                System.IO.Path.GetFileNameWithoutExtension(
                                    info.Path));
                        }

                        byte[] png =
                            ConvertImageToPngBytes(
                                info.Path);

                        int pictureIndex =
                            workbook.AddPicture(
                                png,
                                NPOI.SS.UserModel.PictureType.PNG);

                        int availableWidth =
                            approxCellWidthPixels -
                            (cellPaddingPixels * 2);

                        int availableHeight =
                            approxCellHeightPixels -
                            (cellPaddingPixels * 2) -
                            (showFilename
                                ? filenameReservePixels
                                : 0);

                        double scale =
                            Math.Min(
                                availableWidth /
                                Math.Max(1.0, info.Width),
                                availableHeight /
                                Math.Max(1.0, info.Height));

                        int drawWidth =
                            Math.Max(
                                1,
                                (int)Math.Round(
                                    info.Width * scale));

                        int drawHeight =
                            Math.Max(
                                1,
                                (int)Math.Round(
                                    info.Height * scale));

                        int offsetX =
                            cellPaddingPixels +
                            Math.Max(
                                0,
                                (availableWidth - drawWidth) / 2);

                        int offsetY =
                            cellPaddingPixels +
                            Math.Max(
                                0,
                                (availableHeight - drawHeight) / 2);

                        IClientAnchor anchor =
                            helper.CreateClientAnchor();

                        anchor.Col1 = col;
                        anchor.Row1 = rowIndex;
                        anchor.Col2 = col;
                        anchor.Row2 = rowIndex;
                        anchor.Dx1 =
                            offsetX * emuPerPixel;
                        anchor.Dy1 =
                            offsetY * emuPerPixel;
                        anchor.Dx2 =
                            (offsetX + drawWidth) *
                            emuPerPixel;
                        anchor.Dy2 =
                            (offsetY + drawHeight) *
                            emuPerPixel;

                        drawing.CreatePicture(
                            anchor,
                            pictureIndex);

                        imageIndex++;
                    }

                    Report(
                        progress,
                        "[EXCEL] Placed " +
                        imageIndex +
                        " / " +
                        images.Count +
                        " image(s)");
                }

                using (FileStream stream =
                    new FileStream(
                        outputPath,
                        FileMode.Create,
                        FileAccess.Write))
                {
                    workbook.Write(stream);
                }
            }
            finally
            {
                workbook.Close();
            }
        }

        private static string FormatPdfNumber(
            double value)
        {
            return value.ToString(
                "0.###",
                System.Globalization.CultureInfo.InvariantCulture);
        }

        private static byte[] Ascii(
            string value)
        {
            return Encoding.ASCII.GetBytes(value);
        }

        private static byte[] Combine(
            params byte[][] arrays)
        {
            int total = arrays.Sum(a => a.Length);

            byte[] result =
                new byte[total];

            int offset = 0;

            foreach (byte[] array in arrays)
            {
                Buffer.BlockCopy(
                    array,
                    0,
                    result,
                    offset,
                    array.Length);

                offset += array.Length;
            }

            return result;
        }

        private static void WriteAscii(
            Stream stream,
            string value)
        {
            byte[] bytes =
                Ascii(value);

            stream.Write(
                bytes,
                0,
                bytes.Length);
        }

        private static void Report(
            IProgress<string> progress,
            string message)
        {
            if (progress != null)
                progress.Report(message);
        }
    }
}
