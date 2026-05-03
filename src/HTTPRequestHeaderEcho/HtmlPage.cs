using Microsoft.Extensions.Primitives;
using System.Text;
using System.Text.Encodings.Web;

namespace HTTPRequestHeaderEcho;

public sealed record HtmlPageModel(
    HttpContext Ctx,
    string[] Prefixes,
    string FormTargetGuid,
    string CurrentRequestSpec,
    string CurrentResponseSpec,
    IReadOnlyList<KeyValuePair<string, string>> ValidRequestHeaders,
    IReadOnlyList<KeyValuePair<string, string>> DroppedRequestHeaders,
    IReadOnlyList<string> IgnoredRequestLines,
    IReadOnlyList<string> IgnoredResponseLines);

public static class HtmlPage
{
    public static string Render(HtmlPageModel m)
    {
        var encoder = HtmlEncoder.Default;
        var ctx = m.Ctx;
        var headers = ctx.Request.Headers.WithPrefixFilter(m.Prefixes).WithConsentScrub().ToList();

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

        // Title + meta strip
        sb.Append("<header class=\"top\">\n");
        sb.Append("<h1>HTTP Headers</h1>\n");
        sb.Append("<div class=\"meta\">");
        sb.Append($"<span class=\"chip\">method<strong>{encoder.Encode(ctx.Request.Method)}</strong></span>");
        sb.Append($"<span class=\"chip\">path<strong>{encoder.Encode(ctx.Request.Path.ToString())}</strong></span>");
        sb.Append($"<span class=\"chip\">protocol<strong>{encoder.Encode(ctx.Request.Protocol)}</strong></span>");
        var remoteIp = ctx.Connection.RemoteIpAddress?.ToString() ?? "-";
        sb.Append($"<span class=\"chip\">remote<strong>{encoder.Encode(remoteIp)}</strong></span>");
        sb.Append("</div>\n");

        // Render-time strip
        var renderedAt = DateTime.UtcNow.ToString("o");
        sb.Append("<div class=\"render-time\">\n");
        sb.Append("<span class=\"label\">rendered (UTC)</span>");
        sb.Append($"<strong>{encoder.Encode(renderedAt)}</strong>");
        sb.Append("<span class=\"hint\">page content &mdash; not an HTTP header. If this matches across refreshes, the page came from cache.</span>\n");
        sb.Append("</div>\n");
        sb.Append("</header>\n");

        // Request headers
        sb.Append("<section class=\"band\">\n<div class=\"band-label\">request</div>\n");
        if (m.DroppedRequestHeaders.Count > 0)
        {
            sb.Append("<div class=\"dropped\">\n");
            sb.Append($"<div class=\"dropped-title\">browser dropped {m.DroppedRequestHeaders.Count} request header(s)</div>\n");
            sb.Append("<div class=\"dropped-body\">\n");
            foreach (var (name, value) in m.DroppedRequestHeaders)
            {
                sb.Append($"<div class=\"dropped-line\">{encoder.Encode(name)}: {encoder.Encode(value)}</div>\n");
            }
            sb.Append("</div>\n");
            sb.Append("<div class=\"dropped-hint\">Browsers forbid JS from setting some headers (<code>User-Agent</code>, <code>Cookie</code>, <code>Host</code>, <code>Origin</code>, <code>Referer</code>, <code>Connection</code>, <code>Sec-*</code>, etc.) and a few others may have been merged or stripped. The replay snippets below send these unmodified.</div>\n");
            sb.Append("</div>\n");
        }
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
        sb.Append("<p class=\"note\">Server-side snapshot at render time. Kestrel auto-headers (<code>Date</code>, <code>Server</code>, <code>Content-Length</code>, possibly <code>Transfer-Encoding</code>) are added later in the pipeline &mdash; use the live panel below to see what the browser actually received.</p>\n");
        sb.Append("<div class=\"group\">\n<h2>snapshot (server-side)</h2>\n");
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
        sb.Append("</div>\n");

        // Live response panel (populated by the form-submit fetch below)
        sb.Append("<div class=\"group\" id=\"live-resp-group\">\n<h2>actual (received by browser)</h2>\n");
        sb.Append("<p class=\"note\">Populated by the <strong>send</strong> button below. Lists the response headers the browser received from that <code>fetch()</code>, including Kestrel auto-headers (<code>Date</code>, <code>Server</code>, <code>Content-Length</code>, <code>Transfer-Encoding</code>). <code>Set-Cookie</code> is hidden from JS (forbidden response header).</p>\n");
        sb.Append("<div id=\"live-resp-out\"><div class=\"empty\">submit the form below to populate</div></div>\n");
        sb.Append("</div>\n</section>\n");

        // Test playground form (combined: request + response headers)
        sb.Append("<section class=\"band\">\n<div class=\"band-label\">test playground</div>\n");
        sb.Append($"<form id=\"hform\" action=\"/{encoder.Encode(m.FormTargetGuid)}\" method=\"get\" class=\"hform\">\n");
        sb.Append("<div class=\"field\">\n");
        sb.Append("<label for=\"req-h\">request headers (sent by your client)</label>\n");
        sb.Append($"<textarea id=\"req-h\" name=\"r\" rows=\"4\" placeholder=\"X-Custom: hello&#10;Authorization: Bearer abc\" spellcheck=\"false\">{encoder.Encode(m.CurrentRequestSpec)}</textarea>\n");
        sb.Append("</div>\n");
        sb.Append("<div class=\"field\">\n");
        sb.Append("<label for=\"res-h\">response headers (returned by the server)</label>\n");
        sb.Append($"<textarea id=\"res-h\" name=\"h\" rows=\"4\" placeholder=\"Cache-Control: max-age=60&#10;X-Trace: xyz\" spellcheck=\"false\">{encoder.Encode(m.CurrentResponseSpec)}</textarea>\n");
        sb.Append("</div>\n");
        sb.Append("<button type=\"submit\">send &rarr;</button>\n");
        sb.Append("</form>\n");
        sb.Append("<p class=\"note\">With JS: request headers are sent via <code>fetch()</code>. Without JS: only response headers are applied (browsers can't add arbitrary request headers via plain form submit). <strong>Browsers also forbid JS from setting headers like <code>User-Agent</code>, <code>Cookie</code>, <code>Host</code>, <code>Referer</code>, <code>Origin</code>, <code>Sec-*</code></strong> &mdash; those will silently fail in-browser. The replay snippets below send them verbatim from a terminal. Refreshing the result page re-navigates with browser-default request headers; the response cache test still works because <code>?h=</code> rides in the URL.</p>\n");
        sb.Append("</section>\n");

        // Snippets (only when the user has submitted something)
        var hasInput = !string.IsNullOrEmpty(m.CurrentRequestSpec) || !string.IsNullOrEmpty(m.CurrentResponseSpec);
        if (hasInput)
        {
            var absUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host}{ctx.Request.Path}{ctx.Request.QueryString}";
            sb.Append("<section class=\"band\">\n<div class=\"band-label\">replay snippets</div>\n");
            sb.Append("<p class=\"note\">Refresh-safe replay from a terminal. The browser refresh button doesn't send custom request headers, but these do.</p>\n");

            sb.Append("<div class=\"snippet\">\n");
            sb.Append("<span class=\"snippet-label\">curl</span>\n");
            sb.Append($"<pre>{encoder.Encode(Snippets.Curl(absUrl, m.ValidRequestHeaders))}</pre>\n");
            sb.Append("</div>\n");

            sb.Append("<div class=\"snippet\">\n");
            sb.Append("<span class=\"snippet-label\">powershell (Invoke-RestMethod)</span>\n");
            sb.Append($"<pre>{encoder.Encode(Snippets.PowerShell(absUrl, m.ValidRequestHeaders))}</pre>\n");
            sb.Append("</div>\n");

            sb.Append("</section>\n");
        }

