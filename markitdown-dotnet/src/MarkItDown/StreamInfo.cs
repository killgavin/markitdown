namespace MarkItDown;

/// <summary>
/// Stores metadata about a file or stream being converted.
/// All fields are optional and depend on how the stream was opened.
/// </summary>
public sealed record StreamInfo
{
    /// <summary>MIME type of the stream (e.g. "text/html").</summary>
    public string? MimeType { get; init; }

    /// <summary>File extension including the leading dot (e.g. ".pdf").</summary>
    public string? Extension { get; init; }

    /// <summary>Character set / encoding name (e.g. "utf-8").</summary>
    public string? Charset { get; init; }

    /// <summary>File name, from a local path, URL, or Content-Disposition header.</summary>
    public string? FileName { get; init; }

    /// <summary>Absolute local file path, if the stream was opened from disk.</summary>
    public string? LocalPath { get; init; }

    /// <summary>Source URL, if the stream was opened from a remote resource.</summary>
    public string? Url { get; init; }

    /// <summary>
    /// Returns a new <see cref="StreamInfo"/> with the fields of <paramref name="other"/>
    /// merged in, overwriting only non-null values.
    /// </summary>
    public StreamInfo MergeWith(StreamInfo other) => new()
    {
        MimeType   = other.MimeType   ?? MimeType,
        Extension  = other.Extension  ?? Extension,
        Charset    = other.Charset    ?? Charset,
        FileName   = other.FileName   ?? FileName,
        LocalPath  = other.LocalPath  ?? LocalPath,
        Url        = other.Url        ?? Url,
    };
}
