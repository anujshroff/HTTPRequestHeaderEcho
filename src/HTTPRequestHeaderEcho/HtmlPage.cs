using Microsoft.Extensions.Primitives;
using System.Text;
using System.Text.Encodings.Web;

namespace HTTPRequestHeaderEcho;

public sealed record HtmlPageModel(
    HttpContext Ctx,
    string[] Prefixes,
    string[] HideList,
    string FormTargetGuid,
    string CurrentRequestSpec,
    string CurrentResponseSpec,
    IReadOnlyList<KeyValuePair<string, string>> ValidRequestHeaders,
    IReadOnlyList<KeyValuePair<string, string>> DroppedRequestHeaders,
    IReadOnlyList<string> IgnoredRequestLines,
    IReadOnlyList<string> IgnoredResponseLines,
    string Version);

public static class HtmlPage
{
    public static string Render(HtmlPageModel m)
    {
        var encoder = HtmlEncoder.Default;
        var ctx = m.Ctx;
        var headers = ctx.Request.Headers.WithPrefixFilter(m.Prefixes).WithHideList(m.HideList).WithConsentScrub().ToList();

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
        var methodClass = ctx.Request.Method.ToUpperInvariant() switch
        {
            "GET" => "method-get",
            "POST" => "method-post",
            "PUT" => "method-put",
            "DELETE" => "method-delete",
            _ => "method-other",
        };
        sb.Append($"<span class=\"chip {methodClass}\">method<strong>{encoder.Encode(ctx.Request.Method)}</strong></span>");
        sb.Append($"<span class=\"chip\">path<strong>{encoder.Encode(ctx.Request.Path.ToString())}</strong></span>");
        sb.Append($"<span class=\"chip\">protocol<strong>{encoder.Encode(ctx.Request.Protocol)}</strong></span>");
        sb.Append("</div>\n");

        // Render-time strip
        var renderedAt = DateTime.UtcNow.ToString("o");
        sb.Append("<div class=\"render-time\">\n");
        sb.Append("<span class=\"label\">Rendered (UTC)</span>");
        sb.Append($"<strong>{encoder.Encode(renderedAt)}</strong>");
        sb.Append("<span class=\"hint\">page content &mdash; not an HTTP header. If this matches across refreshes, the page came from cache.</span>\n");
        sb.Append("</div>\n");
        sb.Append("</header>\n");

        // Test playground form (moved up: it's the action input)
        sb.Append("<section class=\"band\">\n<div class=\"band-label\">Test playground</div>\n");
        sb.Append($"<form id=\"hform\" action=\"/{encoder.Encode(m.FormTargetGuid)}\" method=\"get\" class=\"hform\">\n");
        sb.Append("<div class=\"form-cols\">\n");
        sb.Append("<div class=\"field\">\n");
        sb.Append("<label for=\"req-h\">Request headers <span class=\"label-hint\">sent by your client</span></label>\n");
        sb.Append($"<textarea id=\"req-h\" name=\"r\" rows=\"4\" placeholder=\"X-Custom: hello&#10;Authorization: Bearer abc\" spellcheck=\"false\">{encoder.Encode(m.CurrentRequestSpec)}</textarea>\n");
        sb.Append("</div>\n");
        sb.Append("<div class=\"field\">\n");
        sb.Append("<label for=\"res-h\">Response headers <span class=\"label-hint\">returned by the server</span></label>\n");
        sb.Append($"<textarea id=\"res-h\" name=\"h\" rows=\"4\" placeholder=\"Cache-Control: max-age=60&#10;X-Trace: xyz\" spellcheck=\"false\">{encoder.Encode(m.CurrentResponseSpec)}</textarea>\n");
        sb.Append("</div>\n");
        sb.Append("</div>\n");
        sb.Append("<button type=\"submit\">Send &rarr;</button>\n");
        sb.Append("</form>\n");
        sb.Append("<p class=\"note\">With JS: request headers are sent via <code>fetch()</code>. Without JS: only response headers are applied (browsers can't add arbitrary request headers via plain form submit). <strong>Browsers also forbid JS from setting headers like <code>User-Agent</code>, <code>Cookie</code>, <code>Host</code>, <code>Referer</code>, <code>Origin</code>, <code>Sec-*</code></strong> &mdash; those will silently fail in-browser. The replay snippets below send them verbatim from a terminal. Refreshing the result page re-navigates with browser-default request headers; the response cache test still works because <code>?h=</code> rides in the URL.</p>\n");
        sb.Append("</section>\n");

        // Request | Response side-by-side
        sb.Append("<div class=\"grid-2\">\n");

        // Request headers
        sb.Append("<section class=\"band\">\n<div class=\"band-label\">Request headers</div>\n");
        if (m.DroppedRequestHeaders.Count > 0)
        {
            sb.Append("<div class=\"dropped\">\n");
            sb.Append($"<div class=\"dropped-title\">Browser dropped {m.DroppedRequestHeaders.Count} request header(s)</div>\n");
            sb.Append("<div class=\"dropped-body\">\n");
            foreach (var (name, value) in m.DroppedRequestHeaders)
            {
                sb.Append($"<div class=\"dropped-line\">{encoder.Encode(name)}: {encoder.Encode(value)}</div>\n");
            }
            sb.Append("</div>\n");
            sb.Append("<div class=\"dropped-hint\">Browsers forbid JS from setting some headers (<code>User-Agent</code>, <code>Cookie</code>, <code>Host</code>, <code>Origin</code>, <code>Referer</code>, <code>Connection</code>, <code>Sec-*</code>, etc.) and a few others may have been merged or stripped. The replay snippets below send these unmodified.</div>\n");
            sb.Append("</div>\n");
        }
        if (headers.Count == 0 && m.DroppedRequestHeaders.Count == 0)
        {
            sb.Append("<div class=\"empty\">No headers matched the active prefix filter.</div>\n");
        }
        else
        {
            if (standard.Count > 0)
            {
                sb.Append("<div class=\"group\">\n<h2>Standard</h2>\n");
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

        // Response headers (snapshot + actual)
        sb.Append("<section class=\"band\">\n<div class=\"band-label\">Response headers</div>\n");
        sb.Append("<p class=\"note\">Server-side snapshot at render time. Kestrel auto-headers (<code>Date</code>, <code>Server</code>, <code>Content-Length</code>, possibly <code>Transfer-Encoding</code>) are added later in the pipeline &mdash; use the live panel below to see what the browser actually received.</p>\n");
        sb.Append("<div class=\"group\">\n<h2>Snapshot (server-side)</h2>\n");
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

        // Live response panel (populated by the form-submit fetch above)
        sb.Append("<div class=\"group\" id=\"live-resp-group\">\n<h2>Actual (received by browser)</h2>\n");
        sb.Append("<p class=\"note\">Populated by the <strong>Send</strong> button above. Lists the response headers the browser received from that <code>fetch()</code>, including Kestrel auto-headers (<code>Date</code>, <code>Server</code>, <code>Content-Length</code>, <code>Transfer-Encoding</code>). <code>Set-Cookie</code> is hidden from JS (forbidden response header).</p>\n");
        sb.Append("<div id=\"live-resp-out\"><div class=\"empty\">submit the form above to populate</div></div>\n");
        sb.Append("</div>\n</section>\n");

        sb.Append("</div>\n"); // end .grid-2

        // Snippets (only when the user has submitted something)
        var hasInput = !string.IsNullOrEmpty(m.CurrentRequestSpec) || !string.IsNullOrEmpty(m.CurrentResponseSpec);
        if (hasInput)
        {
            var absUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host}{ctx.Request.Path}{ctx.Request.QueryString}";
            sb.Append("<section class=\"band\">\n<div class=\"band-label\">Replay snippets</div>\n");
            sb.Append("<p class=\"note\">Refresh-safe replay from a terminal. The browser refresh button doesn't send custom request headers, but these do.</p>\n");

            sb.Append("<div class=\"snippet\">\n");
            sb.Append("<span class=\"snippet-label\">curl</span>\n");
            sb.Append($"<pre>{encoder.Encode(Snippets.Curl(absUrl, m.ValidRequestHeaders))}</pre>\n");
            sb.Append("<button type=\"button\" class=\"copy-btn\" data-copy-target=\"pre\">Copy</button>\n");
            sb.Append("</div>\n");

            sb.Append("<div class=\"snippet\">\n");
            sb.Append("<span class=\"snippet-label\">PowerShell (Invoke-RestMethod)</span>\n");
            sb.Append($"<pre>{encoder.Encode(Snippets.PowerShell(absUrl, m.ValidRequestHeaders))}</pre>\n");
            sb.Append("<button type=\"button\" class=\"copy-btn\" data-copy-target=\"pre\">Copy</button>\n");
            sb.Append("</div>\n");

            sb.Append("</section>\n");
        }

        // Ignored input
        var hasIgnored = m.IgnoredRequestLines.Count > 0 || m.IgnoredResponseLines.Count > 0;
        if (hasIgnored)
        {
            sb.Append("<section class=\"band\">\n<div class=\"band-label\">Ignored input</div>\n");
            sb.Append("<p class=\"note\">Lines skipped: missing <code>:</code>, invalid header name, or value contained CR/LF.</p>\n");
            if (m.IgnoredRequestLines.Count > 0)
            {
                sb.Append("<div class=\"sub\"><span class=\"sub-label\">Request:</span></div>\n");
                sb.Append("<div class=\"warn\">\n");
                foreach (var line in m.IgnoredRequestLines)
                    sb.Append($"<div class=\"warn-line\">{encoder.Encode(line)}</div>\n");
                sb.Append("</div>\n");
            }
            if (m.IgnoredResponseLines.Count > 0)
            {
                sb.Append("<div class=\"sub\"><span class=\"sub-label\">Response:</span></div>\n");
                sb.Append("<div class=\"warn\">\n");
                foreach (var line in m.IgnoredResponseLines)
                    sb.Append($"<div class=\"warn-line\">{encoder.Encode(line)}</div>\n");
                sb.Append("</div>\n");
            }
            sb.Append("</section>\n");
        }

        // Footer
        sb.Append("<footer>\n");
        sb.Append("<a href=\"/plain\">View as plain text &rarr;</a>\n");
        sb.Append("<a href=\"/\">Start fresh test &rarr;</a>\n");
        if (m.Prefixes.Length > 0)
        {
            var filterText = string.Join(", ", m.Prefixes.Select(encoder.Encode));
            sb.Append($"<span>Active prefix filter: <strong>{filterText}</strong></span>\n");
        }
        sb.Append($"<span class=\"version\">Version: <strong>{encoder.Encode(m.Version)}</strong></span>\n");
        sb.Append("</footer>\n");
        sb.Append("</div>\n");

        // Inline JS
        sb.Append("<script>");
        sb.Append(Js);
        sb.Append("</script>\n");

        sb.Append("</body>\n</html>\n");

        return sb.ToString();
    }

    public static string RenderInterstitial(HttpContext ctx, string version)
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
<div class="band-label">Confirm visit</div>
<p class="note">This service can set arbitrary HTTP response headers on your browser via crafted URLs &mdash; including <code>Set-Cookie</code>, <code>Refresh</code> redirects, long-lived <code>Strict-Transport-Security</code> pins, and <code>Clear-Site-Data</code>. Continue only if you intentionally navigated here.</p>
<p class="note">After accepting, this prompt won't return for 6 hours.</p>
<form method="post" action="/consent" class="hform">
<input type="hidden" name="next" value="{encoder.Encode(nextUrl)}">
<button type="submit">Accept and continue &rarr;</button>
</form>
</section>
<footer>
<a href="/plain">Cancel &mdash; view /plain instead &rarr;</a>
<span class="version">Version: <strong>{encoder.Encode(version)}</strong></span>
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
            var copyText = $"{h.Key}: {h.Value}";
            sb.Append("<div class=\"row\">");
            sb.Append($"<div class=\"name\">{encoder.Encode(h.Key)}</div>");
            sb.Append($"<div class=\"value\">{encoder.Encode(h.Value.ToString())}</div>");
            sb.Append($"<button type=\"button\" class=\"copy-btn\" data-copy=\"{encoder.Encode(copyText)}\">Copy</button>");
            sb.Append("</div>\n");
        }
    }

    private const string Js = """
(function () {
  function esc(s) {
    return String(s).replace(/[&<>"']/g, function (c) {
      return ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'})[c];
    });
  }

  // Delegated copy-to-clipboard
  document.addEventListener('click', function (e) {
    var btn = e.target && e.target.closest && e.target.closest('.copy-btn');
    if (!btn) return;
    var text = btn.getAttribute('data-copy');
    if (!text && btn.getAttribute('data-copy-target') === 'pre') {
      var parent = btn.closest('.snippet');
      var pre = parent && parent.querySelector('pre');
      if (pre) text = pre.textContent;
    }
    if (text == null) return;
    var done = function () {
      var original = btn.textContent;
      btn.textContent = 'Copied';
      btn.classList.add('copied');
      setTimeout(function () {
        btn.textContent = original;
        btn.classList.remove('copied');
      }, 1200);
    };
    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(text).then(done).catch(function () {});
    }
  });

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
      var copy = esc(p[0] + ': ' + p[1]);
      html += '<div class="row">'
            + '<div class="name">' + esc(p[0]) + '</div>'
            + '<div class="value">' + esc(p[1]) + '</div>'
            + '<button type="button" class="copy-btn" data-copy="' + copy + '">Copy</button>'
            + '</div>';
    });
    out.innerHTML = html;
  }

  var form = document.getElementById('hform');
  if (!form) return;
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
    --bg: #0a0a0a;
    --card: #141414;
    --card-2: #1a1a1a;
    --border: #232323;
    --border-strong: #2e2e2e;
    --fg: #fafafa;
    --muted: #8a8a8a;
    --accent: #818cf8;
    --warn: #f0883e;
    --get: #3b82f6;
    --post: #a855f7;
    --put: #f59e0b;
    --delete: #ef4444;
    --sans: 'Inter', system-ui, -apple-system, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
    --mono: 'JetBrains Mono', ui-monospace, 'Cascadia Code', 'SF Mono', Menlo, Consolas, monospace;
  }
  @media (prefers-color-scheme: light) {
    :root {
      --bg: #ffffff;
      --card: #f7f7f8;
      --card-2: #efeff1;
      --border: #e4e4e7;
      --border-strong: #d4d4d8;
      --fg: #18181b;
      --muted: #71717a;
      --accent: #4f46e5;
      --warn: #9a6700;
    }
  }
  * { box-sizing: border-box; }
  html, body { margin: 0; padding: 0; }
  body {
    background: var(--bg);
    color: var(--fg);
    font-family: var(--sans);
    font-size: 14px;
    line-height: 1.55;
    padding: 24px 24px 48px;
    -webkit-font-smoothing: antialiased;
    -moz-osx-font-smoothing: grayscale;
  }
  .container { max-width: 1280px; margin: 0 auto; }

  /* Top bar */
  header.top {
    border-bottom: 1px solid var(--border);
    padding-bottom: 20px;
    margin-bottom: 24px;
  }
  h1 {
    font-size: 20px;
    margin: 0 0 14px;
    color: var(--fg);
    font-weight: 600;
    letter-spacing: -0.01em;
  }
  .meta { display: flex; flex-wrap: wrap; gap: 6px; align-items: center; }
  .chip {
    background: var(--card);
    border: 1px solid var(--border);
    border-radius: 6px;
    padding: 3px 10px;
    font-size: 12px;
    color: var(--muted);
    display: inline-flex;
    align-items: center;
    gap: 6px;
  }
  .chip strong { color: var(--fg); font-weight: 500; font-family: var(--mono); }
  .chip.method-get strong { color: var(--get); }
  .chip.method-post strong { color: var(--post); }
  .chip.method-put strong { color: var(--put); }
  .chip.method-delete strong { color: var(--delete); }
  .chip.method-get { border-color: color-mix(in srgb, var(--get) 40%, var(--border)); }
  .chip.method-post { border-color: color-mix(in srgb, var(--post) 40%, var(--border)); }
  .chip.method-put { border-color: color-mix(in srgb, var(--put) 40%, var(--border)); }
  .chip.method-delete { border-color: color-mix(in srgb, var(--delete) 40%, var(--border)); }

  .render-time {
    margin-top: 14px;
    background: var(--card);
    border: 1px solid var(--border);
    border-radius: 6px;
    padding: 8px 12px;
    font-size: 12px;
    color: var(--muted);
    display: flex;
    align-items: baseline;
    gap: 10px;
    flex-wrap: wrap;
  }
  .render-time .label {
    font-size: 11px;
    color: var(--muted);
    font-weight: 500;
  }
  .render-time strong { color: var(--fg); font-weight: 500; font-family: var(--mono); }
  .render-time .hint { color: var(--muted); font-size: 12px; }

  /* Sections */
  section.band { margin-bottom: 28px; }
  .band-label {
    color: var(--fg);
    font-size: 13px;
    font-weight: 600;
    margin-bottom: 12px;
    padding-bottom: 6px;
    border-bottom: 1px solid var(--border);
    letter-spacing: -0.005em;
  }

  /* Two-column grid for request | response */
  .grid-2 {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 24px;
    margin-bottom: 28px;
  }
  .grid-2 > section.band { margin-bottom: 0; }
  @media (max-width: 900px) {
    .grid-2 { grid-template-columns: 1fr; gap: 28px; }
  }

  .group { margin-bottom: 14px; }
  h2 {
    font-size: 12px;
    margin: 0 0 8px;
    color: var(--muted);
    font-weight: 500;
  }

  /* Header rows */
  .row {
    position: relative;
    background: var(--card);
    border: 1px solid var(--border);
    border-radius: 6px;
    padding: 8px 12px;
    padding-right: 56px;
    margin-bottom: 4px;
    transition: border-color 120ms;
  }
  .row:hover { border-color: var(--border-strong); }
  .row .name {
    color: var(--accent);
    font-size: 12px;
    font-weight: 500;
    font-family: var(--mono);
    word-break: break-all;
  }
  .row .value {
    color: var(--fg);
    word-break: break-all;
    white-space: pre-wrap;
    margin-top: 3px;
    font-family: var(--mono);
    font-size: 12.5px;
    line-height: 1.5;
  }

  .empty {
    color: var(--muted);
    font-style: italic;
    padding: 8px 0;
    font-size: 13px;
  }
  .note {
    color: var(--muted);
    font-size: 12px;
    margin: 4px 0 14px;
    line-height: 1.55;
  }
  .note code, code {
    background: var(--card);
    border: 1px solid var(--border);
    border-radius: 4px;
    padding: 1px 5px;
    font-size: 11.5px;
    font-family: var(--mono);
    color: var(--fg);
  }

  /* Form */
  .hform { display: flex; flex-direction: column; gap: 14px; }
  .form-cols {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 14px;
  }
  @media (max-width: 900px) {
    .form-cols { grid-template-columns: 1fr; }
  }
  .field { display: flex; flex-direction: column; gap: 6px; }
  .field label {
    font-size: 12px;
    color: var(--fg);
    font-weight: 500;
  }
  .field .label-hint {
    color: var(--muted);
    font-weight: 400;
    margin-left: 6px;
  }
  textarea {
    width: 100%;
    background: var(--card);
    color: var(--fg);
    border: 1px solid var(--border);
    border-radius: 6px;
    padding: 10px 12px;
    font-family: var(--mono);
    font-size: 12.5px;
    line-height: 1.5;
    resize: vertical;
    min-height: 96px;
    transition: border-color 120ms;
  }
  textarea:focus {
    outline: none;
    border-color: var(--accent);
  }
  button {
    align-self: flex-start;
    background: var(--accent);
    color: #ffffff;
    border: 1px solid var(--accent);
    border-radius: 6px;
    padding: 7px 18px;
    font-family: var(--sans);
    font-size: 13px;
    font-weight: 600;
    cursor: pointer;
    transition: opacity 120ms;
  }
  button:hover { opacity: 0.88; }

  /* Copy buttons */
  .copy-btn {
    position: absolute;
    top: 6px;
    right: 6px;
    background: var(--card-2);
    color: var(--muted);
    border: 1px solid var(--border);
    border-radius: 4px;
    padding: 2px 8px;
    font-family: var(--sans);
    font-size: 11px;
    font-weight: 500;
    cursor: pointer;
    opacity: 0;
    transition: opacity 120ms, color 120ms, border-color 120ms;
    align-self: auto;
  }
  .row:hover .copy-btn,
  .copy-btn:focus { opacity: 1; }
  .copy-btn:hover { color: var(--fg); border-color: var(--border-strong); }
  .copy-btn.copied { color: var(--accent); border-color: var(--accent); opacity: 1; }
  .snippet .copy-btn { opacity: 1; top: 8px; right: 8px; }

  /* Snippets */
  .snippet { position: relative; margin-bottom: 14px; }
  .snippet-label {
    font-size: 12px;
    color: var(--muted);
    display: block;
    margin-bottom: 6px;
    font-weight: 500;
  }
  .snippet pre {
    margin: 0;
    padding: 12px 14px;
    padding-right: 64px;
    background: var(--card);
    border: 1px solid var(--border);
    border-radius: 6px;
    overflow-x: auto;
    font-family: var(--mono);
    font-size: 12.5px;
    color: var(--fg);
    white-space: pre;
    line-height: 1.5;
  }

  /* Warnings */
  .sub { margin: 10px 0 4px; }
  .sub-label {
    color: var(--muted);
    font-size: 12px;
    font-weight: 500;
  }
  .warn {
    border: 1px solid color-mix(in srgb, var(--warn) 50%, var(--border));
    border-radius: 6px;
    padding: 10px 12px;
    background: color-mix(in srgb, var(--warn) 6%, var(--card));
    margin-bottom: 8px;
  }
  .warn-line {
    color: var(--warn);
    font-size: 12.5px;
    font-family: var(--mono);
    padding: 2px 0;
    word-break: break-all;
  }
  .dropped {
    border: 1px solid color-mix(in srgb, var(--warn) 50%, var(--border));
    border-radius: 6px;
    padding: 12px 14px;
    background: color-mix(in srgb, var(--warn) 6%, var(--card));
    margin-bottom: 14px;
  }
  .dropped-title {
    color: var(--warn);
    font-size: 13px;
    font-weight: 600;
    margin-bottom: 8px;
  }
  .dropped-body { margin-bottom: 8px; }
  .dropped-line {
    color: var(--warn);
    font-size: 12.5px;
    font-family: var(--mono);
    padding: 1px 0;
    word-break: break-all;
  }
  .dropped-hint {
    color: var(--muted);
    font-size: 12px;
    line-height: 1.55;
  }
  .dropped-hint code {
    background: var(--bg);
  }

  footer {
    margin-top: 36px;
    padding-top: 16px;
    border-top: 1px solid var(--border);
    color: var(--muted);
    font-size: 12px;
    display: flex;
    flex-wrap: wrap;
    gap: 18px;
    align-items: center;
  }
  footer a { color: var(--accent); text-decoration: none; }
  footer a:hover { text-decoration: underline; }
  footer strong { color: var(--fg); font-weight: 500; font-family: var(--mono); }
  footer .version { margin-left: auto; }
""";
}
