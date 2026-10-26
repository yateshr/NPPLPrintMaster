using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using SkiaSharp;

namespace NPPLPrintMaster
{
    // Keep the shared cell geometry, but draw text and borders directly into
    // the PDF. Skia embeds the fonts and Unicode character maps for copying.
    internal static class CollagePdfRenderer
    {
        private const float Units = 300f / 72f;

        public static void Generate(IList<CollageImageInfo> images, string outputPath,
            CollagePageSetup setup, IDictionary<string, string> captions,
            IDictionary<string, string> customNames, string displayMode,
            IDictionary<string, string> notes, CollageLayout savedLayout, IProgress<string> progress)
        {
            float width = setup.Landscape ? CollageCellLayout.PageLongSidePoints : CollageCellLayout.PageShortSidePoints;
            float height = setup.Landscape ? CollageCellLayout.PageShortSidePoints : CollageCellLayout.PageLongSidePoints;
            int pages = (images.Count + setup.CellsPerPage - 1) / setup.CellsPerPage;
            DateTime printedOn = DateTime.Now;
            using (var stream = File.Create(outputPath))
            using (var document = SKDocument.CreatePdf(stream))
            using (var measurement = new Bitmap(1, 1))
            using (var g = Graphics.FromImage(measurement))
            using (var captionFont = new Font(CollageCellLayout.CaptionFontFamily,
                setup.CaptionFontPoints * Units, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var format = CollageCellLayout.CreateCaptionFormat())
            using (var typeface = SKTypeface.FromFamilyName(CollageCellLayout.CaptionFontFamily))
            using (var bold = SKTypeface.FromFamilyName(CollageCellLayout.CaptionFontFamily, SKFontStyle.Bold))
            using (var font = new SKFont(typeface, setup.CaptionFontPoints * Units))
            using (var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true })
            using (var border = new SKPaint { Color = SKColors.Gray, Style = SKPaintStyle.Stroke, StrokeWidth = 2 })
            {
                for (int pageIndex = 0; pageIndex < pages; pageIndex++)
                {
                    SKCanvas canvas = document.BeginPage(width, height);
                    canvas.Scale(1f / Units);
                    var page = new RectangleF(0, 0, width * Units, height * Units);
                    DrawHeaderFooter(canvas, page, setup, pageIndex + 1, pages, printedOn, typeface, bold, paint);
                    for (int slot = 0; slot < setup.CellsPerPage; slot++)
                    {
                        int index = pageIndex * setup.CellsPerPage + slot;
                        if (index >= images.Count) break;
                        RectangleF cell = CollageCellLayout.GetCellBounds(page, Units, setup, slot);
                        if (setup.DrawBorders) canvas.DrawRect(Rect(cell), border);
                        var info = images[index];
                        if (info == null || string.IsNullOrWhiteSpace(info.Path)) continue;
                        string remark = Get(customNames, info.Path);
                        if (string.IsNullOrWhiteSpace(remark)) remark = Get(captions, info.Path);
                        string filename, remarkLine, note;
                        CollageCellLayout.ResolveCaptions(info.Path, remark, Get(notes, info.Path), displayMode,
                            out filename, out remarkLine, out note);
                        SKBitmap bitmap;
                        try { bitmap = SKBitmap.Decode(info.Path); }
                        catch (IOException) { continue; }
                        if (bitmap == null) continue;
                        using (bitmap)
                        using (var image = SKImage.FromBitmap(bitmap))
                        {
                            var layout = CollageCellLayout.Arrange(g, captionFont, format, cell, Units,
                                bitmap.Width, bitmap.Height, filename, remarkLine, note);
                            canvas.DrawImage(image, Rect(layout.Image),
                                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
                            DrawText(canvas, filename, layout.Filename, font, paint);
                            DrawText(canvas, remarkLine, layout.Remark, font, paint);
                            DrawText(canvas, note, layout.Note, font, paint);
                        }
                    }
                    document.EndPage();
                    if (progress != null) progress.Report("[PDF] Page " + (pageIndex + 1) + " / " + pages + " rendered");
                }
                document.Close();
            }
            if (savedLayout != null)
            {
                try { AppendLayout(outputPath, savedLayout); }
                catch (Exception ex)
                {
                    if (progress != null) progress.Report("[PDF] Layout could not be stored: " + ex.Message);
                }
            }
            if (progress != null) progress.Report("[PDF] Saved " + pages + " page(s) with selectable text");
        }

        private static string Get(IDictionary<string, string> values, string path)
        {
            string value;
            return values != null && values.TryGetValue(path, out value) ? value : null;
        }

        private static SKRect Rect(RectangleF r) { return new SKRect(r.Left, r.Top, r.Right, r.Bottom); }

        private static void DrawHeaderFooter(SKCanvas canvas, RectangleF page, CollagePageSetup setup,
            int number, int count, DateTime date, SKTypeface regular, SKTypeface bold, SKPaint paint)
        {
            float outer = CollageCellLayout.OuterMarginPoints * Units;
            if (setup.HasHeader)
                using (var font = new SKFont(bold, CollageCellLayout.HeaderFontPoints * Units))
                    DrawText(canvas, setup.HeaderText.Trim(), new RectangleF(outer, outer,
                        page.Width - 2 * outer, CollageCellLayout.HeaderTextHeightPoints * Units), font, paint, 1);
            var footer = new RectangleF(outer,
                page.Height - outer - CollageCellLayout.FooterTextHeightPoints * Units,
                page.Width - 2 * outer, CollageCellLayout.FooterTextHeightPoints * Units);
            using (var font = new SKFont(regular, CollageCellLayout.FooterFontPoints * Units))
            {
                if (setup.ShowDate) DrawText(canvas, CollageCellLayout.FormatDate(date), footer, font, paint, 1, SKTextAlign.Left);
                if (setup.ShowPageNumbers) DrawText(canvas, CollageCellLayout.FormatPageNumber(number, count), footer, font, paint, 1, SKTextAlign.Right);
            }
        }

        // Wrap at word boundaries, split long filenames, and truncate the last
        // visible line with an ellipsis. No text is burned into a bitmap.
        private static void DrawText(SKCanvas canvas, string text, RectangleF box, SKFont font,
            SKPaint paint, int maxLines = CollageCellLayout.MaxCaptionLines, SKTextAlign align = SKTextAlign.Center)
        {
            if (string.IsNullOrWhiteSpace(text) || box.Width <= 0 || box.Height <= 0) return;
            var lines = new List<string>();
            string remaining = text.Replace("\r\n", "\n").Replace('\r', '\n');
            while (remaining.Length > 0 && lines.Count < maxLines)
            {
                int newline = remaining.IndexOf('\n');
                int limit = newline < 0 ? remaining.Length : newline;
                int length = 0;
                while (length < limit)
                {
                    int next = length + (char.IsHighSurrogate(remaining[length]) && length + 1 < limit ? 2 : 1);
                    if (font.MeasureText(remaining.Substring(0, next)) > box.Width) break;
                    length = next;
                }
                if (length == 0 && limit > 0) length = char.IsHighSurrogate(remaining[0]) && limit > 1 ? 2 : 1;
                if (length < limit && lines.Count + 1 < maxLines)
                {
                    int space = remaining.LastIndexOf(' ', Math.Max(0, length - 1), length);
                    if (space > 0) length = space;
                }
                string line = remaining.Substring(0, length).TrimEnd();
                remaining = remaining.Substring(length);
                if (remaining.StartsWith("\n")) remaining = remaining.Substring(1);
                else remaining = remaining.TrimStart(' ', '\t');
                if (lines.Count + 1 == maxLines && remaining.Length > 0)
                {
                    while (line.Length > 0 && font.MeasureText(line + "…") > box.Width)
                        line = line.Substring(0, line.Length - (char.IsLowSurrogate(line[line.Length - 1]) && line.Length > 1 ? 2 : 1));
                    line += "…";
                }
                lines.Add(line);
            }
            SKFontMetrics metrics = font.Metrics;
            float lineHeight = metrics.Descent - metrics.Ascent + metrics.Leading;
            float y = box.Top + Math.Max(0, (box.Height - lines.Count * lineHeight) / 2) - metrics.Ascent;
            canvas.Save();
            canvas.ClipRect(Rect(box));
            foreach (string line in lines)
            {
                float x = align == SKTextAlign.Left ? box.Left : align == SKTextAlign.Right ? box.Right : box.Left + box.Width / 2;
                canvas.DrawText(line, x, y, align, font, paint);
                y += lineHeight;
            }
            canvas.Restore();
        }

        // A valid incremental PDF update preserves Skia's embedded fonts and
        // adds the same layout stream that the existing import code understands.
        private static void AppendLayout(string path, CollageLayout layout)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite))
            {
                stream.Seek(Math.Max(0, stream.Length - 65536), SeekOrigin.Begin);
                byte[] tail = new byte[stream.Length - stream.Position];
                int read = 0;
                while (read < tail.Length)
                {
                    int n = stream.Read(tail, read, tail.Length - read);
                    if (n == 0) throw new EndOfStreamException();
                    read += n;
                }
                string text = Encoding.ASCII.GetString(tail);
                Match trailer = Regex.Match(text, @"trailer\s*<<(?<body>.*?)>>\s*startxref\s*(?<offset>\d+)\s*%%EOF\s*$", RegexOptions.Singleline);
                Match size = Regex.Match(trailer.Groups["body"].Value, @"/Size\s+(\d+)");
                Match root = Regex.Match(trailer.Groups["body"].Value, @"/Root\s+(\d+\s+\d+\s+R)");
                if (!trailer.Success || !size.Success || !root.Success) throw new InvalidDataException("PDF trailer is unavailable.");
                int number = int.Parse(size.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                byte[] data = CollageLayoutFile.BuildPdfObject(layout, Path.GetDirectoryName(path));
                stream.Seek(0, SeekOrigin.End);
                Write(stream, "\n");
                long objectOffset = stream.Position;
                Write(stream, number + " 0 obj\n");
                stream.Write(data, 0, data.Length);
                Write(stream, "\nendobj\n");
                long xref = stream.Position;
                Write(stream, "xref\n0 1\n0000000000 65535 f \n" + number + " 1\n" + objectOffset.ToString("0000000000", System.Globalization.CultureInfo.InvariantCulture) +
                    " 00000 n \ntrailer\n<< /Size " + (number + 1) + " /Root " + root.Groups[1].Value +
                    " /Prev " + trailer.Groups["offset"].Value + " >>\nstartxref\n" + xref + "\n%%EOF\n");
            }
        }

        private static void Write(Stream stream, string text)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(text);
            stream.Write(bytes, 0, bytes.Length);
        }
    }
}
