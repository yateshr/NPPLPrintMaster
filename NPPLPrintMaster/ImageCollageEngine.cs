using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
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

        // page:        the grid, orientation, borders, text size and the
        //              page header / footer.
        // displayMode: which text is printed under each image ("Filename",
        //              "Both", "Remark" or "None").
        // manualSlotPaths, when given, is a manual arrangement: one entry
        // per grid cell in reading order, null for an empty cell.
        public static CollageGenerationResult Generate(
            IList<string> imageFiles,
            string outputPath,
            string outputType,
            CollagePageSetup page,
            IProgress<string> progress,
            IList<string> manualSlotPaths = null,
            IDictionary<string, string> manualCaptions = null,
            IDictionary<string, string> manualCustomNames = null,
            string displayMode = "Filename",
            IDictionary<string, string> manualNotes = null)
        {
            if (imageFiles == null || imageFiles.Count == 0)
                throw new ArgumentException("No images were supplied.");

            if (page == null)
                throw new ArgumentNullException("page");

            page = page.Clone();
            page.Normalize();

            bool showFilename =
                !string.Equals(
                    displayMode,
                    CollageCellLayout.ModeNone,
                    StringComparison.OrdinalIgnoreCase);

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
                        page.CellsPerPage,
                        progress);

                Report(
                    progress,
                    "[COLLAGE] Manual arrangement: " +
                    renderImages.Count(i => i != null && !string.IsNullOrWhiteSpace(i.Path)) +
                    " assigned image(s) across " +
                    (int)Math.Ceiling(renderImages.Count / (double)page.CellsPerPage) +
                    " page(s).");
            }

            if (outputType.Equals("PDF", StringComparison.OrdinalIgnoreCase))
            {
                GeneratePdf(
                    renderImages,
                    outputPath,
                    page,
                    progress,
                    manualCaptions,
                    manualCustomNames,
                    displayMode,
                    manualNotes,
                    BuildLayout(
                        sorted,
                        renderImages,
                        page,
                        manualCaptions,
                        manualCustomNames,
                        displayMode,
                        manualNotes));
            }
            else if (outputType.Equals("Word", StringComparison.OrdinalIgnoreCase))
            {
                GenerateWordDocx(
                    sorted,
                    outputPath,
                    page,
                    showFilename,
                    progress);
            }
            else if (outputType.Equals("Excel", StringComparison.OrdinalIgnoreCase))
            {
                GenerateExcel(
                    sorted,
                    outputPath,
                    page,
                    showFilename,
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

        public static bool IsSupportedImageFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            string extension;

            try
            {
                extension = System.IO.Path.GetExtension(path).ToLowerInvariant();
            }
            catch (ArgumentException)
            {
                return false;
            }

            return extension == ".png" ||
                   extension == ".jpg" ||
                   extension == ".jpeg" ||
                   extension == ".bmp" ||
                   extension == ".tif" ||
                   extension == ".tiff";
        }

        // Describes the arrangement being written so it can be stored in
        // the PDF and imported again later for further editing.
        private static CollageLayout BuildLayout(
            IList<CollageImageInfo> allImages,
            IList<CollageImageInfo> renderImages,
            CollagePageSetup page,
            IDictionary<string, string> captions,
            IDictionary<string, string> customNames,
            string displayMode,
            IDictionary<string, string> notes)
        {
            CollageLayout layout =
                new CollageLayout
                {
                    Page = page.Clone(),
                    DisplayMode = displayMode
                };

            foreach (CollageImageInfo info in allImages)
            {
                layout.Images.Add(info.Path);

                string remark = null;

                if (customNames != null)
                    customNames.TryGetValue(info.Path, out remark);

                if (string.IsNullOrWhiteSpace(remark) && captions != null)
                    captions.TryGetValue(info.Path, out remark);

                if (!string.IsNullOrWhiteSpace(remark))
                    layout.Remarks[info.Path] = remark;

                string note;

                if (notes != null &&
                    notes.TryGetValue(info.Path, out note) &&
                    !string.IsNullOrWhiteSpace(note))
                {
                    layout.Notes[info.Path] = note;
                }
            }

            foreach (CollageImageInfo info in renderImages)
                layout.Slots.Add(info == null ? null : info.Path);

            return layout;
        }

        // Returns the same deterministic order used by the normal collage
        // generator. The manual arrangement editor uses this so the first
        // manual layout exactly matches the existing automatic layout.
        public static List<string> GetDefaultOrderedImagePaths(
            IList<string> imageFiles,
            IProgress<string> progress = null)
        {
            // Only the pixel size is needed for ordering, so the files are
            // not fully decoded here. This keeps opening the arrangement
            // workspace quick with large image sets.
            List<CollageImageInfo> images =
                ReadImageInformation(imageFiles, progress, true);

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
            int cellsPerPage,
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

            // The arrangement may have been made for a different grid. The
            // cells keep their order; empty cells after the last image are
            // dropped so no blank page is printed, then the last page is
            // filled out so all of its cell borders are drawn.
            while (result.Count > 0 && result[result.Count - 1] == null)
                result.RemoveAt(result.Count - 1);

            while (result.Count % cellsPerPage != 0)
                result.Add(null);

            return result;
        }

        private static List<CollageImageInfo> ReadImageInformation(
            IList<string> imageFiles,
            IProgress<string> progress,
            bool headerOnly = false)
        {
            List<CollageImageInfo> result =
                new List<CollageImageInfo>();

            for (int i = 0; i < imageFiles.Count; i++)
            {
                string file = imageFiles[i];

                try
                {
                    using (FileStream stream =
                        new FileStream(
                            file,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.Read))
                    using (Image img =
                        Image.FromStream(stream, false, !headerOnly))
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
        // Creates pages at the chosen paper size with images and selectable text.
        // The Word renderer below continues to use the bitmap page renderer.
        // No extra PDF NuGet package is required.
        // ============================================================
        private static void GeneratePdf(
            IList<CollageImageInfo> images,
            string outputPath,
            CollagePageSetup page,
            IProgress<string> progress,
            IDictionary<string, string> captions,
            IDictionary<string, string> customNames,
            string displayMode,
            IDictionary<string, string> notes,
            CollageLayout layout)
        {
            CollagePdfRenderer.Generate(images, outputPath, page, captions,
                customNames, displayMode, notes, layout, progress);
        }

        private static void DrawCollagePage(
            Graphics g,
            int pageWidth,
            int pageHeight,
            IList<CollageImageInfo> images,
            int startIndex,
            CollagePageSetup setup,
            IDictionary<string, string> captions,
            IDictionary<string, string> customNames,
            string displayMode,
            IDictionary<string, string> notes,
            int pageNumber,
            int pageCount,
            DateTime printedOn)
        {
            // Cell geometry and the placement of the image and its name come
            // from CollageCellLayout, which the arrangement workspace also
            // uses, so the workspace preview matches this output exactly.
            float unitsPerPoint = PdfDpi / 72f;

            RectangleF page =
                new RectangleF(0f, 0f, pageWidth, pageHeight);

            CollageCellLayout.DrawHeaderFooter(
                g,
                page,
                unitsPerPoint,
                setup,
                pageNumber,
                pageCount,
                printedOn);

            using (Pen borderPen = new Pen(Color.Gray, 2f))
            using (Font filenameFont =
                new Font(
                    CollageCellLayout.CaptionFontFamily,
                    setup.CaptionFontPoints * unitsPerPoint,
                    FontStyle.Regular,
                    GraphicsUnit.Pixel))
            using (SolidBrush textBrush =
                new SolidBrush(Color.Black))
            using (StringFormat captionFormat =
                CollageCellLayout.CreateCaptionFormat())
            {
                int perPage = setup.CellsPerPage;

                for (int slot = 0; slot < perPage; slot++)
                {
                    int imageIndex = startIndex + slot;

                    if (imageIndex >= images.Count)
                        break;

                    RectangleF cell =
                        CollageCellLayout.GetCellBounds(
                            page,
                            unitsPerPoint,
                            setup,
                            slot);

                    if (setup.DrawBorders)
                        g.DrawRectangle(
                            borderPen,
                            cell.X,
                            cell.Y,
                            cell.Width,
                            cell.Height);

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
                        string remark = null;

                        if (customNames != null)
                            customNames.TryGetValue(info.Path, out remark);

                        if (string.IsNullOrWhiteSpace(remark) &&
                            captions != null)
                        {
                            captions.TryGetValue(info.Path, out remark);
                        }

                        string note = null;

                        if (notes != null)
                            notes.TryGetValue(info.Path, out note);

                        // The filename line is always the original source
                        // filename. The editable custom remark is shown on
                        // the line below it, unless it is still the same
                        // text, in which case the name is printed once. The
                        // additional remark, if any, gets a line of its own.
                        string filenameLine;
                        string remarkLine;
                        string noteLine;

                        CollageCellLayout.ResolveCaptions(
                            info.Path,
                            remark,
                            note,
                            displayMode,
                            out filenameLine,
                            out remarkLine,
                            out noteLine);

                        using (Image image =
                            Image.FromFile(info.Path))
                        {
                            // The name sits directly below the image,
                            // centered, and the pair is centered in the cell.
                            CollageCellLayout layout =
                                CollageCellLayout.Arrange(
                                    g,
                                    filenameFont,
                                    captionFormat,
                                    cell,
                                    unitsPerPoint,
                                    image.Width,
                                    image.Height,
                                    filenameLine,
                                    remarkLine,
                                    noteLine);

                            g.DrawImage(
                                image,
                                Rectangle.Round(layout.Image),
                                new Rectangle(
                                    0,
                                    0,
                                    image.Width,
                                    image.Height),
                                GraphicsUnit.Pixel);

                            if (filenameLine != null)
                            {
                                g.DrawString(
                                    filenameLine,
                                    filenameFont,
                                    textBrush,
                                    layout.Filename,
                                    captionFormat);
                            }

                            if (remarkLine != null)
                            {
                                g.DrawString(
                                    remarkLine,
                                    filenameFont,
                                    textBrush,
                                    layout.Remark,
                                    captionFormat);
                            }

                            if (noteLine != null)
                            {
                                g.DrawString(
                                    noteLine,
                                    filenameFont,
                                    textBrush,
                                    layout.Note,
                                    captionFormat);
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
            CollagePageSetup page,
            bool showFilename,
            IProgress<string> progress)
        {
            const double a4WidthInches = 8.2677165354;
            const double a4HeightInches = 11.6929133858;

            bool landscape = page.Landscape;
            DateTime printedOn = DateTime.Now;

            int pagePixelWidth =
                (int)Math.Round(
                    (landscape ? a4HeightInches : a4WidthInches) *
                    PdfDpi);

            int pagePixelHeight =
                (int)Math.Round(
                    (landscape ? a4WidthInches : a4HeightInches) *
                    PdfDpi);

            int perPage =
                page.CellsPerPage;

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
                            g.TextRenderingHint =
                                TextRenderingHint.AntiAlias;

                            DrawCollagePage(
                                g,
                                pagePixelWidth,
                                pagePixelHeight,
                                images,
                                pageIndex * perPage,
                                page,
                                null,
                                null,
                                showFilename
                                    ? CollageCellLayout.ModeFilename
                                    : CollageCellLayout.ModeNone,
                                null,
                                pageIndex + 1,
                                totalPages,
                                printedOn);
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
            CollagePageSetup page,
            bool showFilename,
            IProgress<string> progress)
        {
            // A worksheet is one continuous grid, so only the number of
            // columns applies; rows simply continue down the sheet.
            int gridSize = page.Columns;
            bool drawBorders = page.DrawBorders;

            XSSFWorkbook workbook =
                new XSSFWorkbook();

            try
            {
                ISheet sheet =
                    workbook.CreateSheet(
                        "Image Collage");

                sheet.PrintSetup.Landscape =
                    page.Landscape;

                // Excel prints its own header and footer; "&P of &N" is
                // filled in by Excel with the page numbers.
                if (page.HasHeader)
                    sheet.Header.Center = page.HeaderText;

                if (page.ShowDate)
                    sheet.Footer.Left =
                        CollageCellLayout.FormatDate(DateTime.Now);

                if (page.ShowPageNumbers)
                    sheet.Footer.Right = "Page &P of &N";

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

                // One point above the printed size, as before (8 pt -> 9).
                fileNameFont.FontHeightInPoints =
                    Math.Round(page.CaptionFontPoints) + 1;
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

        private static void Report(
            IProgress<string> progress,
            string message)
        {
            if (progress != null)
                progress.Report(message);
        }
    }
}
