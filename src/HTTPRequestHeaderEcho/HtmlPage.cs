using System.Text;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Primitives;

namespace HTTPRequestHeaderEcho;

public static class HtmlPage
{
    public static string Render(HttpContext ctx, string[] prefixes)
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
<title>HTTP Request Headers</title>
<style>{Css}</style>
</head>
<body>
<div class="container">
""");

        sb.Append("<header class=\"top\">\n");
        sb.Append("<h1>HTTP Request Headers</h1>\n");
        sb.Append("<div class=\"meta\">");
        sb.Append($"<span class=\"chip\">method<strong>{encoder.Encode(ctx.Request.Method)}</strong></span>");
        sb.Append($"<span class=\"chip\">path<strong>{encoder.Encode(ctx.Request.Path.ToString())}</strong></span>");
        sb.Append($"<span class=\"chip\">protocol<strong>{encoder.Encode(ctx.Request.Protocol)}</strong></span>");
        var remoteIp = ctx.Connection.RemoteIpAddress?.ToString() ?? "-";
        sb.Append($"<span class=\"chip\">remote<strong>{encoder.Encode(remoteIp)}</strong></span>");
        sb.Append("</div>\n</header>\n");

        if (headers.Count == 0)
        {
            sb.Append("<div class=\"empty\">No headers matched the active prefix filter.</div>\n");
        }
        else
        {
            if (standard.Count > 0)
            {
                sb.Append("<section>\n<h2>standard</h2>\n");
                AppendRows(sb, standard, encoder);
                sb.Append("</section>\n");
            }

            foreach (var group in multi)
            {
                var label = encoder.Encode(group.Key.ToLowerInvariant()) + "-*";
                sb.Append($"<section>\n<h2>{label}</h2>\n");
                AppendRows(sb, group.OrderBy(h => h.Key, StringComparer.OrdinalIgnoreCase), encoder);
                sb.Append("</section>\n");
            }
        }

        sb.Append("<footer>\n");
        sb.Append("<a href=\"/plain\">view as plain text &rarr;</a>\n");
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
    --border: #30363d;
    --card: #161b22;
  }
  @media (prefers-color-scheme: light) {
    :root {
      --bg: #f6f8fa;
      --fg: #1f2328;
      --muted: #57606a;
      --accent: #116329;
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
  section { margin-bottom: 28px; }
  h2 {
    font-size: 12px;
    margin: 0 0 10px;
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
    padding: 12px 0;
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
    justify-content: space-between;
  }
  footer a { color: var(--accent); text-decoration: none; }
  footer a:hover { text-decoration: underline; }
  footer strong { color: var(--fg); font-weight: 500; }
""";
}
