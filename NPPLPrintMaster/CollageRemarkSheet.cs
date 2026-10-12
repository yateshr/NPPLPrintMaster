using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace NPPLPrintMaster
{
    internal sealed class CollageRemarkRow
    {
        public string FileName;
        public string Remark;
        public string Remark2;

        // Written for reference only; ignored when a sheet is read back.
        public string Location;
    }

    /// <summary>
    /// Reads and writes the remarks of a collage as a simple sheet, so the
    /// text for many images can be typed or pasted in Excel instead of one
    /// image at a time.
    ///
    /// Column A is the image file name, column B the remark and column C
    /// the additional remark. A heading row is optional. Excel workbooks
    /// (.xlsx, .xls) and CSV / tab-separated text are accepted.
    /// </summary>
    internal static class CollageRemarkSheet
    {
        public const string OpenFilter =
            "Excel or CSV|*.xlsx;*.xls;*.csv;*.txt|All files|*.*";

        private static readonly string[] HeadingNames =
        {
            "file", "file name", "filename", "image", "image name", "name"
        };

        public static List<CollageRemarkRow> Read(string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();

            List<string[]> table =
                extension == ".csv" || extension == ".txt"
                    ? ReadDelimited(path)
                    : ReadWorkbook(path);

            List<CollageRemarkRow> rows = new List<CollageRemarkRow>();

            for (int i = 0; i < table.Count; i++)
            {
                string name = Cell(table[i], 0);

                if (name.Length == 0)
                    continue;

                if (i == 0 && IsHeading(name))
                    continue;

                rows.Add(
                    new CollageRemarkRow
                    {
                        FileName = name,
                        Remark = Cell(table[i], 1),
                        Remark2 = Cell(table[i], 2)
                    });
            }

            return rows;
        }

        public static void Write(string path, IList<CollageRemarkRow> rows)
        {
            XSSFWorkbook workbook = new XSSFWorkbook();

            try
            {
                ISheet sheet = workbook.CreateSheet("Remarks");

                IFont bold = workbook.CreateFont();
                bold.IsBold = true;

                ICellStyle headingStyle = workbook.CreateCellStyle();
                headingStyle.SetFont(bold);

                string[] headings =
                {
                    "File name", "Remark", "Remark 2", "Placed on"
                };

                IRow heading = sheet.CreateRow(0);

                for (int column = 0; column < headings.Length; column++)
                {
                    ICell cell = heading.CreateCell(column);
                    cell.SetCellValue(headings[column]);
                    cell.CellStyle = headingStyle;
                }

                for (int i = 0; i < rows.Count; i++)
                {
                    IRow row = sheet.CreateRow(i + 1);

                    row.CreateCell(0).SetCellValue(rows[i].FileName ?? string.Empty);
                    row.CreateCell(1).SetCellValue(rows[i].Remark ?? string.Empty);
                    row.CreateCell(2).SetCellValue(rows[i].Remark2 ?? string.Empty);
                    row.CreateCell(3).SetCellValue(rows[i].Location ?? string.Empty);
                }

                sheet.SetColumnWidth(0, 46 * 256);
                sheet.SetColumnWidth(1, 46 * 256);
                sheet.SetColumnWidth(2, 46 * 256);
                sheet.SetColumnWidth(3, 20 * 256);
                sheet.CreateFreezePane(0, 1);

                using (FileStream stream =
                    new FileStream(
                        path,
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

        private static List<string[]> ReadWorkbook(string path)
        {
            List<string[]> table = new List<string[]>();

            // Shared access so a sheet that is still open in Excel can be
            // read (once it has been saved there).
            using (FileStream stream =
                new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite))
            {
                IWorkbook workbook = WorkbookFactory.Create(stream);

                try
                {
                    if (workbook.NumberOfSheets == 0)
                        return table;

                    ISheet sheet = workbook.GetSheetAt(0);
                    DataFormatter formatter = new DataFormatter();

                    IFormulaEvaluator evaluator =
                        workbook.GetCreationHelper().CreateFormulaEvaluator();

                    for (int r = sheet.FirstRowNum; r <= sheet.LastRowNum; r++)
                    {
                        IRow row = sheet.GetRow(r);

                        if (row == null)
                            continue;

                        table.Add(
                            new[]
                            {
                                CellText(row.GetCell(0), formatter, evaluator),
                                CellText(row.GetCell(1), formatter, evaluator),
                                CellText(row.GetCell(2), formatter, evaluator)
                            });
                    }
                }
                finally
                {
                    workbook.Close();
                }
            }

            return table;
        }

        // Text exactly as Excel displays it, so a file name made of digits
        // does not come back as "12345.0".
        private static string CellText(
            ICell cell,
            DataFormatter formatter,
            IFormulaEvaluator evaluator)
        {
            if (cell == null)
                return string.Empty;

            try
            {
                return (formatter.FormatCellValue(cell, evaluator) ?? string.Empty).Trim();
            }
            catch
            {
                return (cell.ToString() ?? string.Empty).Trim();
            }
        }

        private static List<string[]> ReadDelimited(string path)
        {
            List<string[]> table = new List<string[]>();

            string[] lines;

            using (FileStream stream =
                new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite))
            using (StreamReader reader =
                new StreamReader(stream, Encoding.UTF8, true))
            {
                lines =
                    reader.ReadToEnd().Split(
                        new[] { "\r\n", "\n", "\r" },
                        StringSplitOptions.None);
            }

            foreach (string line in lines)
            {
                if (line.Trim().Length == 0)
                    continue;

                char delimiter =
                    line.IndexOf('\t') >= 0
                        ? '\t'
                        : line.IndexOf(';') >= 0 && line.IndexOf(',') < 0
                            ? ';'
                            : ',';

                table.Add(SplitDelimited(line, delimiter));
            }

            return table;
        }

        // One line of CSV: fields may be wrapped in double quotes, and a
        // doubled quote inside a quoted field is a literal quote.
        private static string[] SplitDelimited(string line, char delimiter)
        {
            List<string> fields = new List<string>();
            StringBuilder field = new StringBuilder();
            bool quoted = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (quoted)
                {
                    if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else if (c == '"')
                    {
                        quoted = false;
                    }
                    else
                    {
                        field.Append(c);
                    }
                }
                else if (c == '"' && field.Length == 0)
                {
                    quoted = true;
                }
                else if (c == delimiter)
                {
                    fields.Add(field.ToString());
                    field.Length = 0;
                }
                else
                {
                    field.Append(c);
                }
            }

            fields.Add(field.ToString());
            return fields.ToArray();
        }

        private static string Cell(string[] row, int index)
        {
            return index < row.Length && row[index] != null
                ? row[index].Trim()
                : string.Empty;
        }

        private static bool IsHeading(string text)
        {
            foreach (string heading in HeadingNames)
            {
                if (string.Equals(text, heading, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
