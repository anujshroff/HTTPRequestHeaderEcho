using System.Net;

namespace HTTPRequestHeaderEcho.Tests.Integration;

public class ConsentEndpointTests(WebAppFactory factory) : IClassFixture<WebAppFactory>
{
    private HttpClient NoRedirectClient()
    {
        // Default WebApplicationFactory client follows redirects; for /consent we want to see the 302 itself.
        return factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
    }

    private static FormUrlEncodedContent Form(params (string k, string v)[] fields) =>
        new(fields.Select(f => new KeyValuePair<string, string>(f.k, f.v)));

    [Fact]
    public async Task Origin_match_yields_302_with_consent_cookie()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = NoRedirectClient();
        var req = new HttpRequestMessage(HttpMethod.Post, "/consent")
        {
            Content = Form(("next", "/")),
        };
        req.Headers.Add("Origin", "http://localhost");

        using var resp = await client.SendAsync(req, ct);

        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        Assert.True(resp.Headers.TryGetValues("Set-Cookie", out var cookies));
        Assert.Contains(cookies!, c => c.StartsWith($"{Consent.CookieName}=1"));
    }

    [Fact]
    public async Task Referer_match_yields_302()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = NoRedirectClient();
        var req = new HttpRequestMessage(HttpMethod.Post, "/consent")
        {
            Content = Form(("next", "/")),
        };
        req.Headers.Add("Referer", "http://localhost/");

        using var resp = await client.SendAsync(req, ct);

        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
    }

    [Fact]
    public async Task Missing_origin_and_referer_yields_403()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = NoRedirectClient();
        var req = new HttpRequestMessage(HttpMethod.Post, "/consent")
        {
            Content = Form(("next", "/")),
        };

        using var resp = await client.SendAsync(req, ct);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Cross_origin_yields_403()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = NoRedirectClient();
        var req = new HttpRequestMessage(HttpMethod.Post, "/consent")
        {
            Content = Form(("next", "/")),
        };
        req.Headers.Add("Origin", "http://evil.example");

        using var resp = await client.SendAsync(req, ct);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Theory]
    [InlineData("//evil.example/path", "/")]   // protocol-relative -> sanitized to /
    [InlineData("/\\evil", "/")]               // backslash form -> sanitized to /
    [InlineData("http://evil/x", "/")]         // absolute URL doesn't start with / -> sanitized to /
    [InlineData("/safe/path", "/safe/path")]   // legitimate same-origin path is preserved
    public async Task Sanitizes_next_param_against_open_redirects(string next, string expectedLocation)
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = NoRedirectClient();
        var req = new HttpRequestMessage(HttpMethod.Post, "/consent")
        {
            Content = Form(("next", next)),
        };
        req.Headers.Add("Origin", "http://localhost");

        using var resp = await client.SendAsync(req, ct);

        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        Assert.Equal(expectedLocation, resp.Headers.Location?.ToString());
    }
}
