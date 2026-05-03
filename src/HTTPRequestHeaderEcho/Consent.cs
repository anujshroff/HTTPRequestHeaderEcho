namespace HTTPRequestHeaderEcho;

public static class Consent
{
    public const string CookieName = "consent";

    public static string? ScrubCookieHeader(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;
        var parts = raw
            .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(p =>
            {
                var eq = p.IndexOf('=');
                var name = (eq < 0 ? p : p[..eq]).TrimEnd();
                return !string.Equals(name, CookieName, StringComparison.Ordinal);
            })
            .ToArray();
        return parts.Length == 0 ? null : string.Join("; ", parts);
    }
}
