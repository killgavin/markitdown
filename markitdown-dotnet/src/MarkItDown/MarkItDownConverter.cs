using MarkItDown.Converters;
using System.Net.Http.Headers;

namespace MarkItDown;

/// <summary>
/// Main entry point for converting documents to Markdown.
/// Mirrors the Python MarkItDown class API.
/// </summary>
public sealed class MarkItDownConverter : IDisposable
{
    private readonly List<(DocumentConverter Converter, double Priority)> _converters = [];
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private bool _disposed;

    // Priority constants (lower = tried first)
    private const double PrioritySpecific = 0.0;
    private const double PriorityGeneric  = 10.0;

    /// <summary>
    /// Initialises the converter with all built-in converters registered.
    /// </summary>
    /// <param name="httpClient">
    /// Optional <see cref="HttpClient"/> used for URL conversion.
    /// If <c>null</c>, an internal client is created.
    /// </param>
    public MarkItDownConverter(HttpClient? httpClient = null)
    {
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient();

        RegisterBuiltins();
    }

    // ── Registration API ──────────────────────────────────────────────────────

    /// <summary>
    /// Registers a custom converter with the given priority.
    /// Lower priority values are tried first.
    /// </summary>
    public void RegisterConverter(DocumentConverter converter, double priority = PrioritySpecific)
    {
        _converters.Add((converter, priority));
        _converters.Sort((a, b) => a.Priority.CompareTo(b.Priority));
    }

    // ── Conversion API ────────────────────────────────────────────────────────

    /// <summary>
    /// Converts a local file, URL, or data URI to Markdown.
    /// </summary>
    public DocumentConverterResult Convert(string source, StreamInfo? streamInfo = null)
    {
        // URL?
        if (source.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
         || source.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return ConvertUrl(source, streamInfo);
        }

        // Local file
        return ConvertLocal(source, streamInfo);
    }

    /// <summary>
    /// Converts a local file path to Markdown.
    /// </summary>
    public DocumentConverterResult ConvertLocal(string filePath, StreamInfo? streamInfo = null)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}", filePath);

        string ext  = Path.GetExtension(filePath);
        string name = Path.GetFileName(filePath);

        var info = (streamInfo ?? new StreamInfo()).MergeWith(new StreamInfo
        {
            Extension = ext,
            FileName  = name,
            LocalPath = filePath,
            MimeType  = MimeTypeFromExtension(ext),
        });

        using var stream = File.OpenRead(filePath);
        return ConvertStream(stream, info);
    }

    /// <summary>
    /// Downloads a URL and converts its content to Markdown.
    /// </summary>
    public DocumentConverterResult ConvertUrl(string url, StreamInfo? streamInfo = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = _httpClient.Send(request);
        response.EnsureSuccessStatusCode();

        string? mimeType   = response.Content.Headers.ContentType?.MediaType;
        string? charset    = response.Content.Headers.ContentType?.CharSet;
        string? filename   = GetFilenameFromHeaders(response.Content.Headers.ContentDisposition)
                             ?? Path.GetFileName(new Uri(url).AbsolutePath);
        string? ext        = string.IsNullOrEmpty(filename) ? null : Path.GetExtension(filename);

        var info = (streamInfo ?? new StreamInfo()).MergeWith(new StreamInfo
        {
            MimeType  = mimeType,
            Charset   = charset,
            FileName  = filename,
            Extension = ext,
            Url       = url,
        });

        using var stream = response.Content.ReadAsStream();
        return ConvertStream(stream, info);
    }

    /// <summary>
    /// Converts content from a <see cref="Stream"/> to Markdown.
    /// </summary>
    public DocumentConverterResult ConvertStream(Stream stream, StreamInfo? streamInfo = null)
    {
        var info = streamInfo ?? new StreamInfo();

        foreach (var (converter, _) in _converters)
        {
            if (!converter.Accepts(stream, info)) continue;

            long pos = stream.CanSeek ? stream.Position : 0;
            try
            {
                return converter.Convert(stream, info);
            }
            catch (FileConversionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Converter failed; reset position and try the next one
                if (stream.CanSeek) stream.Position = pos;
                _ = ex; // suppress warning
            }
        }

        throw new UnsupportedFormatException(
            $"No converter found for the given stream. " +
            $"Extension='{info.Extension}', MimeType='{info.MimeType}'.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void RegisterBuiltins()
    {
        // Specific format converters (tried first)
        RegisterConverter(new PdfConverter(),       PrioritySpecific);
        RegisterConverter(new DocxConverter(),      PrioritySpecific);
        RegisterConverter(new XlsxConverter(),      PrioritySpecific);
        RegisterConverter(new PptxConverter(),      PrioritySpecific);
        RegisterConverter(new CsvConverter(),       PrioritySpecific);
        RegisterConverter(new ImageConverter(),     PrioritySpecific);
        RegisterConverter(new ZipConverter(this),   PrioritySpecific);
        RegisterConverter(new HtmlConverter(),      PrioritySpecific);

        // Generic text catch-all (tried last)
        RegisterConverter(new PlainTextConverter(), PriorityGeneric);
    }

    private static string? GetFilenameFromHeaders(ContentDispositionHeaderValue? cd)
    {
        if (cd == null) return null;
        return cd.FileNameStar ?? cd.FileName;
    }

    private static string? MimeTypeFromExtension(string ext)
    {
        return ext.ToLowerInvariant() switch
        {
            ".pdf"   => "application/pdf",
            ".docx"  => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xlsx"  => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".pptx"  => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".html"  => "text/html",
            ".htm"   => "text/html",
            ".xhtml" => "application/xhtml+xml",
            ".csv"   => "text/csv",
            ".tsv"   => "text/tab-separated-values",
            ".txt"   => "text/plain",
            ".md"    => "text/markdown",
            ".zip"   => "application/zip",
            ".jpg"   => "image/jpeg",
            ".jpeg"  => "image/jpeg",
            ".png"   => "image/png",
            ".gif"   => "image/gif",
            ".bmp"   => "image/bmp",
            ".tiff"  => "image/tiff",
            ".tif"   => "image/tiff",
            ".webp"  => "image/webp",
            _        => null,
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_ownsHttpClient)
                _httpClient.Dispose();
            _disposed = true;
        }
    }
}
