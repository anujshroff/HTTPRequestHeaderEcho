namespace HTTPRequestHeaderEcho.Tests.Unit;

public class HeaderSpecTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\n  \n")]
    public void Parse_blank_input_yields_no_results(string? raw)
    {
        var result = HeaderSpec.Parse(raw);
        Assert.Empty(result.Valid);
        Assert.Empty(result.Ignored);
    }

    [Fact]
    public void Parse_single_valid_header()
    {
        var result = HeaderSpec.Parse("X-Custom: hello");
        var kvp = Assert.Single(result.Valid);
        Assert.Equal("X-Custom", kvp.Key);
        Assert.Equal("hello", kvp.Value);
        Assert.Empty(result.Ignored);
    }

    [Fact]
    public void Parse_multiple_headers_split_on_newline()
    {
        var result = HeaderSpec.Parse("X-A: 1\nX-B: 2\nX-C: 3");
        Assert.Equal(3, result.Valid.Count);
        Assert.Equal(new[] { "X-A", "X-B", "X-C" }, result.Valid.Select(h => h.Key));
        Assert.Equal(new[] { "1", "2", "3" }, result.Valid.Select(h => h.Value));
    }

    [Fact]
    public void Parse_trims_surrounding_whitespace_on_name_and_value()
    {
        var result = HeaderSpec.Parse("   X-Spaced  :   value with spaces   ");
        var kvp = Assert.Single(result.Valid);
        Assert.Equal("X-Spaced", kvp.Key);
        Assert.Equal("value with spaces", kvp.Value);
    }

    [Fact]
    public void Parse_skips_blank_lines()
    {
        var result = HeaderSpec.Parse("\n\nX-A: 1\n\n\nX-B: 2\n");
        Assert.Equal(2, result.Valid.Count);
        Assert.Empty(result.Ignored);
    }

    [Fact]
    public void Parse_line_without_colon_is_ignored()
    {
        var result = HeaderSpec.Parse("not-a-header-line");
        Assert.Empty(result.Valid);
        Assert.Equal("not-a-header-line", Assert.Single(result.Ignored));
    }

    [Theory]
    [InlineData("Bad Name: v")]
    [InlineData("Header[1]: v")]
    [InlineData("X/Y: v")]
    [InlineData("X@Y: v")]
    [InlineData(": v")]
    public void Parse_invalid_token_name_is_ignored(string line)
    {
        var result = HeaderSpec.Parse(line);
        Assert.Empty(result.Valid);
        Assert.Equal(line, Assert.Single(result.Ignored));
    }

    [Theory]
    [InlineData(0x01)]  // SOH
    [InlineData(0x07)]  // BEL
    [InlineData(0x1F)]  // US (last C0 ctrl)
    [InlineData(0x7F)]  // DEL
    [InlineData(0x0D)]  // CR (line splitter is \n only, so CR survives into the value)
    public void Parse_control_char_in_value_is_ignored(int charCode)
    {
        var line = "X-A: bad" + (char)charCode + "value";
        var result = HeaderSpec.Parse(line);
        Assert.Empty(result.Valid);
        Assert.Single(result.Ignored);
    }

    [Fact]
    public void Parse_tab_in_value_is_allowed()
    {
        var result = HeaderSpec.Parse("X-A: a\tb");
        var kvp = Assert.Single(result.Valid);
        Assert.Equal("a\tb", kvp.Value);
    }

    [Fact]
    public void Parse_empty_value_is_allowed()
    {
        var result = HeaderSpec.Parse("X-Empty:");
        var kvp = Assert.Single(result.Valid);
        Assert.Equal("X-Empty", kvp.Key);
        Assert.Equal("", kvp.Value);
    }

    [Fact]
    public void Parse_value_containing_colon_keeps_full_value()
    {
        var result = HeaderSpec.Parse("X-Url: http://example.com/a:b");
        var kvp = Assert.Single(result.Valid);
        Assert.Equal("http://example.com/a:b", kvp.Value);
    }

    [Fact]
    public void Parse_all_rfc9110_token_chars_in_name_are_valid()
    {
        var result = HeaderSpec.Parse("!#$%&'*+-.^_`|~AaZz09: ok");
        var kvp = Assert.Single(result.Valid);
        Assert.Equal("!#$%&'*+-.^_`|~AaZz09", kvp.Key);
    }

    [Fact]
    public void Parse_mixes_valid_and_ignored_independently()
    {
        var result = HeaderSpec.Parse("X-Good: 1\nbad line\nAlso bad: \nX-Also-Good: 2");
        Assert.Equal(2, result.Valid.Count);
        Assert.Equal(2, result.Ignored.Count);
    }
}
