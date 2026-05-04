# MarkItDown for .NET

A .NET 10 port of the [MarkItDown](https://github.com/microsoft/markitdown) Python utility — a lightweight tool for converting various file formats to Markdown, suitable for LLM pipelines and text analysis.

## Supported Formats

| Format | Extension(s) | Notes |
|--------|-------------|-------|
| Plain text / source code | `.txt`, `.md`, `.py`, `.cs`, … | Returned as-is |
| HTML / XHTML | `.html`, `.htm`, `.xhtml` | Full DOM-to-Markdown conversion with tables |
| PDF | `.pdf` | Text extraction via [PdfPig](https://github.com/UglyToad/PdfPig) |
| Word (DOCX) | `.docx`, `.dotx` | Headings, paragraphs, tables, bold/italic |
| Excel (XLSX) | `.xlsx`, `.xlsm` | Each worksheet → Markdown table |
| PowerPoint (PPTX) | `.pptx`, `.pptm` | Each slide → section with text and tables |
| CSV / TSV | `.csv`, `.tsv` | Auto-detects delimiter; rendered as table |
| Images | `.jpg`, `.png`, `.gif`, … | EXIF/IPTC metadata extraction |
| ZIP archive | `.zip` | Recursively converts each entry |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## Building

```bash
cd markitdown-dotnet
dotnet build
```

## Usage

### Command-Line

```bash
# Convert a local file
dotnet run --project src/MarkItDown.Cli -- document.pdf

# Save output to file
dotnet run --project src/MarkItDown.Cli -- document.pdf -o document.md

# Pipe content from stdin with an extension hint
cat document.pdf | dotnet run --project src/MarkItDown.Cli -- -x pdf

# Redirect output
dotnet run --project src/MarkItDown.Cli -- document.xlsx > document.md
```

After publishing you can also run the executable directly:

```bash
dotnet publish src/MarkItDown.Cli -c Release -o out/
./out/MarkItDown.Cli document.pdf
```

#### CLI Options

| Option | Description |
|--------|-------------|
| `-o`, `--output <file>` | Write output to a file instead of stdout |
| `-x`, `--extension <ext>` | Hint about the file extension (e.g. `pdf`) |
| `-m`, `--mime-type <type>` | Hint about the MIME type |
| `-c`, `--charset <charset>` | Hint about the character encoding |
| `-v`, `--version` | Print version and exit |
| `-h`, `--help` | Print help and exit |

### Library API

```csharp
using MarkItDown;

// Convert a local file
using var md = new MarkItDownConverter();
var result = md.Convert("document.pdf");
Console.WriteLine(result.Markdown);

// Convert from a URL
var result2 = md.ConvertUrl("https://example.com/page.html");

// Convert from a stream
using var stream = File.OpenRead("data.xlsx");
var result3 = md.ConvertStream(stream, new StreamInfo { Extension = ".xlsx" });

// Register a custom converter
md.RegisterConverter(new MyCustomConverter(), priority: 0.0);
```

## Project Structure

```
markitdown-dotnet/
├── MarkItDown.sln
└── src/
    ├── MarkItDown/                  # Class library
    │   ├── StreamInfo.cs
    │   ├── DocumentConverter.cs
    │   ├── DocumentConverterResult.cs
    │   ├── Exceptions.cs
    │   ├── MarkItDownConverter.cs   # Main orchestrator
    │   └── Converters/
    │       ├── PlainTextConverter.cs
    │       ├── HtmlConverter.cs
    │       ├── PdfConverter.cs
    │       ├── DocxConverter.cs
    │       ├── XlsxConverter.cs
    │       ├── PptxConverter.cs
    │       ├── CsvConverter.cs
    │       ├── ImageConverter.cs
    │       └── ZipConverter.cs
    └── MarkItDown.Cli/              # Console application
        └── Program.cs
```

## Dependencies

| Package | Purpose |
|---------|---------|
| [HtmlAgilityPack](https://html-agility-pack.net/) | HTML parsing |
| [PdfPig](https://github.com/UglyToad/PdfPig) | PDF text extraction |
| [DocumentFormat.OpenXml](https://github.com/dotnet/Open-XML-SDK) | DOCX / XLSX / PPTX |
| [MetadataExtractor](https://github.com/drewnoakes/metadata-extractor-dotnet) | Image EXIF/IPTC metadata |

## Extending with Custom Converters

Implement `DocumentConverter` and register it:

```csharp
public sealed class MyConverter : DocumentConverter
{
    public override bool Accepts(Stream stream, StreamInfo info)
        => (info.Extension ?? "").Equals(".myext", StringComparison.OrdinalIgnoreCase);

    public override DocumentConverterResult Convert(Stream stream, StreamInfo info)
    {
        // … your conversion logic …
        return new DocumentConverterResult("# Hello from MyConverter\n\n…");
    }
}

using var md = new MarkItDownConverter();
md.RegisterConverter(new MyConverter(), priority: 0.0);
```

## License

MIT — same as the original Python MarkItDown project.
