using System.Net;

namespace HTTPRequestHeaderEcho.Tests.Integration;

public class IndexEndpointTests(WebAppFactory factory) : IClassFixture<WebAppFactory>
{
    [Fact]
    public async Task Without_consent_cookie_serves_interstitial_with_no_store()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();
        using var resp = await client.GetAsync("/", ct);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("text/html", resp.Content.Headers.ContentType?.MediaType);

        var cc = resp.Headers.CacheControl;
        Assert.NotNull(cc);
        Assert.True(cc!.NoStore);

        var body = await resp.Content.ReadAsStringAsync(ct);
        // The interstitial posts to /consent — that form action is unique to the gate page.
        Assert.Contains("action=\"/consent\"", body);
    }

    [Fact]
    public async Task With_consent_cookie_serves_full_page_with_private_no_store()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", $"{Consent.CookieName}=1");

        using var resp = await client.GetAsync("/", ct);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var cc = resp.Headers.CacheControl;
        Assert.NotNull(cc);
        Assert.True(cc!.Private);
        Assert.True(cc.NoStore);

        var body = await resp.Content.ReadAsStringAsync(ct);
        // Full page (not interstitial) should not contain the consent form action;
        // it should contain the playground form whose action is the GUID route.
        Assert.DoesNotContain("action=\"/consent\"", body);
    }
}
