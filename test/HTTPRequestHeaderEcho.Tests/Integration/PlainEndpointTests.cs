using System.Net;
using System.Net.Http.Headers;

namespace HTTPRequestHeaderEcho.Tests.Integration;

public class PlainEndpointTests(WebAppFactory factory) : IClassFixture<WebAppFactory>
{
    [Fact]
    public async Task Returns_200_text_plain_with_no_consent_required()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();
        using var resp = await client.GetAsync("/plain", ct);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("text/plain", resp.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Sets_cache_control_private_no_store()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();
        using var resp = await client.GetAsync("/plain", ct);

        var cc = resp.Headers.CacheControl;
        Assert.NotNull(cc);
        Assert.True(cc!.Private);
        Assert.True(cc.NoStore);
    }

    [Fact]
    public async Task Body_lists_request_headers_as_key_colon_value()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-My-Marker", "echo-me");
        var body = await client.GetStringAsync("/plain", ct);

        Assert.Contains("X-My-Marker: echo-me", body);
    }

    [Fact]
    public async Task Honors_HEADER_PREFIX_FILTER_env_var()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory
            .WithConfig(("HEADER_PREFIX_FILTER", "X-Keep-"))
            .CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Keep-A", "yes");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Other-B", "no");

        var body = await client.GetStringAsync("/plain", ct);

        Assert.Contains("X-Keep-A: yes", body);
        Assert.DoesNotContain("X-Other-B", body);
        Assert.DoesNotContain("Host:", body);  // Host normally appears, prefix-filtered out
    }

    [Fact]
    public async Task Honors_HEADER_HIDE_LIST_env_var()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory
            .WithConfig(("HEADER_HIDE_LIST", "X-Secret"))
            .CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Secret", "shh");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Visible", "ok");

        var body = await client.GetStringAsync("/plain", ct);

        Assert.DoesNotContain("X-Secret", body);
        Assert.Contains("X-Visible: ok", body);
    }

    [Fact]
    public async Task Scrubs_consent_cookie_from_Cookie_header_in_output()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", "a=1; consent=1; b=2");

        var body = await client.GetStringAsync("/plain", ct);

        Assert.Contains("Cookie:", body);
        Assert.DoesNotContain("consent=1", body);
        Assert.Contains("a=1", body);
        Assert.Contains("b=2", body);
    }

    [Fact]
    public async Task Drops_Cookie_header_entirely_if_only_consent_present()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", "consent=1");

        var body = await client.GetStringAsync("/plain", ct);

        Assert.DoesNotContain("Cookie:", body);
    }
}
