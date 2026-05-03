using System.Text;
using HTTPRequestHeaderEcho;

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
    Results.Content(HtmlPage.Render(ctx, prefixes), "text/html; charset=utf-8"));

app.Run();
