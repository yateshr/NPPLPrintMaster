using System;
using System.Drawing;
using System.IO;

namespace NPPLPrintMaster
{
    /// <summary>
    /// Single source of truth for how one collage cell is laid out.
    ///
    /// The PDF renderer and the arrangement workspace both call this, so the
    /// workspace preview is exactly what the PDF produces: the image is fitted
    /// inside the cell and its name sits directly below it, centered.
    ///
    /// All measurements are expressed in PDF points and converted with
    /// unitsPerPoint (300 DPI pixels for the PDF, zoomed screen pixels for
    /// the workspace).
    /// </summary>
    internal sealed class CollageCellLayout
    {
        public const float OuterMarginPoints = 20f;
        public const float InnerMarginPoints = 10f;
        public const float CaptionGapPoints = 4f;
        public const int MaxCaptionLines = 2;
        public const string CaptionFontFamily = "Arial";

        // Space taken from the grid for the page header and footer, and
        // the size of their text.
        public const float HeaderBandPoints = 22f;
        public const float HeaderTextHeightPoints = 16f;
        public const float HeaderFontPoints = 10f;
        public const float FooterBandPoints = 18f;
        public const float FooterTextHeightPoints = 12f;
        public const float FooterFontPoints = 8f;

        public const string ModeFilename = "Filename";
        public const string ModeBoth = "Both";
        public const string ModeRemark = "Remark";
        public const string ModeNone = "None";

        public RectangleF Image;
        public RectangleF Filename;
        public RectangleF Remark;
        public RectangleF Note;

        // ------------------------------------------------------------
        // Page geometry
        // ------------------------------------------------------------

        // The part of the page divided into cells: inside the outer margin
        // and below the header / above the footer when those are printed.
        public static RectangleF GetGridBounds(
            RectangleF page,
            float unitsPerPoint,
            CollagePageSetup setup)
        {
            float outer = OuterMarginPoints * unitsPerPoint;

            float top =
                outer +
                (setup.HasHeader ? HeaderBandPoints * unitsPerPoint : 0f);

            float bottom =
                outer +
                (setup.HasFooter ? FooterBandPoints * unitsPerPoint : 0f);

            return new RectangleF(
                page.X + outer,
                page.Y + top,
                Math.Max(1f, page.Width - (outer * 2f)),
                Math.Max(1f, page.Height - top - bottom));
        }

        public static RectangleF GetCellBounds(
            RectangleF page,
            float unitsPerPoint,
            CollagePageSetup setup,
            int cellIndex)
        {
            RectangleF grid = GetGridBounds(page, unitsPerPoint, setup);

            float cellWidth = grid.Width / setup.Columns;
            float cellHeight = grid.Height / setup.Rows;

            return new RectangleF(
                grid.X + ((cellIndex % setup.Columns) * cellWidth),
                grid.Y + ((cellIndex / setup.Columns) * cellHeight),
                cellWidth,
                cellHeight);
        }

        // The cell on this page nearest to a point, so a point on the page
        // margin still maps to the closest cell.
        public static int GetNearestCell(
            RectangleF page,
            float unitsPerPoint,
            CollagePageSetup setup,
            PointF point)
        {
            RectangleF grid = GetGridBounds(page, unitsPerPoint, setup);

            int column =
                (int)Math.Floor(
                    (point.X - grid.X) / (grid.Width / setup.Columns));

            int row =
                (int)Math.Floor(
                    (point.Y - grid.Y) / (grid.Height / setup.Rows));

            column = Math.Max(0, Math.Min(setup.Columns - 1, column));
            row = Math.Max(0, Math.Min(setup.Rows - 1, row));

            return (row * setup.Columns) + column;
        }

