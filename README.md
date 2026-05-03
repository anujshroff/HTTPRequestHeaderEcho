# HTTPRequestHeaderEcho

Tiny ASP.NET Core service that echoes incoming HTTP request headers back to the caller. Useful for debugging proxies, load balancers, ingress rules, and client header behavior.

## Endpoints

- `GET /` — styled HTML page (terminal-themed, dark/light auto) with request and response header sections, plus a form for testing custom response headers. Best in a browser.
- `GET /plain` — every request header as `Key: value` lines, `text/plain`. Best for `curl` and scripts.
- `GET /{guid}` — same page as `/`, but applies any headers from the `?h=` query param to its own response. Each line of `h` is `Name: Value`. Use the form on `/` to generate the URL — it's stable across refreshes so you can exercise HTTP caching (`If-None-Match`, `If-Modified-Since`, etc.) against a real server response.

```
$ curl http://localhost:5291/plain
Host: localhost:5291
User-Agent: curl/8.4.0
Accept: */*
```

```
$ curl -i 'http://localhost:5291/d8e8fca2-dc0f-4a4f-b3a9-3e3b7c4a9c11?h=Cache-Control%3A%20max-age%3D60'
HTTP/1.1 200 OK
Cache-Control: max-age=60
Content-Type: text/html; charset=utf-8
...
```

## Requirements

- .NET 10 SDK

## Run locally

```powershell
dotnet run --project src/HTTPRequestHeaderEcho
```

Listens on `http://localhost:5291` and `https://localhost:7013` by default (see [src/HTTPRequestHeaderEcho/Properties/launchSettings.json](src/HTTPRequestHeaderEcho/Properties/launchSettings.json)).

## Filtering

Set `HEADER_PREFIX_FILTER` to a comma-separated list of prefixes to return only headers whose names start with one of them (case-insensitive). When unset or empty, all headers are returned. The filter applies to both `/` and `/plain`.

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
        ├── Program.cs
        ├── appsettings.json
        ├── appsettings.Development.json
        └── Properties/
            └── launchSettings.json
```

The whole app lives in [src/HTTPRequestHeaderEcho/Program.cs](src/HTTPRequestHeaderEcho/Program.cs).

## License

[MIT](LICENSE).
