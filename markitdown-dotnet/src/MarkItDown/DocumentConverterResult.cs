namespace MarkItDown;

/// <summary>
/// The result of converting a document to Markdown.
/// </summary>
public sealed class DocumentConverterResult
{
    /// <summary>The converted Markdown text.</summary>
    public string Markdown { get; set; }

    /// <summary>Optional title extracted from the document.</summary>
    public string? Title { get; set; }

    public DocumentConverterResult(string markdown, string? title = null)
    {
        Markdown = markdown;
        Title = title;
    }

    /// <inheritdoc/>
    public override string ToString() => Markdown;
}
