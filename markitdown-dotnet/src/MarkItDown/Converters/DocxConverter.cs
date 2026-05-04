using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text;

namespace MarkItDown.Converters;

/// <summary>
/// Converts DOCX (Word 2007+) documents to Markdown using the Open XML SDK.
/// </summary>
public sealed class DocxConverter : DocumentConverter
{
    private static readonly HashSet<string> AcceptedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".docx", ".dotx",
    };

    private static readonly HashSet<string> AcceptedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.template",
    };

    public override bool Accepts(Stream stream, StreamInfo streamInfo)
    {
        var mime = streamInfo.MimeType ?? string.Empty;
        var ext  = streamInfo.Extension ?? string.Empty;

        return AcceptedExtensions.Contains(ext) || AcceptedMimeTypes.Contains(mime);
    }

    public override DocumentConverterResult Convert(Stream stream, StreamInfo streamInfo)
    {
        // Open XML SDK needs a seekable, writable-ish stream; copy to MemoryStream
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        ms.Position = 0;

        try
        {
            using var doc = WordprocessingDocument.Open(ms, isEditable: false);
            var body = doc.MainDocumentPart?.Document?.Body
                       ?? throw new FileConversionException("Could not read DOCX body.");

            var sb = new StringBuilder();
            string? title = null;

            // Try to get title from core properties
            var props = doc.PackageProperties;
            if (!string.IsNullOrEmpty(props.Title))
                title = props.Title;

            foreach (var element in body.ChildElements)
            {
                switch (element)
                {
                    case Paragraph para:
                        AppendParagraph(para, sb);
                        break;

                    case Table table:
                        AppendTable(table, sb);
                        break;
                }
            }

            return new DocumentConverterResult(sb.ToString().Trim(), title);
        }
        catch (FileConversionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new FileConversionException($"Failed to convert DOCX: {ex.Message}", ex);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void AppendParagraph(Paragraph para, StringBuilder sb)
    {
        var styleId = para.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? string.Empty;
        string text = GetParagraphText(para);

        if (string.IsNullOrWhiteSpace(text))
        {
            sb.Append('\n');
            return;
        }

        string prefix = styleId.ToLowerInvariant() switch
        {
            "heading1" or "title"    => "# ",
            "heading2" or "subtitle" => "## ",
            "heading3"               => "### ",
            "heading4"               => "#### ",
            "heading5"               => "##### ",
            "heading6"               => "###### ",
            _                        => string.Empty,
        };

        sb.Append(prefix).Append(text).Append("\n\n");
    }

    private static string GetParagraphText(Paragraph para)
    {
        var sb = new StringBuilder();
        foreach (var run in para.Descendants<Run>())
        {
            bool bold   = run.RunProperties?.Bold != null;
            bool italic = run.RunProperties?.Italic != null;
            bool code   = run.RunProperties?.RunStyle?.Val?.Value
                             ?.Contains("code", StringComparison.OrdinalIgnoreCase) == true;

            string runText = string.Concat(run.Descendants<Text>().Select(t => t.Text));
            if (string.IsNullOrEmpty(runText)) continue;

            if (code)   runText = $"`{runText}`";
            if (bold)   runText = $"**{runText}**";
            if (italic) runText = $"*{runText}*";

            sb.Append(runText);
        }
        return sb.ToString();
    }

    private static void AppendTable(Table table, StringBuilder sb)
    {
        var rows = table.Descendants<TableRow>().ToList();
        if (rows.Count == 0) return;

        var tableData = rows.Select(row =>
            row.Descendants<TableCell>()
               .Select(cell => string.Join(" ", cell.Descendants<Paragraph>()
                   .Select(p => GetParagraphText(p).Trim()))
                   .Replace("\n", " ").Trim())
               .ToList()
        ).ToList();

        int colCount = tableData.Max(r => r.Count);
        foreach (var row in tableData)
            while (row.Count < colCount) row.Add(string.Empty);

        int[] widths = new int[colCount];
        for (int c = 0; c < colCount; c++)
            widths[c] = Math.Max(3, tableData.Max(r => r[c].Length));

        sb.Append('\n');

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
