namespace MarkItDown;

/// <summary>
/// Thrown when a file format is recognised but conversion fails for some reason.
/// </summary>
public sealed class FileConversionException : Exception
{
    public FileConversionException(string message) : base(message) { }
    public FileConversionException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Thrown when no registered converter can handle the given file.
/// </summary>
public sealed class UnsupportedFormatException : Exception
{
    public UnsupportedFormatException(string message) : base(message) { }
}
