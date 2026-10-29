using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace NPPLPrintMaster
{
    /// <summary>
    /// How one collage sheet is set up: the grid, the paper orientation and
    /// what is printed around the images. The PDF writer, the arrangement
    /// workspace and saved layouts all read the same setup, which is what
    /// keeps the workspace preview identical to the printed page.
    /// </summary>
    public sealed class CollagePageSetup
    {
        public const int MaxColumns = 6;
        public const int MaxRows = 8;
        public const float DefaultCaptionFontPoints = 8f;

        // The text sizes offered on the main form and in the workspace.
        public static readonly int[] CaptionFontChoices =
        {
            6, 7, 8, 9, 10, 11, 12, 14, 16
        };

        public static int NearestCaptionFontChoice(float points)
        {
            int nearest = 0;

            for (int i = 1; i < CaptionFontChoices.Length; i++)
            {
                if (Math.Abs(CaptionFontChoices[i] - points) <
                    Math.Abs(CaptionFontChoices[nearest] - points))
                {
                    nearest = i;
                }
            }

            return nearest;
        }

        public int Columns = 2;
        public int Rows = 2;
        public bool Landscape;
        public string PaperSize = "A4";
        public float CustomWidthMm = 210;
        public float CustomHeightMm = 297;
        public const float MinCustomSideMm = 50;
        public const float MaxCustomSideMm = 2000;

        private float ShortSideMm
        {
            get { return PaperSize == "A3" ? 297 : PaperSize == "A2" ? 420 :
                PaperSize == "Custom" ? Math.Min(CustomWidthMm, CustomHeightMm) : 210; }
        }
        private float LongSideMm
        {
            get { return PaperSize == "A3" ? 420 : PaperSize == "A2" ? 594 :
                PaperSize == "Custom" ? Math.Max(CustomWidthMm, CustomHeightMm) : 297; }
        }
        public float WidthMm { get { return Landscape ? LongSideMm : ShortSideMm; } }
        public float HeightMm { get { return Landscape ? ShortSideMm : LongSideMm; } }
        public float WidthPoints { get { return WidthMm * 72f / 25.4f; } }
        public float HeightPoints { get { return HeightMm * 72f / 25.4f; } }
        public string PaperDescription
        {
            get { return PaperSize + " " + (Landscape ? "Landscape" : "Portrait") + " (" +
                WidthMm.ToString("0.##", CultureInfo.InvariantCulture) + " x " +
                HeightMm.ToString("0.##", CultureInfo.InvariantCulture) + " mm)"; }
        }
        public bool DrawBorders;

        // Size of the text printed under each image.
        public float CaptionFontPoints = DefaultCaptionFontPoints;

        // Printed centered at the top of every page; empty for none.
        public string HeaderText = string.Empty;

        // Printed at the bottom of every page.
        public bool ShowDate;
        public bool ShowPageNumbers;

        public int CellsPerPage
        {
            get { return Columns * Rows; }
        }

        public bool HasHeader
        {
            get { return !string.IsNullOrWhiteSpace(HeaderText); }
        }

        public bool HasFooter
        {
            get { return ShowDate || ShowPageNumbers; }
        }

        public CollagePageSetup Clone()
        {
            return (CollagePageSetup)MemberwiseClone();
        }

        // Keeps values read from a file or typed by hand inside the
        // supported range.
        public void Normalize()
        {
            string paper = (PaperSize ?? "A4").Trim().ToUpperInvariant();
            PaperSize = paper == "CUSTOM" ? "Custom" : paper == "A3" || paper == "A2" ? paper : "A4";
            CustomWidthMm = NormalizeSide(CustomWidthMm, 210);
            CustomHeightMm = NormalizeSide(CustomHeightMm, 297);
            Columns = Math.Max(1, Math.Min(MaxColumns, Columns));
            Rows = Math.Max(1, Math.Min(MaxRows, Rows));

            if (float.IsNaN(CaptionFontPoints) ||
                CaptionFontPoints < 4f ||
                CaptionFontPoints > 36f)
            {
                CaptionFontPoints = DefaultCaptionFontPoints;
            }

            HeaderText = (HeaderText ?? string.Empty).Trim();
        }

        private static float NormalizeSide(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback :
                Math.Max(MinCustomSideMm, Math.Min(MaxCustomSideMm, value));
        }
    }

    /// <summary>
    /// A saved collage arrangement: how the page is set up, which images are
    /// in the collage, which cell each one sits in, and the text printed
    /// under it.
    /// </summary>
    public sealed class CollageLayout
    {
        public CollagePageSetup Page = new CollagePageSetup();
        public string DisplayMode = CollageCellLayout.ModeBoth;

        public List<string> Images = new List<string>();

        // One entry per grid cell, page after page; null is an empty cell.
        public List<string> Slots = new List<string>();

        public Dictionary<string, string> Remarks =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, string> Notes =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Filled when a layout is loaded: images that could not be found
        // where the layout says they are.
        public List<string> MissingImages = new List<string>();
    }

    /// <summary>
    /// Reads and writes collage layouts so an arrangement can be reopened and
    /// revised later.
    ///
    /// The layout is written in two places: inside every PDF the collage
    /// builder creates (so the PDF itself can be imported again), and in
    /// standalone ".npplcollage" files saved from the arrangement workspace.
    /// Image files are referenced, not copied. Each one is stored with its
    /// full path and its path relative to the layout, so a job folder that
    /// has been moved as a whole still opens.
    /// </summary>
    public static class CollageLayoutFile
    {
        public const string Extension = ".npplcollage";

        private const string RootName = "NpplCollageLayout";
        private const string PdfObjectHeader = "/Type /NPPLCollageLayout /Length ";
        private const int PdfTailBytes = 4 * 1024 * 1024;

        // ------------------------------------------------------------
        // Standalone files
        // ------------------------------------------------------------
        public static void Save(CollageLayout layout, string filePath)
        {
            File.WriteAllText(
                filePath,
                ToXml(layout, Path.GetDirectoryName(filePath)),
                new UTF8Encoding(false));
        }

        // Loads a layout from a ".npplcollage" file or from a PDF created
        // by the collage builder. Returns null when the file holds no layout.
        public static CollageLayout Load(string filePath)
        {
            string folder = Path.GetDirectoryName(filePath);

            string xml =
                string.Equals(
                    Path.GetExtension(filePath),
                    ".pdf",
                    StringComparison.OrdinalIgnoreCase)
                    ? ReadXmlFromPdf(filePath)
                    : File.ReadAllText(filePath, Encoding.UTF8);

            return string.IsNullOrWhiteSpace(xml)
                ? null
                : FromXml(xml, folder);
        }

        // ------------------------------------------------------------
        // XML
        // ------------------------------------------------------------
        public static string ToXml(CollageLayout layout, string baseFolder)
        {
            Dictionary<string, int> slotByPath =
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < layout.Slots.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(layout.Slots[i]))
                    slotByPath[layout.Slots[i]] = i;
            }

            CollagePageSetup page = layout.Page ?? new CollagePageSetup();

            XElement root =
                new XElement(
                    RootName,
                    new XAttribute("version", 3),
                    new XAttribute("paper", page.PaperSize),
                    new XAttribute("customWidthMm", page.CustomWidthMm.ToString("0.##", CultureInfo.InvariantCulture)),
                    new XAttribute("customHeightMm", page.CustomHeightMm.ToString("0.##", CultureInfo.InvariantCulture)),
                    new XAttribute("columns", page.Columns),
                    new XAttribute("rows", page.Rows),
                    new XAttribute(
                        "orientation",
                        page.Landscape ? "Landscape" : "Portrait"),
                    new XAttribute("borders", page.DrawBorders),
                    new XAttribute(
                        "text",
                        layout.DisplayMode ?? CollageCellLayout.ModeBoth),
                    new XAttribute(
                        "textSize",
                        page.CaptionFontPoints.ToString(
                            "0.##",
                            CultureInfo.InvariantCulture)),
                    new XAttribute(
                        "header",
                        CleanText(page.HeaderText ?? string.Empty)),
                    new XAttribute("date", page.ShowDate),
                    new XAttribute("pageNumbers", page.ShowPageNumbers),
                    new XAttribute("cells", layout.Slots.Count),
                    new XAttribute(
                        "saved",
                        DateTime.Now.ToString("s", CultureInfo.InvariantCulture)));

            foreach (string path in layout.Images)
            {
                XElement image =
                    new XElement("Image", new XAttribute("path", path));

                string relative = MakeRelativePath(baseFolder, path);
                if (relative != null)
                    image.Add(new XAttribute("relative", relative));

                int slot;
                if (slotByPath.TryGetValue(path, out slot))
                    image.Add(new XAttribute("cell", slot));

                string text;

                if (layout.Remarks.TryGetValue(path, out text) &&
                    !string.IsNullOrWhiteSpace(text))
                {
                    image.Add(new XAttribute("remark", CleanText(text)));
                }

                if (layout.Notes.TryGetValue(path, out text) &&
                    !string.IsNullOrWhiteSpace(text))
                {
                    image.Add(new XAttribute("remark2", CleanText(text)));
                }

                root.Add(image);
            }

            return new XDocument(root).ToString();
        }

        public static CollageLayout FromXml(string xml, string baseFolder)
        {
            XElement root = XDocument.Parse(xml).Root;

            if (root == null || root.Name.LocalName != RootName)
                return null;

            // Layouts written before rows and columns could differ only
            // have a single "grid" size.
            int square = ReadInt(root, "grid", 2);

            float textSize;

            if (!float.TryParse(
                (string)root.Attribute("textSize"),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out textSize))
            {
                textSize = CollagePageSetup.DefaultCaptionFontPoints;
            }

            CollagePageSetup page =
                new CollagePageSetup
                {
                    PaperSize = (string)root.Attribute("paper") ?? "A4",
                    CustomWidthMm = ReadMillimeters(root, "customWidthMm", 210),
                    CustomHeightMm = ReadMillimeters(root, "customHeightMm", 297),
                    Columns = ReadInt(root, "columns", square),
                    Rows = ReadInt(root, "rows", square),
                    Landscape =
                        string.Equals(
                            (string)root.Attribute("orientation"),
                            "Landscape",
                            StringComparison.OrdinalIgnoreCase),
                    DrawBorders = ReadBool(root, "borders"),
                    CaptionFontPoints = textSize,
                    HeaderText =
                        (string)root.Attribute("header") ?? string.Empty,
                    ShowDate = ReadBool(root, "date"),
                    ShowPageNumbers = ReadBool(root, "pageNumbers")
                };

            page.Normalize();

            CollageLayout layout =
                new CollageLayout
                {
                    Page = page,
                    DisplayMode =
                        (string)root.Attribute("text") ??
                        CollageCellLayout.ModeBoth
                };

            int cells = Math.Max(0, ReadInt(root, "cells", 0));
            layout.Slots.AddRange(new string[cells]);

            HashSet<string> seen =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (XElement image in root.Elements("Image"))
            {
                string saved = (string)image.Attribute("path");

                if (string.IsNullOrWhiteSpace(saved))
                    continue;

                bool found;

                string path =
                    ResolveImagePath(
                        saved,
                        (string)image.Attribute("relative"),
                        baseFolder,
                        out found);

                if (!seen.Add(path))
                    continue;

                layout.Images.Add(path);

                if (!found)
                    layout.MissingImages.Add(path);

                int cell = ReadInt(image, "cell", -1);

                if (cell >= 0)
                {
                    while (cell >= layout.Slots.Count)
                        layout.Slots.Add(null);

                    if (layout.Slots[cell] == null)
                        layout.Slots[cell] = path;
                }

                string remark = (string)image.Attribute("remark");
                if (!string.IsNullOrWhiteSpace(remark))
                    layout.Remarks[path] = remark;

                string note = (string)image.Attribute("remark2");
                if (!string.IsNullOrWhiteSpace(note))
                    layout.Notes[path] = note;
            }

            return layout;
        }

        // ------------------------------------------------------------
        // Images that have moved
        // ------------------------------------------------------------

        // Looks for the missing images, by file name, in a folder and its
        // sub-folders. Returns how many were found.
        public static int Relink(CollageLayout layout, string folder)
        {
            if (layout.MissingImages.Count == 0 ||
                string.IsNullOrWhiteSpace(folder) ||
                !Directory.Exists(folder))
                return 0;

            Dictionary<string, string> byName =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                foreach (string file in
                    Directory.EnumerateFiles(
                        folder,
                        "*.*",
                        SearchOption.AllDirectories))
                {
                    string name = Path.GetFileName(file);

                    if (!byName.ContainsKey(name))
                        byName[name] = file;
                }
            }
            catch
            {
                // Part of the folder could not be read; use what was found.
            }

            int relinked = 0;

            foreach (string missing in layout.MissingImages.ToList())
            {
                string replacement;

                if (!byName.TryGetValue(Path.GetFileName(missing), out replacement) ||
                    layout.Images.Contains(replacement, StringComparer.OrdinalIgnoreCase))
                    continue;

                ReplacePath(layout, missing, replacement);
                layout.MissingImages.Remove(missing);
                relinked++;
            }

            return relinked;
        }

        // Takes the images that are still missing out of the layout. Their
        // cells are left empty.
        public static void DropMissing(CollageLayout layout)
        {
            foreach (string missing in layout.MissingImages)
            {
                layout.Images.RemoveAll(
                    p => string.Equals(p, missing, StringComparison.OrdinalIgnoreCase));

                for (int i = 0; i < layout.Slots.Count; i++)
                {
                    if (string.Equals(
                        layout.Slots[i],
                        missing,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        layout.Slots[i] = null;
                    }
                }

                layout.Remarks.Remove(missing);
                layout.Notes.Remove(missing);
            }

            layout.MissingImages.Clear();
        }

        private static void ReplacePath(
            CollageLayout layout,
            string oldPath,
            string newPath)
        {
            for (int i = 0; i < layout.Images.Count; i++)
            {
                if (string.Equals(
                    layout.Images[i],
                    oldPath,
                    StringComparison.OrdinalIgnoreCase))
                {
                    layout.Images[i] = newPath;
                }
            }

            for (int i = 0; i < layout.Slots.Count; i++)
            {
                if (string.Equals(
                    layout.Slots[i],
                    oldPath,
                    StringComparison.OrdinalIgnoreCase))
                {
                    layout.Slots[i] = newPath;
                }
            }

            MoveKey(layout.Remarks, oldPath, newPath);
            MoveKey(layout.Notes, oldPath, newPath);
        }

        private static void MoveKey(
            Dictionary<string, string> values,
            string oldKey,
            string newKey)
        {
            string value;

            if (!values.TryGetValue(oldKey, out value))
                return;

            values.Remove(oldKey);
            values[newKey] = value;
        }

        // ------------------------------------------------------------
        // Inside a PDF
        // ------------------------------------------------------------

        // The layout as a PDF stream object. The collage PDF writer adds it
        // as the last object of the file and points to it from the catalog.
        // PDF viewers ignore it.
        public static byte[] BuildPdfObject(CollageLayout layout, string pdfFolder)
        {
            byte[] xml =
                new UTF8Encoding(false).GetBytes(ToXml(layout, pdfFolder));

            byte[] header =
                Encoding.ASCII.GetBytes(
                    "<< " + PdfObjectHeader + xml.Length + " >>\nstream\n");

            byte[] footer = Encoding.ASCII.GetBytes("\nendstream");

            byte[] result =
                new byte[header.Length + xml.Length + footer.Length];

            Buffer.BlockCopy(header, 0, result, 0, header.Length);
            Buffer.BlockCopy(xml, 0, result, header.Length, xml.Length);
            Buffer.BlockCopy(
                footer,
                0,
                result,
                header.Length + xml.Length,
                footer.Length);

            return result;
        }

        // The layout is the last object in the file, so only the end of the
        // PDF is read, however large the page images are.
        private static string ReadXmlFromPdf(string pdfPath)
        {
            byte[] tail;

            using (FileStream stream =
                new FileStream(
                    pdfPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite))
            {
                long start = Math.Max(0L, stream.Length - PdfTailBytes);

                tail = new byte[stream.Length - start];
                stream.Seek(start, SeekOrigin.Begin);

                int read = 0;

                while (read < tail.Length)
                {
                    int count = stream.Read(tail, read, tail.Length - read);

                    if (count <= 0)
                        break;

                    read += count;
                }
            }

            // Latin-1 maps every byte to one character, so positions in the
            // text are positions in the bytes.
            string text = Encoding.GetEncoding(28591).GetString(tail);

            int header =
                text.LastIndexOf(PdfObjectHeader, StringComparison.Ordinal);

            if (header < 0)
                return null;

            int lengthStart = header + PdfObjectHeader.Length;
            int lengthEnd = text.IndexOf(' ', lengthStart);

            int length;

            if (lengthEnd < 0 ||
                !int.TryParse(
                    text.Substring(lengthStart, lengthEnd - lengthStart),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out length))
                return null;

            int marker =
                text.IndexOf("stream\n", lengthEnd, StringComparison.Ordinal);

            if (marker < 0)
                return null;

            int dataStart = marker + "stream\n".Length;

            return length > 0 && dataStart + length <= tail.Length
                ? Encoding.UTF8.GetString(tail, dataStart, length)
                : null;
        }

        // ------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------
        private static float ReadMillimeters(XElement root, string name, float fallback)
        {
            float value;
            return float.TryParse((string)root.Attribute(name), NumberStyles.Float,
                CultureInfo.InvariantCulture, out value) ? value : fallback;
        }

        private static string ResolveImagePath(
            string savedPath,
            string relativePath,
            string baseFolder,
            out bool found)
        {
            if (File.Exists(savedPath))
            {
                found = true;
                return savedPath;
            }

            if (!string.IsNullOrWhiteSpace(relativePath) &&
                !string.IsNullOrWhiteSpace(baseFolder))
            {
                try
                {
                    string candidate =
                        Path.GetFullPath(Path.Combine(baseFolder, relativePath));

                    if (File.Exists(candidate))
                    {
                        found = true;
                        return candidate;
                    }
                }
                catch
                {
                    // Not a usable relative path on this machine.
                }
            }

            found = false;
            return savedPath;
        }

        private static string MakeRelativePath(string baseFolder, string path)
        {
            if (string.IsNullOrWhiteSpace(baseFolder) ||
                string.IsNullOrWhiteSpace(path))
                return null;

            try
            {
                string folder = Path.GetFullPath(baseFolder);

                if (!folder.EndsWith(
                    Path.DirectorySeparatorChar.ToString(),
                    StringComparison.Ordinal))
                {
                    folder += Path.DirectorySeparatorChar;
                }

                Uri relative =
                    new Uri(folder).MakeRelativeUri(
                        new Uri(Path.GetFullPath(path)));

                // Another drive or a network share has no relative form.
                if (relative.IsAbsoluteUri)
                    return null;

                return Uri.UnescapeDataString(relative.ToString())
                    .Replace('/', Path.DirectorySeparatorChar);
            }
            catch
            {
                return null;
            }
        }

        private static int ReadInt(XElement element, string name, int fallback)
        {
            int value;

            return int.TryParse(
                (string)element.Attribute(name),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value)
                ? value
                : fallback;
        }

        private static bool ReadBool(XElement element, string name)
        {
            return string.Equals(
                (string)element.Attribute(name),
                "true",
                StringComparison.OrdinalIgnoreCase);
        }

        // XML cannot hold control characters.
        private static string CleanText(string text)
        {
            return new string(
                text
                    .Where(c => c >= ' ' || c == '\t' || c == '\n' || c == '\r')
                    .ToArray());
        }
    }
}
