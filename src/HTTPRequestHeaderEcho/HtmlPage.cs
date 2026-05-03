using Microsoft.Extensions.Primitives;
using System.Text;
using System.Text.Encodings.Web;

namespace HTTPRequestHeaderEcho;

public static class HtmlPage
{
    public static string Render(
        HttpContext ctx,
        string[] prefixes,
        string formTargetGuid,
        IReadOnlyList<string>? ignoredLines = null)
    {
        var encoder = HtmlEncoder.Default;
        var headers = ctx.Request.Headers.WithPrefixFilter(prefixes).ToList();

        var grouped = headers
            .GroupBy(h => GroupKey(h.Key), StringComparer.OrdinalIgnoreCase)
            .ToList();
        var standard = grouped.Where(g => g.Count() == 1)
            .SelectMany(g => g)
            .OrderBy(h => h.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var multi = grouped.Where(g => g.Count() > 1)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var sb = new StringBuilder();
        sb.Append($"""
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>HTTP Headers</title>
<style>{Css}</style>
</head>
<body>
<div class="container">
""");

        sb.Append("<header class=\"top\">\n");
        sb.Append("<h1>HTTP Headers</h1>\n");
        sb.Append("<div class=\"meta\">");
        sb.Append($"<span class=\"chip\">method<strong>{encoder.Encode(ctx.Request.Method)}</strong></span>");
        sb.Append($"<span class=\"chip\">path<strong>{encoder.Encode(ctx.Request.Path.ToString())}</strong></span>");
        sb.Append($"<span class=\"chip\">protocol<strong>{encoder.Encode(ctx.Request.Protocol)}</strong></span>");
        var remoteIp = ctx.Connection.RemoteIpAddress?.ToString() ?? "-";
        sb.Append($"<span class=\"chip\">remote<strong>{encoder.Encode(remoteIp)}</strong></span>");
        sb.Append("</div>\n</header>\n");

        // Request headers
        sb.Append("<section class=\"band\">\n<div class=\"band-label\">request</div>\n");
        if (headers.Count == 0)
        {
            sb.Append("<div class=\"empty\">No headers matched the active prefix filter.</div>\n");
        }
        else
        {
            if (standard.Count > 0)
            {
                sb.Append("<div class=\"group\">\n<h2>standard</h2>\n");
                AppendRows(sb, standard, encoder);
                sb.Append("</div>\n");
            }

            foreach (var group in multi)
            {
                var label = encoder.Encode(group.Key.ToLowerInvariant()) + "-*";
                sb.Append($"<div class=\"group\">\n<h2>{label}</h2>\n");
                AppendRows(sb, group.OrderBy(h => h.Key, StringComparer.OrdinalIgnoreCase), encoder);
                sb.Append("</div>\n");
            }
        }
        sb.Append("</section>\n");

        // Response headers (snapshot)
        sb.Append("<section class=\"band\">\n<div class=\"band-label\">response</div>\n");
        sb.Append("<p class=\"note\">Snapshot at render time. Auto-headers added by Kestrel later (<code>Date</code>, <code>Server</code>, <code>Content-Length</code>, possibly <code>Transfer-Encoding</code>) are not shown &mdash; check your browser's DevTools for the full response.</p>\n");
        sb.Append("<div class=\"group\">\n");
        var respHeaders = ctx.Response.Headers
            .OrderBy(h => h.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (respHeaders.Count == 0)
        {
            sb.Append("<div class=\"empty\">(none set yet)</div>\n");
        }
        else
        {
            AppendRows(sb, respHeaders, encoder);
        }
        sb.Append("</div>\n</section>\n");

        // Test response headers form
        sb.Append("<section class=\"band\">\n<div class=\"band-label\">test response headers</div>\n");
        sb.Append($"<form action=\"/{encoder.Encode(formTargetGuid)}\" method=\"get\" class=\"hform\">\n");
        sb.Append("<textarea name=\"h\" rows=\"6\" placeholder=\"Cache-Control: max-age=60&#10;X-Custom: hello\" spellcheck=\"false\"></textarea>\n");
        sb.Append("<button type=\"submit\">send &rarr;</button>\n");
        sb.Append("</form>\n");
        sb.Append("<p class=\"note\">Submits as <code>GET</code> to a fresh URL per page load. Refresh the result page to test caching; come back to <code>/</code> for a new URL.</p>\n");
        sb.Append("</section>\n");

        // Ignored input (only when there's something to report)
        if (ignoredLines is { Count: > 0 })
        {
            sb.Append("<section class=\"band\">\n<div class=\"band-label\">ignored input</div>\n");
            sb.Append("<div class=\"warn\">\n");
            foreach (var line in ignoredLines)
            {
                sb.Append($"<div class=\"warn-line\">{encoder.Encode(line)}</div>\n");
            }
            sb.Append("</div>\n");
            sb.Append("<p class=\"note\">These lines were skipped: missing <code>:</code>, invalid header name, or value contained CR/LF.</p>\n");
            sb.Append("</section>\n");
        }

        sb.Append("<footer>\n");
        sb.Append("<a href=\"/plain\">view as plain text &rarr;</a>\n");
        sb.Append("<a href=\"/\">start fresh test &rarr;</a>\n");
        if (prefixes.Length > 0)
        {
            var filterText = string.Join(", ", prefixes.Select(encoder.Encode));
            sb.Append($"<span>active prefix filter: <strong>{filterText}</strong></span>\n");
        }
        sb.Append("</footer>\n");
        sb.Append("</div>\n</body>\n</html>\n");

        return sb.ToString();
    }

    private static string GroupKey(string name)
    {
        var dash = name.IndexOf('-');
        return dash > 0 ? name[..dash] : name;
    }

    private static void AppendRows(
        StringBuilder sb,
        IEnumerable<KeyValuePair<string, StringValues>> rows,
        HtmlEncoder encoder)
    {
        foreach (var h in rows)
        {
            sb.Append("<div class=\"row\">");
            sb.Append($"<div class=\"name\">{encoder.Encode(h.Key)}</div>");
            sb.Append($"<div class=\"value\">{encoder.Encode(h.Value.ToString())}</div>");
            sb.Append("</div>\n");
        }
    }

    private const string Css = """
  :root {
    --bg: #0d1117;
    --fg: #c9d1d9;
    --muted: #8b949e;
    --accent: #7ee787;
    --warn: #f0883e;
    --border: #30363d;
    --card: #161b22;
  }
  @media (prefers-color-scheme: light) {
    :root {
      --bg: #f6f8fa;
      --fg: #1f2328;
      --muted: #57606a;
      --accent: #116329;
      --warn: #9a6700;
      --border: #d0d7de;
      --card: #ffffff;
    }
  }
  * { box-sizing: border-box; }
  html, body { margin: 0; padding: 0; }
  body {
    background: var(--bg);
    color: var(--fg);
    font-family: ui-monospace, "Cascadia Code", "JetBrains Mono", Menlo, Consolas, monospace;
    font-size: 14px;
    line-height: 1.5;
    padding: 24px 16px 48px;
  }
  .container { max-width: 920px; margin: 0 auto; }
  header.top { border-bottom: 1px solid var(--border); padding-bottom: 16px; margin-bottom: 24px; }
  h1 { font-size: 18px; margin: 0 0 12px; color: var(--accent); font-weight: 600; }
  h1::before { content: "> "; color: var(--muted); }
  .meta { display: flex; flex-wrap: wrap; gap: 8px; }
  .chip {
    background: var(--card);
    border: 1px solid var(--border);
    border-radius: 4px;
    padding: 2px 8px;
    font-size: 12px;
    color: var(--muted);
  }
  .chip strong { color: var(--fg); font-weight: 500; margin-left: 4px; }
  section.band { margin-bottom: 32px; }
  .band-label {
    color: var(--accent);
    font-size: 11px;
    font-weight: 600;
    letter-spacing: 0.08em;
    text-transform: uppercase;
    margin-bottom: 8px;
    padding-bottom: 4px;
    border-bottom: 1px dashed var(--border);
  }
  .band-label::before { content: "## "; color: var(--muted); }
  .group { margin-bottom: 12px; }
  h2 {
    font-size: 12px;
    margin: 0 0 8px;
    color: var(--muted);
    font-weight: 500;
    letter-spacing: 0.04em;
    text-transform: lowercase;
  }
  h2::before { content: "[ "; }
  h2::after { content: " ]"; }
  .row {
    background: var(--card);
    border: 1px solid var(--border);
    border-radius: 4px;
    padding: 8px 12px;
    margin-bottom: 6px;
  }
  .row .name {
    color: var(--accent);
    font-size: 12px;
    font-weight: 600;
    text-transform: lowercase;
  }
  .row .value {
    color: var(--fg);
    word-break: break-all;
    white-space: pre-wrap;
    margin-top: 2px;
  }
  .empty {
    color: var(--muted);
    font-style: italic;
    padding: 8px 0;
  }
  .note {
    color: var(--muted);
    font-size: 11px;
    margin: 4px 0 12px;
  }
  .note code {
    background: var(--card);
    border: 1px solid var(--border);
    border-radius: 3px;
    padding: 0 4px;
    font-size: 11px;
  }
  .hform { display: flex; flex-direction: column; gap: 8px; }
  textarea {
    width: 100%;
    background: var(--card);
    color: var(--fg);
    border: 1px solid var(--border);
    border-radius: 4px;
    padding: 8px 12px;
    font-family: inherit;
    font-size: 13px;
    line-height: 1.5;
    resize: vertical;
    min-height: 96px;
  }
  textarea:focus {
    outline: none;
    border-color: var(--accent);
  }
  button {
    align-self: flex-start;
    background: transparent;
    color: var(--accent);
    border: 1px solid var(--accent);
    border-radius: 4px;
    padding: 4px 14px;
    font-family: inherit;
    font-size: 12px;
    font-weight: 600;
    cursor: pointer;
  }
  button:hover { background: var(--accent); color: var(--bg); }
  .warn {
    border: 1px solid var(--warn);
    border-radius: 4px;
    padding: 8px 12px;
    background: var(--card);
  }
  .warn-line {
    color: var(--warn);
    font-size: 13px;
    padding: 2px 0;
    word-break: break-all;
  }
  footer {
    margin-top: 32px;
    padding-top: 16px;
    border-top: 1px solid var(--border);
    color: var(--muted);
    font-size: 12px;
    display: flex;
    flex-wrap: wrap;
    gap: 16px;
  }
  footer a { color: var(--accent); text-decoration: none; }
  footer a:hover { text-decoration: underline; }
  footer strong { color: var(--fg); font-weight: 500; }
""";
}
