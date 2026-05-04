using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using System.Text;

namespace MarkItDown.Converters;

/// <summary>
/// Converts PDF documents to Markdown using PdfPig.
/// Text is extracted page-by-page; pages are separated by horizontal rules.
/// </summary>
public sealed class PdfConverter : DocumentConverter
{
    private static readonly HashSet<string> AcceptedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
    };

    public override bool Accepts(Stream stream, StreamInfo streamInfo)
    {
        var mime = streamInfo.MimeType ?? string.Empty;
        var ext  = streamInfo.Extension ?? string.Empty;

        return AcceptedExtensions.Contains(ext)
            || mime.Equals("application/pdf", StringComparison.OrdinalIgnoreCase);
    }

    public override DocumentConverterResult Convert(Stream stream, StreamInfo streamInfo)
    {
        // PdfPig requires a seekable stream; buffer if necessary
        Stream seekable = stream.CanSeek ? stream : BufferStream(stream);

        var sb = new StringBuilder();
        string? title = null;

        try
        {
            using var pdf = PdfDocument.Open(seekable);

            // Try to extract document title from metadata
            if (pdf.Information.Title is { Length: > 0 } t)
                title = t;

            int pageNumber = 0;
            foreach (Page page in pdf.GetPages())
            {
                pageNumber++;
                if (pageNumber > 1)
                    sb.Append("\n\n---\n\n");

                string pageText = ExtractPageText(page);
                if (!string.IsNullOrWhiteSpace(pageText))
                    sb.Append(pageText.Trim());
            }
        }
        catch (Exception ex)
        {
            throw new FileConversionException($"Failed to convert PDF: {ex.Message}", ex);
        }

        return new DocumentConverterResult(sb.ToString().Trim(), title);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string ExtractPageText(Page page)
    {
        // PdfPig returns words; we group them into lines by vertical position.
        var words = page.GetWords().ToList();
        if (words.Count == 0)
            return string.Empty;

        // Sort words top-to-bottom, left-to-right
        var lines = words
            .GroupBy(w => Math.Round(w.BoundingBox.Bottom, 0))
            .OrderByDescending(g => g.Key)
            .Select(g => string.Join(" ", g.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text)));

        return string.Join("\n", lines);
    }

    private static Stream BufferStream(Stream stream)
    {
        var ms = new MemoryStream();
        stream.CopyTo(ms);
        ms.Position = 0;
        return ms;
    }
}
