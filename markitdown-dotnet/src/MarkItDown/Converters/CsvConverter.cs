using System.Text;

namespace MarkItDown.Converters;

/// <summary>
/// Converts CSV / TSV files to a Markdown table.
/// Auto-detects the delimiter (comma, tab, semicolon, pipe).
/// </summary>
public sealed class CsvConverter : DocumentConverter
{
    private static readonly HashSet<string> AcceptedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".csv", ".tsv", ".tab",
    };

    public override bool Accepts(Stream stream, StreamInfo streamInfo)
    {
        var mime = streamInfo.MimeType ?? string.Empty;
        var ext  = streamInfo.Extension ?? string.Empty;

        return AcceptedExtensions.Contains(ext)
            || mime.Equals("text/csv", StringComparison.OrdinalIgnoreCase)
            || mime.Equals("text/tab-separated-values", StringComparison.OrdinalIgnoreCase);
    }

    public override DocumentConverterResult Convert(Stream stream, StreamInfo streamInfo)
    {
        Encoding enc = ResolveEncoding(streamInfo.Charset);
        using var reader = new StreamReader(stream, enc, detectEncodingFromByteOrderMarks: true, leaveOpen: true);

        var lines = new List<string>();
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (line != null) lines.Add(line);
        }

        if (lines.Count == 0)
            return new DocumentConverterResult(string.Empty);

        char delimiter = DetectDelimiter(streamInfo.Extension, lines[0]);

        var tableData = lines
            .Select(line => SplitCsvLine(line, delimiter))
            .ToList();

        int colCount = tableData.Max(r => r.Count);
        foreach (var row in tableData)
            while (row.Count < colCount) row.Add(string.Empty);

        int[] widths = new int[colCount];
        for (int c = 0; c < colCount; c++)
            widths[c] = Math.Max(3, tableData.Max(r => r[c].Length));

        var sb = new StringBuilder();

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

        return new DocumentConverterResult(sb.ToString().Trim());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static char DetectDelimiter(string? extension, string firstLine)
    {
        if ((extension ?? string.Empty).Equals(".tsv", StringComparison.OrdinalIgnoreCase)
         || (extension ?? string.Empty).Equals(".tab", StringComparison.OrdinalIgnoreCase))
            return '\t';

        // Heuristic: count occurrences of candidate delimiters in first line
        char[] candidates = [',', '\t', ';', '|'];
        return candidates
            .OrderByDescending(c => firstLine.Count(ch => ch == c))
            .First();
    }

    private static List<string> SplitCsvLine(string line, char delimiter)
    {
        // Simple RFC 4180-aware CSV split (handles quoted fields)
        var fields = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++; // skip escaped quote
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == delimiter)
                {
                    fields.Add(current.ToString().Trim());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
        }

        fields.Add(current.ToString().Trim());
        return fields;
    }

    private static Encoding ResolveEncoding(string? charset)
    {
        if (string.IsNullOrWhiteSpace(charset)) return Encoding.UTF8;
        try { return Encoding.GetEncoding(charset); } catch { return Encoding.UTF8; }
    }
}
