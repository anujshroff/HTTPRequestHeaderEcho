using System.Text;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var prefixes = (builder.Configuration["HEADER_PREFIX_FILTER"] ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

app.MapGet("/", (HttpContext ctx) =>
{
    var sb = new StringBuilder();
    foreach (var header in ctx.Request.Headers)
    {
        if (prefixes.Length > 0 &&
            !prefixes.Any(p => header.Key.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            continue;
        }
        sb.AppendLine($"{header.Key}: {header.Value}");
    }
    return Results.Text(sb.ToString());
});

app.Run();
