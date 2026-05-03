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
    ctx.Response.ContentType = "text/html; charset=utf-8";
    var model = new HtmlPageModel(
        Ctx: ctx,
        Prefixes: prefixes,
        FormTargetGuid: Guid.NewGuid().ToString(),
        CurrentRequestSpec: "",
        CurrentResponseSpec: "",
        ValidRequestHeaders: [],
        DroppedRequestHeaders: [],
        IgnoredRequestLines: [],
        IgnoredResponseLines: []);
    return Results.Content(HtmlPage.Render(model), "text/html; charset=utf-8");
});

app.MapGet("/{id:guid}", (HttpContext ctx, Guid id) =>
{
    var rawResp = ctx.Request.Query["h"].ToString();
    var rawReq = ctx.Request.Query["r"].ToString();
    var parsedResp = ResponseHeaderSpec.Parse(rawResp);
    var parsedReq = ResponseHeaderSpec.Parse(rawReq);

    ResponseHeaderSpec.Apply(ctx.Response, parsedResp.Valid);
    ctx.Response.ContentType = "text/html; charset=utf-8";

    var dropped = new List<KeyValuePair<string, string>>();
    foreach (var kvp in parsedReq.Valid)
    {
        if (!ctx.Request.Headers.TryGetValue(kvp.Key, out var actual) ||
            actual.ToString() != kvp.Value)
        {
            dropped.Add(kvp);
        }
    }

    var model = new HtmlPageModel(
        Ctx: ctx,
        Prefixes: prefixes,
        FormTargetGuid: id.ToString(),
        CurrentRequestSpec: rawReq,
        CurrentResponseSpec: rawResp,
        ValidRequestHeaders: parsedReq.Valid,
        DroppedRequestHeaders: dropped,
        IgnoredRequestLines: parsedReq.Ignored,
        IgnoredResponseLines: parsedResp.Ignored);
    return Results.Content(HtmlPage.Render(model), "text/html; charset=utf-8");
});

app.Run();
