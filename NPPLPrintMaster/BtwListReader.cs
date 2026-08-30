using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NPOI.SS.UserModel;

namespace NPPLPrintMaster
{
    /// <summary>
    /// Reads BTW filename lists without requiring Microsoft Excel.
    /// Supports TXT, XLS and XLSX through the NPOI library.
    /// </summary>
    public static class BtwListReader
    {
        public static List<string> ReadNames(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("A list file is required.");

            if (!File.Exists(filePath))
                throw new FileNotFoundException(
                    "The list file was not found.",
                    filePath);

            string extension =
                Path.GetExtension(filePath)
                    .ToLowerInvariant();

            switch (extension)
            {
                case ".txt":
                    return ReadTextFile(filePath);

                case ".xls":
                case ".xlsx":
                    return ReadExcelFile(filePath);

                default:
                    throw new NotSupportedException(
                        "Supported list formats are .txt, .xls and .xlsx.");
            }
        }

        private static List<string> ReadTextFile(
            string filePath)
        {
            return File.ReadAllLines(filePath)
                .Select(NormalizeValue)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .ToList();
        }

        private static List<string> ReadExcelFile(
            string filePath)
        {
            List<string> results =
                new List<string>();

            using (FileStream stream =
                new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite))
            {
                IWorkbook workbook =
                    WorkbookFactory.Create(stream);

                DataFormatter formatter =
                    new DataFormatter();

                try
                {
                    for (int sheetIndex = 0;
                         sheetIndex < workbook.NumberOfSheets;
                         sheetIndex++)
                    {
                        ISheet sheet =
                            workbook.GetSheetAt(sheetIndex);

                        if (sheet == null)
                            continue;

                        ReadSheet(
                            sheet,
                            formatter,
                            results);
                    }
                }
                finally
                {
                    workbook.Close();
                }
            }

            return results
                .Select(NormalizeValue)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void ReadSheet(
            ISheet sheet,
            DataFormatter formatter,
            List<string> output)
        {
            int headerRowIndex;
            int targetColumn;

            if (TryFindBtwHeader(
                sheet,
                formatter,
                out headerRowIndex,
                out targetColumn))
            {
                for (int rowIndex = headerRowIndex + 1;
                     rowIndex <= sheet.LastRowNum;
                     rowIndex++)
                {
                    IRow row =
                        sheet.GetRow(rowIndex);

                    if (row == null)
                        continue;

                    ICell cell =
                        row.GetCell(
                            targetColumn,
                            MissingCellPolicy.RETURN_BLANK_AS_NULL);

                    if (cell == null)
                        continue;

                    string value =
                        formatter.FormatCellValue(cell);

                    if (!string.IsNullOrWhiteSpace(value))
                        output.Add(value);
                }

                return;
            }

            // No recognizable header: use the first non-empty cell
            // from each row. This works well for simple one-column lists.
            for (int rowIndex = sheet.FirstRowNum;
                 rowIndex <= sheet.LastRowNum;
                 rowIndex++)
            {
                IRow row =
                    sheet.GetRow(rowIndex);

                if (row == null)
                    continue;

                for (int columnIndex = row.FirstCellNum;
                     columnIndex >= 0 &&
                     columnIndex < row.LastCellNum;
                     columnIndex++)
                {
                    ICell cell =
                        row.GetCell(
                            columnIndex,
                            MissingCellPolicy.RETURN_BLANK_AS_NULL);

                    if (cell == null)
                        continue;

                    string value =
                        formatter.FormatCellValue(cell);

                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        if (!LooksLikeHeader(value))
                            output.Add(value);

                        break;
                    }
                }
            }
        }

        private static bool TryFindBtwHeader(
            ISheet sheet,
            DataFormatter formatter,
            out int headerRowIndex,
            out int targetColumn)
        {
            headerRowIndex = -1;
            targetColumn = -1;

            int lastRowToInspect =
                Math.Min(
                    sheet.LastRowNum,
                    sheet.FirstRowNum + 19);

            for (int rowIndex = sheet.FirstRowNum;
                 rowIndex <= lastRowToInspect;
                 rowIndex++)
            {
                IRow row =
                    sheet.GetRow(rowIndex);

                if (row == null)
                    continue;

                for (int columnIndex = row.FirstCellNum;
                     columnIndex >= 0 &&
                     columnIndex < row.LastCellNum;
                     columnIndex++)
                {
                    ICell cell =
                        row.GetCell(
                            columnIndex,
                            MissingCellPolicy.RETURN_BLANK_AS_NULL);

                    if (cell == null)
                        continue;

                    string value =
                        formatter.FormatCellValue(cell);

                    if (LooksLikeHeader(value))
                    {
                        headerRowIndex = rowIndex;
                        targetColumn = columnIndex;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool LooksLikeHeader(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string normalized =
                value.Trim()
                    .ToLowerInvariant()
                    .Replace("_", " ")
                    .Replace("-", " ");

            while (normalized.Contains("  "))
                normalized = normalized.Replace("  ", " ");

            return normalized == "btw" ||
                   normalized == "btw name" ||
                   normalized == "btw filename" ||
                   normalized == "btw file name" ||
                   normalized == "filename" ||
                   normalized == "file name" ||
                   normalized == "label name";
        }

        private static string NormalizeValue(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string result =
                value.Trim()
                    .Trim('"')
                    .Trim();

            if (result.EndsWith(
                ".btw",
                StringComparison.OrdinalIgnoreCase))
            {
                result =
                    Path.GetFileNameWithoutExtension(result);
            }

            return result.Trim();
        }
    }
}
