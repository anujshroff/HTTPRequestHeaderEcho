namespace HTTPRequestHeaderEcho.Tests.Unit;

public class ConsentTests
{
    [Fact]
    public void Empty_input_returned_unchanged()
    {
        Assert.Equal("", Consent.ScrubCookieHeader(""));
    }

    [Fact]
    public void No_consent_cookie_returned_unchanged()
    {
        Assert.Equal("a=1; b=2", Consent.ScrubCookieHeader("a=1; b=2"));
    }

    [Fact]
    public void Only_consent_cookie_returns_null()
    {
        Assert.Null(Consent.ScrubCookieHeader("consent=1"));
    }

    [Fact]
    public void Consent_in_middle_is_removed()
    {
        Assert.Equal("a=1; b=2", Consent.ScrubCookieHeader("a=1; consent=1; b=2"));
    }

    [Fact]
    public void Consent_first_is_removed()
    {
        Assert.Equal("a=1; b=2", Consent.ScrubCookieHeader("consent=1; a=1; b=2"));
    }

    [Fact]
    public void Consent_last_is_removed()
    {
        Assert.Equal("a=1; b=2", Consent.ScrubCookieHeader("a=1; b=2; consent=1"));
    }

    [Fact]
    public void Cookies_without_value_still_filtered_by_name()
    {
        // RFC 6265 cookie pairs without "=" are odd but possible — name match should still apply.
        Assert.Equal("a=1", Consent.ScrubCookieHeader("a=1; consent"));
    }

    [Fact]
    public void Match_is_case_sensitive_by_design()
    {
        // Implementation uses StringComparison.Ordinal — uppercase "Consent" should NOT be scrubbed.
        Assert.Equal("Consent=1; a=1", Consent.ScrubCookieHeader("Consent=1; a=1"));
    }

    [Fact]
    public void Multiple_consent_pairs_all_removed()
    {
        Assert.Equal("a=1", Consent.ScrubCookieHeader("consent=1; a=1; consent=2"));
    }

    [Fact]
    public void Whitespace_around_pairs_is_trimmed_during_split()
    {
        // Split uses TrimEntries — leading/trailing spaces around each pair are dropped.
        Assert.Equal("a=1; b=2", Consent.ScrubCookieHeader("  a=1 ;   consent=1 ;  b=2  "));
    }

    [Fact]
    public void Empty_pairs_are_dropped_during_split()
    {
        // Split uses RemoveEmptyEntries — stray ";;" should not produce empty cookie names.
        Assert.Equal("a=1; b=2", Consent.ScrubCookieHeader("a=1;; b=2"));
    }
}
