using Microsoft.Extensions.Primitives;

namespace HTTPRequestHeaderEcho;

public static class HeaderFilter
{
    public static IEnumerable<KeyValuePair<string, StringValues>> WithPrefixFilter(
        this IHeaderDictionary headers, string[] prefixes) =>
        prefixes.Length == 0
            ? headers
            : headers.Where(h =>
                prefixes.Any(p => h.Key.StartsWith(p, StringComparison.OrdinalIgnoreCase)));

    public static IEnumerable<KeyValuePair<string, StringValues>> WithHideList(
        this IEnumerable<KeyValuePair<string, StringValues>> headers, string[] hidden) =>
        hidden.Length == 0
            ? headers
            : headers.Where(h =>
                !hidden.Any(name => string.Equals(h.Key, name, StringComparison.OrdinalIgnoreCase)));

    public static IEnumerable<KeyValuePair<string, StringValues>> WithConsentScrub(
        this IEnumerable<KeyValuePair<string, StringValues>> headers) =>
        headers.SelectMany(h =>
        {
            if (!string.Equals(h.Key, "Cookie", StringComparison.OrdinalIgnoreCase))
                return [h];
            var scrubbed = Consent.ScrubCookieHeader(h.Value.ToString());
            return scrubbed is null
                ? []
                : new[] { new KeyValuePair<string, StringValues>(h.Key, new StringValues(scrubbed)) };
        });
}
