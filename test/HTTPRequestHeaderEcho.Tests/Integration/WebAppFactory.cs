using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace HTTPRequestHeaderEcho.Tests.Integration;

public sealed class WebAppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
    }

    public WebApplicationFactory<Program> WithConfig(params (string key, string value)[] settings) =>
        WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) =>
            c.AddInMemoryCollection(settings.Select(s =>
                new KeyValuePair<string, string?>(s.key, s.value)))));
}