        // Ignored input
        var hasIgnored = m.IgnoredRequestLines.Count > 0 || m.IgnoredResponseLines.Count > 0;
        if (hasIgnored)
        {
            sb.Append("<section class=\"band\">\n<div class=\"band-label\">ignored input</div>\n");
            sb.Append("<p class=\"note\">Lines skipped: missing <code>:</code>, invalid header name, or value contained CR/LF.</p>\n");
            if (m.IgnoredRequestLines.Count > 0)
            {
                sb.Append("<div class=\"sub\"><span class=\"sub-label\">request:</span></div>\n");
                sb.Append("<div class=\"warn\">\n");
                foreach (var line in m.IgnoredRequestLines)
                    sb.Append($"<div class=\"warn-line\">{encoder.Encode(line)}</div>\n");
                sb.Append("</div>\n");
            }
            if (m.IgnoredResponseLines.Count > 0)
            {
                sb.Append("<div class=\"sub\"><span class=\"sub-label\">response:</span></div>\n");
                sb.Append("<div class=\"warn\">\n");
                foreach (var line in m.IgnoredResponseLines)
                    sb.Append($"<div class=\"warn-line\">{encoder.Encode(line)}</div>\n");
                sb.Append("</div>\n");
            }
            sb.Append("</section>\n");
        }

