using MarkItDown;
using System.Text;

// ── Argument parsing ──────────────────────────────────────────────────────────

string? inputFile     = null;
string? outputFile    = null;
string? extensionHint = null;
string? mimeHint      = null;
string? charsetHint   = null;
bool    showHelp      = false;
bool    showVersion   = false;

const string Version = "1.0.0";

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "-h":
        case "--help":
            showHelp = true;
            break;
        case "-v":
        case "--version":
            showVersion = true;
            break;
        case "-o":
        case "--output":
            outputFile = NextArg(args, ref i, "--output");
            break;
        case "-x":
        case "--extension":
            extensionHint = NextArg(args, ref i, "--extension");
            break;
        case "-m":
        case "--mime-type":
            mimeHint = NextArg(args, ref i, "--mime-type");
            break;
        case "-c":
        case "--charset":
            charsetHint = NextArg(args, ref i, "--charset");
            break;
        default:
            if (args[i].StartsWith('-'))
            {
                Console.Error.WriteLine($"Unknown option: {args[i]}");
                PrintUsage();
                return 1;
            }
            inputFile = args[i];
            break;
    }
}

if (showVersion)
{
    Console.WriteLine($"markitdown-dotnet {Version}");
    return 0;
}

if (showHelp)
{
    PrintUsage();
    return 0;
}

// ── Validate hints ────────────────────────────────────────────────────────────

if (extensionHint is { Length: > 0 } && !extensionHint.StartsWith('.'))
    extensionHint = "." + extensionHint;

if (mimeHint is { Length: > 0 } && mimeHint.Count(c => c == '/') != 1)
{
    Console.Error.WriteLine($"Invalid MIME type: {mimeHint}");
    return 1;
}

if (!string.IsNullOrEmpty(charsetHint))
{
    try
    {
        charsetHint = Encoding.GetEncoding(charsetHint).WebName;
    }
    catch
    {
        Console.Error.WriteLine($"Invalid charset: {charsetHint}");
        return 1;
    }
}

var streamInfo = (extensionHint != null || mimeHint != null || charsetHint != null)
    ? new StreamInfo { Extension = extensionHint, MimeType = mimeHint, Charset = charsetHint }
    : null;

// ── Convert ───────────────────────────────────────────────────────────────────

using var converter = new MarkItDownConverter();

DocumentConverterResult result;

try
{
    if (inputFile is not null)
    {
        result = converter.ConvertLocal(inputFile, streamInfo);
    }
    else
    {
        using var stdin = Console.OpenStandardInput();
        result = converter.ConvertStream(stdin, streamInfo);
    }
}
catch (FileNotFoundException ex)
{
    Console.Error.WriteLine($"File not found: {ex.FileName}");
    return 1;
}
catch (UnsupportedFormatException ex)
{
    Console.Error.WriteLine($"Unsupported format: {ex.Message}");
    return 1;
}
catch (FileConversionException ex)
{
    Console.Error.WriteLine($"Conversion failed: {ex.Message}");
    return 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}

// ── Output ────────────────────────────────────────────────────────────────────

if (outputFile is not null)
{
    await File.WriteAllTextAsync(outputFile, result.Markdown, Encoding.UTF8);
}
else
{
    using var stdout = new StreamWriter(
        Console.OpenStandardOutput(),
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        leaveOpen: true);
    await stdout.WriteAsync(result.Markdown);
    await stdout.FlushAsync();
}

return 0;

// ── Helpers ───────────────────────────────────────────────────────────────────

static string NextArg(string[] args, ref int i, string flag)
{
    if (++i >= args.Length)
    {
        Console.Error.WriteLine($"Option {flag} requires an argument.");
        Environment.Exit(1);
    }
    return args[i];
}

static void PrintUsage()
{
    Console.WriteLine("""
        markitdown-dotnet — Convert various file formats to Markdown (.NET 10)

        SYNTAX:
          markitdown-dotnet [OPTIONS] [FILENAME]

        DESCRIPTION:
          If FILENAME is omitted, content is read from stdin.

        OPTIONS:
          -o, --output <file>       Write output to file instead of stdout
          -x, --extension <ext>     Hint about the input file extension (e.g. pdf)
          -m, --mime-type <type>    Hint about the input MIME type
          -c, --charset <charset>   Hint about the input charset (e.g. utf-8)
          -v, --version             Show version number and exit
          -h, --help                Show this help message and exit

        EXAMPLES:
          markitdown-dotnet document.pdf
          markitdown-dotnet document.pdf -o document.md
          cat document.pdf | markitdown-dotnet -x pdf
          markitdown-dotnet document.xlsx > document.md
        """);
}

