using System.Text;

namespace MarkItDown.Converters;

/// <summary>
/// Converts plain-text files (text/*, .txt, .md, .rst, .log, etc.) to Markdown.
/// The content is returned as-is since plain text is already compatible.
/// </summary>
public sealed class PlainTextConverter : DocumentConverter
{
    private static readonly HashSet<string> AcceptedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".markdown", ".rst", ".log", ".ini", ".cfg",
        ".yaml", ".yml", ".toml", ".xml", ".json", ".csv", ".tsv",
        ".py", ".js", ".ts", ".cs", ".java", ".cpp", ".c", ".h",
        ".sh", ".bash", ".ps1", ".bat", ".rb", ".go", ".rs", ".php",
    };

    public override bool Accepts(Stream stream, StreamInfo streamInfo)
    {
        var mime = streamInfo.MimeType ?? string.Empty;
        var ext  = streamInfo.Extension ?? string.Empty;

        return mime.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
            || AcceptedExtensions.Contains(ext);
    }

    public override DocumentConverterResult Convert(Stream stream, StreamInfo streamInfo)
    {
        Encoding enc = ResolveEncoding(streamInfo.Charset);
        using var reader = new StreamReader(stream, enc, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        string content = reader.ReadToEnd();
        return new DocumentConverterResult(content);
    }

    private static Encoding ResolveEncoding(string? charset)
    {
        if (string.IsNullOrWhiteSpace(charset))
            return Encoding.UTF8;
        try
        {
            return Encoding.GetEncoding(charset);
        }
        catch
        {
            return Encoding.UTF8;
        }
    }
}
