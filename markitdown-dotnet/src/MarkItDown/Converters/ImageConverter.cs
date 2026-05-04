using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Iptc;
using System.Text;

namespace MarkItDown.Converters;

/// <summary>
/// Converts image files to Markdown by extracting EXIF/IPTC metadata.
/// The image binary is not embedded in the output; only textual metadata is rendered.
/// </summary>
public sealed class ImageConverter : DocumentConverter
{
    private static readonly HashSet<string> AcceptedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".tif",
        ".webp", ".heic", ".heif", ".avif",
    };

    private static readonly HashSet<string> AcceptedMimeTypePrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/",
    };

    public override bool Accepts(Stream stream, StreamInfo streamInfo)
    {
        var mime = streamInfo.MimeType ?? string.Empty;
        var ext  = streamInfo.Extension ?? string.Empty;

        return AcceptedExtensions.Contains(ext)
            || AcceptedMimeTypePrefixes.Any(p => mime.StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }

    public override DocumentConverterResult Convert(Stream stream, StreamInfo streamInfo)
    {
        var sb = new StringBuilder();
        string fileName = streamInfo.FileName ?? streamInfo.LocalPath ?? "Image";

        sb.Append($"# {Path.GetFileName(fileName)}\n\n");
        sb.Append("## Image Metadata\n\n");

        try
        {
            // Buffer the stream (MetadataExtractor may need to seek)
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            ms.Position = 0;

            var directories = ImageMetadataReader.ReadMetadata(ms);
            bool hasMetadata = false;

            foreach (var directory in directories)
            {
                if (directory.TagCount == 0) continue;

                // Filter to EXIF and IPTC directories for relevant metadata
                bool isRelevant = directory is ExifIfd0Directory
                                            or ExifSubIfdDirectory
                                            or GpsDirectory
                                            or IptcDirectory;
                if (!isRelevant) continue;

                hasMetadata = true;
                sb.Append($"### {directory.Name}\n\n");

                foreach (var tag in directory.Tags)
                {
                    string value = directory.GetDescription(tag.Type) ?? tag.DirectoryName;
                    sb.Append($"- **{tag.Name}**: {value}\n");
                }

                sb.Append('\n');
            }

            if (!hasMetadata)
                sb.Append("*No EXIF/IPTC metadata found.*\n");
        }
        catch (Exception ex)
        {
            sb.Append($"*Could not read image metadata: {ex.Message}*\n");
        }

        return new DocumentConverterResult(sb.ToString().Trim());
    }
}
