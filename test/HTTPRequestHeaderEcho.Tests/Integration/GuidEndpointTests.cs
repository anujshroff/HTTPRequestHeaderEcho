using System.Net;

namespace HTTPRequestHeaderEcho.Tests.Integration;

public class GuidEndpointTests(WebAppFactory factory) : IClassFixture<WebAppFactory>
{
    private static string GuidPath() => "/" + Guid.NewGuid();

    private HttpClient ConsentedClient()
    {
        var c = factory.CreateClient();
        c.DefaultRequestHeaders.Add("Cookie", $"{Consent.CookieName}=1");
        return c;
    }

    [Fact]
    public async Task Without_consent_cookie_returns_interstitial()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();
        using var resp = await client.GetAsync(GuidPath(), ct);

        var body = await resp.Content.ReadAsStringAsync(ct);
        Assert.Contains("action=\"/consent\"", body);
    }

    [Fact]
    public async Task Non_guid_segment_returns_404()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = ConsentedClient();
        using var resp = await client.GetAsync("/not-a-guid", ct);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Applies_valid_response_headers_from_h_query_param()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = ConsentedClient();
        var url = GuidPath() + "?h=" + Uri.EscapeDataString("X-Echo-A: one\nX-Echo-B: two");

        using var resp = await client.GetAsync(url, ct);

        Assert.True(resp.Headers.TryGetValues("X-Echo-A", out var a));
        Assert.Equal("one", a!.Single());
        Assert.True(resp.Headers.TryGetValues("X-Echo-B", out var b));
        Assert.Equal("two", b!.Single());
    }

    [Fact]
    public async Task Ignores_invalid_response_header_lines()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = ConsentedClient();
        var url = GuidPath() + "?h=" + Uri.EscapeDataString("not-a-header\nX-Good: yes");

        using var resp = await client.GetAsync(url, ct);

        Assert.True(resp.Headers.TryGetValues("X-Good", out var good));
        Assert.Equal("yes", good!.Single());
    }

    [Fact]
    public async Task Detects_dropped_request_header_when_browser_would_strip_it()
    {
        // We ask the server to "expect" a request header that we don't actually send,
        // simulating the browser dropping it. The page should mark it as dropped.
        var ct = TestContext.Current.CancellationToken;
        using var client = ConsentedClient();
        var url = GuidPath() + "?r=" + Uri.EscapeDataString("X-Pretend-Sent: value");

        var body = await client.GetStringAsync(url, ct);

        // The dropped-header callout in HtmlPage.cs contains the header name in its summary.
        Assert.Contains("X-Pretend-Sent", body);
    }
}
