namespace HTTPRequestHeaderEcho.Tests.Unit;

public class SnippetsTests
{
    private static KeyValuePair<string, string>[] H(params (string k, string v)[] pairs) =>
        pairs.Select(p => new KeyValuePair<string, string>(p.k, p.v)).ToArray();

    // ---------- Curl ----------

    [Fact]
    public void Curl_no_headers_just_url()
    {
        var s = Snippets.Curl("https://example.com/", H());
        Assert.Equal("curl -i \\\n  'https://example.com/'", s);
    }

    [Fact]
    public void Curl_single_header()
    {
        var s = Snippets.Curl("https://example.com/", H(("X-A", "1")));
        Assert.Equal("curl -i \\\n  -H 'X-A: 1' \\\n  'https://example.com/'", s);
    }

    [Fact]
    public void Curl_multiple_headers_each_on_own_continuation_line()
    {
        var s = Snippets.Curl("https://x/", H(("X-A", "1"), ("X-B", "2")));
        Assert.Equal("curl -i \\\n  -H 'X-A: 1' \\\n  -H 'X-B: 2' \\\n  'https://x/'", s);
    }

    [Fact]
    public void Curl_value_with_single_quote_uses_posix_quote_escape()
    {
        // POSIX trick: close-quote, escaped-quote, re-open-quote => '\''
        var s = Snippets.Curl("https://x/", H(("X-A", "it's")));
        Assert.Contains("-H 'X-A: it'\\''s'", s);
    }

    [Fact]
    public void Curl_url_with_single_quote_is_quoted_too()
    {
        var s = Snippets.Curl("https://x/'a", H());
        Assert.EndsWith("'https://x/'\\''a'", s);
    }

    // ---------- PowerShell ----------

    [Fact]
    public void PowerShell_no_headers_omits_headers_block()
    {
        var s = Snippets.PowerShell("https://example.com/", H());
        Assert.Equal("Invoke-RestMethod -Uri 'https://example.com/'", s);
    }

    [Fact]
    public void PowerShell_single_header_emits_hashtable()
    {
        var s = Snippets.PowerShell("https://x/", H(("X-A", "1")));
        Assert.Equal(
            "Invoke-RestMethod -Uri 'https://x/' `\n  -Headers @{\n    'X-A' = '1'\n  }",
            s);
    }

    [Fact]
    public void PowerShell_multiple_headers_each_on_own_line()
    {
        var s = Snippets.PowerShell("https://x/", H(("X-A", "1"), ("X-B", "2")));
        Assert.Contains("    'X-A' = '1'\n    'X-B' = '2'\n", s);
    }

    [Fact]
    public void PowerShell_value_with_single_quote_doubles_it()
    {
        // PowerShell single-quoted-string escape: '' represents a literal '
        var s = Snippets.PowerShell("https://x/", H(("X-A", "it's")));
        Assert.Contains("'X-A' = 'it''s'", s);
    }
}
