using HtmlAgilityPack;
using System.Text;
using System.Web;

namespace MarkItDown.Converters;

/// <summary>
/// Converts HTML / XHTML files and streams to Markdown.
/// Uses HtmlAgilityPack to parse the DOM and a custom tree-walker
/// to produce well-formed Markdown output.
/// </summary>
public sealed class HtmlConverter : DocumentConverter
{
    private static readonly HashSet<string> AcceptedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".html", ".htm", ".xhtml", ".xht",
    };

    public override bool Accepts(Stream stream, StreamInfo streamInfo)
    {
        var mime = streamInfo.MimeType ?? string.Empty;
        var ext  = streamInfo.Extension ?? string.Empty;

        return AcceptedExtensions.Contains(ext)
            || mime.StartsWith("text/html", StringComparison.OrdinalIgnoreCase)
            || mime.StartsWith("application/xhtml", StringComparison.OrdinalIgnoreCase);
    }

    public override DocumentConverterResult Convert(Stream stream, StreamInfo streamInfo)
    {
        var doc = new HtmlDocument();
        doc.Load(stream, ResolveEncoding(streamInfo.Charset));

        // Remove script and style nodes
        RemoveNodes(doc, "//script");
        RemoveNodes(doc, "//style");

        string? title = doc.DocumentNode
            .SelectSingleNode("//title")?.InnerText?.Trim();
        title = string.IsNullOrEmpty(title) ? null : HttpUtility.HtmlDecode(title);

        var bodyNode = doc.DocumentNode.SelectSingleNode("//body")
                       ?? doc.DocumentNode;

        var sb = new StringBuilder();
        ConvertNode(bodyNode, sb, 0);

        string markdown = NormalizeBlankLines(sb.ToString().Trim());
        return new DocumentConverterResult(markdown, title);
    }

    /// <summary>Converts an HTML string directly to Markdown.</summary>
    public DocumentConverterResult ConvertString(string html, string? url = null)
    {
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(html));
        return Convert(ms, new StreamInfo
        {
            MimeType = "text/html",
            Extension = ".html",
            Charset = "utf-8",
            Url = url,
        });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void RemoveNodes(HtmlDocument doc, string xpath)
    {
        foreach (var node in doc.DocumentNode.SelectNodes(xpath)?.ToList() ?? [])
            node.Remove();
    }

    private static Encoding ResolveEncoding(string? charset)
    {
        if (string.IsNullOrWhiteSpace(charset)) return Encoding.UTF8;
        try { return Encoding.GetEncoding(charset); } catch { return Encoding.UTF8; }
    }

    private static string NormalizeBlankLines(string text)
    {
        // Collapse 3+ blank lines to 2 blank lines
        return System.Text.RegularExpressions.Regex.Replace(text, @"\n{3,}", "\n\n");
    }

    // ── DOM-to-Markdown tree walker ───────────────────────────────────────────

    private static void ConvertNode(HtmlNode node, StringBuilder sb, int listDepth)
    {
        switch (node.NodeType)
        {
            case HtmlNodeType.Text:
                string text = HttpUtility.HtmlDecode(node.InnerText);
                // Preserve inline whitespace but collapse internal newlines
                text = System.Text.RegularExpressions.Regex.Replace(text, @"[\r\n]+", " ");
                if (!string.IsNullOrWhiteSpace(text))
                    sb.Append(text);
                return;

            case HtmlNodeType.Comment:
                return;
        }

        // Element node
        string tag = node.Name.ToLowerInvariant();

        switch (tag)
        {
            case "h1": WrapBlock(node, sb, listDepth, "# ");  break;
            case "h2": WrapBlock(node, sb, listDepth, "## "); break;
            case "h3": WrapBlock(node, sb, listDepth, "### ");break;
            case "h4": WrapBlock(node, sb, listDepth, "#### ");break;
            case "h5": WrapBlock(node, sb, listDepth, "##### ");break;
            case "h6": WrapBlock(node, sb, listDepth, "###### ");break;

            case "p":
            case "div":
            case "section":
            case "article":
            case "header":
            case "footer":
            case "main":
            case "nav":
            case "aside":
                sb.Append("\n\n");
                ConvertChildren(node, sb, listDepth);
                sb.Append("\n\n");
                break;

            case "br":
                sb.Append("  \n");
                break;

            case "hr":
                sb.Append("\n\n---\n\n");
                break;

            case "strong":
            case "b":
                sb.Append("**");
                ConvertChildren(node, sb, listDepth);
                sb.Append("**");
                break;

            case "em":
            case "i":
                sb.Append("*");
                ConvertChildren(node, sb, listDepth);
                sb.Append("*");
                break;

            case "code":
                if (node.ParentNode?.Name == "pre")
                {
                    ConvertChildren(node, sb, listDepth);
                }
                else
                {
                    sb.Append('`');
                    ConvertChildren(node, sb, listDepth);
                    sb.Append('`');
                }
                break;

            case "pre":
                sb.Append("\n\n```\n");
                var preInner = HttpUtility.HtmlDecode(node.InnerText);
                sb.Append(preInner.TrimEnd());
                sb.Append("\n```\n\n");
                break;

            case "blockquote":
                var bqSb = new StringBuilder();
                sb.Append("\n\n");
                ConvertChildren(node, bqSb, listDepth);
                foreach (var line in bqSb.ToString().Trim().Split('\n'))
                    sb.Append("> ").Append(line).Append('\n');
                sb.Append('\n');
                break;

            case "a":
                string href = node.GetAttributeValue("href", string.Empty);
                var linkText = new StringBuilder();
                ConvertChildren(node, linkText, listDepth);
                string lt = linkText.ToString().Trim();
                if (string.IsNullOrEmpty(lt)) lt = href;
                if (!string.IsNullOrEmpty(href))
                    sb.Append('[').Append(lt).Append("](").Append(href).Append(')');
                else
                    sb.Append(lt);
                break;

            case "img":
                string src = node.GetAttributeValue("src", string.Empty);
                string alt = node.GetAttributeValue("alt", string.Empty);
                if (!string.IsNullOrEmpty(src))
                    sb.Append("![").Append(alt).Append("](").Append(src).Append(')');
                break;

            case "ul":
                sb.Append('\n');
                foreach (var li in node.ChildNodes.Where(n => n.Name == "li"))
                {
                    string indent = new(' ', listDepth * 2);
                    sb.Append(indent).Append("- ");
                    var liSb = new StringBuilder();
                    ConvertChildren(li, liSb, listDepth + 1);
                    sb.Append(liSb.ToString().Trim()).Append('\n');
                }
                sb.Append('\n');
                break;

            case "ol":
                sb.Append('\n');
                int idx = 1;
                foreach (var li in node.ChildNodes.Where(n => n.Name == "li"))
                {
                    string indent = new(' ', listDepth * 2);
                    sb.Append(indent).Append(idx++).Append(". ");
                    var liSb = new StringBuilder();
                    ConvertChildren(li, liSb, listDepth + 1);
                    sb.Append(liSb.ToString().Trim()).Append('\n');
                }
                sb.Append('\n');
                break;

            case "table":
                ConvertTable(node, sb);
                break;

            case "script":
            case "style":
            case "head":
            case "meta":
            case "link":
                // Ignored
                break;

            default:
                ConvertChildren(node, sb, listDepth);
                break;
        }
    }

    private static void WrapBlock(HtmlNode node, StringBuilder sb, int listDepth, string prefix)
    {
        sb.Append("\n\n").Append(prefix);
        ConvertChildren(node, sb, listDepth);
        sb.Append("\n\n");
    }

    private static void ConvertChildren(HtmlNode node, StringBuilder sb, int listDepth)
    {
        foreach (var child in node.ChildNodes)
            ConvertNode(child, sb, listDepth);
    }

    private static void ConvertTable(HtmlNode table, StringBuilder sb)
    {
        sb.Append("\n\n");
        var rows = table.SelectNodes(".//tr")?.ToList() ?? [];
        if (rows.Count == 0) return;

        // Collect all rows as lists of cell text
        var tableData = new List<List<string>>();
        foreach (var row in rows)
        {
            var cells = row.ChildNodes
                .Where(n => n.Name is "td" or "th")
                .Select(n => HttpUtility.HtmlDecode(n.InnerText).Trim().Replace("\n", " "))
                .ToList();
            tableData.Add(cells);
        }

        int colCount = tableData.Max(r => r.Count);

        // Pad rows to same width
        foreach (var row in tableData)
            while (row.Count < colCount) row.Add(string.Empty);

        // Calculate column widths
        int[] widths = new int[colCount];
        for (int c = 0; c < colCount; c++)
            widths[c] = Math.Max(3, tableData.Max(r => r[c].Length));

        static string PadCell(string s, int w) => s.PadRight(w);

        void AppendRow(List<string> row)
        {
            sb.Append('|');
            for (int c = 0; c < colCount; c++)
                sb.Append(' ').Append(PadCell(row[c], widths[c])).Append(" |");
            sb.Append('\n');
        }

        // Header row
        AppendRow(tableData[0]);

        // Separator
        sb.Append('|');
        for (int c = 0; c < colCount; c++)
            sb.Append(' ').Append(new string('-', widths[c])).Append(" |");
        sb.Append('\n');

        // Data rows
        for (int r = 1; r < tableData.Count; r++)
            AppendRow(tableData[r]);

        sb.Append('\n');
    }
}
