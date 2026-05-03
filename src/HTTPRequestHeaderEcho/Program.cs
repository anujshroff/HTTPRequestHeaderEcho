using HTTPRequestHeaderEcho;
using Microsoft.AspNetCore.HttpOverrides;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});
var app = builder.Build();
app.UseForwardedHeaders();

var prefixes = (builder.Configuration["HEADER_PREFIX_FILTER"] ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

var hideList = (builder.Configuration["HEADER_HIDE_LIST"] ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

static IResult? Gate(HttpContext ctx)
{
    if (ctx.Request.Cookies.ContainsKey(Consent.CookieName)) return null;
    ctx.Response.Headers.CacheControl = "no-store";
    return Results.Content(HtmlPage.RenderInterstitial(ctx), "text/html; charset=utf-8");
}

app.MapGet("/plain", (HttpContext ctx) =>
{
    ctx.Response.Headers.CacheControl = "private, no-store";
    var sb = new StringBuilder();
    foreach (var h in ctx.Request.Headers.WithPrefixFilter(prefixes).WithHideList(hideList).WithConsentScrub())
        sb.AppendLine($"{h.Key}: {h.Value}");
    return Results.Text(sb.ToString());
});

app.MapGet("/", (HttpContext ctx) =>
{
    if (Gate(ctx) is { } g) return g;
    ctx.Response.Headers.CacheControl = "private, no-store";
    ctx.Response.ContentType = "text/html; charset=utf-8";
    var model = new HtmlPageModel(
        Ctx: ctx,
        Prefixes: prefixes,
        HideList: hideList,
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
    if (Gate(ctx) is { } g) return g;
    var rawResp = ctx.Request.Query["h"].ToString();
    var rawReq = ctx.Request.Query["r"].ToString();
    var parsedResp = HeaderSpec.Parse(rawResp);
    var parsedReq = HeaderSpec.Parse(rawReq);

    HeaderSpec.Apply(ctx.Response, parsedResp.Valid);
    ctx.Response.ContentType = "text/html; charset=utf-8";

    var dropped = new List<KeyValuePair<string, string>>();
    foreach (var kvp in parsedReq.Valid)
    {
        if (!ctx.Request.Headers.TryGetValue(kvp.Key, out var actual) ||
            !HeaderValueMatches(actual.ToString(), kvp.Value))
        {
            dropped.Add(kvp);
        }
    }

    static bool HeaderValueMatches(string actual, string target)
    {
        if (actual == target) return true;
        foreach (var part in actual.Split(','))
            if (part.Trim() == target) return true;
        return false;
    }

    var model = new HtmlPageModel(
        Ctx: ctx,
        Prefixes: prefixes,
        HideList: hideList,
        FormTargetGuid: id.ToString(),
        CurrentRequestSpec: rawReq,
        CurrentResponseSpec: rawResp,
        ValidRequestHeaders: parsedReq.Valid,
        DroppedRequestHeaders: dropped,
        IgnoredRequestLines: parsedReq.Ignored,
        IgnoredResponseLines: parsedResp.Ignored);
    return Results.Content(HtmlPage.Render(model), "text/html; charset=utf-8");
});

app.MapPost("/consent", async (HttpContext ctx) =>
{
    var expectedOrigin = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
    var origin = ctx.Request.Headers.Origin.ToString();
    var sameOrigin = !string.IsNullOrEmpty(origin) && origin == expectedOrigin;
    if (!sameOrigin)
    {
        var referer = ctx.Request.Headers.Referer.ToString();
        sameOrigin = !string.IsNullOrEmpty(referer)
            && (referer == expectedOrigin
                || referer.StartsWith(expectedOrigin + "/", StringComparison.Ordinal));
    }
    if (!sameOrigin) return Results.StatusCode(StatusCodes.Status403Forbidden);

    var form = await ctx.Request.ReadFormAsync();
    var next = form["next"].ToString();
    if (string.IsNullOrEmpty(next)
        || next[0] != '/'
        || (next.Length > 1 && (next[1] == '/' || next[1] == '\\')))
    {
        next = "/";
    }
    ctx.Response.Cookies.Append(Consent.CookieName, "1", new CookieOptions
    {
        MaxAge = TimeSpan.FromHours(6),
        HttpOnly = true,
        SameSite = SameSiteMode.Strict,
        Secure = ctx.Request.IsHttps,
        Path = "/",
    });
    return Results.Redirect(next);
});

app.Run();