        // Footer
        sb.Append("<footer>\n");
        sb.Append("<a href=\"/plain\">view as plain text &rarr;</a>\n");
        sb.Append("<a href=\"/\">start fresh test &rarr;</a>\n");
        if (m.Prefixes.Length > 0)
        {
            var filterText = string.Join(", ", m.Prefixes.Select(encoder.Encode));
            sb.Append($"<span>active prefix filter: <strong>{filterText}</strong></span>\n");
        }
        sb.Append("</footer>\n");
        sb.Append("</div>\n");

        // Inline JS
        sb.Append("<script>");
        sb.Append(Js);
        sb.Append("</script>\n");

        sb.Append("</body>\n</html>\n");

        return sb.ToString();
    }

    public static string RenderInterstitial(HttpContext ctx)
    {
        var encoder = HtmlEncoder.Default;
        var nextUrl = $"{ctx.Request.Path}{ctx.Request.QueryString}";

        return $"""
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>HTTP Headers &mdash; confirm</title>
<style>{Css}</style>
</head>
<body>
<div class="container">
<header class="top"><h1>HTTP Headers</h1></header>
<section class="band">
<div class="band-label">confirm visit</div>
<p class="note">This service can set arbitrary HTTP response headers on your browser via crafted URLs &mdash; including <code>Set-Cookie</code>, <code>Refresh</code> redirects, long-lived <code>Strict-Transport-Security</code> pins, and <code>Clear-Site-Data</code>. Continue only if you intentionally navigated here.</p>
<p class="note">After accepting, this prompt won't return for 6 hours.</p>
<form method="post" action="/consent" class="hform">
<input type="hidden" name="next" value="{encoder.Encode(nextUrl)}">
<button type="submit">accept and continue &rarr;</button>
</form>
</section>
<footer>
<a href="/plain">cancel &mdash; view /plain instead &rarr;</a>
</footer>
</div>
</body>
</html>
""";
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

    private const string Js = """
(function () {
  var form = document.getElementById('hform');
  if (!form) return;
  function esc(s) {
    return String(s).replace(/[&<>"']/g, function (c) {
      return ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'})[c];
    });
  }
  function renderLivePairs(pairs) {
    var out = document.getElementById('live-resp-out');
    if (!out) return;
    if (!pairs || pairs.length === 0) {
      out.innerHTML = '<div class="empty">(no headers exposed)</div>';
      return;
    }
    pairs.sort(function (a, b) { return a[0].localeCompare(b[0]); });
    var html = '';
    pairs.forEach(function (p) {
      html += '<div class="row"><div class="name">' + esc(p[0]) + '</div>'
            + '<div class="value">' + esc(p[1]) + '</div></div>';
    });
    out.innerHTML = html;
  }
  form.addEventListener('submit', function (e) {
    e.preventDefault();
    var reqEl = document.getElementById('req-h');
    var resEl = document.getElementById('res-h');
    var reqText = reqEl ? reqEl.value : '';
    var resText = resEl ? resEl.value : '';
    var headers = {};
    reqText.split('\n').forEach(function (line) {
      var t = line.trim();
      if (!t) return;
      var c = t.indexOf(':');
      if (c < 0) return;
      var n = t.slice(0, c).trim();
      var v = t.slice(c + 1).trim();
      if (!n) return;
      try { headers[n] = v; } catch (_) {}
    });
    var params = new URLSearchParams();
    if (resText) params.set('h', resText);
    if (reqText) params.set('r', reqText);
    var qs = params.toString();
    var url = form.getAttribute('action') + (qs ? '?' + qs : '');
    fetch(url, { headers: headers, redirect: 'follow' })
      .then(function (r) {
        var pairs = [];
        r.headers.forEach(function (v, k) { pairs.push([k, v]); });
        return r.text().then(function (text) { return { pairs: pairs, text: text }; });
      })
      .then(function (result) {
        history.pushState({}, '', url);
        document.open();
        document.write(result.text);
        document.close();
        renderLivePairs(result.pairs);
      })
      .catch(function () { window.location.href = url; });
  });
})();
""";

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
  .render-time {
    margin-top: 12px;
    background: var(--card);
    border: 1px dashed var(--border);
    border-radius: 4px;
    padding: 6px 10px;
    font-size: 12px;
    color: var(--muted);
    display: flex;
    align-items: baseline;
    gap: 8px;
    flex-wrap: wrap;
  }
  .render-time .label {
    text-transform: uppercase;
    letter-spacing: 0.06em;
    font-size: 10px;
    color: var(--muted);
  }
  .render-time strong { color: var(--accent); font-weight: 600; }
  .render-time .hint { color: var(--muted); font-size: 11px; font-style: italic; }
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
  .hform { display: flex; flex-direction: column; gap: 12px; }
  .field { display: flex; flex-direction: column; gap: 4px; }
  .field label {
    font-size: 11px;
    color: var(--muted);
    text-transform: lowercase;
    letter-spacing: 0.04em;
  }
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
    min-height: 80px;
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
  .snippet { margin-bottom: 12px; }
  .snippet-label {
    font-size: 11px;
    color: var(--muted);
    text-transform: lowercase;
    letter-spacing: 0.04em;
    display: block;
    margin-bottom: 4px;
  }
  .snippet pre {
    margin: 0;
    padding: 10px 12px;
    background: var(--card);
    border: 1px solid var(--border);
    border-radius: 4px;
    overflow-x: auto;
    font-family: inherit;
    font-size: 12px;
    color: var(--fg);
    white-space: pre;
  }
  .sub { margin: 8px 0 4px; }
  .sub-label {
    color: var(--muted);
    font-size: 11px;
    text-transform: lowercase;
    letter-spacing: 0.04em;
  }
  .warn {
    border: 1px solid var(--warn);
    border-radius: 4px;
    padding: 8px 12px;
    background: var(--card);
    margin-bottom: 8px;
  }
  .warn-line {
    color: var(--warn);
    font-size: 13px;
    padding: 2px 0;
    word-break: break-all;
  }
  .dropped {
    border: 1px solid var(--warn);
    border-radius: 4px;
    padding: 8px 12px;
    background: var(--card);
    margin-bottom: 12px;
  }
  .dropped-title {
    color: var(--warn);
    font-size: 12px;
    font-weight: 600;
    text-transform: lowercase;
    letter-spacing: 0.04em;
    margin-bottom: 6px;
  }
  .dropped-title::before { content: "! "; }
  .dropped-body { margin-bottom: 6px; }
  .dropped-line {
    color: var(--warn);
    font-size: 13px;
    padding: 1px 0;
    word-break: break-all;
  }
  .dropped-hint {
    color: var(--muted);
    font-size: 11px;
    line-height: 1.5;
  }
  .dropped-hint code {
    background: var(--bg);
    border: 1px solid var(--border);
    border-radius: 3px;
    padding: 0 4px;
    font-size: 11px;
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
