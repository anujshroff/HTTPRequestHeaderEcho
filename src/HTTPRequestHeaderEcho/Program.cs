using System.Text;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", (HttpContext ctx) =>
{
    var sb = new StringBuilder();
    foreach (var header in ctx.Request.Headers)
    {
        sb.AppendLine($"{header.Key}: {header.Value}");
    }
    return Results.Text(sb.ToString());
});

app.Run();
