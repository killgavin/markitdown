using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using System.Text;

namespace MarkItDown.Converters;

/// <summary>
/// Converts XLSX (Excel 2007+) workbooks to Markdown.
/// Each worksheet becomes a level-2 heading followed by a Markdown table.
/// </summary>
public sealed class XlsxConverter : DocumentConverter
{
    private static readonly HashSet<string> AcceptedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xlsx", ".xlsm", ".xltx", ".xltm",
    };

    private static readonly HashSet<string> AcceptedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.template",
    };

    public override bool Accepts(Stream stream, StreamInfo streamInfo)
    {
        var mime = streamInfo.MimeType ?? string.Empty;
        var ext  = streamInfo.Extension ?? string.Empty;

        return AcceptedExtensions.Contains(ext) || AcceptedMimeTypes.Contains(mime);
    }

    public override DocumentConverterResult Convert(Stream stream, StreamInfo streamInfo)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        ms.Position = 0;

        try
        {
            using var workbook = SpreadsheetDocument.Open(ms, isEditable: false);
            var wbPart = workbook.WorkbookPart
                         ?? throw new FileConversionException("Could not read XLSX workbook.");

            // Build shared strings table
            var sharedStrings = BuildSharedStrings(wbPart);

            var sb = new StringBuilder();
            string? title = null;

            // Try title from core properties
            var props = workbook.PackageProperties;
            if (!string.IsNullOrEmpty(props.Title))
                title = props.Title;

            var sheets = wbPart.Workbook.Descendants<Sheet>().ToList();

            foreach (var sheet in sheets)
            {
                string sheetName = sheet.Name?.Value ?? "Sheet";
                if (!string.IsNullOrEmpty(sheetName))
                    sb.Append("## ").Append(sheetName).Append("\n\n");

                if (sheet.Id?.Value is not string relId) continue;
                if (wbPart.GetPartById(relId) is not WorksheetPart wsPart) continue;

                var rows = wsPart.Worksheet.Descendants<Row>().ToList();
                if (rows.Count == 0) continue;

                // Collect all cells per row, mapping to column index
                var tableData = new List<List<string>>();
                int maxCol = 0;

                foreach (var row in rows)
                {
                    var cells = row.Descendants<Cell>().ToList();
                    var rowData = new Dictionary<int, string>();

                    foreach (var cell in cells)
                    {
                        int colIdx = ColumnNameToIndex(GetColumnName(cell.CellReference?.Value ?? "A1"));
                        string value = GetCellValue(cell, sharedStrings);
                        rowData[colIdx] = value;
                        if (colIdx > maxCol) maxCol = colIdx;
                    }

                    tableData.Add(Enumerable.Range(0, maxCol + 1)
                        .Select(i => rowData.TryGetValue(i, out var v) ? v : string.Empty)
                        .ToList());
                }

                AppendTable(tableData, sb);
            }

            return new DocumentConverterResult(sb.ToString().Trim(), title);
        }
        catch (FileConversionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new FileConversionException($"Failed to convert XLSX: {ex.Message}", ex);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string[] BuildSharedStrings(WorkbookPart wbPart)
    {
        var sst = wbPart.SharedStringTablePart?.SharedStringTable;
        if (sst == null) return [];
        return sst.Elements<SharedStringItem>()
                  .Select(i => i.InnerText)
                  .ToArray();
    }

    private static string GetCellValue(Cell cell, string[] sharedStrings)
    {
        string? raw = cell.CellValue?.Text;
        if (raw == null) return string.Empty;

        if (cell.DataType?.Value == CellValues.SharedString
            && int.TryParse(raw, out int idx)
            && idx < sharedStrings.Length)
        {
            return sharedStrings[idx];
        }

        if (cell.DataType?.Value == CellValues.Boolean)
            return raw == "1" ? "TRUE" : "FALSE";

        return raw;
    }

    private static string GetColumnName(string cellRef)
    {
        int i = 0;
        while (i < cellRef.Length && char.IsLetter(cellRef[i])) i++;
        return cellRef[..i];
    }

    private static int ColumnNameToIndex(string colName)
    {
        int result = 0;
        foreach (char c in colName.ToUpperInvariant())
            result = result * 26 + (c - 'A' + 1);
        return result - 1;
    }

    private static void AppendTable(List<List<string>> tableData, StringBuilder sb)
    {
        if (tableData.Count == 0) return;

        int colCount = tableData.Max(r => r.Count);
        foreach (var row in tableData)
            while (row.Count < colCount) row.Add(string.Empty);

        int[] widths = new int[colCount];
        for (int c = 0; c < colCount; c++)
            widths[c] = Math.Max(3, tableData.Max(r => r[c].Length));

        void AppendRow(List<string> row)
        {
            sb.Append('|');
            for (int c = 0; c < colCount; c++)
                sb.Append(' ').Append(row[c].PadRight(widths[c])).Append(" |");
            sb.Append('\n');
        }

        AppendRow(tableData[0]);

        sb.Append('|');
        for (int c = 0; c < colCount; c++)
            sb.Append(' ').Append(new string('-', widths[c])).Append(" |");
        sb.Append('\n');

        for (int r = 1; r < tableData.Count; r++)
            AppendRow(tableData[r]);

        sb.Append('\n');
    }
}
