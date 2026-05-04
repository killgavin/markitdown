namespace MarkItDown;

/// <summary>
/// Abstract base class for all document converters.
/// Each converter handles one or more file formats and produces Markdown output.
/// </summary>
public abstract class DocumentConverter
{
    /// <summary>
    /// Returns <c>true</c> if this converter can handle the given stream.
    /// The decision is primarily based on <paramref name="streamInfo"/>
    /// (MIME type, extension, URL). Implementations that need to peek into
    /// the stream must restore the stream position before returning.
    /// </summary>
    public abstract bool Accepts(Stream stream, StreamInfo streamInfo);

    /// <summary>
    /// Converts the content of <paramref name="stream"/> to Markdown.
    /// </summary>
    /// <exception cref="FileConversionException">
    /// Thrown when the format is recognised but conversion fails.
    /// </exception>
    public abstract DocumentConverterResult Convert(Stream stream, StreamInfo streamInfo);
}
