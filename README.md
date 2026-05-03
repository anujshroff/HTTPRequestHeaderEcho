# HTTPRequestHeaderEcho

Tiny ASP.NET Core service that echoes incoming HTTP request headers back to the caller, plus an interactive HTML playground for crafting custom request/response header tests in a browser. Useful for debugging proxies, load balancers, ingress rules, client header behavior, and HTTP caching.

## Endpoints

- `GET /` — interactive HTML page (terminal-themed, dark/light auto). Shows incoming request headers (grouped, prefix-filtered), the response headers the server sent, the live response headers the browser actually received (read via `fetch`), and a playground form for submitting custom request and response headers. First visit shows a consent interstitial warning that the page can set arbitrary response headers (Set-Cookie, HSTS, Refresh, Clear-Site-Data); accepting sets a 6-hour cookie.
- `GET /plain` — every request header as `Key: value` lines, `text/plain`. No consent gate, no UI. Best for `curl` and scripts.
- `GET /{guid}` — same HTML page as `/`, with two query params:
  - `?h=` — newline-separated `Name: Value` lines, applied as **response** headers on this request.
  - `?r=` — newline-separated `Name: Value` lines the playground asked the browser to send as **request** headers; any the browser dropped or rewrote (`User-Agent`, `Cookie`, `Host`, `Origin`, `Referer`, `Connection`, most `Sec-*`, etc.) are flagged on the page.
  - The GUID makes the URL stable across refreshes so you can exercise HTTP caching (`If-None-Match`, `If-Modified-Since`, etc.) against a real server response.

```
$ curl http://localhost:5291/plain
Host: localhost:5291
User-Agent: curl/8.4.0
Accept: */*
```

```
$ curl -i 'http://localhost:5291/d8e8fca2-dc0f-4a4f-b3a9-3e3b7c4a9c11?r=X-Trace%3A%20abc123&h=Cache-Control%3A%20max-age%3D60'
HTTP/1.1 200 OK
Cache-Control: max-age=60
Content-Type: text/html; charset=utf-8
...
```

## Playground

- Two textareas: one for request headers (sent via `fetch`), one for response headers (applied server-side via `?h=`).
- A consent gate intercepts the first visit to `/` and `/{guid}` and warns about response-header injection risks before letting you in.
- Headers the browser refused to send (or merged) are called out as "dropped" in a banner.
- Each submission renders **replay snippets** — curl and PowerShell `Invoke-RestMethod` — so you can re-run the same request from a terminal where browsers' forbidden-header rules don't apply.
- A "headers actually received by the browser" panel shows what `Response.headers` exposed to JS. Forbidden headers like `Set-Cookie` are absent there by spec — the server sent them, but the browser hides them from scripts.

## Requirements

- .NET 10 SDK

## Run locally

```powershell
dotnet run --project src/HTTPRequestHeaderEcho
```

Listens on `http://localhost:5291` by default (see [src/HTTPRequestHeaderEcho/Properties/launchSettings.json](src/HTTPRequestHeaderEcho/Properties/launchSettings.json)).

## Filtering

Set `HEADER_PREFIX_FILTER` to a comma-separated list of prefixes to return only headers whose names start with one of them (case-insensitive). When unset or empty, all headers are returned. The filter applies to `/`, `/plain`, and `/{guid}`.

```powershell
$env:HEADER_PREFIX_FILTER = "x-,sec-"
dotnet run --project src/HTTPRequestHeaderEcho
```

```
$ curl http://localhost:5291/plain
X-Forwarded-For: 203.0.113.7
Sec-Fetch-Site: cross-site
```

## Project layout

```
.
├── HTTPRequestHeaderEcho.sln
└── src/
    └── HTTPRequestHeaderEcho/
        ├── HTTPRequestHeaderEcho.csproj
        ├── Program.cs           # endpoints + consent gate
        ├── HtmlPage.cs          # server-rendered HTML/CSS/JS
        ├── HeaderSpec.cs        # parsing/validation of header lines
        ├── Snippets.cs          # curl + PowerShell replay snippets
        ├── HeaderFilter.cs      # prefix filter + consent-cookie scrub
        ├── Consent.cs
        ├── appsettings.json
        ├── appsettings.Development.json
        └── Properties/
            └── launchSettings.json
```

## License

[MIT](LICENSE).
