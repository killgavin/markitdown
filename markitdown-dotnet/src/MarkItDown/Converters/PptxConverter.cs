using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using System.Text;
using A = DocumentFormat.OpenXml.Drawing;

namespace MarkItDown.Converters;

/// <summary>
/// Converts PPTX (PowerPoint 2007+) presentations to Markdown.
/// Each slide becomes a section; shapes/text are rendered as paragraphs.
/// </summary>
public sealed class PptxConverter : DocumentConverter
{
    private static readonly HashSet<string> AcceptedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pptx", ".pptm", ".potx", ".potm",
    };

    private static readonly HashSet<string> AcceptedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        "application/vnd.openxmlformats-officedocument.presentationml.template",
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
            using var prs = PresentationDocument.Open(ms, isEditable: false);
            var prPart = prs.PresentationPart
                         ?? throw new FileConversionException("Could not read PPTX presentation part.");

            string? title = null;
            var props = prs.PackageProperties;
            if (!string.IsNullOrEmpty(props.Title))
                title = props.Title;

            var sb = new StringBuilder();
            int slideNum = 0;

            foreach (var slideId in prPart.Presentation.SlideIdList?.OfType<SlideId>() ?? [])
            {
                if (slideId.RelationshipId?.Value is not string relId) continue;
                if (prPart.GetPartById(relId) is not SlidePart slidePart) continue;

                slideNum++;
                if (slideNum > 1) sb.Append("\n\n---\n\n");

                sb.Append($"## Slide {slideNum}\n\n");

                // Extract text from all shapes
                foreach (var shape in slidePart.Slide.Descendants<Shape>())
                {
                    var txBody = shape.TextBody;
                    if (txBody == null) continue;

                    foreach (var para in txBody.Descendants<A.Paragraph>())
                    {
                        string paraText = string.Concat(para.Descendants<A.Text>().Select(t => t.Text));
                        if (!string.IsNullOrWhiteSpace(paraText))
                            sb.Append(paraText.Trim()).Append("\n\n");
                    }
                }

                // Extract alt text from pictures
                foreach (var pic in slidePart.Slide.Descendants<DocumentFormat.OpenXml.Drawing.Pictures.Picture>())
                {
                    string? altText = pic.NonVisualPictureProperties?
                        .NonVisualDrawingProperties?.Description?.Value;
                    if (!string.IsNullOrEmpty(altText))
                        sb.Append($"![{altText}]\n\n");
                }

                // Extract table data from slides
                foreach (var tbl in slidePart.Slide.Descendants<A.Table>())
                {
                    AppendTable(tbl, sb);
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
            throw new FileConversionException($"Failed to convert PPTX: {ex.Message}", ex);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void AppendTable(A.Table tbl, StringBuilder sb)
    {
        var rows = tbl.Descendants<A.TableRow>().ToList();
        if (rows.Count == 0) return;

        var tableData = rows.Select(row =>
            row.Descendants<A.TableCell>()
               .Select(cell => string.Concat(cell.Descendants<A.Text>().Select(t => t.Text)).Trim())
               .ToList()
        ).ToList();

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
