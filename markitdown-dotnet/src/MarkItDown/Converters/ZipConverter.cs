using System.IO.Compression;
using System.Text;

namespace MarkItDown.Converters;

/// <summary>
/// Converts ZIP archives to Markdown by recursively converting each contained file
/// and separating them with horizontal rules.
/// </summary>
public sealed class ZipConverter : DocumentConverter
{
    private readonly MarkItDownConverter _parent;

    public ZipConverter(MarkItDownConverter parent)
    {
        _parent = parent;
    }

    private static readonly HashSet<string> AcceptedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip",
    };

    public override bool Accepts(Stream stream, StreamInfo streamInfo)
    {
        var mime = streamInfo.MimeType ?? string.Empty;
        var ext  = streamInfo.Extension ?? string.Empty;

        return AcceptedExtensions.Contains(ext)
            || mime.Equals("application/zip", StringComparison.OrdinalIgnoreCase)
            || mime.Equals("application/x-zip-compressed", StringComparison.OrdinalIgnoreCase);
    }

    public override DocumentConverterResult Convert(Stream stream, StreamInfo streamInfo)
    {
        var sb = new StringBuilder();
        bool first = true;

        try
        {
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);

            foreach (var entry in zip.Entries.OrderBy(e => e.FullName))
            {
                // Skip directories
                if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
                    continue;

                if (!first) sb.Append("\n\n---\n\n");
                first = false;

                sb.Append($"## {entry.FullName}\n\n");

                try
                {
                    using var entryStream = entry.Open();
                    using var buffered = new MemoryStream();
                    entryStream.CopyTo(buffered);
                    buffered.Position = 0;

                    string entryExt = Path.GetExtension(entry.Name);
                    var entryInfo = new StreamInfo
                    {
                        Extension = string.IsNullOrEmpty(entryExt) ? null : entryExt,
                        FileName = entry.Name,
                    };

                    var result = _parent.ConvertStream(buffered, entryInfo);
                    sb.Append(result.Markdown.Trim());
                }
                catch (Exception ex)
                {
                    sb.Append($"*Could not convert file: {ex.Message}*");
                }
            }
        }
        catch (Exception ex)
        {
            throw new FileConversionException($"Failed to read ZIP archive: {ex.Message}", ex);
        }

        return new DocumentConverterResult(sb.ToString().Trim());
    }
}