        // ------------------------------------------------------------
        // Header and footer
        // ------------------------------------------------------------
        public static string FormatDate(DateTime date)
        {
            return date.ToString(
                "dd-MMM-yyyy",
                System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string FormatPageNumber(int pageNumber, int pageCount)
        {
            return "Page " + pageNumber + " of " + pageCount;
        }

        // Draws the page header (centered at the top) and the footer (date
        // on the left, page number on the right). Used by the PDF writer
        // and by the workspace preview so both show the same thing.
        public static void DrawHeaderFooter(
            Graphics g,
            RectangleF page,
            float unitsPerPoint,
            CollagePageSetup setup,
            int pageNumber,
            int pageCount,
            DateTime date)
        {
            if (!setup.HasHeader && !setup.HasFooter)
                return;

            float outer = OuterMarginPoints * unitsPerPoint;
            float width = Math.Max(1f, page.Width - (outer * 2f));

            using (StringFormat format =
                new StringFormat
                {
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap
                })
            {
                if (setup.HasHeader &&
                    HeaderFontPoints * unitsPerPoint >= 3f)
                {
                    using (Font font =
                        new Font(
                            CaptionFontFamily,
                            HeaderFontPoints * unitsPerPoint,
                            FontStyle.Bold,
                            GraphicsUnit.Pixel))
                    {
                        format.Alignment = StringAlignment.Center;

                        g.DrawString(
                            setup.HeaderText.Trim(),
                            font,
                            Brushes.Black,
                            new RectangleF(
                                page.X + outer,
                                page.Y + outer,
                                width,
                                HeaderTextHeightPoints * unitsPerPoint),
                            format);
                    }
                }

                if (setup.HasFooter &&
                    FooterFontPoints * unitsPerPoint >= 3f)
                {
                    RectangleF footer =
                        new RectangleF(
                            page.X + outer,
                            page.Bottom - outer -
                            (FooterTextHeightPoints * unitsPerPoint),
                            width,
                            FooterTextHeightPoints * unitsPerPoint);

                    using (Font font =
                        new Font(
                            CaptionFontFamily,
                            FooterFontPoints * unitsPerPoint,
                            FontStyle.Regular,
                            GraphicsUnit.Pixel))
                    {
                        if (setup.ShowDate)
                        {
                            format.Alignment = StringAlignment.Near;

                            g.DrawString(
                                FormatDate(date),
                                font,
                                Brushes.Black,
                                footer,
                                format);
                        }

                        if (setup.ShowPageNumbers)
                        {
                            format.Alignment = StringAlignment.Far;

                            g.DrawString(
                                FormatPageNumber(pageNumber, pageCount),
                                font,
                                Brushes.Black,
                                footer,
                                format);
                        }
                    }
                }
            }
        }

        // ------------------------------------------------------------
        // One cell
        // ------------------------------------------------------------

        public static StringFormat CreateCaptionFormat()
        {
            return new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Near,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.LineLimit
            };
        }

        // Up to three lines are printed under an image:
        //   1. the original source filename,
        //   2. the editable remark (printed once with the filename while it
        //      has not been changed from it),
        //   3. the additional remark, when one has been typed.
        // Lines 2 and 3 are shown whenever remarks are shown.
        public static void ResolveCaptions(
            string imagePath,
            string remark,
            string note,
            string displayMode,
            out string filenameLine,
            out string remarkLine,
            out string noteLine)
        {
            string filename =
                Path.GetFileNameWithoutExtension(imagePath) ?? string.Empty;

            string effectiveRemark =
                string.IsNullOrWhiteSpace(remark)
                    ? filename
                    : remark.Trim();

            bool showFilename =
                string.Equals(displayMode, ModeFilename, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(displayMode, ModeBoth, StringComparison.OrdinalIgnoreCase);

            bool showRemark =
                string.Equals(displayMode, ModeRemark, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(displayMode, ModeBoth, StringComparison.OrdinalIgnoreCase);

            filenameLine = showFilename ? filename : null;
            remarkLine = showRemark ? effectiveRemark : null;

            noteLine =
                showRemark && !string.IsNullOrWhiteSpace(note)
                    ? note.Trim()
                    : null;

            if (filenameLine != null &&
                remarkLine != null &&
                string.Equals(
                    filenameLine.Trim(),
                    remarkLine.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                remarkLine = null;
            }
        }

        public static CollageCellLayout Arrange(
            Graphics g,
            Font captionFont,
            StringFormat captionFormat,
            RectangleF cell,
            float unitsPerPoint,
            int imageWidth,
            int imageHeight,
            string filenameLine,
            string remarkLine,
            string noteLine)
        {
            // The usual padding, eased off in the small cells of dense
            // grids so the image keeps most of the cell.
            float inner =
                Math.Min(
                    InnerMarginPoints * unitsPerPoint,
                    Math.Min(cell.Width, cell.Height) * 0.08f);

            RectangleF content =
                new RectangleF(
                    cell.X + inner,
                    cell.Y + inner,
                    Math.Max(1f, cell.Width - (inner * 2f)),
                    Math.Max(1f, cell.Height - (inner * 2f)));

            float lineHeight = captionFont.GetHeight(g);

            float filenameHeight =
                MeasureCaptionHeight(
                    g,
                    captionFont,
                    captionFormat,
                    filenameLine,
                    content.Width,
                    lineHeight);

            float remarkHeight =
                MeasureCaptionHeight(
                    g,
                    captionFont,
                    captionFormat,
                    remarkLine,
                    content.Width,
                    lineHeight);

            float noteHeight =
                MeasureCaptionHeight(
                    g,
                    captionFont,
                    captionFormat,
                    noteLine,
                    content.Width,
                    lineHeight);

            float captionHeight =
                filenameHeight + remarkHeight + noteHeight;

            float gap =
                captionHeight > 0f
                    ? CaptionGapPoints * unitsPerPoint
                    : 0f;

            float imageMaxHeight =
                Math.Max(1f, content.Height - captionHeight - gap);

            double scale =
                Math.Min(
                    content.Width / Math.Max(1.0, imageWidth),
                    imageMaxHeight / Math.Max(1.0, imageHeight));

            float width = (float)(Math.Max(1, imageWidth) * scale);
            float height = (float)(Math.Max(1, imageHeight) * scale);

            // Image and name are centered in the cell as one block so the
            // name always hugs the bottom edge of its image.
            float blockHeight = height + gap + captionHeight;
            float top = content.Y + ((content.Height - blockHeight) / 2f);
            float textTop = top + height + gap;

            return new CollageCellLayout
            {
                Image =
                    new RectangleF(
                        content.X + ((content.Width - width) / 2f),
                        top,
                        width,
                        height),
                Filename =
                    filenameHeight > 0f
                        ? new RectangleF(
                            content.X,
                            textTop,
                            content.Width,
                            filenameHeight)
                        : RectangleF.Empty,
                Remark =
                    remarkHeight > 0f
                        ? new RectangleF(
                            content.X,
                            textTop + filenameHeight,
                            content.Width,
                            remarkHeight)
                        : RectangleF.Empty,
                Note =
                    noteHeight > 0f
                        ? new RectangleF(
                            content.X,
                            textTop + filenameHeight + remarkHeight,
                            content.Width,
                            noteHeight)
                        : RectangleF.Empty
            };
        }

        private static float MeasureCaptionHeight(
            Graphics g,
            Font font,
            StringFormat format,
            string text,
            float width,
            float lineHeight)
        {
            if (string.IsNullOrWhiteSpace(text) || lineHeight <= 0f)
                return 0f;

            SizeF measured =
                g.MeasureString(
                    text,
                    font,
                    new SizeF(width, lineHeight * 20f),
                    format);

            int lines =
                Math.Max(
                    1,
                    Math.Min(
                        MaxCaptionLines,
                        (int)Math.Round(measured.Height / lineHeight)));

            // A little slack so LineLimit never drops the last full line.
            return (lineHeight * lines) + (lineHeight * 0.12f);
        }
    }
}
