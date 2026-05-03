namespace HTTPRequestHeaderEcho;

public static class HeaderSpec
{
    public sealed record ParseResult(
        IReadOnlyList<KeyValuePair<string, string>> Valid,
        IReadOnlyList<string> Ignored);

    public static ParseResult Parse(string? raw)
    {
        var valid = new List<KeyValuePair<string, string>>();
        var ignored = new List<string>();
        if (string.IsNullOrWhiteSpace(raw))
            return new ParseResult(valid, ignored);

        foreach (var rawLine in raw.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;

            var colon = line.IndexOf(':');
            if (colon < 0)
            {
                ignored.Add(line);
                continue;
            }

            var name = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();

            if (!IsToken(name) || HasForbiddenValueChar(value))
            {
                ignored.Add(line);
                continue;
            }

            valid.Add(new KeyValuePair<string, string>(name, value));
        }

        return new ParseResult(valid, ignored);
    }

    private static bool HasForbiddenValueChar(string s)
    {
        // RFC 9110 §5.5: field-value may contain VCHAR, SP, HTAB, and obs-text.
        // Reject all CTL chars (0x00-0x1F, 0x7F) except HTAB.
        foreach (var c in s)
        {
            if (c == '\t') continue;
            if (c < 0x20 || c == 0x7F) return true;
        }
        return false;
    }

    public static void Apply(
        HttpResponse response,
        IReadOnlyList<KeyValuePair<string, string>> headers)
    {
        foreach (var (name, value) in headers)
            response.Headers.Append(name, value);
    }

    private static bool IsToken(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        foreach (var c in s)
        {
            if (c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9'))
                continue;
            switch (c)
            {
                case '!':
                case '#':
                case '$':
                case '%':
                case '&':
                case '\'':
                case '*':
                case '+':
                case '-':
                case '.':
                case '^':
                case '_':
                case '`':
                case '|':
                case '~':
                    continue;
                default:
                    return false;
            }
        }
        return true;
    }
}
