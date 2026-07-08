using System.Net;
using System.Text;

namespace GitPulse.App.Services;

/// <summary>
/// Converts a GitHub unified diff <c>patch</c> string into a self-contained
/// HTML document with line-by-line syntax coloring. The output is designed
/// to be loaded into a MAUI <c>WebView</c> via its <c>Html</c> source.
/// </summary>
/// <remarks>
/// <para>
/// The generated HTML uses inline CSS (no external dependencies) so it
/// renders correctly in a WebView without network access. Color scheme:
/// </para>
/// <list type="bullet">
/// <item><c>+</c> lines: green background (#e6ffed light / #1a3a1a dark)</item>
/// <item><c>-</c> lines: red background (#ffeef0 light / #3a1a1a dark)</item>
/// <item><c>@@</c> hunk headers: blue text on gray background</item>
/// <item>context lines: default background</item>
/// </list>
/// <para>
/// Uses <c>prefers-color-scheme</c> media query for light/dark adaptation.
/// Monospace font with line numbers for readability.
/// </para>
/// </remarks>
public static class DiffHtmlGenerator
{
    /// <summary>
    /// Generate a complete HTML document from a unified diff patch string.
    /// Returns a minimal "binary file" notice when <paramref name="patch"/>
    /// is null or empty.
    /// </summary>
    public static string GenerateHtml(string? patch, string? filename = null)
    {
        if (string.IsNullOrEmpty(patch))
            return BuildHtmlDocument(BuildBinaryNotice(filename));

        var rows = new StringBuilder();
        var lines = patch.Split('\n');
        var oldLine = 0;
        var newLine = 0;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Length == 0)
                continue;

            var (rowHtml, oldNum, newNum) = RenderLine(line, oldLine, newLine);
            rows.Append(rowHtml);
            oldLine = oldNum;
            newLine = newNum;
        }

        return BuildHtmlDocument(rows.ToString());
    }

    private static (string html, int oldLine, int newLine) RenderLine(
        string line, int oldLine, int newLine)
    {
        var cls = "ctx";
        var oldNum = "";
        var newNum = "";
        var nextOld = oldLine;
        var nextNew = newLine;

        if (line.StartsWith("@@"))
        {
            cls = "hunk";
            // Parse hunk header: @@ -oldStart,oldCount +newStart,newCount @@
            (nextOld, nextNew) = ParseHunkHeader(line);
        }
        else if (line.StartsWith('+'))
        {
            cls = "add";
            nextNew = newLine + 1;
            newNum = newLine.ToString();
        }
        else if (line.StartsWith('-'))
        {
            cls = "del";
            nextOld = oldLine + 1;
            oldNum = oldLine.ToString();
        }
        else
        {
            cls = "ctx";
            nextOld = oldLine + 1;
            nextNew = newLine + 1;
            oldNum = oldLine.ToString();
            newNum = newLine.ToString();
        }

        var escaped = WebUtility.HtmlEncode(line.Length > 0 ? line[1..] : line);
        var indicator = line.Length > 0 ? line[0].ToString() : " ";

        var row = $"<tr class=\"{cls}\">" +
                  $"<td class=\"ln old\">{oldNum}</td>" +
                  $"<td class=\"ln new\">{newNum}</td>" +
                  $"<td class=\"ind\">{indicator}</td>" +
                  $"<td class=\"code\"><pre>{escaped}</pre></td>" +
                  $"</tr>";

        return (row, nextOld, nextNew);
    }

    /// <summary>Parse <c>@@ -oldStart,oldCount +newStart,newCount @@</c> header.</summary>
    private static (int oldStart, int newStart) ParseHunkHeader(string line)
    {
        // Format: @@ -start,count +start,count @@
        var parts = line.Split(' ');
        if (parts.Length < 3)
            return (0, 0);

        var oldPart = parts[1].TrimStart('-');
        var newPart = parts[2].TrimStart('+');

        var oldStart = ExtractLineNumber(oldPart);
        var newStart = ExtractLineNumber(newPart);

        return (oldStart, newStart);
    }

    private static int ExtractLineNumber(string part)
    {
        var commaIdx = part.IndexOf(',');
        var numStr = commaIdx >= 0 ? part[..commaIdx] : part;
        return int.TryParse(numStr, out var n) ? n : 0;
    }

    private static string BuildBinaryNotice(string? filename)
    {
        var name = WebUtility.HtmlEncode(filename ?? "file");
        return $"<div class=\"binary\">Binary file {name} — diff not available.</div>";
    }

    private static string BuildHtmlDocument(string body)
    {
        return $$"""
<!DOCTYPE html>
<html>
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<style>
:root {
  --bg: #ffffff;
  --fg: #1f2328;
  --add-bg: #e6ffed;
  --add-fg: #22863a;
  --del-bg: #ffeef0;
  --del-fg: #cb2431;
  --hunk-bg: #f1f8ff;
  --hunk-fg: #0366d6;
  --ln-bg: #f6f8fa;
  --ln-fg: #959da5;
  --border: #e1e4e8;
}
@media (prefers-color-scheme: dark) {
  :root {
    --bg: #0d1117;
    --fg: #c9d1d9;
    --add-bg: #1a3a1a;
    --add-fg: #56d364;
    --del-bg: #3a1a1a;
    --del-fg: #f85149;
    --hunk-bg: #1a2540;
    --hunk-fg: #58a6ff;
    --ln-bg: #161b22;
    --ln-fg: #6e7681;
    --border: #30363d;
  }
}
body {
  margin: 0;
  padding: 8px;
  background: var(--bg);
  color: var(--fg);
  font-family: 'Cascadia Mono', 'Consolas', 'Courier New', monospace;
  font-size: 13px;
  line-height: 1.5;
  -webkit-text-size-adjust: 100%;
}
table {
  border-collapse: collapse;
  width: 100%;
}
tr.hunk { background: var(--hunk-bg); }
tr.hunk .code pre { color: var(--hunk-fg); font-weight: bold; }
tr.add { background: var(--add-bg); }
tr.add .code pre { color: var(--add-fg); }
tr.del { background: var(--del-bg); }
tr.del .code pre { color: var(--del-fg); }
td.ln {
  background: var(--ln-bg);
  color: var(--ln-fg);
  text-align: right;
  padding: 0 8px;
  white-space: nowrap;
  user-select: none;
  min-width: 32px;
  border-right: 1px solid var(--border);
}
td.ind {
  padding: 0 4px;
  text-align: center;
  user-select: none;
  color: var(--ln-fg);
}
td.code pre {
  margin: 0;
  padding: 0 8px;
  white-space: pre-wrap;
  word-break: break-all;
  font-family: inherit;
}
.binary {
  padding: 16px;
  text-align: center;
  color: var(--ln-fg);
}
</style>
</head>
<body>
<table>
{{body}}
</table>
</body>
</html>
""";
    }
}
