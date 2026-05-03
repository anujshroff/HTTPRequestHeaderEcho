namespace HTTPRequestHeaderEcho;

public static class ResponseHeaderSpec
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

            if (!IsToken(name) || value.Contains('\r') || value.Contains('\n'))
            {
                ignored.Add(line);
                continue;
            }

            valid.Add(new KeyValuePair<string, string>(name, value));
        }

        return new ParseResult(valid, ignored);
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
