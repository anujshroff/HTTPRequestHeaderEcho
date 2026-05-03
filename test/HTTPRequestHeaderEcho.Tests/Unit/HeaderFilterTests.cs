using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace HTTPRequestHeaderEcho.Tests.Unit;

public class HeaderFilterTests
{
    private static IHeaderDictionary HD(params (string k, string v)[] entries)
    {
        var dict = new HeaderDictionary();
        foreach (var (k, v) in entries)
            dict[k] = new StringValues(v);
        return dict;
    }

    private static IEnumerable<KeyValuePair<string, StringValues>> Seq(params (string k, string v)[] entries) =>
        entries.Select(e => new KeyValuePair<string, StringValues>(e.k, new StringValues(e.v)));

    // ---------- WithPrefixFilter ----------

    [Fact]
    public void WithPrefixFilter_empty_prefixes_returns_all()
    {
        var headers = HD(("Host", "x"), ("X-Custom", "y"));
        var result = headers.WithPrefixFilter([]).ToArray();
        Assert.Equal(2, result.Length);
    }

    [Fact]
    public void WithPrefixFilter_only_matching_prefixes_kept()
    {
        var headers = HD(("X-A", "1"), ("Host", "x"), ("X-B", "2"));
        var keys = headers.WithPrefixFilter(["X-"]).Select(h => h.Key).ToArray();
        Assert.Equal(new[] { "X-A", "X-B" }, keys);
    }

    [Fact]
    public void WithPrefixFilter_is_case_insensitive()
    {
        var headers = HD(("X-Upper", "1"), ("x-lower", "2"));
        var result = headers.WithPrefixFilter(["x-"]).ToArray();
        Assert.Equal(2, result.Length);
    }

    [Fact]
    public void WithPrefixFilter_multiple_prefixes_union()
    {
        var headers = HD(("X-A", "1"), ("Sec-B", "2"), ("Host", "x"));
        var keys = headers.WithPrefixFilter(["X-", "Sec-"]).Select(h => h.Key).ToArray();
        Assert.Equal(new[] { "X-A", "Sec-B" }, keys);
    }

    [Fact]
    public void WithPrefixFilter_no_match_returns_empty()
    {
        var headers = HD(("Host", "x"), ("Accept", "y"));
        Assert.Empty(headers.WithPrefixFilter(["X-"]));
    }

    // ---------- WithHideList ----------

    [Fact]
    public void WithHideList_empty_returns_all()
    {
        var input = Seq(("A", "1"), ("B", "2"));
        var result = input.WithHideList([]).ToArray();
        Assert.Equal(2, result.Length);
    }

    [Fact]
    public void WithHideList_removes_matching()
    {
        var input = Seq(("Authorization", "x"), ("X-A", "1"));
        var keys = input.WithHideList(["Authorization"]).Select(h => h.Key).ToArray();
        Assert.Equal(new[] { "X-A" }, keys);
    }

    [Fact]
    public void WithHideList_is_case_insensitive()
    {
        var input = Seq(("Authorization", "x"), ("X-A", "1"));
        var keys = input.WithHideList(["AUTHORIZATION"]).Select(h => h.Key).ToArray();
        Assert.Equal(new[] { "X-A" }, keys);
    }

    [Fact]
    public void WithHideList_unmatched_names_have_no_effect()
    {
        var input = Seq(("X-A", "1"));
        var result = input.WithHideList(["NotPresent"]).ToArray();
        Assert.Single(result);
    }

    // ---------- WithConsentScrub ----------

    [Fact]
    public void WithConsentScrub_passes_through_non_cookie_headers()
    {
        var input = Seq(("X-A", "1"));
        var result = input.WithConsentScrub().ToArray();
        Assert.Single(result);
        Assert.Equal("X-A", result[0].Key);
        Assert.Equal("1", result[0].Value.ToString());
    }

    [Fact]
    public void WithConsentScrub_removes_consent_pair_from_cookie_value()
    {
        var input = Seq(("Cookie", "a=1; consent=1; b=2"));
        var result = input.WithConsentScrub().ToArray();
        var cookie = Assert.Single(result);
        Assert.Equal("Cookie", cookie.Key);
        Assert.Equal("a=1; b=2", cookie.Value.ToString());
    }

    [Fact]
    public void WithConsentScrub_drops_cookie_header_entirely_when_only_consent()
    {
        var input = Seq(("Cookie", "consent=1"), ("X-A", "1"));
        var keys = input.WithConsentScrub().Select(h => h.Key).ToArray();
        Assert.Equal(new[] { "X-A" }, keys);
    }

    [Fact]
    public void WithConsentScrub_matches_cookie_header_case_insensitively()
    {
        var input = Seq(("cookie", "consent=1; a=1"));
        var result = input.WithConsentScrub().ToArray();
        var cookie = Assert.Single(result);
        Assert.Equal("a=1", cookie.Value.ToString());
    }
}
