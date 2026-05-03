using HTTPRequestHeaderEcho;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var prefixes = (builder.Configuration["HEADER_PREFIX_FILTER"] ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

app.MapGet("/plain", (HttpContext ctx) =>
{
    var sb = new StringBuilder();
    foreach (var h in ctx.Request.Headers.WithPrefixFilter(prefixes))
        sb.AppendLine($"{h.Key}: {h.Value}");
    return Results.Text(sb.ToString());
});

app.MapGet("/", (HttpContext ctx) =>
{
    var formGuid = Guid.NewGuid().ToString();
    ctx.Response.ContentType = "text/html; charset=utf-8";
    return Results.Content(HtmlPage.Render(ctx, prefixes, formGuid), "text/html; charset=utf-8");
});

app.MapGet("/{id:guid}", (HttpContext ctx, Guid id) =>
{
    var raw = ctx.Request.Query["h"].ToString();
    var parsed = ResponseHeaderSpec.Parse(raw);
    ResponseHeaderSpec.Apply(ctx.Response, parsed.Valid);
    ctx.Response.ContentType = "text/html; charset=utf-8";
    return Results.Content(HtmlPage.Render(ctx, prefixes, id.ToString(), parsed.Ignored), "text/html; charset=utf-8");
});

app.Run();
